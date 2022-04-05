using System.Collections.Concurrent;

namespace SignalRems.Server.Data
{
    internal class SubscriptionCommand
    {
        public SubscriptionCommand(SubscriptionContext context, string commandName, TaskCompletionSource<string?> completeSource, params object[] parameters)
        {
            Context = context;
            CommandName = commandName;
            CompleteSource = completeSource;
            Parameters = parameters;
        }

        public SubscriptionContext Context { get; }
        public string CommandName { get; set; }
        public TaskCompletionSource<string?> CompleteSource { get; set; }
        public object[] Parameters { get; set; }
    }
}
