using Samples.Model;
using SignalRems.Core.Interfaces;

namespace Samples.Server
{
    public class Worker: BackgroundService
    {
        private readonly IPublisherService _publisherService;
        public Worker(IPublisherService publisherService)
        {
            _publisherService = publisherService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _publisherService.Start();
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
