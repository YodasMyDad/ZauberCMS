namespace ZauberCMS.Core.Installer.Models;

/// <summary>One choice in an installer list.</summary>
/// <param name="Value">What the form posts.</param>
/// <param name="Name">What people read.</param>
/// <param name="Description">A sentence under the name, where the list shows one.</param>
public sealed record InstallerOption(string Value, string Name, string? Description = null);
