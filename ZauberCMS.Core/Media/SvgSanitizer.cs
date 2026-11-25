using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ZauberCMS.Core.Media;

/// <summary>
/// Makes an uploaded SVG safe to serve from the site's own origin.
/// An SVG is a document, not just a picture: it can carry scripts, event handlers and references to other
/// addresses. Opened directly in a browser those would run with the visitor's (or an admin's) session.
/// The sanitiser keeps drawing elements only and removes scripts, <c>foreignObject</c>, event handler attributes,
/// links, and every reference that is not to an element inside the same file (or an embedded bitmap).
/// </summary>
public static partial class SvgSanitizer
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "svg", "g", "defs", "symbol", "use", "switch", "title", "desc", "metadata", "style",
        "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "text", "tspan", "textPath", "image", "marker", "clipPath", "mask", "pattern",
        "linearGradient", "radialGradient", "stop", "view",
        "filter", "feBlend", "feColorMatrix", "feComponentTransfer", "feComposite", "feConvolveMatrix",
        "feDiffuseLighting", "feDisplacementMap", "feDistantLight", "feDropShadow", "feFlood", "feFuncA", "feFuncB",
        "feFuncG", "feFuncR", "feGaussianBlur", "feImage", "feMerge", "feMergeNode", "feMorphology", "feOffset",
        "fePointLight", "feSpecularLighting", "feSpotLight", "feTile", "feTurbulence",
        "animate", "animateTransform", "animateMotion", "mpath", "set"
    };

    private static readonly HashSet<string> AnimationElements = new(StringComparer.Ordinal)
    {
        "animate", "animateTransform", "animateMotion", "set"
    };

    /// <summary>
    /// Parses and cleans an SVG. Returns false when the input is not a well-formed SVG document; nothing should
    /// be stored in that case.
    /// </summary>
    public static bool TrySanitize(Stream input, out string sanitized)
    {
        sanitized = string.Empty;
        XDocument document;
        try
        {
            // No DTD processing and no resolver: entity expansion and external entities are not read.
            using var reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                MaxCharactersInDocument = 20_000_000
            });
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return false;
        }

        var root = document.Root;
        if (root == null || root.Name != Svg + "svg")
        {
            return false;
        }

        Clean(root);

        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = false }))
        {
            // Written from the root element, so a DOCTYPE or stylesheet instruction is not carried over.
            root.WriteTo(writer);
        }

        sanitized = builder.ToString();
        return true;
    }

    private static void Clean(XElement element)
    {
        foreach (var child in element.Elements().ToList())
        {
            if (child.Name.Namespace != Svg || !AllowedElements.Contains(child.Name.LocalName) || IsUnsafeAnimation(child))
            {
                child.Remove();
            }
            else
            {
                Clean(child);
            }
        }

        foreach (var attribute in element.Attributes().ToList())
        {
            if (!IsSafeAttribute(element, attribute))
            {
                attribute.Remove();
            }
            else if (attribute.Name.LocalName == "style")
            {
                attribute.Value = CleanCss(attribute.Value);
            }
        }

        if (element.Name.LocalName == "style")
        {
            element.Value = CleanCss(element.Value);
        }
    }

    /// <summary>An animation may not change where something points to (that is how a link becomes a script).</summary>
    private static bool IsUnsafeAnimation(XElement element)
    {
        if (!AnimationElements.Contains(element.Name.LocalName))
        {
            return false;
        }

        var target = element.Attribute("attributeName")?.Value.Trim() ?? string.Empty;
        return target.EndsWith("href", StringComparison.OrdinalIgnoreCase) ||
               target.StartsWith("on", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeAttribute(XElement element, XAttribute attribute)
    {
        if (attribute.IsNamespaceDeclaration)
        {
            // Declarations of the SVG and XLink namespaces are needed; others have no elements or attributes left.
            return attribute.Value == Svg.NamespaceName || attribute.Value == XLink.NamespaceName;
        }

        var name = attribute.Name;
        if (name.Namespace != XNamespace.None && name.Namespace != XLink && name.Namespace != XNamespace.Xml)
        {
            return false;
        }

        if (name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name.LocalName == "href" || name.LocalName == "src")
        {
            return IsSafeReference(element, attribute.Value);
        }

        // Presentation attributes (fill, filter, clip-path, mask, …) may point at an element of this file.
        return !ContainsExternalReference(attribute.Value);
    }

    private static bool IsSafeReference(XElement element, string value)
    {
        value = value.Trim();
        if (value.StartsWith('#'))
        {
            return true;
        }

        return element.Name.LocalName is "image" or "feImage" && EmbeddedBitmap().IsMatch(value);
    }

    private static bool ContainsExternalReference(string value) =>
        ScriptScheme().IsMatch(value) || CssUrl().Matches(value).Any(m => !m.Groups[1].Value.StartsWith('#'));

    private static string CleanCss(string css)
    {
        css = CssComment().Replace(css, string.Empty);
        css = CssImport().Replace(css, string.Empty);
        css = CssUrl().Replace(css, m => m.Groups[1].Value.StartsWith('#') ? m.Value : "none");
        css = ScriptScheme().Replace(css, string.Empty);
        return CssExpression().Replace(css, string.Empty);
    }

    [GeneratedRegex(@"^data:image/(png|jpeg|gif|webp);base64,[A-Za-z0-9+/=\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmbeddedBitmap();

    [GeneratedRegex(@"url\(\s*['""]?\s*([^'"")]*)['""]?\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex CssUrl();

    [GeneratedRegex(@"@import[^;]*;?", RegexOptions.IgnoreCase)]
    private static partial Regex CssImport();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CssComment();

    [GeneratedRegex(@"(java|vb)script\s*:|data\s*:\s*text", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptScheme();

    [GeneratedRegex(@"expression\s*\(|-moz-binding|behavior\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex CssExpression();
}
