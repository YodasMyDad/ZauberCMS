using Blazored.Modal.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ZauberCMS.Components.Editors;
using ZauberCMS.Components.Editors.ToolbarItems;
using ZauberCMS.Components.Shared;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Components.Tests.Editors;

/// <summary>
/// Roadmap D120: a built-in editor made with <c>new</c> (the template's starter kit reads an editor's alias that
/// way) still works and shows ZauberCMS's English, and one built by discovery or by Blazor gets the site's localiser.
/// </summary>
public class EditorConstructionTests
{
    private static readonly Type[] Editors = typeof(TextProperty).Assembly.GetTypes()
        .Where(t => t is { IsPublic: true, IsAbstract: false } && typeof(IContentProperty).IsAssignableFrom(t))
        .OrderBy(t => t.FullName, StringComparer.Ordinal)
        .ToArray();

    // Editors whose text another part of Phase 10h converts (the API-key picker is 10h-4's); the test says when one
    // is converted, so its line goes
    private static readonly HashSet<string> NotConvertedYet = ["ApiKeysPickerProperty"];

    [Fact]
    public void An_editor_made_with_new_has_its_English_name_and_its_alias()
    {
        var editor = new TextProperty();

        Assert.Equal("Textbox", editor.Name);
        Assert.Equal("Standard text box", editor.Description);
        Assert.Equal("ZauberCMS.Textbox", editor.Alias);
    }

    [Fact]
    public void Every_built_in_editor_made_with_new_shows_the_English_that_discovery_shows()
    {
        using var provider = TextServices().BuildServiceProvider();
        var problems = new List<string>();
        foreach (var type in Editors)
        {
            var made = (IContentProperty)Activator.CreateInstance(type)!;
            var discovered = (IContentProperty)ActivatorUtilities.CreateInstance(provider, type);
            if (made.Name != discovered.Name || made.Description != discovered.Description ||
                made.Name.Contains("editors_", StringComparison.Ordinal))
            {
                problems.Add($"{type.Name}: '{made.Name}' / '{discovered.Name}'");
            }
        }

        Assert.NotEmpty(Editors);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Discovery_and_Blazor_build_every_editor_with_the_sites_localiser()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStringLocalizer<ZauberText>, MarkerText>();
        services.AddScoped<IModalService, ModalService>();
        using var provider = services.BuildServiceProvider();

        // ExtensionManager.GetInstances builds with ActivatorUtilities.CreateInstance; Blazor's component
        // activator with ActivatorUtilities.CreateFactory. Both must choose the localising constructor.
        var problems = new List<string>();
        foreach (var type in Editors)
        {
            var discovered = (IContentProperty)ActivatorUtilities.CreateInstance(provider, type);
            var rendered = (IContentProperty)ActivatorUtilities.CreateFactory(type, Type.EmptyTypes)(provider, null);
            var localised = discovered.Name.StartsWith("marker:", StringComparison.Ordinal) &&
                            rendered.Description.StartsWith("marker:", StringComparison.Ordinal);
            if (localised == NotConvertedYet.Contains(type.Name))
            {
                problems.Add(localised
                    ? $"{type.Name} is localised now: take it out of NotConvertedYet"
                    : $"{type.Name}: '{discovered.Name}' / '{rendered.Description}'");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        Assert.Equal("marker:editors_insertMedia", ActivatorUtilities.CreateInstance<InsertMediaItem>(provider).Label);
        Assert.StartsWith("marker:", ActivatorUtilities.CreateInstance<InsertContentLinkItem>(provider).Label);
    }

    [Fact]
    public void A_name_set_on_an_editor_wins_over_its_text()
    {
        // The setters are kept from before v5: a site or a subclass may rename an editor
        var editor = new TextProperty { Name = "Short text", Description = "One line" };

        Assert.Equal("Short text", editor.Name);
        Assert.Equal("One line", editor.Description);
    }

    private static ServiceCollection TextServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddZauberText([typeof(ZauberText).Assembly, typeof(AdminErrorBoundary).Assembly],
            [typeof(ZauberText).Assembly, typeof(AdminErrorBoundary).Assembly]);
        return services;
    }

    private sealed class MarkerText : IStringLocalizer<ZauberText>
    {
        public LocalizedString this[string name] => new(name, "marker:" + name);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
