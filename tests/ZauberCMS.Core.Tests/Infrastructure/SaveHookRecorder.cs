using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Interfaces;
using ZauberCMS.Core.Tags.Models;
using ContentItem = ZauberCMS.Core.Content.Models.Content;
using MediaItem = ZauberCMS.Core.Media.Models.Media;

namespace ZauberCMS.Core.Tests.Infrastructure;

/// <summary>
/// Records the save hooks a test's service calls run, and can veto a row. The hooks below are discovered for every
/// test in the assembly, so they only act on the async flow of a test that called <see cref="Start"/>.
/// </summary>
public sealed class SaveHookRecorder : IDisposable
{
    private static readonly AsyncLocal<SaveHookRecorder?> CurrentRecorder = new();

    public static SaveHookRecorder? Current => CurrentRecorder.Value;

    private readonly List<(bool Before, object Row, EntityState State)> _calls = [];

    /// <summary>Returns true for a row a before-save hook should refuse.</summary>
    public Func<object, EntityState, bool>? Veto { get; set; }

    public static SaveHookRecorder Start() => CurrentRecorder.Value = new SaveHookRecorder();

    public void Dispose() => CurrentRecorder.Value = null;

    public void Record(bool before, object row, EntityState state)
    {
        lock (_calls) _calls.Add((before, row, state));
    }

    /// <summary>The rows of type <typeparamref name="T"/> the before-save hooks saw in <paramref name="state"/>.</summary>
    public List<T> Before<T>(EntityState state) => Rows<T>(true, state);

    /// <summary>The rows of type <typeparamref name="T"/> the after-save hooks saw in <paramref name="state"/>.</summary>
    public List<T> After<T>(EntityState state) => Rows<T>(false, state);

    public void Clear()
    {
        lock (_calls) _calls.Clear();
    }

    private List<T> Rows<T>(bool before, EntityState state)
    {
        lock (_calls)
        {
            return _calls.Where(c => c.Before == before && c.State == state).Select(c => c.Row).OfType<T>().ToList();
        }
    }
}

public abstract class RecordingSaveHook<T> : IBeforeEntitySave<T>, IAfterEntitySave<T> where T : class
{
    public int SortOrder => 0;

    public bool BeforeSave(T entity, EntityState entityState)
    {
        var recorder = SaveHookRecorder.Current;
        if (recorder == null) return true;
        recorder.Record(true, entity, entityState);
        return recorder.Veto?.Invoke(entity, entityState) != true;
    }

    public bool AfterSave(T entity, EntityState entityState)
    {
        SaveHookRecorder.Current?.Record(false, entity, entityState);
        return false;
    }
}

public class RecordingContentSaveHook : RecordingSaveHook<ContentItem>;

public class RecordingUnpublishedContentSaveHook : RecordingSaveHook<UnpublishedContent>;

public class RecordingContentTypeSaveHook : RecordingSaveHook<ContentType>;

public class RecordingContentVersionSaveHook : RecordingSaveHook<ContentVersion>;

public class RecordingMediaSaveHook : RecordingSaveHook<MediaItem>;

public class RecordingTagItemSaveHook : RecordingSaveHook<TagItem>;
