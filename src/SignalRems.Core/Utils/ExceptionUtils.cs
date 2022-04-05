using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("SignalRems.Server")]
[assembly: InternalsVisibleTo("SignalRems.Client")]

namespace SignalRems.Core.Utils;

internal static class ExceptionUtils
{
    internal static string GetFullMessage(this Exception e)
    {
        return GetFullMessage(e, string.Empty);
    }

    private static string GetFullMessage(Exception e, string prefix)
    {
        return $"{prefix}{e.Message}\n" +
               $"{prefix}{e.StackTrace}\n" +
               (e.InnerException == null
                   ? string.Empty
                   : $"{prefix}:\n" + GetFullMessage(e.InnerException, prefix + "\t"));
    }
}