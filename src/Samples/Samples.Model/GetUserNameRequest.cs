using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using SignalRems.Core.Interfaces;

namespace Samples.Model
{

    [MessagePackObject]
    public class GetUserNameRequest : IRpcRequest
    {
        public GetUserNameRequest()
        {
            RequestId = Guid.NewGuid().ToString();
        }

        [Key(0)]
        public string RequestId { get; set; }

        [Key(1)]
        public string? UserId { get; set; }
    }
}
