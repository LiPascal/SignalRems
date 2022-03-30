using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRems.Core.Interfaces;

public interface IRpcClient : IClient
{
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
        where TRequest : IRpcRequest where TResponse : class, IRpcResponse, new();
}