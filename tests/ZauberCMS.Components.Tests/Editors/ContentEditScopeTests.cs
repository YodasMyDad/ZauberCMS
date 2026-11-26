using System.Text.Json;
using ZauberCMS.Components.Editors.Models;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Components.Tests.Editors;

/// <summary>
/// The unsaved block edits of a content editor (Docs/BlockListEditor.md §6). The root scope's drafts are what the
/// page save writes, so a draft that leaks out of a cancelled editor is data loss.
/// </summary>
public class ContentEditScopeTests
{
    private static Content Block(string name, Guid? id = null, params Content[] children)
    {
        var block = new Content { Id = id ?? Guid.NewGuid(), Name = name, IsNestedContent = true };
        block.PropertyData.Add(new ContentPropertyValue { Alias = "text", Value = name });
        block.PropertyData.Add(new ContentPropertyValue
        {
            Alias = "children", Value = JsonSerializer.Serialize(children.Select(c => c.Id).ToList())
        });
        return block;
    }

    [Fact]
    public void A_saved_block_editor_hands_its_edits_to_the_editor_that_opened_it()
    {
        var root = ContentEditScope.CreateRoot();
        var block = Block("block");

        var child = root.BeginChild();
        child.SetDraft(block);
        child.Commit();

        Assert.Same(block, root.GetDraft(block.Id));
        Assert.Same(block, Assert.Single(root.Drafts));
        Assert.True(child.IsClosed);
    }

    // F1 / ZA-034: cancel at any level discards every change made inside that editor, nested ones included
    [Fact]
    public void Cancelling_a_block_editor_drops_its_edits_and_those_of_blocks_opened_inside_it()
    {
        var root = ContentEditScope.CreateRoot();
        var outer = root.BeginChild();
        var inner = outer.BeginChild();
        inner.SetDraft(Block("nested edit"));
        inner.Commit();
        outer.SetDraft(Block("outer edit"));

        outer.Rollback();

        Assert.Empty(root.Drafts);
    }

    // F3 / ZA-036: add B1 with a new B2, close, reopen B1 and edit B2: the edit is what gets saved
    [Fact]
    public void Editing_a_block_again_keeps_the_newest_values()
    {
        var root = ContentEditScope.CreateRoot();
        var first = Block("v1");
        var b1 = root.BeginChild();
        var b2 = b1.BeginChild();
        b2.SetDraft(first);
        b2.Commit();
        b1.Commit();

        var edited = Block("v2", first.Id);
        var reopenedB1 = root.BeginChild();
        var reopenedB2 = reopenedB1.BeginChild();
        Assert.Same(first, reopenedB2.GetDraft(first.Id));
        reopenedB2.SetDraft(edited);
        reopenedB2.Commit();
        reopenedB1.Commit();

        Assert.Same(edited, Assert.Single(root.Drafts));
    }

    [Fact]
    public void A_child_scope_sees_the_drafts_of_the_scopes_it_sits_in_without_changing_them()
    {
        var root = ContentEditScope.CreateRoot();
        var saved = Block("saved earlier");
        root.BeginChild().Also(s => s.SetDraft(saved)).Commit();

        var child = root.BeginChild();
        var changed = Block("changed", saved.Id);
        child.SetDraft(changed);

        Assert.Same(changed, child.GetDraft(saved.Id));
        Assert.Same(saved, root.GetDraft(saved.Id));
    }

    [Fact]
    public void A_closed_scope_takes_no_more_edits_and_committing_it_again_does_nothing()
    {
        var root = ContentEditScope.CreateRoot();
        var child = root.BeginChild();
        child.Rollback();

        Assert.Throws<InvalidOperationException>(() => child.SetDraft(Block("late")));
        child.Commit();
        Assert.Empty(root.Drafts);
    }

    [Fact]
    public void The_root_scope_is_saved_not_committed()
    {
        Assert.Throws<InvalidOperationException>(() => ContentEditScope.CreateRoot().Commit());
    }

    // Previews show unsaved blocks: the copy carries them, all the way down
    [Fact]
    public void A_preview_copy_carries_the_unsaved_blocks_inside_it()
    {
        var root = ContentEditScope.CreateRoot();
        var grandchild = Block("grandchild");
        var child = Block("child", children: grandchild);
        var unchanged = Block("stored, not edited");
        var parent = Block("parent", children: [child, unchanged]);
        var scope = root.BeginChild();
        scope.SetDraft(grandchild);
        scope.SetDraft(child);
        scope.Commit();

        var preview = root.CreatePreviewCopy(parent);

        Assert.NotSame(parent, preview);
        Assert.Empty(parent.PendingBlockListChanges);
        var pending = preview.PendingBlockListChanges["children"];
        Assert.Equal([child.Id], pending.Keys.ToList());
        Assert.Equal("child", pending[child.Id].GetValue<string>("text"));
        Assert.Equal([grandchild.Id], pending[child.Id].PendingBlockListChanges["children"].Keys.ToList());
    }

    [Fact]
    public void A_preview_copy_stops_at_a_block_that_contains_itself()
    {
        var root = ContentEditScope.CreateRoot();
        var looping = Block("loop");
        looping.PropertyData.Single(p => p.Alias == "children").Value =
            JsonSerializer.Serialize(new List<Guid> { looping.Id });
        root.BeginChild().Also(s => s.SetDraft(looping)).Commit();

        var preview = root.CreatePreviewCopy(looping);

        Assert.False(preview.PendingBlockListChanges.ContainsKey("children"));
    }
}

internal static class ScopeTestExtensions
{
    public static ContentEditScope Also(this ContentEditScope scope, Action<ContentEditScope> action)
    {
        action(scope);
        return scope;
    }
}
