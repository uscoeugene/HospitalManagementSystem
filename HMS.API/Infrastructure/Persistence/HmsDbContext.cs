using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HMS.API.Domain.Common;
using Microsoft.EntityFrameworkCore;
using HMS.API.Domain.Patient;

namespace HMS.API.Infrastructure.Persistence
{
    public class HmsDbContext : DbContext
    {
        public HmsDbContext(DbContextOptions<HmsDbContext> options) : base(options)
        {
        }

        public DbSet<Patient> Patients { get; set; } = null!;
        public DbSet<Visit> Visits { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Patient>(b =>
            {
                b.HasKey(p => p.Id);
                b.Property(p => p.FirstName).IsRequired().HasMaxLength(200);
                b.Property(p => p.LastName).IsRequired().HasMaxLength(200);
                b.Property(p => p.Gender).HasMaxLength(50);
                b.HasMany(p => p.Visits).WithOne(v => v.Patient).HasForeignKey(v => v.PatientId);
                b.HasIndex(p => p.MedicalRecordNumber).IsUnique(false);
            });

            modelBuilder.Entity<Visit>(b =>
            {
                b.HasKey(v => v.Id);
                b.Property(v => v.VisitType).HasMaxLength(100);
                b.Property(v => v.Notes).HasMaxLength(2000);
                b.HasOne(v => v.Patient).WithMany(p => p.Visits).HasForeignKey(v => v.PatientId);
            });

            // Apply global query filter for soft-delete
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var method = typeof(HmsDbContext).GetMethod(nameof(ApplySoftDeleteQueryFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                                 ?.MakeGenericMethod(entityType.ClrType);
                    method?.Invoke(null, new object[] { modelBuilder });
                }
            }
        }

        private static void ApplySoftDeleteQueryFilter<TEntity>(ModelBuilder builder) where TEntity : BaseEntity
        {
            builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
        }

        public override int SaveChanges()
        {
            UpdateAuditFields();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            UpdateAuditFields();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateAuditFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            UpdateAuditFields();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void UpdateAuditFields()
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            var now = DateTimeOffset.UtcNow;

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = now;
                        // CreatedBy should be set by application/service layer before SaveChanges
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = now;
                        // UpdatedBy should be set by application/service layer before SaveChanges
                        break;
                    case EntityState.Deleted:
                        // Convert hard delete to soft delete
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.DeletedAt = now;
                        // DeletedBy should be set by application/service layer before SaveChanges
                        break;
                }
            }
        }
    }
}