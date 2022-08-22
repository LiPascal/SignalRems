namespace SignalRems.Core.Models
{
    public static class Command
    {
        // Commands from server to client
        public const string Publish = "Publish";
        public const string Delete = "Delete";
        public const string Snapshot = "Snapshot";
        
        // Commands from client to server
        public const string Subscribe = "Subscribe";
        public const string UnSubscribe = "UnSubscribe";
        public const string GetSnapshot = "GetSnapshot";
        public const string GetSnapshotAndSubscribe = "GetSnapshotAndSubscribe";
        public const string SubscribeWithKeys = "SubscribeWithKeys";
        public const string AddSubscriptionKeys = "AddSubscriptionKeys";
        public const string RemoveSubscriptionKeys = "RemoveSubscriptionKeys";

        public const string Send = "Send";

    }
}
