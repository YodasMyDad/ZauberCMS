using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Interfaces;
using ZauberCMS.Core.Tags.Models;
using ContentItem = ZauberCMS.Core.Content.Models.Content;
using MediaItem = ZauberCMS.Core.Media.Models.Media;

namespace ZauberCMS.Core.Tests.Infrastructure;

/// <summary>
/// Records the save hooks a test's service calls run (before-save, after-save, after-commit), and can veto a row or
/// make an after-commit hook throw. The hooks below are discovered for every test in the assembly, so they only act
/// on the async flow of a test that called <see cref="Start"/>.
/// </summary>
public sealed class SaveHookRecorder : IDisposable
{
    private static readonly AsyncLocal<SaveHookRecorder?> CurrentRecorder = new();

    public static SaveHookRecorder? Current => CurrentRecorder.Value;

    public enum Hook
    {
        Before,
        After,
        Committed
    }

    private readonly List<(Hook Hook, object Row, EntityState State)> _calls = [];

    /// <summary>Returns true for a row a before-save hook should refuse.</summary>
    public Func<object, EntityState, bool>? Veto { get; set; }

    /// <summary>When set, <see cref="ThrowingMediaCommitHook"/> (which runs first) throws for every media row.</summary>
    public bool ThrowOnCommit { get; set; }

    public static SaveHookRecorder Start() => CurrentRecorder.Value = new SaveHookRecorder();

    public void Dispose() => CurrentRecorder.Value = null;

    public void Record(Hook hook, object row, EntityState state)
    {
        lock (_calls) _calls.Add((hook, row, state));
    }

    /// <summary>The rows of type <typeparamref name="T"/> the before-save hooks saw in <paramref name="state"/>.</summary>
    public List<T> Before<T>(EntityState state) => Rows<T>(Hook.Before, state);

    /// <summary>The rows of type <typeparamref name="T"/> the after-save hooks saw in <paramref name="state"/>.</summary>
    public List<T> After<T>(EntityState state) => Rows<T>(Hook.After, state);

    /// <summary>The rows of type <typeparamref name="T"/> the after-commit hooks saw in <paramref name="state"/>.</summary>
    public List<T> Committed<T>(EntityState state) => Rows<T>(Hook.Committed, state);

    /// <summary>Every after-commit call so far, of any type and state.</summary>
    public int CommittedCount
    {
        get
        {
            lock (_calls) return _calls.Count(c => c.Hook == Hook.Committed);
        }
    }

    public void Clear()
    {
        lock (_calls) _calls.Clear();
    }

    private List<T> Rows<T>(Hook hook, EntityState state)
    {
        lock (_calls)
        {
            return _calls.Where(c => c.Hook == hook && c.State == state).Select(c => c.Row).OfType<T>().ToList();
        }
    }
}

public abstract class RecordingSaveHook<T> : IBeforeEntitySave<T>, IAfterEntitySave<T>, IAfterEntityCommit<T>
    where T : class
{
    public int SortOrder => 0;

    public bool BeforeSave(T entity, EntityState entityState)
    {
        var recorder = SaveHookRecorder.Current;
        if (recorder == null) return true;
        recorder.Record(SaveHookRecorder.Hook.Before, entity, entityState);
        return recorder.Veto?.Invoke(entity, entityState) != true;
    }

    public bool AfterSave(T entity, EntityState entityState)
    {
        SaveHookRecorder.Current?.Record(SaveHookRecorder.Hook.After, entity, entityState);
        return false;
    }

    public Task AfterCommitAsync(T entity, EntityState entityState, CancellationToken cancellationToken = default)
    {
        SaveHookRecorder.Current?.Record(SaveHookRecorder.Hook.Committed, entity, entityState);
        return Task.CompletedTask;
    }
}

public class RecordingContentSaveHook : RecordingSaveHook<ContentItem>;

public class RecordingUnpublishedContentSaveHook : RecordingSaveHook<UnpublishedContent>;

public class RecordingContentTypeSaveHook : RecordingSaveHook<ContentType>;

public class RecordingContentVersionSaveHook : RecordingSaveHook<ContentVersion>;

public class RecordingMediaSaveHook : RecordingSaveHook<MediaItem>;

public class RecordingTagItemSaveHook : RecordingSaveHook<TagItem>;

public class RecordingDomainSaveHook : RecordingSaveHook<Domain>;

public class RecordingSeoRedirectSaveHook : RecordingSaveHook<SeoRedirect>;

/// <summary>An after-commit hook that runs before the recording one and throws when the test asks it to.</summary>
public class ThrowingMediaCommitHook : IAfterEntityCommit<MediaItem>
{
    public int SortOrder => -1;

    public Task AfterCommitAsync(MediaItem entity, EntityState entityState,
        CancellationToken cancellationToken = default) =>
        SaveHookRecorder.Current?.ThrowOnCommit == true
            ? throw new InvalidOperationException("after-commit hook failure for the test")
            : Task.CompletedTask;
}
