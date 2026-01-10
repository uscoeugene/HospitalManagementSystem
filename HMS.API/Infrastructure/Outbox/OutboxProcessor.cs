using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using HMS.API.Domain.Common;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMS.API.Infrastructure.Outbox
{
    public class OutboxProcessor : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OutboxProcessor> _logger;

        public OutboxProcessor(IServiceProvider services, ILogger<OutboxProcessor> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Outbox processor started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<HmsDbContext>();
                    var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                    var message = await db.OutboxMessages.Where(o => o.ProcessedAt == null).OrderBy(o => o.OccurredAt).FirstOrDefaultAsync(stoppingToken);
                    if (message == null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        continue;
                    }

                    try
                    {
                        object? payload = null;
                        try
                        {
                            payload = JsonSerializer.Deserialize<object>(message.Content);
                        }
                        catch { payload = message.Content; }

                        // attempt publish with retries
                        try
                        {
                            await publisher.PublishAsync(payload ?? message);
                            message.ProcessedAt = DateTimeOffset.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);
                        }
                        catch (Exception exPub)
                        {
                            message.Attempts += 1;
                            await db.SaveChangesAsync(stoppingToken);

                            var backoff = ComputeBackoff(message.Attempts);
                            _logger.LogWarning(exPub, "Publish failed for outbox message {Id}, will retry after {Backoff}s", message.Id, backoff.TotalSeconds);
                            await Task.Delay(backoff, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        message.Attempts += 1;
                        _logger.LogError(ex, "Outbox processing error for message {Id}", message.Id);
                        await db.SaveChangesAsync(stoppingToken);
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox loop failed");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        private static TimeSpan ComputeBackoff(int attempts)
        {
            // exponential backoff with jitter
            var baseMs = Math.Min(1000 * Math.Pow(2, attempts - 1), 30_000);
            var jitter = new Random().Next(0, 500);
            return TimeSpan.FromMilliseconds(baseMs + jitter);
        }
    }
}