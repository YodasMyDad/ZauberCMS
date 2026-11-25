using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;

namespace ZauberCMS.Core.Data;

/// <summary>
/// Applies the filter and sort strings of an admin data grid to a query.
/// The strings are text (built by the grid from what the user typed), parsed into an expression by Radzen's parser.
/// Before the query runs, the parsed expression is checked: it may read the entity's public properties and use
/// methods of simple value types (string, numbers, dates, Guid) and LINQ, nothing else, and it may never touch a
/// secret column such as the password hash. Anything else is refused with an <see cref="ArgumentException"/>.
/// </summary>
internal static class GridQueryGuard
{
    /// <summary>Columns that must not be filtered or sorted on: comparing them leaks their value bit by bit.</summary>
    private static readonly HashSet<string> SecretMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp"
    };

    private static readonly HashSet<Type> ValueOwners =
    [
        typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly),
        typeof(TimeSpan), typeof(Guid), typeof(Math), typeof(Convert), typeof(Enumerable), typeof(Queryable),
        typeof(Nullable<>), typeof(List<>), typeof(ICollection<>), typeof(IEnumerable<>), typeof(IList<>),
        typeof(IReadOnlyCollection<>), typeof(Dictionary<,>)
    ];

    public static IQueryable<T> Filter<T>(IQueryable<T> query, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return query;
        }

        return Checked(query, () => query.Where(filter), "filter");
    }

    public static IQueryable<T> OrderBy<T>(IQueryable<T> query, string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return query;
        }

        return Checked(query, () => query.OrderBy(orderBy), "sort order");
    }

    private static IQueryable<T> Checked<T>(IQueryable<T> source, Func<IQueryable<T>> apply, string what)
    {
        IQueryable<T> result;
        try
        {
            // Builds the expression only; nothing is sent to the database until the query is enumerated.
            result = apply();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new ArgumentException($"The grid {what} is not a valid expression.", e);
        }

        new Checker(source.Expression, typeof(T).Assembly, what).Visit(result.Expression);
        return result;
    }

    private sealed class Checker(Expression source, Assembly entityAssembly, string what) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
        {
            // The query the grid string was applied to is ours; only what the string added is checked.
            if (ReferenceEquals(node, source))
            {
                return node;
            }

            switch (node?.NodeType)
            {
                case ExpressionType.Invoke:
                case ExpressionType.MemberInit:
                case ExpressionType.ListInit:
                case ExpressionType.Block:
                case ExpressionType.Assign:
                case ExpressionType.Dynamic:
                case ExpressionType.Extension:
                case ExpressionType.Goto:
                case ExpressionType.Loop:
                case ExpressionType.Try:
                case ExpressionType.Switch:
                    throw Refused($"it uses a {node.NodeType} expression");
            }

            return base.Visit(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (SecretMembers.Contains(node.Member.Name))
            {
                throw Refused($"'{node.Member.Name}' cannot be used");
            }

            if (!IsEntityType(node.Member.DeclaringType) && !IsValueOwner(node.Member.DeclaringType))
            {
                throw Refused($"'{node.Member.DeclaringType?.Name}.{node.Member.Name}' cannot be used");
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var owner = node.Method.DeclaringType;
            var onObject = owner == typeof(object) && node.Method.Name is nameof(ToString) or nameof(Equals);
            if (!onObject && !IsValueOwner(owner))
            {
                throw Refused($"the method '{owner?.Name}.{node.Method.Name}' cannot be used");
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitNew(NewExpression node)
        {
            if (!IsValueOwner(node.Type))
            {
                throw Refused($"it creates a '{node.Type.Name}'");
            }

            return base.VisitNew(node);
        }

        private bool IsEntityType(Type? type) =>
            type != null && (type.Assembly == entityAssembly || type.Assembly == typeof(ZauberSetup).Assembly ||
                             type.Namespace == "Microsoft.AspNetCore.Identity");

        private static bool IsValueOwner(Type? type)
        {
            if (type == null)
            {
                return false;
            }

            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive || type.IsEnum ||
                   ValueOwners.Contains(type.IsGenericType ? type.GetGenericTypeDefinition() : type);
        }

        private ArgumentException Refused(string reason) => new($"The grid {what} was refused: {reason}.");
    }
}
