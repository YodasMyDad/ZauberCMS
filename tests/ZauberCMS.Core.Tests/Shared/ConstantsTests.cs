using System.Reflection;

namespace ZauberCMS.Core.Tests.Shared;

public class ConstantsTests
{
    [Fact]
    public void ZA025_Every_tree_alias_is_its_own()
    {
        // Context menus and tree state are keyed on the alias: two trees with one alias share their menus
        var aliases = typeof(Constants.Sections.Trees)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => (Name: field.Name, Value: (string)field.GetRawConstantValue()!))
            .ToList();

        Assert.NotEmpty(aliases);
        var repeated = aliases.GroupBy(alias => alias.Value).Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group.Select(alias => alias.Name)));
        Assert.Empty(repeated);
    }
}
