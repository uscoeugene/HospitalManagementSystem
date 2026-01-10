using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HMS.API.Application.Sync;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HMS.API.Domain.Patient;
using HMS.API.Domain.Billing;
using HMS.API.Domain.Lab;
using HMS.API.Domain.Pharmacy;
using HMS.API.Domain.Payments;

namespace HMS.API.Infrastructure.Sync
{
    public class SyncManager : ISyncManager
    {
        private readonly HmsDbContext _db;
        private readonly ICloudSyncClient _cloud;
        private readonly ILogger<SyncManager> _logger;

        public SyncManager(HmsDbContext db, ICloudSyncClient cloud, ILogger<SyncManager> logger)
        {
            _db = db;
            _cloud = cloud;
            _logger = logger;
        }

        public async Task RunOnceAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            // define syncable types explicitly
            var tasks = new List<Task>
            {
                SyncEntitiesAsync(_db.Patients, "Patient", cancellationToken),
                SyncEntitiesAsync(_db.Invoices, "Invoice", cancellationToken),
                SyncEntitiesAsync(_db.InvoiceItems, "InvoiceItem", cancellationToken),
                SyncEntitiesAsync(_db.LabRequests, "LabRequest", cancellationToken),
                SyncEntitiesAsync(_db.Prescriptions, "Prescription", cancellationToken),
                SyncEntitiesAsync(_db.Drugs, "Drug", cancellationToken),
                SyncEntitiesAsync(_db.Payments, "Payment", cancellationToken),
                SyncEntitiesAsync(_db.Refunds, "Refund", cancellationToken),
                SyncEntitiesAsync(_db.Reservations, "Reservation", cancellationToken),
                SyncEntitiesAsync(_db.Receipts, "Receipt", cancellationToken)
            };

            await Task.WhenAll(tasks);
        }

        private async Task SyncEntitiesAsync<T>(DbSet<T> set, string name, System.Threading.CancellationToken cancellationToken) where T : class
        {
            try
            {
                var unsynced = await set.AsNoTracking().Where(e => EF.Property<bool>(e, "IsSynced") == false).ToArrayAsync(cancellationToken);
                if (unsynced.Length > 0)
                {
                    await _cloud.PushAsync(name, unsynced.Cast<object>().ToArray());
                    foreach (var u in unsynced)
                    {
                        try
                        {
                            var entry = _db.Attach(u);
                            entry.Property("IsSynced").CurrentValue = true;
                            entry.Property("SyncVersion").CurrentValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        }
                        catch (Exception exu)
                        {
                            _logger.LogWarning(exu, "Failed to mark synced for {Type}", name);
                        }
                    }
                    await _db.SaveChangesAsync(cancellationToken);
                }

                // pull recent changes since 1 hour ago
                var pulled = await _cloud.PullAsync(name, DateTimeOffset.UtcNow.AddHours(-1));
                foreach (var p in pulled)
                {
                    var j = JsonSerializer.Serialize(p);
                    try
                    {
                        var remote = JsonSerializer.Deserialize<T>(j);
                        if (remote == null) continue;

                        // assume Id property exists
                        var idProp = typeof(T).GetProperty("Id");
                        if (idProp == null) continue;
                        var id = (Guid)idProp.GetValue(remote)!;

                        var existing = await set.FindAsync(new object[] { id }, cancellationToken);
                        if (existing == null)
                        {
                            await set.AddAsync(remote, cancellationToken);
                        }
                        else
                        {
                            var remoteUpdatedProp = typeof(T).GetProperty("UpdatedAt");
                            DateTimeOffset? remoteUpdated = null;
                            if (remoteUpdatedProp != null) remoteUpdated = (DateTimeOffset?)remoteUpdatedProp.GetValue(remote);

                            var localUpdated = (DateTimeOffset?)remoteUpdatedProp?.GetValue(existing);

                            if (remoteUpdated.HasValue && (!localUpdated.HasValue || remoteUpdated > localUpdated))
                            {
                                _db.Entry(existing).CurrentValues.SetValues(remote!);
                            }
                        }
                    }
                    catch (Exception exp)
                    {
                        _logger.LogWarning(exp, "Failed to apply pulled {Type} record", name);
                    }
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed for {Type}", name);
            }
        }

        public Task TriggerAsync()
        {
            _ = Task.Run(() => RunOnceAsync());
            return Task.CompletedTask;
        }
    }
}