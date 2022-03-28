using System.Collections.Concurrent;

namespace SignalRems.Server.Data
{
    internal class ClientCommand
    {
        public ClientCommand(SubscriptionContext context, string commandName, TaskCompletionSource<Exception?> completeSource, params object[] parameters)
        {
            Context = context;
            CommandName = commandName;
            CompleteSource = completeSource;
            Parameters = parameters;
        }

        public SubscriptionContext Context { get; }
        public string CommandName { get; set; }
        public TaskCompletionSource<Exception?> CompleteSource { get; set; }
        public object[] Parameters { get; set; }
    }
}
