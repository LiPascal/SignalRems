using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace SignalRems.Client
{
    internal class RetryPolicy: IRetryPolicy
    {
        private readonly ILogger _logger;
        private readonly string _url;
        private readonly double[] _retryTimes = new double[] {1,2,4,8,16,32 };
        

        public RetryPolicy(ILogger logger, string url)
        {
            _logger = logger;
            _url = url;
        }

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            var seconds = retryContext.PreviousRetryCount > 5
                ? 60
                : _retryTimes[retryContext.PreviousRetryCount];
            _logger.LogWarning("Connection unstable, retry to connect to {url} after {sec} seconds", _url, seconds);
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
