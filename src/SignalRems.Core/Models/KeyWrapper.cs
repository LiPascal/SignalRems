using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("SignalRems.Server")]
[assembly: InternalsVisibleTo("SignalRems.Client")]

namespace SignalRems.Core.Models;

internal class KeyWrapper<TKey>
{
    public KeyWrapper()
    {
    }

    public KeyWrapper(TKey[] keys)
    {
        Keys = keys;
    }

    public TKey[] Keys { get; set; } = Array.Empty<TKey>();

    public static KeyWrapper<TKey>? FromJson(string? json)
    {
        if (json == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<KeyWrapper<TKey>>(json);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }
}