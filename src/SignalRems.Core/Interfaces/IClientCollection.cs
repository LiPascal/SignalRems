using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRems.Core.Interfaces
{
    internal interface IClientCollection<T>
    {
        bool TryGetValue(string key, out T client);
        T this[string key] { get; set; }
        bool ContainsKey(string key);
        ICollection<T> Values { get; }
        T GetOrAdd(string key, Func<string, T> factory);
    }
}
