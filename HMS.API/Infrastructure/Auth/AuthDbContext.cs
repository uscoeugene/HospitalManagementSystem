using System;
using System.Linq;
using HMS.API.Application.Common;
using HMS.API.Domain.Auth;
using HMS.API.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Infrastructure.Auth
{
    public class AuthDbContext : DbContext
    {
        private readonly ICurrentUserService? _currentUserService;

        public AuthDbContext(DbContextOptions<AuthDbContext> options, ICurrentUserService? currentUserService = null) : base(options)
        {
            _currentUserService = currentUserService;
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<UserRole> UserRoles { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<AuthAudit> AuthAudits { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.HasIndex(u => u.Username).IsUnique();
                b.Property(u => u.Username).IsRequired().HasMaxLength(100);
                b.Property(u => u.Email).HasMaxLength(255);

                b.HasMany(u => u.UserRoles).WithOne(ur => ur.User).HasForeignKey(ur => ur.UserId);
                b.HasMany(u => u.RefreshTokens).WithOne(rt => rt.User).HasForeignKey(rt => rt.UserId);
            });

            modelBuilder.Entity<Role>(b =>
            {
                b.HasKey(r => r.Id);
                b.Property(r => r.Name).IsRequired().HasMaxLength(100);
                b.HasMany(r => r.UserRoles).WithOne(ur => ur.Role).HasForeignKey(ur => ur.RoleId);
                b.HasMany(r => r.RolePermissions).WithOne(rp => rp.Role).HasForeignKey(rp => rp.RoleId);
            });

            modelBuilder.Entity<Permission>(b =>
            {
                b.HasKey(p => p.Id);
                b.Property(p => p.Code).IsRequired().HasMaxLength(200);
                b.HasIndex(p => p.Code).IsUnique();
                b.HasMany(p => p.RolePermissions).WithOne(rp => rp.Permission).HasForeignKey(rp => rp.PermissionId);
            });

            modelBuilder.Entity<UserRole>(b =>
            {
                b.HasKey(nameof(UserRole.UserId), nameof(UserRole.RoleId));
                b.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
                b.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
            });

            modelBuilder.Entity<RolePermission>(b =>
            {
                b.HasKey(nameof(RolePermission.RoleId), nameof(RolePermission.PermissionId));
                b.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
                b.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId);
            });

            modelBuilder.Entity<AuthAudit>(b =>
            {
                b.HasKey(a => a.Id);
                b.Property(a => a.Action).IsRequired().HasMaxLength(200);
                b.Property(a => a.Details).HasMaxLength(1000);
            });

            modelBuilder.Entity<RefreshToken>(b =>
            {
                b.HasKey(rt => rt.Id);
                b.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(256);
                b.HasIndex(rt => rt.TokenHash).IsUnique();
            });

            // Apply global query filter for soft-delete on BaseEntity
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var method = typeof(AuthDbContext).GetMethod(nameof(ApplySoftDeleteQueryFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
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
            ApplyCurrentUserAuditFields();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyCurrentUserAuditFields();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override System.Threading.Tasks.Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            ApplyCurrentUserAuditFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override System.Threading.Tasks.Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, System.Threading.CancellationToken cancellationToken = default)
        {
            ApplyCurrentUserAuditFields();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ApplyCurrentUserAuditFields()
        {
            var now = DateTimeOffset.UtcNow;
            var userId = _currentUserService?.UserId;

            var entries = ChangeTracker.Entries<BaseEntity>().ToList();
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