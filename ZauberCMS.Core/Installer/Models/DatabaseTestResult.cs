namespace ZauberCMS.Core.Installer.Models;

/// <summary>The outcome of trying a database connection in the installer.</summary>
/// <param name="Success">The site can use this database (or create it).</param>
/// <param name="DatabaseExists">The database is already there; false when it will be created.</param>
/// <param name="Message">What happened, written for the person installing the site.</param>
public sealed record DatabaseTestResult(bool Success, bool DatabaseExists, string Message);
