namespace SignalRems.Server.Exceptions;

public class InvalidRequestException : Exception
{
    public static readonly InvalidRequestException Instance = new();
    private InvalidRequestException() : base("RpcRequest is not supported")
    {
    }
}