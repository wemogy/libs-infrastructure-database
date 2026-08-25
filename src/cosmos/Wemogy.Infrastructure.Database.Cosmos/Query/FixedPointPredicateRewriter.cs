using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Cosmos.Query
{
    /// <summary>
    ///     Rewrites a predicate so that it compares against the value the document actually carries
    ///     for a member marked with the <see cref="FixedPointAttribute"/>.
    ///     <para>
    ///         The document holds <c>500000</c> where the entity reads <c>0.5</c>, so a predicate
    ///         handed to the Cosmos LINQ provider unchanged would compare <c>0.5</c> against
    ///         <c>500000</c> - a cap would hold where it must not, and a query would quietly return
    ///         the wrong rows. Every constant a fixed-point member is compared against is therefore
    ///         scaled by the same factor the serializer applies, which leaves the comparison exact
    ///         and needs no arithmetic on the server.
    ///     </para>
    ///     <para>
    ///         The in-memory provider evaluates the predicate against the decimal the entity
    ///         carries and must not be rewritten - the two providers meet at the same answer, not
    ///         at the same expression.
    ///     </para>
    /// </summary>
    internal static class FixedPointPredicateRewriter
    {
        /// <summary>
        ///     Returns the predicate with every comparison against a fixed-point member scaled,
        ///     or the predicate itself when the entity type does not use the feature.
        /// </summary>
        /// <param name="predicate">The predicate to rewrite, may be null</param>
        /// <typeparam name="TEntity">The entity type the predicate acts on</typeparam>
        /// <returns>The rewritten predicate</returns>
        public static Expression<Func<TEntity, bool>>? Rewrite<TEntity>(Expression<Func<TEntity, bool>>? predicate)
        {
            if (predicate == null || !FixedPointMetadata.HasFixedPointMembers(typeof(TEntity)))
            {
                return predicate;
            }

            var rewriter = new Rewriter(
                predicate.Parameters[0],
                predicate.ToString());
            var body = rewriter.Rewrite(predicate.Body);

            return ReferenceEquals(body, predicate.Body)
                ? predicate
                : Expression.Lambda<Func<TEntity, bool>>(
                    body,
                    predicate.Parameters);
        }

        private sealed class Rewriter : ExpressionVisitor
        {
            private readonly ParameterExpression _parameter;
            private readonly string _description;

            /// <summary>
            ///     Every fixed-point member access of the predicate, by how often it occurs. A
            ///     comparison that is rewritten takes its accesses out again, so whatever is left
            ///     over is an access this class could not scale - reported instead of handed to the
            ///     database unscaled, which would silently answer the wrong question.
            /// </summary>
            private readonly Dictionary<MemberExpression, int> _unscaledAccesses =
                new Dictionary<MemberExpression, int>();

            public Rewriter(ParameterExpression parameter, string description)
            {
                _parameter = parameter;
                _description = description;
            }

            public Expression Rewrite(Expression body)
            {
                new AccessCollector(
                    _parameter,
                    _unscaledAccesses).Visit(body);

                if (_unscaledAccesses.Count == 0)
                {
                    return body;
                }

                var rewritten = Visit(body);

                var leftOver = _unscaledAccesses.FirstOrDefault(x => x.Value > 0);

                if (leftOver.Key != null)
                {
                    throw FixedPointError.ExpressionNotSupported(
                        _description,
                        $"{leftOver.Key.Member.Name} is stored as a scaled integer and is only understood in a comparison against a value the client can evaluate, optionally with + and - applied to it");
                }

                return rewritten;
            }

            protected override Expression VisitBinary(BinaryExpression node)
            {
                if (!IsComparison(node.NodeType))
                {
                    return base.VisitBinary(node);
                }

                var (left, leftScale) = Analyze(node.Left);
                var (right, rightScale) = Analyze(node.Right);

                if (leftScale != rightScale)
                {
                    // the side that is not scaled yet is a value the client holds, e.g. the cap of
                    // a quota; scaling it by the same factor makes the comparison exact without
                    // any arithmetic on the server
                    if (leftScale == 0)
                    {
                        left = Scale(
                            left,
                            rightScale);
                    }
                    else if (rightScale == 0)
                    {
                        right = Scale(
                            right,
                            leftScale);
                    }
                    else
                    {
                        throw FixedPointError.ExpressionNotSupported(
                            _description,
                            $"the two sides of the comparison are stored with different scales, {leftScale} and {rightScale}");
                    }
                }

                return node.Update(
                    left,
                    node.Conversion,
                    right);
            }

            private static bool IsComparison(ExpressionType nodeType)
            {
                return nodeType is ExpressionType.Equal or ExpressionType.NotEqual or ExpressionType.LessThan
                    or ExpressionType.LessThanOrEqual or ExpressionType.GreaterThan
                    or ExpressionType.GreaterThanOrEqual;
            }

            private static bool IsAdditive(ExpressionType nodeType)
            {
                return nodeType is ExpressionType.Add or ExpressionType.AddChecked or ExpressionType.Subtract
                    or ExpressionType.SubtractChecked;
            }

            private static bool IsNullableValueAccess(MemberExpression node)
            {
                var declaringType = node.Member.DeclaringType;

                return node.Member.Name == nameof(Nullable<int>.Value) &&
                    declaringType is { IsGenericType: true } &&
                    declaringType.GetGenericTypeDefinition() == typeof(Nullable<>);
            }

            /// <summary>
            ///     Whether the member access reads a field of the document rather than a value the
            ///     client captured - <c>x.Balance</c> is stored scaled, <c>otherEntity.Balance</c>
            ///     is the decimal the entity carries in memory.
            /// </summary>
            private static bool IsRootedAtParameter(Expression? expression, ParameterExpression parameter)
            {
                while (expression is MemberExpression memberExpression)
                {
                    expression = memberExpression.Expression;
                }

                return expression == parameter;
            }

            /// <summary>
            ///     Returns the expression together with the scale the value it produces is stored
            ///     with, 0 for everything that is not a fixed-point value. Anything this method
            ///     does not understand is returned untouched with a scale of 0; the leftover check
            ///     in <see cref="Rewrite"/> is what turns that into an error.
            /// </summary>
            private (Expression Expression, int Scale) Analyze(Expression node)
            {
                switch (node)
                {
                    case MemberExpression memberExpression when IsNullableValueAccess(memberExpression):
                        {
                            var (_, scale) = Analyze(memberExpression.Expression!);
                            return (memberExpression, scale);
                        }

                    case MemberExpression memberExpression
                        when IsRootedAtParameter(
                            memberExpression.Expression,
                            _parameter):
                        {
                            var scale = FixedPointMetadata.GetScale(memberExpression.Member);

                            if (scale == null)
                            {
                                return (memberExpression, 0);
                            }

                            Consume(memberExpression);
                            return (memberExpression, scale.Value);
                        }

                    case UnaryExpression unaryExpression when unaryExpression.NodeType is ExpressionType.Convert
                        or ExpressionType.ConvertChecked or ExpressionType.Negate or ExpressionType.NegateChecked:
                        {
                            var (operand, scale) = Analyze(unaryExpression.Operand);

                            if (scale == 0)
                            {
                                return (unaryExpression.Update(operand), 0);
                            }

                            // a conversion out of decimal would compare the scaled integer in a
                            // number type that cannot hold it exactly
                            var underlyingType =
                                Nullable.GetUnderlyingType(unaryExpression.Type) ?? unaryExpression.Type;

                            if (underlyingType != typeof(decimal))
                            {
                                throw FixedPointError.ExpressionNotSupported(
                                    _description,
                                    $"a fixed-point member cannot be converted to {unaryExpression.Type.Name}");
                            }

                            return (unaryExpression.Update(operand), scale);
                        }

                    case BinaryExpression binaryExpression when IsAdditive(binaryExpression.NodeType):
                        {
                            var (left, leftScale) = Analyze(binaryExpression.Left);
                            var (right, rightScale) = Analyze(binaryExpression.Right);

                            if (leftScale == 0 && rightScale == 0)
                            {
                                return (
                                    binaryExpression.Update(
                                        left,
                                        binaryExpression.Conversion,
                                        right),
                                    0);
                            }

                            // adding two scaled integers of the same scale gives the scaled sum, so
                            // the operand that is not scaled yet is lifted to the scale of the other
                            if (leftScale != rightScale)
                            {
                                if (leftScale == 0)
                                {
                                    left = Scale(
                                        left,
                                        rightScale);
                                    leftScale = rightScale;
                                }
                                else if (rightScale == 0)
                                {
                                    right = Scale(
                                        right,
                                        leftScale);
                                }
                                else
                                {
                                    throw FixedPointError.ExpressionNotSupported(
                                        _description,
                                        $"the two operands of the {binaryExpression.NodeType} are stored with different scales, {leftScale} and {rightScale}");
                                }
                            }

                            return (
                                binaryExpression.Update(
                                    left,
                                    binaryExpression.Conversion,
                                    right),
                                leftScale);
                        }
                }

                return (Visit(node), 0);
            }

            /// <summary>
            ///     Replaces a value the client can evaluate with the scaled integer the document
            ///     would carry for it, e.g. the <c>100m</c> of a cap with <c>100000000</c> at scale
            ///     6.
            /// </summary>
            private Expression Scale(Expression expression, int scale)
            {
                object? value;

                try
                {
                    value = Expression.Lambda(expression).Compile().DynamicInvoke();
                }
                catch (Exception)
                {
                    // reached when the expression reads the entity, e.g. a comparison of a
                    // fixed-point member against another field of the same document
                    throw FixedPointError.ExpressionNotSupported(
                        _description,
                        "a fixed-point member can only be compared against a value the client can evaluate, not against another field of the document");
                }

                // null is null at every scale, so the comparison stays as it is
                if (value == null)
                {
                    return expression;
                }

                var underlyingType = Nullable.GetUnderlyingType(expression.Type) ?? expression.Type;

                if (underlyingType != typeof(decimal))
                {
                    throw FixedPointError.ExpressionNotSupported(
                        _description,
                        $"a fixed-point member cannot be compared against a {expression.Type.Name}");
                }

                var scaled = FixedPointScale.ToScaled(
                    Convert.ToDecimal(
                        value,
                        CultureInfo.InvariantCulture),
                    scale,
                    _description);

                // wrapped in the conversion the compiler would have emitted instead of typing the
                // constant as the nullable itself, which Expression.Constant does not accept
                Expression constant = Expression.Constant((decimal)scaled);

                return constant.Type == expression.Type
                    ? constant
                    : Expression.Convert(
                        constant,
                        expression.Type);
            }

            private void Consume(MemberExpression memberExpression)
            {
                if (_unscaledAccesses.TryGetValue(
                        memberExpression,
                        out var count))
                {
                    _unscaledAccesses[memberExpression] = count - 1;
                }
            }
        }

        /// <summary>
        ///     Collects every fixed-point member access of a predicate, so the rewriter can tell
        ///     afterwards whether it scaled all of them.
        /// </summary>
        private sealed class AccessCollector : ExpressionVisitor
        {
            private readonly ParameterExpression _parameter;
            private readonly Dictionary<MemberExpression, int> _accesses;

            public AccessCollector(ParameterExpression parameter, Dictionary<MemberExpression, int> accesses)
            {
                _parameter = parameter;
                _accesses = accesses;
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (IsFixedPointAccess(node))
                {
                    _accesses[node] = _accesses.TryGetValue(
                        node,
                        out var count)
                        ? count + 1
                        : 1;
                }

                return base.VisitMember(node);
            }

            private bool IsFixedPointAccess(MemberExpression node)
            {
                if (FixedPointMetadata.GetScale(node.Member) == null)
                {
                    return false;
                }

                Expression? expression = node.Expression;

                while (expression is MemberExpression memberExpression)
                {
                    expression = memberExpression.Expression;
                }

                return expression == _parameter;
            }
        }
    }
}
