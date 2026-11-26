using ZauberCMS.Components.Editors.Models;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Tests.Editors;

/// <summary>
/// What one Block List editor shows. The list's value (the block ids in order) is what the page save writes, so
/// mistakes here change which blocks a page has (Docs/BlockListEditor.md). Edits are tracked by
/// <see cref="ContentEditScope"/>, see <see cref="ContentEditScopeTests"/>.
/// </summary>
public class BlockListStateTests
{
    private static Content Block(string name, Guid? id = null) =>
        new() { Id = id ?? Guid.NewGuid(), Name = name, IsNestedContent = true };

    private static BlockListState StateWithSaved(params Content[] saved) => new() { Items = [..saved] };

    [Fact]
    public void Adding_a_block_appends_it()
    {
        var existing = Block("existing");
        var added = Block("new");

        var state = StateWithSaved(existing).AddItem(added);

        Assert.Equal([existing.Id, added.Id], state.GetSerializedIds());
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
    }

    [Fact]
    public void Updating_a_block_replaces_it_in_place()
    {
        var first = Block("v1");
        var other = Block("other");
        var edited = Block("v2", first.Id);

        var state = StateWithSaved(first, other).UpdateItem(edited);

        Assert.Same(edited, state.Items[0]);
        Assert.Equal([first.Id, other.Id], state.GetSerializedIds());
    }

    [Fact]
    public void Updating_an_unknown_block_changes_nothing()
    {
        var state = StateWithSaved(Block("existing"));

        Assert.Same(state, state.UpdateItem(Block("stranger")));
    }

    [Fact]
    public void Deleting_a_block_removes_it_from_the_list()
    {
        var kept = Block("kept");
        var removed = Block("removed");

        var state = StateWithSaved(kept, removed).DeleteItem(removed.Id);

        Assert.Equal([kept.Id], state.GetSerializedIds());
    }

    [Fact]
    public void Reordering_moves_the_block()
    {
        var a = Block("a");
        var b = Block("b");
        var c = Block("c");

        var state = StateWithSaved(a, b, c).ReorderItems(oldIndex: 0, newIndex: 2);

        Assert.Equal([b.Id, c.Id, a.Id], state.GetSerializedIds());
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
}