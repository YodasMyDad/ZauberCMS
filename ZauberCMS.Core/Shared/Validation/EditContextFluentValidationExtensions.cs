using System.Globalization;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Results;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Core.Shared.Validation;

public static class EditContextFluentValidationExtensions
{
    private static readonly char[] Separators = { '.', '[' };
    private static readonly Lock ScanLock = new();
    private static readonly HashSet<string> ScannedAssemblies = new(StringComparer.Ordinal);
    private static readonly List<AssemblyScanner.AssemblyScanResult> AssemblyScanResults = new();
    public const string PendingAsyncValidation = "AsyncValidationTask";

    /// <summary>The message shown when a validator throws, so the form stays invalid instead of passing.</summary>
    public const string ValidatorFailedMessage = "Validation could not run. Try again, or contact your administrator.";

    /// <summary>
    /// Validates <paramref name="editContext"/> with FluentValidation. Dispose the result to stop: it unsubscribes from
    /// the edit context and clears the messages this added. A validator that throws is logged and leaves the form
    /// invalid; it never ends the circuit.
    /// </summary>
    public static IDisposable AddFluentValidation(this EditContext editContext, IServiceProvider serviceProvider, IValidator? validator, FluentValidationValidator fluentValidationValidator)
    {
        ArgumentNullException.ThrowIfNull(editContext, nameof(editContext));

        var messages = new ValidationMessageStore(editContext);
        var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(FluentValidationValidator));

        // Event handlers are async void, so nothing may escape them: an exception there ends the circuit.
        EventHandler<ValidationRequestedEventArgs> onValidationRequested = async (sender, _) =>
        {
            var context = (EditContext)sender!;
            try
            {
                await ValidateModel(context, messages, serviceProvider, fluentValidationValidator, validator, logger);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Validating {Model} failed", context.Model.GetType().FullName);
                messages.Clear();
                messages.Add(new FieldIdentifier(context.Model, string.Empty), ValidatorFailedMessage);
                context.NotifyValidationStateChanged();
            }
        };

        EventHandler<FieldChangedEventArgs> onFieldChanged = async (_, eventArgs) =>
        {
            try
            {
                await ValidateField(editContext, messages, eventArgs.FieldIdentifier, serviceProvider, validator);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Validating {Field} on {Model} failed", eventArgs.FieldIdentifier.FieldName,
                    eventArgs.FieldIdentifier.Model.GetType().FullName);
                messages.Clear(eventArgs.FieldIdentifier);
                messages.Add(eventArgs.FieldIdentifier, ValidatorFailedMessage);
                editContext.NotifyValidationStateChanged();
            }
        };

        editContext.OnValidationRequested += onValidationRequested;
        editContext.OnFieldChanged += onFieldChanged;

        return new Subscription(() =>
        {
            editContext.OnValidationRequested -= onValidationRequested;
            editContext.OnFieldChanged -= onFieldChanged;
            messages.Clear();
            editContext.NotifyValidationStateChanged();
        });
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }

    private static async Task ValidateModel(EditContext editContext,
        ValidationMessageStore messages,
        IServiceProvider serviceProvider,
        FluentValidationValidator fluentValidationValidator,
        IValidator? validator,
        ILogger? logger)
    {
        validator ??= GetValidatorForModel(serviceProvider, editContext.Model);

        ValidationContext<object> context;
        if (fluentValidationValidator.ValidateOptions is not null)
        {
            context = ValidationContext<object>.CreateWithOptions(editContext.Model, fluentValidationValidator.ValidateOptions);
        }
        else if (fluentValidationValidator.Options is not null)
        {
            context = ValidationContext<object>.CreateWithOptions(editContext.Model, fluentValidationValidator.Options);
        }
        else
        {
            context = new ValidationContext<object>(editContext.Model);
        }

        // Always leave a task for FluentValidationValidator.ValidateAsync to await, one that never faults: a model with
        // no validator passes, and a validator that throws fails with ValidatorFailedMessage.
        var asyncValidationTask = RunValidator(validator, context, editContext.Model, logger);
        editContext.Properties[PendingAsyncValidation] = asyncValidationTask;
        var validationResults = await asyncValidationTask;

        messages.Clear();
        foreach (var validationResult in validationResults.Errors)
        {
            var fieldIdentifier = ToFieldIdentifier(editContext, validationResult.PropertyName);
            messages.Add(fieldIdentifier, validationResult.ErrorMessage);
        }

        editContext.NotifyValidationStateChanged();
    }

    private static async Task<ValidationResult> RunValidator(IValidator? validator, ValidationContext<object> context,
        object model, ILogger? logger)
    {
        if (validator is null)
        {
            return new ValidationResult();
        }

        try
        {
            return await validator.ValidateAsync(context);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Validator {Validator} threw for {Model}", validator.GetType().FullName,
                model.GetType().FullName);
            return new ValidationResult([new ValidationFailure(string.Empty, ValidatorFailedMessage)]);
        }
    }

    private static async Task ValidateField(EditContext editContext,
        ValidationMessageStore messages,
        FieldIdentifier fieldIdentifier,
        IServiceProvider serviceProvider,
        IValidator? validator = null)
    {
        var properties = new[] { fieldIdentifier.FieldName };
        var context = new ValidationContext<object>(fieldIdentifier.Model, new PropertyChain(), new MemberNameValidatorSelector(properties));

        validator ??= GetValidatorForModel(serviceProvider, fieldIdentifier.Model);

        if (validator is not null)
        {
            var validationResults = await validator.ValidateAsync(context);

            messages.Clear(fieldIdentifier);
            messages.Add(fieldIdentifier, validationResults.Errors.Select(error => error.ErrorMessage));

            editContext.NotifyValidationStateChanged();
        }
    }

    private static IValidator? GetValidatorForModel(IServiceProvider serviceProvider, object model)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(model.GetType());
        if (serviceProvider.GetService(validatorType) is IValidator validator)
        {
            return validator;
        }

        var modelValidatorType = FindValidatorType(validatorType);
        return modelValidatorType is null
            ? null
            : (IValidator)ActivatorUtilities.CreateInstance(serviceProvider, modelValidatorType);
    }

    /// <summary>
    /// Finds a validator for <paramref name="interfaceValidatorType"/> in the scanned assemblies. Each assembly is
    /// scanned once per process; circuits share the results, so the scan and the read are under one lock.
    /// </summary>
    private static Type? FindValidatorType(Type interfaceValidatorType)
    {
        lock (ScanLock)
        {
            foreach (var assembly in AssemblyManager.Assemblies)
            {
                if (assembly?.FullName is not { } name || !ScannedAssemblies.Add(name))
                {
                    continue;
                }

                try
                {
                    AssemblyScanResults.AddRange(AssemblyScanner.FindValidatorsInAssembly(assembly));
                }
                catch (Exception)
                {
                    // An assembly whose types can't all be loaded has no validators we could build.
                }
            }

            return AssemblyScanResults.FirstOrDefault(i => interfaceValidatorType.IsAssignableFrom(i.InterfaceType))?.ValidatorType;
        }
    }

    private static FieldIdentifier ToFieldIdentifier(in EditContext editContext, in string propertyPath)
    {
        // This code is taken from an article by Steve Sanderson (https://blog.stevensanderson.com/2019/09/04/blazor-fluentvalidation/)
        // all credit goes to him for this code.

        // This method parses property paths like 'SomeProp.MyCollection[123].ChildProp'
        // and returns a FieldIdentifier which is an (instance, propName) pair. For example,
        // it would return the pair (SomeProp.MyCollection[123], "ChildProp"). It traverses
        // as far into the propertyPath as it can go until it finds any null instance.

        var obj = editContext.Model;
        var nextTokenEnd = propertyPath.IndexOfAny(Separators);
            
        // Optimize for a scenario when parsing isn't needed.
        if (nextTokenEnd < 0)
        {
            return new FieldIdentifier(obj, propertyPath);
        }

        ReadOnlySpan<char> propertyPathAsSpan = propertyPath;

        while (true)
        {
            var nextToken = propertyPathAsSpan.Slice(0, nextTokenEnd);
            propertyPathAsSpan = propertyPathAsSpan.Slice(nextTokenEnd + 1);

            object? newObj;
            if (nextToken.EndsWith("]"))
            {
                // It's an indexer
                // This code assumes C# conventions (one indexer named Item with one param)
                nextToken = nextToken.Slice(0, nextToken.Length - 1);
                var prop = obj.GetType().GetProperty("Item");

                if (prop is not null)
                {
                    // we've got an Item property
                    var indexerType = prop.GetIndexParameters()[0].ParameterType;
                    var indexerValue = Convert.ChangeType(nextToken.ToString(), indexerType, CultureInfo.InvariantCulture);
                        
                    newObj = prop.GetValue(obj, new [] { indexerValue });
                }
                else
                {
                    // If there is no Item property
                    // Try to cast the object to array
                    if (obj is object[] array)
                    {
                        var indexerValue = int.Parse(nextToken, CultureInfo.InvariantCulture);
                        newObj = array[indexerValue];
                    }
                    else
                    {
                        throw new InvalidOperationException($"Could not find indexer on object of type {obj.GetType().FullName}.");
                    }
                }
            }
            else
            {
                // It's a regular property
                var prop = obj.GetType().GetProperty(nextToken.ToString());
                if (prop == null)
                {
                    throw new InvalidOperationException($"Could not find property named {nextToken.ToString()} on object of type {obj.GetType().FullName}.");
                }
                newObj = prop.GetValue(obj);
            }

            if (newObj == null)
            {
                // This is as far as we can go
                return new FieldIdentifier(obj, nextToken.ToString());
            }

            obj = newObj;
                
            nextTokenEnd = propertyPathAsSpan.IndexOfAny(Separators);
            if (nextTokenEnd < 0)
            {
                return new FieldIdentifier(obj, propertyPathAsSpan.ToString());
            }
        }
    }
}