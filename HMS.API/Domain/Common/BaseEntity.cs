using System;

namespace HMS.API.Domain.Common
{
    public abstract class BaseEntity : IEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Tenant information (multi-tenant support)
        // TenantId: internal GUID identifying the hospital/tenant.
        public Guid? TenantId { get; set; }

        // Timestamps
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? UpdatedAt { get; set; }

        // Audit (who performed actions)
        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }

        public Guid? DeletedBy { get; set; }

        // Soft-delete
        public bool IsDeleted { get; set; } = false;

        public DateTimeOffset? DeletedAt { get; set; }

        // Sync
        public bool IsSynced { get; set; } = false;

        public long SyncVersion { get; set; } = 0;

        public void SoftDelete(Guid? deletedBy = null)
        {
            IsDeleted = true;
            DeletedAt = DateTimeOffset.UtcNow;
            DeletedBy = deletedBy;
        }

        public void Restore()
        {
            IsDeleted = false;
            DeletedAt = null;
            DeletedBy = null;
        }
    }
}