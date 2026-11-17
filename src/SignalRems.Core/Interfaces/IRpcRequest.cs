using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRems.Core.Interfaces
{
    public interface IRpcRequest
    {
        string RequestId { get; }

        /// <summary>
        /// Set by the server before the handler runs when the client may have sent this request before, i.e. the
        /// client resent it because the connection dropped before the response arrived. The client cannot know
        /// whether the handler already completed, so the flag means "may be a duplicate".
        /// Declare this property on the request type to receive the value; the default implementation ignores it,
        /// so request types and handlers that don't need it require no change.
        /// </summary>
        bool PossibleResend
        {
            get => false;
            set { }
        }
    }
}
