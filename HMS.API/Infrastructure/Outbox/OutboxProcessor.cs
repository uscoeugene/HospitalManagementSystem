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
using Microsoft.AspNetCore.SignalR;
using HMS.API.Hubs;
using System.Collections.Generic;

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
                    var notifier = scope.ServiceProvider.GetService<INotificationService>();
                    var hubContext = scope.ServiceProvider.GetService<IHubContext<NotificationHub>>();

                    var message = await db.OutboxMessages.Where(o => o.ProcessedAt == null).OrderBy(o => o.OccurredAt).FirstOrDefaultAsync(stoppingToken);
                    if (message == null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        continue;
                    }

                    try
                    {
                        object? payload = null;
                        JsonDocument? doc = null;
                        try
                        {
                            doc = JsonDocument.Parse(message.Content);
                            payload = JsonSerializer.Deserialize<object>(message.Content);
                        }
                        catch { payload = message.Content; }

                        // attempt publish with retries
                        try
                        {
                            await publisher.PublishAsync(payload ?? message);
                            message.ProcessedAt = DateTimeOffset.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);

                            // notify in-process listeners
                            if (notifier != null)
                            {
                                try
                                {
                                    await notifier.NotifyAsync(message.Type, payload ?? message);
                                }
                                catch (Exception exNotify)
                                {
                                    _logger.LogWarning(exNotify, "Notification failed for outbox message {Id}", message.Id);
                                }
                            }

                            // broadcast via SignalR to relevant groups
                            try
                            {
                                var groups = new List<string>();

                                if (string.Equals(message.Type, "PrescriptionCharged", StringComparison.OrdinalIgnoreCase))
                                {
                                    groups.Add("pharmacy");
                                    // try extract patientId
                                    if (doc != null && doc.RootElement.TryGetProperty("PatientId", out var pid) && pid.ValueKind == JsonValueKind.String)
                                    {
                                        groups.Add($"patient-{pid.GetString()}");
                                    }
                                }
                                else if (string.Equals(message.Type, "PrescriptionDispensed", StringComparison.OrdinalIgnoreCase))
                                {
                                    groups.Add("pharmacy");
                                    // try get prescription id and lookup patient
                                    if (doc != null && doc.RootElement.TryGetProperty("PrescriptionId", out var prIdElem) && prIdElem.ValueKind == JsonValueKind.String)
                                    {
                                        if (Guid.TryParse(prIdElem.GetString(), out var prId))
                                        {
                                            var pres = await db.Prescriptions.AsNoTracking().SingleOrDefaultAsync(p => p.Id == prId, stoppingToken);
                                            if (pres != null) groups.Add($"patient-{pres.PatientId}");
                                        }
                                    }
                                }
                                else if (string.Equals(message.Type, "LabInvoiceCreated", StringComparison.OrdinalIgnoreCase) || string.Equals(message.Type, "InvoiceStatusChangedEvent", StringComparison.OrdinalIgnoreCase))
                                {
                                    groups.Add("lab");
                                    if (doc != null && doc.RootElement.TryGetProperty("PatientId", out var pid2) && pid2.ValueKind == JsonValueKind.String)
                                    {
                                        groups.Add($"patient-{pid2.GetString()}");
                                    }
                                }
                                else
                                {
                                    // generic: send to admin group
                                    groups.Add("admin");
                                }

                                if (hubContext != null)
                                {
                                    foreach (var g in groups)
                                    {
                                        try
                                        {
                                            await hubContext.Clients.Group(g).SendAsync("notification", message.Type, payload ?? message);
                                        }
                                        catch (Exception exHub)
                                        {
                                            _logger.LogWarning(exHub, "SignalR group broadcast failed for outbox message {Id} to group {Group}", message.Id, g);
                                        }
                                    }
                                }
                            }
                            catch (Exception exHub)
                            {
                                _logger.LogWarning(exHub, "SignalR broadcast failed for outbox message {Id}", message.Id);
                            }
                        }
                        catch (Exception exPub)
                        {
                            message.Attempts += 1;
                            await db.SaveChangesAsync(stoppingToken);

                            var backoff = ComputeBackoff(message.Attempts);
                            _logger.LogWarning(exPub, "Publish failed for outbox message {Id}, will retry after {Backoff}s", message.Id, backoff.TotalSeconds);
                            await Task.Delay(backoff, stoppingToken);
                        }
                        finally
                        {
                            doc?.Dispose();
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