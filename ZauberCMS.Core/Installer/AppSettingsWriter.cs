using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ZauberCMS.Core.Installer;

/// <summary>
/// Writes the database choice into a site's <c>appsettings.json</c>, leaving every other key as it was.
/// Comments in the file are not kept (JSON has none; the configuration reader only tolerates them).
/// </summary>
internal static class AppSettingsWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Connection strings are full of ' = ; + and the file is not HTML: keep them readable
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Sets <c>Zauber:DatabaseProvider</c> and <c>Zauber:ConnectionString</c> in the file at <paramref name="path"/>,
    /// creating the file when it is missing. Returns the reason when the file cannot be read as JSON or written.
    /// </summary>
    public static AppSettingsWriteResult Write(string path, string databaseProvider, string connectionString)
    {
        try
        {
            var hasBom = false;
            JsonObject root;
            if (File.Exists(path))
            {
                var bytes = File.ReadAllBytes(path);
                hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
                var text = Encoding.UTF8.GetString(hasBom ? bytes.AsSpan(Encoding.UTF8.Preamble.Length) : bytes);
                if (string.IsNullOrWhiteSpace(text))
                {
                    root = new JsonObject();
                }
                else if (JsonNode.Parse(text, documentOptions: ReadOptions) is JsonObject parsed)
                {
                    root = parsed;
                }
                else
                {
                    return new AppSettingsWriteResult(false, "appsettings.json does not contain a JSON object.");
                }
            }
            else
            {
                root = new JsonObject();
            }

            Set(root, "DatabaseProvider", databaseProvider);
            Set(root, "ConnectionString", connectionString);

            var json = root.ToJsonString(WriteOptions) + Environment.NewLine;
            var output = new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom);
            WriteAtomically(path, output.GetPreamble().Concat(output.GetBytes(json)).ToArray());
            return new AppSettingsWriteResult(true, null);
        }
        // ArgumentException: a file that has the same key twice cannot be edited as a JSON object
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                       or NotSupportedException or System.Security.SecurityException
                                       or ArgumentException or InvalidOperationException)
        {
            return new AppSettingsWriteResult(false, ex.Message);
        }
    }

    /// <summary>The JSON a site owner adds to <c>appsettings.json</c> by hand when the file cannot be written.</summary>
    public static string Snippet(string databaseProvider, string connectionString) =>
        new JsonObject
        {
            [Constants.SettingsConfigName] = new JsonObject
            {
                ["DatabaseProvider"] = databaseProvider,
                ["ConnectionString"] = connectionString
            }
        }.ToJsonString(WriteOptions);

    // The value goes where the file already has the key, in the spelling it has there (configuration keys are
    // not case-sensitive): as a flat "Zauber:Key" at the top, which the JSON provider also accepts, or inside
    // the "Zauber" object. Writing it in the other place as well would be the same key twice, which the
    // configuration reader refuses.
    private static void Set(JsonObject root, string key, string value)
    {
        var flat = FindKey(root, $"{Constants.SettingsConfigName}:{key}");
        if (flat != null)
        {
            root[flat] = value;
            return;
        }

        var sectionKey = FindKey(root, Constants.SettingsConfigName) ?? Constants.SettingsConfigName;
        if (root[sectionKey] is not JsonObject section)
        {
            section = new JsonObject();
            root[sectionKey] = section;
        }

        section[FindKey(section, key) ?? key] = value;
    }

    private static string? FindKey(JsonObject node, string key) =>
        node.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

    // The configuration system reloads the file as soon as it changes. A reader must never see half of it, so
    // the new content goes to a temporary file that then replaces the original in one step.
    private static void WriteAtomically(string path, byte[] content)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The folder refuses new files but the file itself may still be writable
            File.WriteAllBytes(path, content);
            return;
        }

        try
        {
            // The file is about to hold a database password: the replacement must not be more readable than
            // the original was
            if (!File.Exists(path))
            {
                File.Move(temp, path);
            }
            else if (OperatingSystem.IsWindows())
            {
                // Keeps the original's access control list
                File.Replace(temp, path, destinationBackupFileName: null);
            }
            else
            {
                File.SetUnixFileMode(temp, File.GetUnixFileMode(path));
                File.Move(temp, path, overwrite: true);
            }
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
                // Nothing more to do: report the original failure
            }

            throw;
        }
    }
}

/// <param name="Success">The file now holds the values.</param>
/// <param name="Error">Why it does not, when <paramref name="Success"/> is false.</param>
internal sealed record AppSettingsWriteResult(bool Success, string? Error);
