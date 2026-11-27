using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Serilog;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Core.Data;

/// <summary>
/// Runs the <see cref="IAfterEntityCommit{TEntity}"/> hooks when the change is stored for good (Roadmap D62). A save
/// outside a transaction is committed by <c>SaveChanges</c> itself, so its hooks run at once. A save inside one
/// (<c>Database.BeginTransaction</c>) leaves its hooks with this interceptor, which runs them when that transaction
/// commits and drops them when it rolls back or fails, as Umbraco raises its "-ed" notifications when the scope
/// completes. The interceptor is stateless; what waits is kept per context.
/// </summary>
internal sealed class AfterCommitHooks : DbTransactionInterceptor
{
    /// <summary>The one instance each ZauberCMS DbContext adds in <c>OnConfiguring</c>.</summary>
    internal static readonly AfterCommitHooks Interceptor = new();

    private static readonly ConditionalWeakTable<DbContext, Waiting> WaitingByContext = new();

    private AfterCommitHooks()
    {
    }

    /// <summary>One hook call for one committed row.</summary>
    internal sealed record Call(string HookName, Func<CancellationToken, Task> Run);

    private sealed class Waiting
    {
        public Guid TransactionId;
        public List<Call> Calls { get; } = [];
    }

    /// <summary>
    /// Runs <paramref name="calls"/> now when <paramref name="context"/> has no transaction open (the save was
    /// committed), or keeps them for the commit of the open one.
    /// </summary>
    internal static Task RunOrDeferAsync(DbContext? context, IReadOnlyList<Call> calls,
        CancellationToken cancellationToken)
    {
        if (calls.Count == 0) return Task.CompletedTask;
        var transaction = context?.Database.CurrentTransaction;
        if (context == null || transaction == null)
        {
            return RunAsync(calls, cancellationToken);
        }

        if (!IsAdded(context))
        {
            // A context built without the interceptor (a site's own IZauberDbContext) never tells us about the
            // commit: run the hooks now rather than never, and say so once
            WarnOnce(context);
            return RunAsync(calls, cancellationToken);
        }

        var waiting = WaitingByContext.GetOrCreateValue(context);
        lock (waiting)
        {
            if (waiting.TransactionId != transaction.TransactionId)
            {
                // Left from a transaction that was disposed without a commit or a rollback: it never happened
                waiting.TransactionId = transaction.TransactionId;
                waiting.Calls.Clear();
            }

            waiting.Calls.AddRange(calls);
        }

        return Task.CompletedTask;
    }

    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        await RunAsync(Take(eventData), cancellationToken);

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        var calls = Take(eventData);
        if (calls.Count == 0) return;
        // A synchronous Commit(): the hooks are asynchronous, and run off the caller's context so a wait from a
        // Blazor circuit can't deadlock on their continuations
        Task.Run(() => RunAsync(calls, CancellationToken.None)).GetAwaiter().GetResult();
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        Take(eventData);

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Take(eventData);
        return Task.CompletedTask;
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) =>
        Drop(eventData.Context);

    public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Drop(eventData.Context);
        return Task.CompletedTask;
    }

    /// <summary>The calls waiting for this transaction, removed from the context.</summary>
    private static List<Call> Take(TransactionEndEventData eventData)
    {
        if (eventData.Context == null || !WaitingByContext.TryGetValue(eventData.Context, out var waiting))
        {
            return [];
        }

        lock (waiting)
        {
            if (waiting.Calls.Count == 0) return [];
            var calls = waiting.TransactionId == eventData.TransactionId ? waiting.Calls.ToList() : [];
            waiting.Calls.Clear();
            return calls;
        }
    }

    private static void Drop(DbContext? context)
    {
        if (context == null || !WaitingByContext.TryGetValue(context, out var waiting)) return;
        lock (waiting) waiting.Calls.Clear();
    }

    /// <summary>
    /// Each call in order. The change is stored, so a hook that throws is logged and the others still run; the
    /// caller's result stays a success.
    /// </summary>
    private static async Task RunAsync(IReadOnlyList<Call> calls, CancellationToken cancellationToken)
    {
        foreach (var call in calls)
        {
            try
            {
                await call.Run(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "The after-commit hook {Hook} failed; the change it was told about is stored",
                    call.HookName);
            }
        }
    }

    private static bool IsAdded(DbContext context) =>
        context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.Interceptors?
            .Contains(Interceptor) == true;

    private static readonly ConditionalWeakTable<Type, object> Warned = new();

    private static void WarnOnce(DbContext context)
    {
        if (Warned.TryAdd(context.GetType(), new object()))
        {
            Log.Warning(
                "{Context} does not add ZauberCMS's after-commit interceptor, so IAfterEntityCommit hooks of a save " +
                "inside a transaction run before that transaction commits", context.GetType().FullName);
        }
    }
}
