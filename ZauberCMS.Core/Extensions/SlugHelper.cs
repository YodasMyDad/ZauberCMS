using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ZauberCMS.Core.Extensions;

    public class SlugHelper
    {
        private Config Configuration { get; }

        public SlugHelper() :
            this(new Config())
        {

        }

        public SlugHelper(Config? config)
        {
            Configuration = config ?? new Config();
        }

        public string GenerateSlug(string? str)
        {
            if (!str.IsNullOrWhiteSpace())
            {
                if (Configuration is { UseCamelCase: false, ForceLowerCase: true })
                {
                    str = str.ToLower();
                }
                str = CleanWhiteSpace(str, Configuration.CollapseWhiteSpace);
                str = ApplyReplacements(str, Configuration.CharacterReplacements);
                str = RemoveDiacritics(str);
                str = DeleteCharacters(str, Configuration.DeniedCharactersRegex);
                return str;   
            }

            return string.Empty;
        }

        private static string CleanWhiteSpace(string str, bool collapse)
        {
            return Regex.Replace(str, collapse ? @"\s+" : @"\s", " ");
        }

        // Thanks http://stackoverflow.com/a/249126!
        private static string RemoveDiacritics(string str)
        {
            var stFormD = str.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var t in stFormD)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(t);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(t);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string ApplyReplacements(string str, Dictionary<string, string> replacements)
        {
            var sb = new StringBuilder(str);

            foreach (var replacement in replacements)
                sb.Replace(replacement.Key, replacement.Value);

            return sb.ToString();
        }

        private static string DeleteCharacters(string str, string regex)
        {
            return Regex.Replace(str, regex, "");
        }

        public class Config
        {
            public Dictionary<string, string> CharacterReplacements { get; set; } = new() {{" ", "-"}};
            public bool ForceLowerCase { get; set; } = true;
            public bool CollapseWhiteSpace { get; set; } = true;
            public bool UseCamelCase { get; set; }
            public string DeniedCharactersRegex { get; set; } = @"[^a-zA-Z0-9\-\._]";
        }

    }