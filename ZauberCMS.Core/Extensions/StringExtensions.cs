using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace ZauberCMS.Core.Extensions;

public static partial class StringExtensions
{
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value) => string.IsNullOrWhiteSpace(value);

    [return: NotNullIfNotNull("defaultValue")]
    public static string? IfNullOrWhiteSpace(this string? str, string? defaultValue) =>
        str.IsNullOrWhiteSpace() ? defaultValue : str;

    /// <summary>
    /// Takes Json from another system, and then uses the JsonSerializer
    /// </summary>
    /// <param name="s"></param>
    /// <returns>non indented/compressed whitespace json in a string</returns>
    public static string? CompressJson(this string? s)
    {
        if (s != null)
        {
            JsonSerializerOptions? options = null;
            var tempObj = JsonSerializer.Deserialize<object>(s, options);
            return JsonSerializer.Serialize(tempObj);
        }

        return s;
    }

    /// <summary>
    /// Removes or replaces all line breaks in a string
    /// </summary>
    /// <param name="s"></param>
    /// <param name="replaceWith"></param>
    /// <returns></returns>
    public static string RemoveAllLineBreaks(this string? s, string replaceWith = "")
    {
        if (!s.IsNullOrWhiteSpace())
        {
            return s.Replace("\r\n", replaceWith).Replace("\n", replaceWith).Replace("\r", replaceWith);
        }

        return string.Empty;
    }

    /// <summary>
    /// Splits a string into a list based on a separator
    /// </summary>
    /// <param name="source"></param>
    /// <param name="separator"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<T> SplitStringUsing<T>(string? source, string separator = ",")
        where T : IConvertible
    {
        if (source != null)
        {
            return source.Split(Convert.ToChar(separator))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => Convert.ChangeType(x, typeof(T)))
                .Cast<T>()
                .Where(x => x != null)
                .ToList();
        }

        return Enumerable.Empty<T>();
    }

    /// <summary>
    /// Splits a string into a list of Guids
    /// </summary>
    /// <param name="source"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    public static IEnumerable<Guid> SplitStringGuid(string? source, string separator = ",")
    {
        if (source != null)
        {
            return source.Split(Convert.ToChar(separator))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(Guid.Parse)
                .ToList();
        }

        return Enumerable.Empty<Guid>();
    }

    /// <summary>
    /// Does this string contain any of the strings from the list
    /// </summary>
    /// <param name="s"></param>
    /// <param name="strings"></param>
    /// <returns></returns>
    public static bool Contains(this string s, List<string> strings)
    {
        if (strings?.Count > 0 && !s.IsNullOrWhiteSpace())
        {
            foreach (var str in strings)
            {
                if (s.Contains(str, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }


    /// <summary>
    ///     Ensures a string does not end with a character.
    /// </summary>
    /// <param name="input">
    ///     The input string.
    /// </param>
    /// <param name="value">
    ///     The char value to assert
    /// </param>
    /// <returns>
    ///     The asserted string.
    /// </returns>
    public static string EnsureNotEndsWith(this string input, char value)
    {
        return !input.EndsWith(value.ToString(CultureInfo.InvariantCulture))
            ? input
            : input.Remove(input.LastIndexOf(value), 1);
    }

    /// <summary>
    ///     Ensures a string does not start with a character.
    /// </summary>
    /// <param name="input">
    ///     The input string.
    /// </param>
    /// <param name="value">
    ///     The char value to assert
    /// </param>
    /// <returns>
    ///     The asserted string.
    /// </returns>
    public static string EnsureNotStartsWith(this string input, char value)
    {
        return !input.StartsWith(value.ToString(CultureInfo.InstalledUICulture)) ? input : input.Remove(0, 1);
    }

    /// <summary>
    ///     Ensures a string does not start or end with a character.
    /// </summary>
    /// <param name="input">
    ///     The input string.
    /// </param>
    /// <param name="value">
    ///     The char value to assert
    /// </param>
    /// <returns>
    ///     The asserted string.
    /// </returns>
    public static string EnsureNotStartsOrEndsWith(this string input, char value)
    {
        return input.EnsureNotStartsWith(value).EnsureNotEndsWith(value);
    }

    /// <summary>
    ///     Encodes url segments.
    /// </summary>
    /// <param name="urlPath">
    ///     The url path.
    /// </param>
    /// <returns>
    ///     The <see cref="string" />.
    /// </returns>
    /// <seealso cref="https://github.com/Shandem/Articulate/blob/master/Articulate/StringExtensions.cs" />
    private static string SafeEncodeUrlSegments(this string urlPath)
    {
        return string.Join(
            "/",
            urlPath.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                {
                    var urlEncode = HttpUtility.UrlEncode(x);
                    return urlEncode != null ? urlEncode.Replace("+", "-") : null;
                })
                .WhereNotNull()

                //// we are not supporting dots in our URLs it's just too difficult to
                //// support across the board with all the different config options
                .Select(x => x.Replace('.', '-')));
    }

    /// <summary>
    ///     The remove special characters.
    /// </summary>
    /// <param name="input">
    ///     The input.
    /// </param>
    /// <returns>
    ///     The <see cref="string" />.
    /// </returns>
    internal static string RemoveSpecialCharacters(string input)
    {
        var regex = new Regex("(?:[^a-z0-9 ]|(?<=['\"])s)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        return regex.Replace(input, string.Empty);
    }

    public static string? ToMd5(this string str)
    {
        return string.IsNullOrEmpty(str) ? null : Encoding.ASCII.GetBytes(str).ToMd5();
    }

    private static string? ToMd5(this byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null;

        using var md5 = MD5.Create();
        return string.Join("", md5.ComputeHash(bytes).Select(x => x.ToString("X2")));
    }

    public static List<int> ToListInt(this string s)
    {
        return !s.IsNullOrWhiteSpace() ? s.Split(',').Select(int.Parse).ToList() : new List<int>();
    }

    public static string Truncate(this string source, int length)
    {
        if (!source.IsNullOrWhiteSpace())
        {
            if (source.Length > length)
            {
                source = source[..length];
                source = Regex.Replace(source, @"\t|\n|\r", "");
            }
        }

        return source;
    }

    public static string ReduceWhitespace(this string value)
    {
        var newString = new StringBuilder();
        var previousIsWhitespace = false;
        foreach (var t in value)
        {
            if (char.IsWhiteSpace(t))
            {
                if (previousIsWhitespace)
                {
                    continue;
                }

                previousIsWhitespace = true;
            }
            else
            {
                previousIsWhitespace = false;
            }

            newString.Append(t);
        }

        return newString.ToString();
    }

    public static string ToYouTubeVideo(this string str)
    {
        if (str.IndexOf("youtube.com", StringComparison.CurrentCultureIgnoreCase) > 0 ||
            str.IndexOf("youtu.be", StringComparison.CurrentCultureIgnoreCase) > 0)
        {
            const string pattern =
                @"(?:https?:\/\/)?(?:www\.)?(?:(?:(?:youtube.com\/watch\?[^?]*v=|youtu.be\/)([\w\-]+))(?:[^\s?]+)?)";
            const string replacement =
                "<iframe class='w-full aspect-video' src='https://www.youtube.com/embed/$1?rel=0' loading='lazy' frameborder='0' allow='accelerometer; autoplay; gyroscope' allowfullscreen></iframe>";

            var rgx = new Regex(pattern);
            str = rgx.Replace(str, replacement);
            return str;
        }

        return string.Empty;
    }

    /*public static string ConvertVimeoVideo(string str)
    {
        if (str.IndexOf("vimeo.com", StringComparison.CurrentCultureIgnoreCase) > 0)
        {
            const string pattern = @"(?:https?:\/\/)?vimeo\.com/(?:.*#|.#1#videos/)?([0-9]+)";
            const string replacement =
                "<iframe src=\"https://player.vimeo.com/video/$1?portrait=0\" loading='lazy' frameborder=\"0\" allow='accelerometer; autoplay; gyroscope' allowfullscreen></iframe>";

            var rgx = new Regex(pattern);
            str = rgx.Replace(str, replacement);
        }

        return str;
    }

    public static string ConvertScreenrVideo(string str)
    {
        if (str.IndexOf("screenr.com", StringComparison.CurrentCultureIgnoreCase) > 0)
        {
            const string pattern = @"(?:https?:\/\/)?(?:www\.)screenr\.com/([a-zA-Z0-9]+)";
            const string replacement =
                "<iframe src=\"https://www.screenr.com/embed/$1\" width=\"500\" height=\"281\" frameborder=\"0\"></iframe>";

            var rgx = new Regex(pattern);
            str = rgx.Replace(str, replacement);
        }

        return str;
    }*/

    [GeneratedRegex(
        "(?:https?:\\/\\/)?(?:www\\.)?(?:(?:(?:youtube.com\\/watch\\?[^?]*v=|youtu.be\\/)([\\w\\-]+))(?:[^\\s?]+)?)")]
    private static partial Regex MyRegex();
}