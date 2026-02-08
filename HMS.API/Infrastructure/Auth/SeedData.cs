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
            var permPharmInventory = new Permission { Code = "pharmacy.inventory.manage", Description = "Manage pharmacy inventory" };
            var permPharmDelete = new Permission { Code = "pharmacy.delete", Description = "Delete pharmacy items (soft delete)" };
            var permPharmCreate = new Permission { Code = "pharmacy.create", Description = "Create prescriptions" };
            var permPharmDispense = new Permission { Code = "pharmacy.dispense", Description = "Dispense medications" };

            db.Permissions.AddRange(permPharmView, permPharmManage, permPharmInventory, permPharmDelete, permPharmCreate, permPharmDispense);

            // profile permissions
            var permProfileRead = new Permission { Code = "PROFILE.READ", Description = "Read user profiles" };
            var permProfileUpdate = new Permission { Code = "PROFILE.UPDATE", Description = "Update user profiles" };
            var permProfileManage = new Permission { Code = "PROFILE.MANAGE", Description = "Manage user profiles" };

            db.Permissions.AddRange(permProfileRead, permProfileUpdate, permProfileManage);

            // reporting permissions
            var permReportsPatientsView = new Permission { Code = "reports.patients.view", Description = "View patient reports" };
            var permReportsProfilesView = new Permission { Code = "reports.profiles.view", Description = "View profile reports" };

            db.Permissions.AddRange(permReportsPatientsView, permReportsProfilesView);

            // new credit permissions
            var permLabChargeOnCredit = new Permission { Code = "lab.charge.credit", Description = "Allow charging lab items on credit" };
            var permPharmDispenseOnCredit = new Permission { Code = "pharmacy.dispense.credit", Description = "Allow dispensing pharmacy items on credit" };
            db.Permissions.AddRange(permLabChargeOnCredit, permPharmDispenseOnCredit);

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
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permProfileRead });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permProfileUpdate });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permProfileManage });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permReportsPatientsView });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permReportsProfilesView });
            // grant admin credit permissions
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permLabChargeOnCredit });
            db.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permPharmDispenseOnCredit });

            // assign permissions to cashier
            db.RolePermissions.Add(new RolePermission { Role = cashierRole, Permission = permPaymentsCreate });
            db.RolePermissions.Add(new RolePermission { Role = cashierRole, Permission = permPaymentsView });
            db.RolePermissions.Add(new RolePermission { Role = cashierRole, Permission = permBillingView });

            // assign permissions to lab role
            db.RolePermissions.Add(new RolePermission { Role = labRole, Permission = permLabRequest });
            db.RolePermissions.Add(new RolePermission { Role = labRole, Permission = permLabProcess });
            db.RolePermissions.Add(new RolePermission { Role = labRole, Permission = permLabView });
            // grant lab role credit permission
            db.RolePermissions.Add(new RolePermission { Role = labRole, Permission = permLabChargeOnCredit });

            // assign permissions to pharmacist role
            db.RolePermissions.Add(new RolePermission { Role = pharmRole, Permission = permPharmView });
            db.RolePermissions.Add(new RolePermission { Role = pharmRole, Permission = permPharmCreate });
            db.RolePermissions.Add(new RolePermission { Role = pharmRole, Permission = permPharmDispense });
            db.RolePermissions.Add(new RolePermission { Role = pharmRole, Permission = permPharmManage });
            db.RolePermissions.Add(new RolePermission { Role = pharmRole, Permission = permPharmInventory });
            db.RolePermissions.Add(new RolePermission { Role = pharmRole, Permission = permPharmDelete });
            // grant pharmacist credit permission
            db.RolePermissions.Add(new RolePermission { Role = pharmRole, Permission = permPharmDispenseOnCredit });

            await db.SaveChangesAsync();

            // create central admin user
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

            // create regular user for tests
            var user = new User
            {
                Username = "user",
                Email = "user@localhost",
                PasswordHash = hasher.Hash("User@12345")
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.UserRoles.Add(new UserRole { User = user, Role = userRole });
            await db.SaveChangesAsync();

            // seed central tenant record in auth DB so the system has a central reference
            if (!await db.Set<HMS.API.Domain.Common.Tenant>().AnyAsync())
            {
                var central = new HMS.API.Domain.Common.Tenant { Name = "Central HMS", Code = "CENTRAL", IsCentral = true };
                db.Set<HMS.API.Domain.Common.Tenant>().Add(central);
                await db.SaveChangesAsync();

                // Create two sample tenant hospitals and assign a tenant admin user to each
                var hospA = new HMS.API.Domain.Common.Tenant { Name = "St Mary Hospital", Code = "SMH", IsCentral = false };
                var hospB = new HMS.API.Domain.Common.Tenant { Name = "Green Valley Clinic", Code = "GVC", IsCentral = false };
                db.Set<HMS.API.Domain.Common.Tenant>().AddRange(hospA, hospB);
                await db.SaveChangesAsync();

                // create tenant admin users and assign Admin role, set TenantId on user
                var adminA = new User { Username = "smh_admin", Email = "admin@smh.local", PasswordHash = hasher.Hash("SmhAdmin@123"), TenantId = hospA.Id };
                var adminB = new User { Username = "gvc_admin", Email = "admin@gvc.local", PasswordHash = hasher.Hash("GvcAdmin@123"), TenantId = hospB.Id };
                db.Users.AddRange(adminA, adminB);
                await db.SaveChangesAsync();

                db.UserRoles.Add(new UserRole { User = adminA, Role = adminRole });
                db.UserRoles.Add(new UserRole { User = adminB, Role = adminRole });
                await db.SaveChangesAsync();

                // create default subscriptions
                var subA = new HMS.API.Domain.Common.TenantSubscription { TenantId = hospA.Id, Plan = "pro", Status = HMS.API.Domain.Common.SubscriptionStatus.Active, StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddYears(1) };
                var subB = new HMS.API.Domain.Common.TenantSubscription { TenantId = hospB.Id, Plan = "basic", Status = HMS.API.Domain.Common.SubscriptionStatus.Trial, StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddMonths(1) };
                db.Set<HMS.API.Domain.Common.TenantSubscription>().AddRange(subA, subB);
                await db.SaveChangesAsync();
            }
        }
    }
}