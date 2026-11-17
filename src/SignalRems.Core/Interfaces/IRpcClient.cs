using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SignalRems.Core.Interfaces;

public interface IRpcClient : IClient
{
    /// <summary>
    /// Sends a request and waits for its response.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="level">Log level used for the request/response payloads.</param>
    /// <param name="compressInRequest">Compress the request payload.</param>
    /// <param name="compressInResult">Ask the server to compress the response payload.</param>
    /// <param name="autoRetry">
    /// When enabled (the default) a request whose response was lost because the connection dropped is sent again
    /// automatically once the client is connected again, so the caller only observes the eventual response.
    /// The resent attempt is flagged with <see cref="IRpcRequest.PossibleResend"/> so the server handler knows the
    /// call may already have run; declaring that property on the request type delivers the flag to the handler.
    /// With <c>false</c> such a call returns immediately with <c>Success = false</c> and an error message.
    /// </param>
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, LogLevel level = LogLevel.None, bool compressInRequest = false, bool compressInResult = false, bool autoRetry = true)
        where TRequest : class, IRpcRequest, new()
        where TResponse : class, IRpcResponse, new();
}