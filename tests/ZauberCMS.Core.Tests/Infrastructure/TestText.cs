using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Core.Tests.Infrastructure;

/// <summary>
/// ZauberCMS's text as the site has it (Roadmap D90, D92), for a test that builds a check or calls a helper itself
/// instead of going through <see cref="TestSite"/>: Core's English, in the test's current culture.
/// </summary>
internal static class TestText
{
    private static readonly Lazy<IStringLocalizer<ZauberText>> English = new(() =>
    {
        var core = typeof(ZauberText).Assembly;
        return new ServiceCollection().AddLogging().AddZauberText([core], [core]).BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<ZauberText>>();
    });

    public static IStringLocalizer<ZauberText> Localizer => English.Value;
}
