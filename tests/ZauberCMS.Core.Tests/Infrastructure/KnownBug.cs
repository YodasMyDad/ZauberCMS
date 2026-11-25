namespace ZauberCMS.Core.Tests.Infrastructure;

/// <summary>
/// Skip reasons for tests that describe the correct behaviour of a bug that is still open in .audit/report.md.
/// Each was run once without the skip and failed for the stated reason. When the finding is fixed, remove the
/// skip in the same change: the test is then the regression test for that fix.
/// </summary>
public static class KnownBug
{
    public const string Za011 =
        "ZA-011 (open): Identity and the services get two different DbContext instances per scope.";
}
