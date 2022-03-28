namespace SignalRems.Server.Exceptions;

internal class InvalidPubSubEntityException : Exception
{
    public InvalidPubSubEntityException(int keyAttributeCount, Type type) : base(GetMessage(keyAttributeCount, type))
    {
    }

    public InvalidPubSubEntityException(string message) : base(message)
    {
    }

    private static string GetMessage(int keyAttributeCount, Type type)
    {
        if (keyAttributeCount == 0) return $"No key attribute found in type {type}";
        return $"Multiple key attributes found in type {type}";
    }
}