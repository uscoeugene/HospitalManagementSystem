using System;
using System.Threading;
using System.Threading.Tasks;
using HMS.API.Application.Billing;
using HMS.API.Application.Patient;
using HMS.API.Application.Profile;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMS.API.Infrastructure.Reporting
{
    public class ReportingAggregatorService : BackgroundService
    {
        private readonly IBillingReportService _billing;
        private readonly IPatientReportService _patients;
        private readonly IProfileReportService _profiles;
        private readonly IDistributedCache _cache;
        private readonly ILogger<ReportingAggregatorService> _logger;

        public ReportingAggregatorService(IBillingReportService billing, IPatientReportService patients, IProfileReportService profiles, IDistributedCache cache, ILogger<ReportingAggregatorService> logger)
        {
            _billing = billing;
            _patients = patients;
            _profiles = profiles;
            _cache = cache;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // compute heavy aggregates and cache them as JSON
                    var billing = await _billing.GetSummaryKpiAsync();
                    var monthly = await _billing.GetMonthlyRevenueAsync(6);

                    await _cache.SetStringAsync("reports:billing:kpi", System.Text.Json.JsonSerializer.Serialize(billing), stoppingToken);
                    await _cache.SetStringAsync("reports:billing:monthly", System.Text.Json.JsonSerializer.Serialize(monthly), stoppingToken);

                    // small patient/profile samples
                    var patients = await _patients.GetPatientSummaryAsync(1, 10);
                    await _cache.SetStringAsync("reports:patients:recent", System.Text.Json.JsonSerializer.Serialize(patients), stoppingToken);

                    var profiles = await _profiles.GetProfileSummaryAsync(1, 10);
                    await _cache.SetStringAsync("reports:profiles:recent", System.Text.Json.JsonSerializer.Serialize(profiles), stoppingToken);

                    // run once every 5 minutes
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reporting aggregator failed");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }
    }
}
