using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SignalRems.Core.Interfaces;

namespace Samples.Model
{

    public class GetUserNameRequest : IRpcRequest
    {
        public GetUserNameRequest()
        {
            RequestId = Guid.NewGuid().ToString();
        }
        public string RequestId { get; set; }

        public string? UserId { get; set; }
    }
}
