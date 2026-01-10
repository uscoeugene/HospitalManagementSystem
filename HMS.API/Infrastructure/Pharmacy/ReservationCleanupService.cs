using System;
using System.Threading;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMS.API.Infrastructure.Pharmacy
{
    public class ReservationCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ReservationCleanupService> _logger;

        public ReservationCleanupService(IServiceProvider services, ILogger<ReservationCleanupService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Reservation cleanup service started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<HMS.API.Application.Pharmacy.IPharmacyService>();

                    await svc.CleanupExpiredReservationsAsync();
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reservation cleanup failed");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }
    }
}