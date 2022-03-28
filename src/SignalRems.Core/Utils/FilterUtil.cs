using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Serialize.Linq;
using Serialize.Linq.Factories;
using Serialize.Linq.Nodes;
using Serialize.Linq.Serializers;

namespace SignalRems.Core.Utils;

public static class FilterUtil
{
    private static readonly JsonSerializer Serializer = new();
    private static readonly NodeFactory Factory = new ();
    public static string? ToFilterString<T>(Expression<Func<T, bool>>? filter)
    {
        if (filter == null)
        {
            return null;
        }
        var node = Factory.Create(filter);
        return Serializer.Serialize(node);
    }

    public static Func<T, bool>? ToFilter<T>(string? filterString)
    {
        if (string.IsNullOrEmpty(filterString))
        {
            return null;
        }
        var exprNode = Serializer.Deserialize<LambdaExpressionNode>(filterString);
        var expr = exprNode.ToBooleanExpression<T>();
        return expr.Compile();

    }
}