namespace ZauberCMS.Core.Localization;

/// <summary>
/// The text ZauberCMS shows: the admin, the installer and the account pages (Roadmap D37). Ask for
/// <c>IStringLocalizer&lt;ZauberText&gt;</c> and look a text up by its key, <c>area_name</c>
/// (<c>L["general_save"]</c>, <c>L["contextMenus_deleteConfirm", page.Name]</c>).
/// </summary>
/// <remarks>
/// <para>
/// The texts are resource files (<c>.resx</c>) in a folder named <see cref="ResourceFolder"/>, one file per area
/// (<c>General.resx</c> holds the <c>general_*</c> keys). ZauberCMS ships English; its files are the neutral ones.
/// A site, a plugin or a language pack in an assembly <c>AddZauberCms</c> scans adds files of its own in a
/// <see cref="ResourceFolder"/> folder: <c>General.fr.resx</c> translates keys into French, <c>General.en.resx</c>
/// changes the English wording, a file with no culture changes the text in every language without a translation
/// of that key. A plugin brings its own area the same way (<c>MyPlugin.resx</c>, keys <c>myPlugin_*</c>).
/// </para>
/// <para>
/// A key is looked up in the most specific culture first (<c>fr-CA</c>, then <c>fr</c>, then the neutral files);
/// at each step the site wins over the plugins (in the order passed to <c>AddZauberCms</c>) and the plugins over
/// ZauberCMS. A key no file has shows as the key itself and is logged once. A site that keeps its texts somewhere
/// else registers its own <c>IStringLocalizer&lt;ZauberText&gt;</c>. Docs/AdminUI.md "Localisation".
/// </para>
/// </remarks>
public sealed class ZauberText
{
    /// <summary>
    /// The name of the folder (the namespace segment of the resource's name) whose <c>.resx</c> files hold
    /// ZauberCMS's text, in ZauberCMS's own assemblies and in a site's or plugin's.
    /// </summary>
    public const string ResourceFolder = "ZauberText";

    private ZauberText()
    {
    }
}
