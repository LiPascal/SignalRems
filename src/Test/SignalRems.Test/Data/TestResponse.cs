using MessagePack;
using SignalRems.Core.Interfaces;

namespace SignalRems.Test.Data
{
    [MessagePackObject(true)]
    public class TestResponse: IRpcResponse
    {
        public string RequestId { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}
