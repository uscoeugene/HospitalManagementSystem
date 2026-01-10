using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace HMS.API.Infrastructure.Sync
{
    public class BackgroundSyncService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<BackgroundSyncService> _logger;

        public BackgroundSyncService(IServiceProvider services, ILogger<BackgroundSyncService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background sync service started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var manager = scope.ServiceProvider.GetRequiredService<HMS.API.Application.Sync.ISyncManager>();
                    await manager.RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background sync run failed");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}