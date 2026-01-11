using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using HMS.API.Domain.Common;
using Microsoft.EntityFrameworkCore;
using HMS.API.Domain.Patient;
using HMS.API.Domain.Billing;
using HMS.API.Domain.Payments;
using HMS.API.Domain.Profile;
using HMS.API.Infrastructure.Persistence;

namespace HMS.API.Infrastructure.Persistence
{
    public class HmsDbContext : DbContext
    {
        private readonly ICurrentUserService? _currentUserService;

        public HmsDbContext(DbContextOptions<HmsDbContext> options, ICurrentUserService? currentUserService = null) : base(options)
        {
            _currentUserService = currentUserService;
        }

        public DbSet<Patient> Patients { get; set; } = null!;
        public DbSet<Visit> Visits { get; set; } = null!;

        // Billing
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<InvoiceItem> InvoiceItems { get; set; } = null!;
        public DbSet<InvoicePayment> InvoicePayments { get; set; } = null!;
        public DbSet<BillingAudit> BillingAudits { get; set; } = null!;

        // Outbox
        public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<Receipt> Receipts { get; set; } = null!;
        public DbSet<Refund> Refunds { get; set; } = null!;
        public DbSet<RefundReversal> RefundReversals { get; set; } = null!;

        // Lab
        public DbSet<HMS.API.Domain.Lab.LabTest> LabTests { get; set; } = null!;
        public DbSet<HMS.API.Domain.Lab.LabRequest> LabRequests { get; set; } = null!;
        public DbSet<HMS.API.Domain.Lab.LabRequestItem> LabRequestItems { get; set; } = null!;

        // Pharmacy
        public DbSet<HMS.API.Domain.Pharmacy.Drug> Drugs { get; set; } = null!;
        public DbSet<HMS.API.Domain.Pharmacy.Prescription> Prescriptions { get; set; } = null!;
        public DbSet<HMS.API.Domain.Pharmacy.PrescriptionItem> PrescriptionItems { get; set; } = null!;
        public DbSet<HMS.API.Domain.Pharmacy.DispenseLog> DispenseLogs { get; set; } = null!;
        public DbSet<HMS.API.Domain.Pharmacy.Reservation> Reservations { get; set; } = null!;

        // Profiles - integrated into the main HMS DB
        public DbSet<UserProfile> UserProfiles { get; set; } = null!;

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

            // Billing mappings
            modelBuilder.Entity<Invoice>(b =>
            {
                b.HasKey(i => i.Id);
                b.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(100);
                b.Property(i => i.TotalAmount).HasColumnType("decimal(18,2)");
                b.Property(i => i.AmountPaid).HasColumnType("decimal(18,2)");
                b.Property(i => i.Currency).IsRequired().HasMaxLength(3);
                b.HasMany(i => i.Items).WithOne(it => it.Invoice).HasForeignKey(it => it.InvoiceId);
                b.HasMany(i => i.Payments).WithOne(p => p.Invoice).HasForeignKey(p => p.InvoiceId);
            });

            modelBuilder.Entity<InvoiceItem>(b =>
            {
                b.HasKey(ii => ii.Id);
                b.Property(ii => ii.Description).HasMaxLength(1000);
                b.Property(ii => ii.UnitPrice).HasColumnType("decimal(18,2)");
                b.Property(ii => ii.Quantity);
                b.HasOne(ii => ii.Invoice).WithMany(i => i.Items).HasForeignKey(ii => ii.InvoiceId);
            });

            modelBuilder.Entity<InvoicePayment>(b =>
            {
                b.HasKey(ip => ip.Id);
                b.Property(ip => ip.Amount).HasColumnType("decimal(18,2)");
                b.Property(ip => ip.ExternalReference).HasMaxLength(500);
                b.HasOne(ip => ip.Invoice).WithMany(i => i.Payments).HasForeignKey(ip => ip.InvoiceId);
            });

            modelBuilder.Entity<BillingAudit>(b =>
            {
                b.HasKey(a => a.Id);
                b.Property(a => a.Action).IsRequired().HasMaxLength(200);
                b.Property(a => a.Details).HasMaxLength(1000);
            });

            modelBuilder.Entity<OutboxMessage>(b =>
            {
                b.HasKey(o => o.Id);
                b.Property(o => o.Type).IsRequired().HasMaxLength(200);
                b.Property(o => o.Content).IsRequired();
                b.Property(o => o.OccurredAt).IsRequired();
                b.Property(o => o.ProcessedAt);
                b.Property(o => o.Attempts);
            });

            modelBuilder.Entity<Payment>(b =>
            {
                b.HasKey(p => p.Id);
                b.Property(p => p.Amount).HasColumnType("decimal(18,2)");
                b.Property(p => p.Currency).HasMaxLength(3);
                b.Property(p => p.Status).IsRequired();
                b.HasOne<Receipt>().WithOne(r => r.Payment).HasForeignKey<Receipt>(r => r.PaymentId);
            });

            modelBuilder.Entity<Receipt>(b =>
            {
                b.HasKey(r => r.Id);
                b.Property(r => r.ReceiptNumber).IsRequired().HasMaxLength(100);
                b.Property(r => r.Details).HasMaxLength(2000);
            });

            modelBuilder.Entity<Refund>(b =>
            {
                b.HasKey(r => r.Id);
                b.Property(r => r.Amount).HasColumnType("decimal(18,2)");
                b.Property(r => r.Reason).HasMaxLength(1000);
                b.HasOne(r => r.Payment).WithMany().HasForeignKey(r => r.PaymentId);
            });

            modelBuilder.Entity<RefundReversal>(b =>
            {
                b.HasKey(r => r.Id);
                b.Property(r => r.ProcessedAt).IsRequired();
                b.Property(r => r.Reason).HasMaxLength(1000);
                b.HasOne(r => r.Refund).WithMany().HasForeignKey(r => r.RefundId);
            });

            // Lab mappings
            modelBuilder.Entity<HMS.API.Domain.Lab.LabTest>(b =>
            {
                b.HasKey(t => t.Id);
                b.Property(t => t.Code).IsRequired().HasMaxLength(50);
                b.Property(t => t.Name).IsRequired().HasMaxLength(200);
                b.Property(t => t.Price).HasColumnType("decimal(18,2)");
                b.Property(t => t.Currency).HasMaxLength(3);
            });

            modelBuilder.Entity<HMS.API.Domain.Lab.LabRequest>(b =>
            {
                b.HasKey(r => r.Id);
                b.HasMany(r => r.Items).WithOne(i => i.LabRequest).HasForeignKey(i => i.LabRequestId);
            });

            modelBuilder.Entity<HMS.API.Domain.Lab.LabRequestItem>(b =>
            {
                b.HasKey(i => i.Id);
                b.Property(i => i.Price).HasColumnType("decimal(18,2)");
                b.Property(i => i.Currency).HasMaxLength(3);
                b.HasOne(i => i.LabTest).WithMany().HasForeignKey(i => i.LabTestId);
            });

            // Pharmacy mappings
            modelBuilder.Entity<HMS.API.Domain.Pharmacy.Drug>(b =>
            {
                b.HasKey(d => d.Id);
                b.Property(d => d.Code).IsRequired().HasMaxLength(100);
                b.Property(d => d.Name).IsRequired().HasMaxLength(200);
                b.Property(d => d.Price).HasColumnType("decimal(18,2)");
                b.Property(d => d.Currency).HasMaxLength(3);
                b.Property(d => d.ReservedStock).HasDefaultValue(0);
            });

            modelBuilder.Entity<HMS.API.Domain.Pharmacy.Prescription>(b =>
            {
                b.HasKey(p => p.Id);
                b.HasMany(p => p.Items).WithOne(i => i.Prescription).HasForeignKey(i => i.PrescriptionId);
            });

            modelBuilder.Entity<HMS.API.Domain.Pharmacy.PrescriptionItem>(b =>
            {
                b.HasKey(i => i.Id);
                b.Property(i => i.Price).HasColumnType("decimal(18,2)");
                b.Property(i => i.Currency).HasMaxLength(3);
                b.HasOne(i => i.Drug).WithMany().HasForeignKey(i => i.DrugId);
            });

            modelBuilder.Entity<HMS.API.Domain.Pharmacy.DispenseLog>(b =>
            {
                b.HasKey(d => d.Id);
                b.Property(d => d.DispensedAt).IsRequired();
            });

            // Pharmacy reservations
            modelBuilder.Entity<HMS.API.Domain.Pharmacy.Reservation>(b =>
            {
                b.HasKey(r => r.Id);
                b.Property(r => r.Quantity).IsRequired();
                b.Property(r => r.ExpiresAt).IsRequired();
                b.Property(r => r.Processed).HasDefaultValue(false);
                b.Property(r => r.CreatedAt).IsRequired();
            });

            // Profile mappings
            modelBuilder.Entity<UserProfile>(b =>
            {
                b.HasKey(p => p.Id);
                b.Property(p => p.FirstName).IsRequired().HasMaxLength(200);
                b.Property(p => p.LastName).IsRequired().HasMaxLength(200);
                b.Property(p => p.Email).IsRequired().HasMaxLength(320);
                b.HasIndex(p => p.UserId).IsUnique();
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
            var userId = _currentUserService?.UserId;

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = now;
                        if (userId.HasValue) entry.Entity.CreatedBy = userId.Value;
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = now;
                        if (userId.HasValue) entry.Entity.UpdatedBy = userId.Value;
                        break;
                    case EntityState.Deleted:
                        // Convert hard delete to soft delete
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.DeletedAt = now;
                        if (userId.HasValue) entry.Entity.DeletedBy = userId.Value;
                        break;
                }
            }
        }
    }
}