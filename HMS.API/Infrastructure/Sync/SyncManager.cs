using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HMS.API.Application.Sync;
using HMS.API.Application.Sync.DTOs;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HMS.API.Domain.Patient;
using HMS.API.Domain.Billing;
using HMS.API.Domain.Lab;
using HMS.API.Domain.Pharmacy;
using HMS.API.Domain.Payments;
using HMS.API.Domain.Profile;

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
                SyncEntitiesAsync(_db.Receipts, "Receipt", cancellationToken),
                // User profiles should be synced as well using DTO mapping
                SyncUserProfilesAsync(cancellationToken)
            };

            await Task.WhenAll(tasks);
        }

        public async Task RunOnceAsync(Guid tenantId, System.Threading.CancellationToken cancellationToken = default)
        {
            // tenant-scoped sync: pull subscription and user profiles relevant to this tenant
            try
            {
                // pull subscription info from cloud
                var subsArr = await _cloud.PullAsync("TenantSubscription", null);
                foreach (var p in subsArr)
                {
                    try
                    {
                        var j = JsonSerializer.Serialize(p);
                        var dto = JsonSerializer.Deserialize<HMS.API.Domain.Common.TenantSubscription>(j);
                        if (dto == null) continue;

                        if (dto.TenantId != tenantId) continue;

                        var existing = await _db.Set<HMS.API.Domain.Common.TenantSubscription>().FindAsync(new object[] { dto.Id }, cancellationToken);
                        if (existing == null)
                        {
                            await _db.Set<HMS.API.Domain.Common.TenantSubscription>().AddAsync(dto, cancellationToken);
                        }
                        else
                        {
                            _db.Entry(existing).CurrentValues.SetValues(dto);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to apply pulled subscription record");
                    }
                }

                // push local users to cloud
                var localUsers = await _db.Set<HMS.API.Domain.Auth.LocalUser>().AsNoTracking().Where(u => u.TenantId == tenantId && !u.IsDeleted).ToArrayAsync(cancellationToken);
                if (localUsers.Length > 0)
                {
                    await _cloud.PushAsync("LocalUser", localUsers.Cast<object>().ToArray());
                }

                // pull central local user records (authoritative)
                var pulled = await _cloud.PullAsync("LocalUser", null);
                foreach (var p in pulled)
                {
                    try
                    {
                        var j = JsonSerializer.Serialize(p);
                        var dto = JsonSerializer.Deserialize<HMS.API.Domain.Auth.LocalUser>(j);
                        if (dto == null) continue;
                        if (dto.TenantId != tenantId) continue;

                        var existing = await _db.Set<HMS.API.Domain.Auth.LocalUser>().FindAsync(new object[] { dto.Id }, cancellationToken);
                        if (existing == null)
                        {
                            await _db.Set<HMS.API.Domain.Auth.LocalUser>().AddAsync(dto, cancellationToken);
                        }
                        else
                        {
                            _db.Entry(existing).CurrentValues.SetValues(dto);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to apply pulled local user record");
                    }
                }

                // pull user profiles for tenant (existing code)
                var pulledProfiles = await _cloud.PullAsync("UserProfile", null);
                foreach (var p in pulledProfiles)
                {
                    try
                    {
                        var j = JsonSerializer.Serialize(p);
                        var dto = JsonSerializer.Deserialize<HMS.API.Application.Sync.DTOs.UserProfileSyncDto>(j);
                        if (dto == null) continue;
                        // here we assume profiles have a TenantId in Metadata or that User has TenantId in auth DB to correlate
                        // for simplicity, apply profiles as-is
                        var existing = await _db.UserProfiles.FindAsync(new object[] { dto.Id }, cancellationToken);
                        if (existing == null)
                        {
                            var entity = new HMS.API.Domain.Profile.UserProfile
                            {
                                Id = dto.Id,
                                UserId = dto.UserId,
                                FirstName = dto.FirstName,
                                LastName = dto.LastName,
                                OtherNames = dto.OtherNames,
                                Gender = dto.Gender,
                                DateOfBirth = dto.DateOfBirth,
                                PhoneNumber = dto.PhoneNumber,
                                Email = dto.Email,
                                Address = dto.Address,
                                PhotoUrl = dto.PhotoUrl,
                                StaffNumber = dto.StaffNumber,
                                Department = dto.Department,
                                JobTitle = dto.JobTitle,
                                IsMedicalStaff = dto.IsMedicalStaff,
                                CreatedAt = dto.CreatedAt,
                                UpdatedAt = dto.UpdatedAt
                            };
                            await _db.UserProfiles.AddAsync(entity, cancellationToken);
                        }
                        else
                        {
                            _db.Entry(existing).CurrentValues.SetValues(new HMS.API.Domain.Profile.UserProfile
                            {
                                Id = dto.Id,
                                UserId = dto.UserId,
                                FirstName = dto.FirstName,
                                LastName = dto.LastName,
                                OtherNames = dto.OtherNames,
                                Gender = dto.Gender,
                                DateOfBirth = dto.DateOfBirth,
                                PhoneNumber = dto.PhoneNumber,
                                Email = dto.Email,
                                Address = dto.Address,
                                PhotoUrl = dto.PhotoUrl,
                                StaffNumber = dto.StaffNumber,
                                Department = dto.Department,
                                JobTitle = dto.JobTitle,
                                IsMedicalStaff = dto.IsMedicalStaff,
                                CreatedAt = dto.CreatedAt,
                                UpdatedAt = dto.UpdatedAt,
                                UpdatedBy = dto.UpdatedByUserId
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to apply pulled profile record");
                    }
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tenant-scoped sync failed for {TenantId}", tenantId);
            }
        }

        private async Task SyncUserProfilesAsync(System.Threading.CancellationToken cancellationToken)
        {
            const string name = "UserProfile";
            try
            {
                // pull/push logic uses DTOs to avoid coupling to domain
                var unsynced = await _db.UserProfiles.AsNoTracking().Where(p => !p.IsSynced).ToArrayAsync(cancellationToken);
                if (unsynced.Length > 0)
                {
                    var payload = unsynced.Select(p => ToDto(p)).ToArray();
                    await _cloud.PushAsync(name, payload.Cast<object>().ToArray());

                    foreach (var p in unsynced)
                    {
                        try
                        {
                            var entry = _db.Attach(p);
                            entry.Property("IsSynced").CurrentValue = true;
                            entry.Property("SyncVersion").CurrentValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        }
                        catch (Exception exu)
                        {
                            _logger.LogWarning(exu, "Failed to mark profile synced for {Type}", name);
                        }
                    }

                    await _db.SaveChangesAsync(cancellationToken);
                }

                var pulled = await _cloud.PullAsync(name, DateTimeOffset.UtcNow.AddHours(-1));
                foreach (var p in pulled)
                {
                    try
                    {
                        var j = JsonSerializer.Serialize(p);
                        var dto = JsonSerializer.Deserialize<UserProfileSyncDto>(j);
                        if (dto == null) continue;

                        var existing = await _db.UserProfiles.FindAsync(new object[] { dto.Id }, cancellationToken);
                        if (existing == null)
                        {
                            var entity = FromDto(dto);
                            await _db.UserProfiles.AddAsync(entity, cancellationToken);
                        }
                        else
                        {
                            DateTimeOffset? remoteUpdated = dto.UpdatedAt;
                            DateTimeOffset? localUpdated = existing.UpdatedAt;

                            if (remoteUpdated.HasValue && (!localUpdated.HasValue || remoteUpdated > localUpdated))
                            {
                                _db.Entry(existing).CurrentValues.SetValues(FromDto(dto));
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

        private static UserProfileSyncDto ToDto(UserProfile p) => new()
        {
            Id = p.Id,
            UserId = p.UserId,
            FirstName = p.FirstName,
            LastName = p.LastName,
            OtherNames = p.OtherNames,
            Gender = p.Gender,
            DateOfBirth = p.DateOfBirth,
            PhoneNumber = p.PhoneNumber,
            Email = p.Email,
            Address = p.Address,
            PhotoUrl = p.PhotoUrl,
            StaffNumber = p.StaffNumber,
            Department = p.Department,
            JobTitle = p.JobTitle,
            IsMedicalStaff = p.IsMedicalStaff,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            UpdatedByUserId = p.UpdatedBy,
            IsSynced = p.IsSynced,
            SyncVersion = p.SyncVersion
        };

        private static UserProfile FromDto(UserProfileSyncDto d) => new()
        {
            Id = d.Id,
            UserId = d.UserId,
            FirstName = d.FirstName,
            LastName = d.LastName,
            OtherNames = d.OtherNames,
            Gender = d.Gender,
            DateOfBirth = d.DateOfBirth,
            PhoneNumber = d.PhoneNumber,
            Email = d.Email,
            Address = d.Address,
            PhotoUrl = d.PhotoUrl,
            StaffNumber = d.StaffNumber,
            Department = d.Department,
            JobTitle = d.JobTitle,
            IsMedicalStaff = d.IsMedicalStaff,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            UpdatedBy = d.UpdatedByUserId,
            IsSynced = d.IsSynced,
            SyncVersion = d.SyncVersion
        };

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