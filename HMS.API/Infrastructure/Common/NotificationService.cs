using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace HMS.API.Infrastructure.Common
{
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly ConcurrentQueue<string> _recent = new ConcurrentQueue<string>();

        public NotificationService(ILogger<NotificationService> logger)
        {
            _logger = logger;
        }

        public Task NotifyAsync(string channel, object payload)
        {
            var msg = JsonSerializer.Serialize(new { channel, payload, at = System.DateTimeOffset.UtcNow });
            _recent.Enqueue(msg);
            while (_recent.Count > 100) _recent.TryDequeue(out _);
            _logger.LogInformation("Notify {Channel}: {Payload}", channel, payload);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetRecentAsync()
        {
            return Task.FromResult<IEnumerable<string>>(_recent.ToArray().Reverse());
        }
    }
}