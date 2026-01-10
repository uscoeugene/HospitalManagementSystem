using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Auth.DTOs;
using HMS.API.Domain.Auth;
using HMS.API.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Infrastructure.Auth
{
    public static class SeedData
    {
        public static async Task EnsureSeedDataAsync(AuthDbContext db, global::HMS.API.Application.Auth.IPasswordHasher hasher)
        {
            if (await db.Roles.AnyAsync()) return;

            // create permissions
            var permManageUsers = new Permission { Code = "users.manage", Description = "Manage users" };
            var permManageRoles = new Permission { Code = "roles.manage", Description = "Manage roles" };
            var permAuthLogin = new Permission { Code = "auth.login", Description = "Login" };
            var permPatientsManage = new Permission { Code = "patients.manage", Description = "Manage patients" };
            var permPatientsView = new Permission { Code = "patients.view", Description = "View patients" };

            db.Permissions.AddRange(permManageUsers, permManageRoles, permAuthLogin, permPatientsManage, permPatientsView);

            // create roles
            var adminRole = new Role { Name = "Admin", Description = "Administrator" };
            var userRole = new Role { Name = "User", Description = "Default user" };

            db.Roles.AddRange(adminRole, userRole);

            await db.SaveChangesAsync();

            // assign permissions to admin
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permManageUsers });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permManageRoles });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permAuthLogin });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPatientsManage });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPatientsView });

            await db.SaveChangesAsync();

            // create admin user
            var admin = new User
            {
                Username = "admin",
                Email = "admin@localhost",
                PasswordHash = hasher.Hash("Admin@12345")
            };

            db.Users.Add(admin);
            await db.SaveChangesAsync();

            db.UserRoles.Add(new UserRole { User = admin, Role = adminRole });
            await db.SaveChangesAsync();
        }
    }
}