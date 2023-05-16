using System.Collections.Generic;
using System.Linq.Expressions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

internal class SubstExpressionVisitor : ExpressionVisitor
{
    internal Dictionary<Expression, Expression> Subst { get; } = new();

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (Subst.TryGetValue(
                node,
                out var newValue))
        {
            return newValue;
        }

        return node;
    }
}
