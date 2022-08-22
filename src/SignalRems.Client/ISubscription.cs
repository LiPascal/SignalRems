using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRems.Client;

internal interface ISubscriptionByKeys<in TKey>: ISubscription
{
    Task<string?> AddKeysAsync(params TKey[] keys);
    Task<string?> RemoveKeysAsync(params TKey[] keys);
}