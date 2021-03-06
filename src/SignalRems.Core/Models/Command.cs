namespace SignalRems.Core.Models
{
    public static class Command
    {
        // Commands from server to client
        public const string Publish = "Publish";
        public const string Snapshot = "Snapshot";
        
        // Commands from client to server
        public const string Subscribe = "Subscribe";
        public const string UnSubscribe = "UnSubscribe";
        public const string GetSnapshot = "GetSnapshot";
        public const string GetSnapshotAndSubscribe = "GetSnapshotAndSubscribe";

        public const string Send = "Send";

    }
}
