using ZauberCMS.Components.Editors.Models;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Tests.Editors;

/// <summary>
/// Change tracking of the block list editor. What ends up in Added / Updated / Deleted is exactly what the root
/// save writes to the database, so mistakes here are data loss (Docs/BlockListEditor.md).
/// </summary>
public class BlockListStateTests
{
    private static Content Block(string name, Guid? id = null) =>
        new() { Id = id ?? Guid.NewGuid(), Name = name, IsNestedContent = true };

    private static BlockListState StateWithSaved(params Content[] saved) => new() { Items = [..saved] };

    [Fact]
    public void Adding_a_block_appends_it_and_tracks_it_as_added()
    {
        var existing = Block("existing");
        var added = Block("new");

        var state = StateWithSaved(existing).AddItem(added);

        Assert.Equal([existing.Id, added.Id], state.GetSerializedIds());
        Assert.Same(added, Assert.Single(state.AddedItems));
        Assert.Empty(state.UpdatedItems);
        Assert.Empty(state.DeletedItems);
    }

    [Fact]
    public void Adding_a_block_at_an_index_inserts_it_there()
    {
        var first = Block("first");
        var second = Block("second");
        var inserted = Block("inserted");

        var state = StateWithSaved(first, second).AddItem(inserted, insertAtIndex: 1);

        Assert.Equal([first.Id, inserted.Id, second.Id], state.GetSerializedIds());
    }

    [Fact]
    public void Operations_return_a_new_state_and_leave_the_original_untouched()
    {
        var original = StateWithSaved(Block("existing"));

        var changed = original.AddItem(Block("new"));

        Assert.NotSame(original, changed);
        Assert.Single(original.Items);
        Assert.Empty(original.AddedItems);
    }

    [Fact]
    public void Updating_a_saved_block_tracks_it_as_updated_once()
    {
        var saved = Block("v1");
        var edit1 = Block("v2", saved.Id);
        var edit2 = Block("v3", saved.Id);

        var state = StateWithSaved(saved).UpdateItem(edit1).UpdateItem(edit2);

        Assert.Same(edit2, Assert.Single(state.UpdatedItems));
        Assert.Same(edit2, Assert.Single(state.Items));
        Assert.Empty(state.AddedItems);
    }

    [Fact]
    public void Updating_a_block_added_in_this_session_keeps_it_as_added_with_the_new_values()
    {
        var added = Block("v1");
        var edited = Block("v2", added.Id);

        var state = new BlockListState().AddItem(added).UpdateItem(edited);

        Assert.Same(edited, Assert.Single(state.AddedItems));
        Assert.Empty(state.UpdatedItems);
    }

    [Fact]
    public void Updating_an_unknown_block_changes_nothing()
    {
        var state = StateWithSaved(Block("existing"));

        Assert.Same(state, state.UpdateItem(Block("stranger")));
    }

    [Fact]
    public void Deleting_a_saved_block_tracks_it_as_deleted_and_drops_its_pending_update()
    {
        var saved = Block("saved");

        var state = StateWithSaved(saved).UpdateItem(Block("edited", saved.Id)).DeleteItem(saved.Id);

        Assert.Empty(state.Items);
        Assert.Equal(saved.Id, Assert.Single(state.DeletedItems).Id);
        Assert.Empty(state.UpdatedItems);
    }

    [Fact]
    public void Deleting_a_block_added_in_this_session_leaves_nothing_to_save()
    {
        var added = Block("new");

        var state = new BlockListState().AddItem(added).DeleteItem(added.Id);

        Assert.Empty(state.Items);
        Assert.Empty(state.AddedItems);
        Assert.Empty(state.DeletedItems);
        Assert.False(state.GetChanges().HasChanges);
    }

    [Fact]
    public void Reordering_moves_the_block_without_recording_a_change()
    {
        var a = Block("a");
        var b = Block("b");
        var c = Block("c");

        var state = StateWithSaved(a, b, c).ReorderItems(oldIndex: 0, newIndex: 2);

        Assert.Equal([b.Id, c.Id, a.Id], state.GetSerializedIds());
        Assert.False(state.GetChanges().HasChanges);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    public void Reordering_with_an_invalid_or_unchanged_position_changes_nothing(int oldIndex, int newIndex)
    {
        var state = StateWithSaved(Block("a"), Block("b"), Block("c"));

        Assert.Same(state, state.ReorderItems(oldIndex, newIndex));
    }

    [Fact]
    public void Merging_nested_changes_collects_adds_updates_and_deletes()
    {
        var nestedAdded = Block("nested added");
        var nestedUpdated = Block("nested updated");
        var nestedDeleted = Block("nested deleted");

        var state = new BlockListState().MergeNestedChanges(new BlockListChanges
        {
            AddedItems = [nestedAdded],
            UpdatedItems = [nestedUpdated],
            DeletedItems = [nestedDeleted]
        });

        Assert.Same(nestedAdded, Assert.Single(state.AddedItems));
        Assert.Same(nestedUpdated, Assert.Single(state.UpdatedItems));
        Assert.Same(nestedDeleted, Assert.Single(state.DeletedItems));
    }

    [Fact]
    public void Merging_a_nested_delete_cancels_the_pending_add_or_update_of_that_block()
    {
        var pendingAdd = Block("pending add");
        var pendingUpdate = Block("pending update");
        var state = new BlockListState
        {
            AddedItems = [pendingAdd],
            UpdatedItems = [pendingUpdate]
        };

        var merged = state.MergeNestedChanges(new BlockListChanges { DeletedItems = [pendingAdd, pendingUpdate] });

        Assert.Empty(merged.AddedItems);
        Assert.Empty(merged.UpdatedItems);
    }

    [Fact]
    public void Merging_no_changes_returns_the_same_state()
    {
        var state = StateWithSaved(Block("existing"));

        Assert.Same(state, state.MergeNestedChanges(null));
    }

    // Add B1 containing a new B2, close, reopen B1 and edit B2: the edit must be what gets saved.
    // Run once without the skip: fails, the first version stays in AddedItems. Remove the skip with the fix.
    [Fact(Skip = "ZA-036 (open): a nested update is dropped when the block is already pending as added.")]
    public void Merging_an_update_of_a_block_that_is_pending_as_added_keeps_the_newer_values()
    {
        var firstVersion = Block("v1");
        var edited = Block("v2", firstVersion.Id);
        var state = new BlockListState { AddedItems = [firstVersion] };

        var merged = state.MergeNestedChanges(new BlockListChanges { UpdatedItems = [edited] });

        Assert.Same(edited, Assert.Single(merged.AddedItems));
        Assert.Empty(merged.UpdatedItems);
    }
}
