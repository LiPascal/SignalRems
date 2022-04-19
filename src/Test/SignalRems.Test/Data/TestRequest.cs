using MessagePack;
using SignalRems.Core.Interfaces;

namespace SignalRems.Test.Data
{
    [MessagePackObject(true)]
    public class TestRequest:IRpcRequest
    {
        public string RequestId { get; set; }

        public int ProcessTime { get; set; }
    }
}
