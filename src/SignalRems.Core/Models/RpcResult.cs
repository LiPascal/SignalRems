using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;


namespace SignalRems.Core.Models;

public class RpcResult
{
    public RpcResult()
    {
    }

    public RpcResult(string? result, string? error)
    {
        Result = result;
        Error = error;
    }

    public string? Result { get; set; }
    public string? Error { get; set; }
}