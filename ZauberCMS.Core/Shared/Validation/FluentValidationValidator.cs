using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace ZauberCMS.Core.Shared.Validation;

public class FluentValidationValidator : ComponentBase, IDisposable
{
    private const string MissingEditContextMessage =
        "FluentValidationValidator needs a cascading EditContext: put it inside an EditForm.";

    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;

    [CascadingParameter] private EditContext? CurrentEditContext { get; set; }

    [Parameter] public IValidator? Validator { get; set; }
    [Parameter] public Action<ValidationStrategy<object>>? Options { get; set; }
    internal Action<ValidationStrategy<object>>? ValidateOptions { get; set; }

    public bool Validate(Action<ValidationStrategy<object>>? options = null)
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException(MissingEditContextMessage);
        }

        ValidateOptions = options;

        try
        {
            return CurrentEditContext.Validate();
        }
        finally
        {
            ValidateOptions = null;
        }
    }

    /// <summary>
    /// Validates this <see cref="EditContext"/>.
    /// </summary>
    /// <returns>True if there are no validation messages after validation; otherwise false.</returns>
    public async Task<bool> ValidateAsync(Action<ValidationStrategy<object>>? options = null)
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException(MissingEditContextMessage);
        }
        
        ValidateOptions = options;

        try
        {
            CurrentEditContext.Validate();

            if (!CurrentEditContext!.Properties.TryGetValue(
                    EditContextFluentValidationExtensions.PendingAsyncValidation, out var asyncValidationTask))
            {
                throw new InvalidOperationException("No pending ValidationResult found");
            }

            await (Task<ValidationResult>) asyncValidationTask;

            return !CurrentEditContext.GetValidationMessages().Any();
        }
        finally
        {
            ValidateOptions = null;
        }
    }

    protected override void OnParametersSet()
    {
        if (CurrentEditContext == null)
        {
            throw new InvalidOperationException($"{nameof(FluentValidationValidator)} requires a cascading " +
                                                $"parameter of type {nameof(EditContext)}. For example, you can use {nameof(FluentValidationValidator)} " +
                                                $"inside an {nameof(EditForm)}.");
        }

        // Subscribe once per edit context and validator type; a new form model gives a new EditContext, so move to it.
        // By type, not instance: Validator="@(new PersonValidator())" is a new instance on every render, and moving
        // clears the messages, so a failed submit would lose its errors at once.
        if (ReferenceEquals(CurrentEditContext, _subscribedContext) && Validator?.GetType() == _subscribedValidatorType)
        {
            return;
        }

        _subscription?.Dispose();
        _subscription = CurrentEditContext.AddFluentValidation(ServiceProvider, Validator, this);
        _subscribedContext = CurrentEditContext;
        _subscribedValidatorType = Validator?.GetType();
    }

    private IDisposable? _subscription;
    private EditContext? _subscribedContext;
    private Type? _subscribedValidatorType;

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
        GC.SuppressFinalize(this);
    }
}