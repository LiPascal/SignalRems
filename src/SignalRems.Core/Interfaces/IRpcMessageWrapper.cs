using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("SignalRems.Server")]
[assembly: InternalsVisibleTo("SignalRems.Client")]

namespace SignalRems.Core.Interfaces;

internal interface IRpcMessageWrapper
{
    byte[] Payload { get; set; }

    bool UsingCompress { get; set; }

    byte[]? GetPayload();

    string? GetPayloadText();
    void SetPayload(byte[] payload, bool usingCompress = false);
}