using Samples.Model;
using SignalRems.Core.Interfaces;

namespace Samples.Server
{
    public class Worker: BackgroundService
    {
        private readonly IPublisherService _publisherService;
        private readonly IRpcService _rpcService;
        private readonly UserInfoQueryHandler _handler;

        public Worker(IPublisherService publisherService, IRpcService rpcService, UserInfoQueryHandler handler)
        {
            _publisherService = publisherService;
            _rpcService = rpcService;
            _handler = handler;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _publisherService.Start();
            _rpcService.RegisterHandler<GetUserAgeRequest, GetUserAgeResponse>(_handler);
            _rpcService.RegisterHandler<GetUserNameRequest, GetUserNameResponse>(_handler);
            _rpcService.Start();
            var publisher =  _publisherService.CreatePublisher<Person, int>("Message");
            int id = 0;
            var random = new Random();
            while (!stoppingToken.IsCancellationRequested)
            {
                publisher.Publish(new Person() { Id = id, Age = random.Next(95), Name = $"Person_{id:000}" });
                id++;
                await Task.Delay(1000, stoppingToken);
            }
            publisher.Dispose();
        }
    }
}
