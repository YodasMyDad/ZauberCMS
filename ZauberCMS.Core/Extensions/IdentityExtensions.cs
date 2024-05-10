using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ZauberCMS.Core.Extensions;

public static class IdentityExtensions
{
    public static void LogErrors<T>(this IdentityResult identityResult, ILogger<T> logger)
    {
        foreach (var identityResultError in identityResult.Errors)
        {
            logger.LogError(identityResultError.Description);
        }
    }

    public static void LogErrors(this IdentityResult identityResult)
    {
        foreach (var identityResultError in identityResult.Errors)
        {
            Log.Error(identityResultError.Description);
        }
    }

    public static IEnumerable<string> ToErrorsList(this IdentityResult identityResult)
    {
        return identityResult.Errors.Select(x => x.Description);
    }
}