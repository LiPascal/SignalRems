using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("SignalRems.Server")]
[assembly: InternalsVisibleTo("SignalRems.Client")]

namespace SignalRems.Core.Utils;

internal static class TypeExtension
{
    internal static string ToLogName(this Type type)
    {
        return type.GenericTypeArguments.Any()
            ? $"{type.Name[..^2]}<{string.Join(',', type.GenericTypeArguments.Select(x => x.Name))})>"
            : type.Name;
    }
}