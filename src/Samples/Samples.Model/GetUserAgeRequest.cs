using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SignalRems.Core.Interfaces;

namespace Samples.Model
{
    public class GetUserAgeRequest : IRpcRequest
    {
        public GetUserAgeRequest()
        {
            RequestId = new Guid().ToString();
        }

        public string RequestId { get; }
        public string? UserId { get; set; }
    }
}
