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
            var permBillingCreate = new Permission { Code = "billing.create", Description = "Create invoices" };
            var permBillingView = new Permission { Code = "billing.view", Description = "View invoices" };
            var permBillingApply = new Permission { Code = "billing.applypayment", Description = "Apply payments to invoices" };
            var permBillingExport = new Permission { Code = "billing.export", Description = "Export invoices" };
            var permPaymentsCreate = new Permission { Code = "payments.create", Description = "Create payments (cashier)" };
            var permPaymentsView = new Permission { Code = "payments.view", Description = "View payments" };

            db.Permissions.AddRange(permManageUsers, permManageRoles, permAuthLogin, permPatientsManage, permPatientsView, permBillingCreate, permBillingView, permBillingApply, permBillingExport, permPaymentsCreate, permPaymentsView);

            // add permissions
            var permLabRequest = new Permission { Code = "lab.request", Description = "Create lab requests" };
            var permLabProcess = new Permission { Code = "lab.process", Description = "Process lab requests" };
            var permLabView = new Permission { Code = "lab.view", Description = "View lab tests and requests" };
            var permLabManage = new Permission { Code = "lab.manage", Description = "Manage lab test catalog" };

            db.Permissions.AddRange(permLabRequest, permLabProcess, permLabView, permLabManage);

            // pharmacy permissions
            var permPharmView = new Permission { Code = "pharmacy.view", Description = "View drugs and prescriptions" };
            var permPharmManage = new Permission { Code = "pharmacy.manage", Description = "Manage drug catalog" };
            var permPharmCreate = new Permission { Code = "pharmacy.create", Description = "Create prescriptions" };
            var permPharmDispense = new Permission { Code = "pharmacy.dispense", Description = "Dispense medications" };

            db.Permissions.AddRange(permPharmView, permPharmManage, permPharmCreate, permPharmDispense);

            // create roles
            var adminRole = new Role { Name = "Admin", Description = "Administrator" };
            var userRole = new Role { Name = "User", Description = "Default user" };
            var cashierRole = new Role { Name = "Cashier", Description = "Cashier role for payments" };
            var labRole = new Role { Name = "LabTech", Description = "Laboratory technician" };
            var pharmRole = new Role { Name = "Pharmacist", Description = "Pharmacist role" };

            db.Roles.AddRange(adminRole, userRole, cashierRole, labRole, pharmRole);

            await db.SaveChangesAsync();

            // assign permissions to admin
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permManageUsers });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permManageRoles });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permAuthLogin });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPatientsManage });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPatientsView });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permBillingCreate });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permBillingView });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permBillingApply });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permBillingExport });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPaymentsCreate });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPaymentsView });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permLabRequest });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permLabProcess });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permLabView });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permLabManage });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPharmView });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPharmManage });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPharmCreate });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPharmDispense });

            // assign permissions to cashier
            db.RolePermissions.Add(new RolePermission { Role = cashierRole, Permission = permPaymentsCreate });
            db.RolePermissions.Add(new RolePermission { Role = cashierRole, Permission = permPaymentsView });
            db.RolePermissions.Add(new RolePermission { Role = cashierRole, Permission = permBillingView });

            // assign permissions to lab role
            db.RolePermissions.Add(new RolePermission { Role = labRole, Permission = permLabRequest });
            db.RolePermissions.Add(new RolePermission { Role = labRole, Permission = permLabProcess });
            db.RolePermissions.Add(new RolePermission { Role = labRole, Permission = permLabView });

            // assign permissions to pharmacist role
            db.RolePermissions.Add(new RolePermission { Role = pharmRole, Permission = permPharmView });
            db.RolePermissions.Add(new RolePermission { Role = pharmRole, Permission = permPharmCreate });
            db.RolePermissions.Add(new RolePermission { Role = pharmRole, Permission = permPharmDispense });

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