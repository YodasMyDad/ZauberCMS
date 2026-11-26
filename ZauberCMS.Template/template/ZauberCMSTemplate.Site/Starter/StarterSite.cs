using System.Net;
using ZauberCMS.Components.Editors;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Installer.Interfaces;
using ZauberCMS.Core.Installer.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMSTemplate.Site.Views;

namespace ZauberCMSTemplate.Site.Starter;

/// <summary>
/// The starter content the installer offers: two page types (Home Page, Text Page) and two pages that use the
/// views in the Views folder. ZauberCMS finds this class by itself because it implements IStarterKit.
/// It runs once, from the installer. After that this file is only an example of creating content in code:
/// keep it, change it, or delete it.
/// </summary>
public class StarterSite(IContentService contentService) : IStarterKit
{
    public const string HomePageAlias = "homePage";
    public const string TextPageAlias = "textPage";

    public string Name => "A small starter site";

    public string Description =>
        "A home page and an About page, with the page types and Razor views behind them. " +
        "The views are in this project, ready to change.";

    public async Task InstallAsync(StarterKitContext context, CancellationToken cancellationToken = default)
    {
        // Page types first. A page type lists its properties, grouped in tabs, and the views its pages may use.
        var textTab = new Tab { Name = "Content", SortOrder = 1 };
        var textHeading = TextBox("Heading", textTab, 1);
        var textBody = RichText("Body", textTab, 2);
        var textPage = await SaveAsync(new ContentType
        {
            Name = "Text Page",
            Alias = TextPageAlias,
            Icon = "article",
            AvailableContentViews = [typeof(TextPageView).FullName!],
            ContentProperties = [textHeading, textBody],
            Tabs = WithSystemTab(textTab)
        }, cancellationToken);

        var homeTab = new Tab { Name = "Content", SortOrder = 1 };
        var homeHeading = TextBox("Heading", homeTab, 1);
        var homeIntro = TextArea("Intro", homeTab, 2);
        var homeBody = RichText("Body", homeTab, 3);
        var homePage = await SaveAsync(new ContentType
        {
            Name = "Home Page",
            Alias = HomePageAlias,
            Icon = "home",
            AllowAtRoot = true,
            AllowedChildContentTypes = [textPage.Id],
            AvailableContentViews = [typeof(HomeView).FullName!],
            ContentProperties = [homeHeading, homeIntro, homeBody],
            Tabs = WithSystemTab(homeTab)
        }, cancellationToken);

        // Then the pages. The home page is the root: it answers "/", and its language is the language of
        // every page below it.
        var siteName = WebUtility.HtmlEncode(context.SiteName);
        var home = await SaveAsync(new Content
        {
            Name = "Home",
            ContentTypeId = homePage.Id,
            ContentTypeAlias = homePage.Alias,
            IsRootContent = true,
            Published = true,
            ViewComponent = typeof(HomeView).FullName!,
            LanguageId = context.Language?.Id,
            PropertyData =
            [
                Value(homeHeading, context.SiteName),
                Value(homeIntro, "This is the starter site. Everything on it is yours to change."),
                Value(homeBody,
                    "<h2>Where to go next</h2>" +
                    "<ul>" +
                    "<li>Sign in at /admin to edit this page or add new ones.</li>" +
                    "<li>The page types are under Structure: Home Page and Text Page.</li>" +
                    "<li>The Razor views are in this project's Views folder, the layout in Layouts.</li>" +
                    "</ul>")
            ]
        }, cancellationToken);

        await SaveAsync(new Content
        {
            Name = "About",
            ContentTypeId = textPage.Id,
            ContentTypeAlias = textPage.Alias,
            ParentId = home.Id,
            Published = true,
            ViewComponent = typeof(TextPageView).FullName!,
            PropertyData =
            [
                Value(textHeading, "About"),
                Value(textBody, $"<p>Replace this with a few words about {siteName}.</p>")
            ]
        }, cancellationToken);
    }

    private async Task<ContentType> SaveAsync(ContentType contentType, CancellationToken cancellationToken)
    {
        var result = await contentService.SaveContentTypeAsync(
            new SaveContentTypeParameters { ContentType = contentType }, cancellationToken);
        return result.Success
            ? result.Entity!
            : throw new InvalidOperationException(
                $"Page type \"{contentType.Name}\": {string.Join(" ", result.Messages.Select(m => m.Message))}");
    }

    private async Task<Content> SaveAsync(Content content, CancellationToken cancellationToken)
    {
        var result = await contentService.SaveContentAsync(
            new SaveContentParameters { Content = content }, cancellationToken);
        return result.Success
            ? result.Entity!
            : throw new InvalidOperationException(
                $"Page \"{content.Name}\": {string.Join(" ", result.Messages.Select(m => m.Message))}");
    }

    // A property is an editor placed on a tab. The editor is stored by its full type name and its alias; both
    // are taken from the editor itself, never typed out, so a rename cannot leave them behind.
    private static PropertyType TextBox(string name, Tab tab, int sortOrder) =>
        Property(name, tab, sortOrder, new TextProperty());

    private static PropertyType TextArea(string name, Tab tab, int sortOrder) =>
        Property(name, tab, sortOrder, new TextAreaProperty());

    private static PropertyType RichText(string name, Tab tab, int sortOrder) =>
        Property(name, tab, sortOrder, new ZauberCMSEditorProperty());

    private static PropertyType Property(string name, Tab tab, int sortOrder, IContentProperty editor) => new()
    {
        Name = name,
        Alias = name,
        Component = editor.GetType().FullName,
        ComponentAlias = editor.Alias,
        TabId = tab.Id,
        TabAlias = tab.Alias,
        SortOrder = sortOrder
    };

    // Every page type keeps the built-in System tab next to its own
    private static List<Tab> WithSystemTab(Tab tab)
    {
        var tabs = new ContentType().Tabs;
        tabs.Insert(0, tab);
        return tabs;
    }

    private static ContentPropertyValue Value(PropertyType property, string value) => new()
    {
        Alias = property.Alias!,
        ContentTypePropertyId = property.Id,
        Value = value
    };
}
