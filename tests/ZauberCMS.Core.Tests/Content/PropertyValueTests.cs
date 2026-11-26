using System.Globalization;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Validation;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Content;

/// <summary>
/// Roadmap Phase 10c, through the services: pickers return the editor's order (ZA-078), and a content type can't give
/// a page two properties with one alias (ZA-028).
/// </summary>
public class PropertyValueTests : IAsyncLifetime
{
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    public async ValueTask InitializeAsync() =>
        _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<T> WithContentService<T>(Func<IContentService, Task<T>> call)
    {
        using var scope = _site.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IContentService>());
    }

    private static PropertyType TextBox(string alias) => new()
    {
        Alias = alias, Name = alias, ComponentAlias = "ZauberCMS.Textbox",
        Component = "ZauberCMS.Components.Editors.TextBox"
    };

    private Task<HandlerResult<ContentType>> SaveTypeAsync(ContentType type) =>
        WithContentService(s => s.SaveContentTypeAsync(new SaveContentTypeParameters { ContentType = type }, Ct));

    private async Task<ContentType> CreateTypeAsync(string alias, bool isComposition = false,
        List<Guid>? compositionIds = null, params PropertyType[] properties)
    {
        var result = await SaveTypeAsync(new ContentType
        {
            Name = alias, Alias = alias, AllowAtRoot = true, IsComposition = isComposition,
            CompositionIds = compositionIds ?? [], ContentProperties = [.. properties]
        });
        Assert.True(result.Success, TestSite.Describe(result.Messages));
        return result.Entity!;
    }

    private async Task<ContentItem> CreatePageAsync(ContentType type, string name, bool published = true)
    {
        var page = new ContentItem
        {
            Name = name, ContentTypeId = type.Id, ContentTypeAlias = type.Alias, IsRootContent = true,
            Published = published
        };
        var result = await WithContentService(s => s.SaveContentAsync(new SaveContentParameters { Content = page }, Ct));
        Assert.True(result.Success, TestSite.Describe(result.Messages));
        return result.Entity!;
    }

    private static ContentItem Holding(string alias, string value)
    {
        var holder = new ContentItem { Name = "Holder" };
        holder.PropertyData.Add(new ContentPropertyValue { ContentId = holder.Id, Alias = alias, Value = value });
        return holder;
    }

    [Fact]
    public async Task ZA078_A_content_picker_returns_its_pages_in_the_order_the_editor_chose()
    {
        var type = await CreateTypeAsync("page");
        var first = await CreatePageAsync(type, "First");
        var second = await CreatePageAsync(type, "Second");
        var third = await CreatePageAsync(type, "Third");
        var draft = await CreatePageAsync(type, "Draft", published: false);

        // Saved last, so the query's own order (newest first) would put "Third" first
        var holder = Holding("related", new[] { second.Id, draft.Id, third.Id, first.Id }.ToPickerValue());

        var items = await WithContentService(s => holder.GetContentItems("related", s));

        // The editor's order; an unpublished page is not shown to visitors
        Assert.Equal([second.Id, third.Id, first.Id], items.Select(x => x.Id));

        // A value from before D30 (one bare id) still reads
        var legacy = Holding("related", first.Id.ToString());
        var legacyItems = await WithContentService(s => legacy.GetContentItems("related", s));
        Assert.Equal([first.Id], legacyItems.Select(x => x.Id));
        var single = await WithContentService(s => legacy.GetContent("related", s));
        Assert.Equal(first.Id, single?.Id);
    }

    [Fact]
    public async Task ZA028_A_content_type_with_two_properties_of_one_alias_is_refused()
    {
        var result = await SaveTypeAsync(new ContentType
        {
            Name = "twice", Alias = "twice", ContentProperties = [TextBox("title"), TextBox("Title")]
        });

        Assert.False(result.Success);
        Assert.Contains(result.Messages, m => m.Message.Contains("'Title'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZA028_An_alias_that_clashes_through_a_composition_is_refused_from_either_side()
    {
        var seo = await CreateTypeAsync("seo", isComposition: true, properties: TextBox("metaTitle"));

        // The type brings an alias its composition already has
        var clash = await SaveTypeAsync(new ContentType
        {
            Name = "article", Alias = "article", CompositionIds = [seo.Id], ContentProperties = [TextBox("MetaTitle")]
        });
        Assert.False(clash.Success);

        // A type that uses the composition without a clash saves
        var article = await CreateTypeAsync("article", compositionIds: [seo.Id], properties: TextBox("heading"));

        // The composition then gains an alias the type already has
        seo.ContentProperties.Add(TextBox("heading"));
        var compositionClash = await SaveTypeAsync(seo);
        Assert.False(compositionClash.Success);
        Assert.Contains(compositionClash.Messages, m => m.Message.Contains(article.Name!, StringComparison.Ordinal));

        // Saving the type again with nothing new is fine
        var resaved = await SaveTypeAsync(article);
        Assert.True(resaved.Success, TestSite.Describe(resaved.Messages));
    }

    [Fact]
    public async Task ZA028_A_clash_already_stored_on_another_type_does_not_block_saving_its_composition()
    {
        var seo = await CreateTypeAsync("seo", isComposition: true, properties: TextBox("metaTitle"));
        var legacy = await CreateTypeAsync("legacy", compositionIds: [seo.Id], properties: TextBox("body"));

        // Stored before the check existed: the type repeats an alias of its own
        await _site.QueryDatabaseAsync(async db =>
        {
            var stored = await db.ContentTypes.SingleAsync(x => x.Id == legacy.Id, Ct);
            stored.ContentProperties = [.. stored.ContentProperties, TextBox("Body")];
            return await db.SaveChangesAsync(Ct);
        });

        // The composition's own change doesn't touch that clash
        seo.ContentProperties.Add(TextBox("metaDescription"));
        var saved = await SaveTypeAsync(seo);
        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class PropertyValueTestsOnPostgreSql : PropertyValueTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class PropertyValueTestsOnSqlServer : PropertyValueTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}

/// <summary>
/// Roadmap Phase 10c, stored value formats without a database: one picker shape (ZA-029, D30), invariant numbers and
/// dates (ZA-027), repeated aliases (ZA-028) and FluentValidation in an edit form (ZA-015).
/// </summary>
public class StoredValueFormatTests
{
    private static readonly Guid A = Guid.Parse("0a000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("0b000000-0000-0000-0000-00000000000b");

    private static T InCulture<T>(string culture, Func<T> read)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return read();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ZA029_A_picker_value_is_one_json_array_for_any_count_and_old_values_still_read()
    {
        Assert.Equal($"[\"{A}\"]", new[] { A }.ToPickerValue());
        Assert.Equal(string.Empty, Array.Empty<Guid>().ToPickerValue());
        Assert.Equal($"[\"{A}\",\"{B}\"]", new[] { A, Guid.Empty, B, A }.ToPickerValue());

        Assert.Equal([A], A.ToString().ToPickerIds());
        Assert.Equal([B, A], JsonSerializer.Serialize(new[] { B, A, B, Guid.Empty }).ToPickerIds());
        Assert.Empty("not ids".ToPickerIds());
        Assert.Empty(((string?)null).ToPickerIds());

        // Reading a property the way a view does: a one-item array as one id, one bare id as a list
        Assert.Equal(A, $"[\"{A}\"]".ToValue<Guid>());
        Assert.Equal([A], A.ToString().ToValue<List<Guid>>());
        Assert.Equal(A, A.ToString().ToValue<Guid?>());
        Assert.Equal(A, $"[\"{A}\"]".ToValue<Guid?>());
        Assert.Null("[]".ToValue<Guid?>());
        Assert.Equal([A], A.ToString().ToValue<Guid[]>()!);
        Assert.Equal([A], A.ToString().ToValue<IEnumerable<Guid>>()!);
        Assert.Equal([A, B], JsonSerializer.Serialize(new[] { A, B }).ToValue<IReadOnlyList<Guid>>()!);
        // Not a picker read: object stays as it was
        Assert.Null(A.ToString().ToValue<object>());

        // The single-id fields the admin fills from a picker take either shape
        var page = new ContentItem { InternalRedirectIdAsString = $"[\"{A}\"]" };
        Assert.Equal(A, page.InternalRedirectId);
        page.InternalRedirectIdAsString = "[]";
        Assert.Equal(Guid.Empty, page.InternalRedirectId);
        var type = new ContentType { MediaIdAsString = B.ToString() };
        Assert.Equal(B, type.MediaId);
    }

    [Fact]
    public void ZA078_Items_are_put_back_in_the_stored_order_and_missing_ones_left_out()
    {
        var items = new[] { (Id: B, Name: "b"), (Id: A, Name: "a") };
        var ordered = items.InPickerOrder([A, Guid.NewGuid(), B], x => x.Id);
        Assert.Equal(["a", "b"], ordered.Select(x => x.Name));
    }

    [Fact]
    public void ZA027_Numbers_and_dates_read_the_same_in_every_culture_and_old_values_still_read()
    {
        // Stored invariant: a German page reads 1.5, not 15
        Assert.Equal(1.5, InCulture("de-DE", () => "1.5".ToValue<double>()));
        Assert.Equal(1.5m, InCulture("de-DE", () => "1.5".ToValue<decimal>()));
        // Written by a German admin before ZA-027
        Assert.Equal(1.5, InCulture("de-DE", () => "1,5".ToValue<double>()));
        // Never read as fifteen
        Assert.NotEqual(15m, InCulture("en-GB", () => "1,5".ToValue<decimal>()));

        var when = new DateTime(2026, 11, 26, 14, 30, 0);
        var stored = when.ToString("s", CultureInfo.InvariantCulture);
        Assert.Equal(when, InCulture("de-DE", () => stored.ToValue<DateTime>()));
        Assert.Equal(when, InCulture("en-US", () => stored.ToValue<DateTime?>()));
        var oldGerman = InCulture("de-DE", () => when.ToLongDateString());
        Assert.Equal(when.Date, InCulture("de-DE", () => oldGerman.ToValue<DateTime>()));
    }

    [Fact]
    public void ZA028_Values_that_repeat_an_alias_read_the_first_instead_of_throwing()
    {
        var page = new ContentItem();
        page.PropertyData.Add(new ContentPropertyValue { Alias = "title", Value = "First" });
        page.PropertyData.Add(new ContentPropertyValue { Alias = "title", Value = "Second" });

        Assert.Equal("First", page.GetValue<string>("title"));
    }

    private sealed class Model
    {
        public string? Name { get; set; }
    }

    private sealed class ThrowingValidator : AbstractValidator<Model>
    {
        public ThrowingValidator() => RuleFor(x => x.Name).Must(_ => throw new InvalidOperationException("boom"));
    }

    private sealed class NameRequired : AbstractValidator<Model>
    {
        public NameRequired() => RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
    }

    private static IServiceProvider Services() => new ServiceCollection().AddLogging().BuildServiceProvider();

    [Fact]
    public void ZA015_A_validator_that_throws_leaves_the_form_invalid_instead_of_ending_the_circuit()
    {
        var context = new EditContext(new Model { Name = "x" });
        using var subscription = context.AddFluentValidation(Services(), new ThrowingValidator(),
            new FluentValidationValidator());

        Assert.False(context.Validate());
        Assert.Contains(EditContextFluentValidationExtensions.ValidatorFailedMessage, context.GetValidationMessages());

        // A field change runs the validator too, from an async void handler: it must not throw
        context.NotifyFieldChanged(context.Field(nameof(Model.Name)));
        Assert.Contains(EditContextFluentValidationExtensions.ValidatorFailedMessage,
            context.GetValidationMessages(context.Field(nameof(Model.Name))));
    }

    [Fact]
    public void ZA015_Disposing_the_subscription_stops_validation_and_clears_its_messages()
    {
        var context = new EditContext(new Model());
        var subscription = context.AddFluentValidation(Services(), new NameRequired(), new FluentValidationValidator());

        Assert.False(context.Validate());
        Assert.Contains("Name is required", context.GetValidationMessages());

        subscription.Dispose();
        Assert.Empty(context.GetValidationMessages());
        // No handler left on the edit context
        Assert.True(context.Validate());
        subscription.Dispose();
    }
}
