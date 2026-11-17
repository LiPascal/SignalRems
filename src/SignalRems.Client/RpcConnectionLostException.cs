using System;

namespace SignalRems.Client;

/// <summary>
/// Signals that a request was lost because the hub connection dropped before its reply arrived.
/// Used internally to tell a connection failure apart from an error returned by the server.
/// </summary>
internal sealed class RpcConnectionLostException : Exception
{
    public RpcConnectionLostException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}
