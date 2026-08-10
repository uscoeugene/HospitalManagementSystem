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
            var isFreshDatabase = !await db.Roles.AnyAsync();

            var permissions = await EnsureBuiltInPermissionsAsync(db);
            var roles = await EnsureBuiltInRolesAsync(db);

            // ensure app settings exist (System:DeploymentMode, PlatformContext:Hosts)
            await EnsureAppSettingsAsync(db);

            await GrantRolePermissionsAsync(db, roles.User, permissions.ProfileRead, permissions.ProfileUpdate);
            await GrantRolePermissionsAsync(db, roles.SystemAdministrator,
                permissions.ProfileRead,
                permissions.ProfileUpdate,
                permissions.ProfileManage,
                permissions.UsersManage,
                permissions.RolesManage,
                permissions.AuthLogin,
                permissions.IntegrationsManage,
                permissions.SecuritySettingsManage,
                permissions.TenantsManage,
                permissions.ApprovalsManage,
                permissions.PatientsManage,
                permissions.PatientsView,
                permissions.ClinicalNotesManage,
                permissions.OrdersManage,
                permissions.CarePlansManage,
                permissions.VitalsManage,
                permissions.MedicationsAdminister,
                permissions.NursingNotesManage,
                permissions.AppointmentsManage,
                permissions.AppointmentsView,
                permissions.CheckInManage,
                permissions.DemographicsManage,
                permissions.BillingCreate,
                permissions.BillingView,
                permissions.BillingApply,
                permissions.BillingExport,
                permissions.PaymentsCreate,
                permissions.PaymentsView,
                permissions.InsuranceView,
                permissions.InsuranceManage,
                permissions.ClaimsManage,
                permissions.ClaimsView,
                permissions.PreauthorizationManage,
                permissions.RefundsManage,
                permissions.LabRequest,
                permissions.LabProcess,
                permissions.LabView,
                permissions.LabManage,
                permissions.LabSpecimenManage,
                permissions.LabValidate,
                permissions.RadiologyRequest,
                permissions.RadiologySchedule,
                permissions.RadiologyProcess,
                permissions.RadiologyView,
                permissions.PharmacyView,
                permissions.PharmacyManage,
                permissions.PharmacyCreate,
                permissions.PharmacyDispense,
                permissions.PharmacyInventoryManage,
                permissions.PharmacyDelete,
                permissions.InventoryView,
                permissions.InventoryManage,
                permissions.InventoryReceive,
                permissions.InventoryDispense,
                permissions.InventoryTransfer,
                permissions.InventoryCount,
                permissions.RecordsView,
                permissions.RecordsManage,
                permissions.DocumentsManage,
                permissions.CodingManage,
                permissions.CorrectionsManage,
                permissions.StaffView,
                permissions.StaffManage,
                permissions.ContractsManage,
                permissions.AttendanceManage,
                permissions.LeaveManage,
                permissions.ProcurementManage,
                permissions.PurchaseOrdersManage,
                permissions.SuppliersManage,
                permissions.FinanceView,
                permissions.FinanceManage,
                permissions.LedgerManage,
                permissions.ExpensesManage,
                permissions.ReconciliationManage,
                permissions.DepartmentApprovalsManage,
                permissions.DepartmentStaffView,
                permissions.DepartmentReportsView,
                permissions.WardsView,
                permissions.WardsManage,
                permissions.BedsView,
                permissions.BedsManage,
                permissions.TheatreManage,
                permissions.OperationsKpiView,
                permissions.SchedulingManage,
                permissions.AuditView,
                permissions.PatientPortalView,
                permissions.PatientPortalAppointments,
                permissions.PatientPortalBilling,
                permissions.PatientPortalPrescriptions,
                permissions.PatientPortalResults,
                permissions.PatientPortalRecords,
                permissions.ReportsPatientsView,
                permissions.ReportsProfilesView,
                permissions.AdminDashboardView,
                permissions.LabChargeOnCredit,
                permissions.PharmacyDispenseOnCredit);

            await GrantRolePermissionsAsync(db, roles.HospitalAdministrator,
                permissions.ProfileRead,
                permissions.ProfileUpdate,
                permissions.AuthLogin,
                permissions.ApprovalsManage,
                permissions.PatientsManage,
                permissions.PatientsView,
                permissions.ClinicalNotesManage,
                permissions.OrdersManage,
                permissions.CarePlansManage,
                permissions.VitalsManage,
                permissions.AppointmentsManage,
                permissions.AppointmentsView,
                permissions.CheckInManage,
                permissions.DemographicsManage,
                permissions.BillingView,
                permissions.BillingExport,
                permissions.PaymentsView,
                permissions.InsuranceView,
                permissions.ClaimsView,
                permissions.LabView,
                permissions.RadiologyView,
                permissions.PharmacyView,
                permissions.InventoryView,
                permissions.RecordsView,
                permissions.StaffView,
                permissions.ContractsManage,
                permissions.AttendanceManage,
                permissions.LeaveManage,
                permissions.ProcurementManage,
                permissions.SuppliersManage,
                permissions.FinanceView,
                permissions.DepartmentApprovalsManage,
                permissions.DepartmentStaffView,
                permissions.DepartmentReportsView,
                permissions.WardsView,
                permissions.BedsView,
                permissions.TheatreManage,
                permissions.OperationsKpiView,
                permissions.SchedulingManage,
                permissions.AuditView,
                permissions.ReportsPatientsView,
                permissions.ReportsProfilesView);

            await GrantRolePermissionsAsync(db, roles.Doctor,
                permissions.PatientsView,
                permissions.PatientsManage,
                permissions.ClinicalNotesManage,
                permissions.OrdersManage,
                permissions.CarePlansManage,
                permissions.VitalsManage,
                permissions.LabRequest,
                permissions.RadiologyRequest,
                permissions.PharmacyCreate,
                permissions.BillingView,
                permissions.AppointmentsView,
                permissions.RecordsView);

            await GrantRolePermissionsAsync(db, roles.Nurse,
                permissions.PatientsView,
                permissions.CarePlansManage,
                permissions.VitalsManage,
                permissions.MedicationsAdminister,
                permissions.NursingNotesManage,
                permissions.AppointmentsView,
                permissions.RecordsView);

            await GrantRolePermissionsAsync(db, roles.Pharmacist,
                permissions.PharmacyView,
                permissions.PharmacyCreate,
                permissions.PharmacyDispense,
                permissions.PharmacyManage,
                permissions.PharmacyInventoryManage,
                permissions.PharmacyDelete,
                permissions.InventoryView,
                permissions.InventoryManage,
                permissions.InventoryReceive,
                permissions.InventoryDispense,
                permissions.InventoryTransfer,
                permissions.InventoryCount,
                permissions.PharmacyDispenseOnCredit);

            await GrantRolePermissionsAsync(db, roles.LaboratoryStaff,
                permissions.LabRequest,
                permissions.LabProcess,
                permissions.LabView,
                permissions.LabManage,
                permissions.LabSpecimenManage,
                permissions.LabValidate,
                permissions.InventoryView,
                permissions.InventoryManage,
                permissions.InventoryDispense,
                permissions.LabChargeOnCredit);

            await GrantRolePermissionsAsync(db, roles.RadiologyStaff,
                permissions.RadiologyRequest,
                permissions.RadiologySchedule,
                permissions.RadiologyProcess,
                permissions.RadiologyView,
                permissions.PatientsView,
                permissions.AppointmentsView,
                permissions.RecordsView);

            await GrantRolePermissionsAsync(db, roles.Receptionist,
                permissions.PatientsView,
                permissions.PatientsManage,
                permissions.AppointmentsManage,
                permissions.AppointmentsView,
                permissions.CheckInManage,
                permissions.DemographicsManage);

            await GrantRolePermissionsAsync(db, roles.BillingAccountsOfficer,
                permissions.BillingCreate,
                permissions.BillingView,
                permissions.BillingApply,
                permissions.BillingExport,
                permissions.PaymentsCreate,
                permissions.PaymentsView,
                permissions.InsuranceView,
                permissions.ClaimsView,
                permissions.RefundsManage);

            await GrantRolePermissionsAsync(db, roles.InsuranceClaimsOfficer,
                permissions.InsuranceView,
                permissions.InsuranceManage,
                permissions.ClaimsManage,
                permissions.ClaimsView,
                permissions.PreauthorizationManage,
                permissions.BillingView,
                permissions.RefundsManage);

            await GrantRolePermissionsAsync(db, roles.Cashier,
                permissions.PaymentsCreate,
                permissions.PaymentsView,
                permissions.BillingView,
                permissions.BillingApply);

            await GrantRolePermissionsAsync(db, roles.MedicalRecordsOfficer,
                permissions.RecordsView,
                permissions.RecordsManage,
                permissions.DocumentsManage,
                permissions.CodingManage,
                permissions.CorrectionsManage,
                permissions.PatientsView,
                permissions.ReportsProfilesView,
                permissions.AuditView);

            await GrantRolePermissionsAsync(db, roles.HrStaffManager,
                permissions.StaffView,
                permissions.StaffManage,
                permissions.ContractsManage,
                permissions.AttendanceManage,
                permissions.LeaveManage);

            await GrantRolePermissionsAsync(db, roles.ProcurementOfficer,
                permissions.ProcurementManage,
                permissions.PurchaseOrdersManage,
                permissions.SuppliersManage,
                permissions.InventoryReceive,
                permissions.InventoryView);

            await GrantRolePermissionsAsync(db, roles.InventoryStoreManager,
                permissions.InventoryView,
                permissions.InventoryManage,
                permissions.InventoryReceive,
                permissions.InventoryDispense,
                permissions.InventoryTransfer,
                permissions.InventoryCount,
                permissions.ExpiryManage,
                permissions.StockCountManage,
                permissions.StockTransferManage,
                permissions.ProcurementManage);

            await GrantRolePermissionsAsync(db, roles.FinanceManager,
                permissions.FinanceView,
                permissions.FinanceManage,
                permissions.LedgerManage,
                permissions.ExpensesManage,
                permissions.ReconciliationManage,
                permissions.BillingExport,
                permissions.PaymentsView,
                permissions.AuditView,
                permissions.ReportsPatientsView,
                permissions.ReportsProfilesView);

            await GrantRolePermissionsAsync(db, roles.DepartmentManager,
                permissions.DepartmentApprovalsManage,
                permissions.DepartmentStaffView,
                permissions.DepartmentReportsView,
                permissions.PatientsView,
                permissions.AppointmentsView,
                permissions.AuditView);

            await GrantRolePermissionsAsync(db, roles.HospitalOperationsManager,
                permissions.WardsView,
                permissions.WardsManage,
                permissions.BedsView,
                permissions.BedsManage,
                permissions.TheatreManage,
                permissions.SchedulingManage,
                permissions.OperationsKpiView,
                permissions.PatientsView,
                permissions.InventoryView);

            await GrantRolePermissionsAsync(db, roles.Auditor,
                permissions.AuditView,
                permissions.ReportsPatientsView,
                permissions.ReportsProfilesView,
                permissions.BillingView,
                permissions.PaymentsView,
                permissions.FinanceView,
                permissions.RecordsView,
                permissions.InventoryView);

            await GrantRolePermissionsAsync(db, roles.User,
                permissions.PatientPortalView,
                permissions.PatientPortalAppointments,
                permissions.PatientPortalBilling,
                permissions.PatientPortalPrescriptions,
                permissions.PatientPortalResults,
                permissions.PatientPortalRecords,
                permissions.AppointmentsView,
                permissions.BillingView,
                permissions.RecordsView);

            await db.SaveChangesAsync();

            if (!isFreshDatabase)
            {
                return;
            }

            var admin = new User
            {
                Username = "admin",
                Email = "admin@localhost",
                PasswordHash = hasher.Hash("Admin@12345")
            };

            db.Users.Add(admin);
            await db.SaveChangesAsync();

            await GrantUserRolesAsync(db, admin, roles.SystemAdministrator, roles.HospitalAdministrator);

            var user = new User
            {
                Username = "user",
                Email = "user@localhost",
                PasswordHash = hasher.Hash("User@12345")
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            await GrantUserRolesAsync(db, user, roles.User);

            if (!await db.Set<HMS.API.Domain.Common.Tenant>().AnyAsync())
            {
                var central = new HMS.API.Domain.Common.Tenant { Name = "Central HMS", Code = "CENTRAL", IsCentral = true };
                db.Set<HMS.API.Domain.Common.Tenant>().Add(central);
                await db.SaveChangesAsync();

                var hospA = new HMS.API.Domain.Common.Tenant { Name = "St Mary Hospital", Code = "SMH", IsCentral = false };
                var hospB = new HMS.API.Domain.Common.Tenant { Name = "Green Valley Clinic", Code = "GVC", IsCentral = false };
                db.Set<HMS.API.Domain.Common.Tenant>().AddRange(hospA, hospB);
                await db.SaveChangesAsync();

                db.Set<HMS.API.Domain.Common.TenantDomain>().Add(new HMS.API.Domain.Common.TenantDomain { TenantId = hospA.Id, Domain = "smh.localtest.me", IsPrimary = true });
                db.Set<HMS.API.Domain.Common.TenantDomain>().Add(new HMS.API.Domain.Common.TenantDomain { TenantId = hospB.Id, Domain = "gvc.localtest.me", IsPrimary = true });
                await db.SaveChangesAsync();

                var adminA = new User { Username = "smh_admin", Email = "admin@smh.local", PasswordHash = hasher.Hash("SmhAdmin@123"), TenantId = hospA.Id };
                var adminB = new User { Username = "gvc_admin", Email = "admin@gvc.local", PasswordHash = hasher.Hash("GvcAdmin@123"), TenantId = hospB.Id };
                db.Users.AddRange(adminA, adminB);
                await db.SaveChangesAsync();

                await GrantUserRolesAsync(db, adminA, roles.HospitalAdministrator);
                await GrantUserRolesAsync(db, adminB, roles.HospitalAdministrator);

                var subA = new HMS.API.Domain.Common.TenantSubscription { TenantId = hospA.Id, Plan = "pro", Status = HMS.API.Domain.Common.SubscriptionStatus.Active, StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddYears(1) };
                var subB = new HMS.API.Domain.Common.TenantSubscription { TenantId = hospB.Id, Plan = "basic", Status = HMS.API.Domain.Common.SubscriptionStatus.Trial, StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddMonths(1) };
                db.Set<HMS.API.Domain.Common.TenantSubscription>().AddRange(subA, subB);
                await db.SaveChangesAsync();
            }
        }

        // Ensure some critical app settings exist so the UI and runtime can resolve deployment mode reliably
        private static async Task EnsureAppSettingsAsync(AuthDbContext db)
        {
            // Add System:DeploymentMode if missing so health endpoints and UI can read an authoritative value
            var key = "System:DeploymentMode";
            var existing = await db.AppSettings.SingleOrDefaultAsync(x => x.Key == key);
            if (existing == null)
            {
                db.AppSettings.Add(new HMS.API.Domain.Common.AppSetting { Key = key, Value = "Bootstrap" });
            }

            // Add a default PlatformContext:Hosts entry if missing (used for host-based tenant resolution)
            var hostsKey = "PlatformContext:Hosts";
            var hostsExisting = await db.AppSettings.SingleOrDefaultAsync(x => x.Key == hostsKey);
            if (hostsExisting == null)
            {
                // default empty array (JSON) - UI/tenant middleware will ignore when empty
                db.AppSettings.Add(new HMS.API.Domain.Common.AppSetting { Key = hostsKey, Value = "[]" });
            }

            await db.SaveChangesAsync();
        }

        public static async Task EnsurePermissionCatalogAsync(AuthDbContext db)
        {
            var permissions = await EnsureBuiltInPermissionsAsync(db);
            var roles = await EnsureBuiltInRolesAsync(db);

            await GrantRolePermissionsAsync(db, roles.SystemAdministrator,
                permissions.ProfileRead,
                permissions.ProfileUpdate,
                permissions.ProfileManage,
                permissions.UsersManage,
                permissions.RolesManage,
                permissions.AuthLogin,
                permissions.IntegrationsManage,
                permissions.SecuritySettingsManage,
                permissions.TenantsManage,
                permissions.ApprovalsManage,
                permissions.PatientsManage,
                permissions.PatientsView,
                permissions.ClinicalNotesManage,
                permissions.OrdersManage,
                permissions.CarePlansManage,
                permissions.VitalsManage,
                permissions.MedicationsAdminister,
                permissions.NursingNotesManage,
                permissions.AppointmentsManage,
                permissions.AppointmentsView,
                permissions.CheckInManage,
                permissions.DemographicsManage,
                permissions.BillingCreate,
                permissions.BillingView,
                permissions.BillingApply,
                permissions.BillingExport,
                permissions.PaymentsCreate,
                permissions.PaymentsView,
                permissions.InsuranceView,
                permissions.InsuranceManage,
                permissions.ClaimsManage,
                permissions.ClaimsView,
                permissions.PreauthorizationManage,
                permissions.RefundsManage,
                permissions.LabRequest,
                permissions.LabProcess,
                permissions.LabView,
                permissions.LabManage,
                permissions.LabSpecimenManage,
                permissions.LabValidate,
                permissions.RadiologyRequest,
                permissions.RadiologySchedule,
                permissions.RadiologyProcess,
                permissions.RadiologyView,
                permissions.PharmacyView,
                permissions.PharmacyManage,
                permissions.PharmacyCreate,
                permissions.PharmacyDispense,
                permissions.PharmacyInventoryManage,
                permissions.PharmacyDelete,
                permissions.InventoryView,
                permissions.InventoryManage,
                permissions.InventoryReceive,
                permissions.InventoryDispense,
                permissions.InventoryTransfer,
                permissions.InventoryCount,
                permissions.RecordsView,
                permissions.RecordsManage,
                permissions.DocumentsManage,
                permissions.CodingManage,
                permissions.CorrectionsManage,
                permissions.StaffView,
                permissions.StaffManage,
                permissions.ContractsManage,
                permissions.AttendanceManage,
                permissions.LeaveManage,
                permissions.ProcurementManage,
                permissions.PurchaseOrdersManage,
                permissions.SuppliersManage,
                permissions.FinanceView,
                permissions.FinanceManage,
                permissions.LedgerManage,
                permissions.ExpensesManage,
                permissions.ReconciliationManage,
                permissions.DepartmentApprovalsManage,
                permissions.DepartmentStaffView,
                permissions.DepartmentReportsView,
                permissions.WardsView,
                permissions.WardsManage,
                permissions.BedsView,
                permissions.BedsManage,
                permissions.TheatreManage,
                permissions.SchedulingManage,
                permissions.OperationsKpiView,
                permissions.AuditView,
                permissions.PatientPortalView,
                permissions.PatientPortalAppointments,
                permissions.PatientPortalBilling,
                permissions.PatientPortalPrescriptions,
                permissions.PatientPortalResults,
                permissions.PatientPortalRecords,
                permissions.ReportsPatientsView,
                permissions.ReportsProfilesView,
                permissions.AdminDashboardView,
                permissions.SystemPermissionsManage,
                permissions.SystemMaintenanceManage,
                permissions.LabChargeOnCredit,
                permissions.PharmacyDispenseOnCredit,
                permissions.ExpiryManage,
                permissions.StockCountManage,
                permissions.StockTransferManage);

            await db.SaveChangesAsync();
        }

        private static async Task<(Permission ProfileRead, Permission ProfileUpdate, Permission ProfileManage, Permission UsersManage, Permission RolesManage, Permission AuthLogin, Permission IntegrationsManage, Permission SecuritySettingsManage, Permission TenantsManage, Permission ApprovalsManage, Permission PatientsManage, Permission PatientsView, Permission ClinicalNotesManage, Permission OrdersManage, Permission CarePlansManage, Permission VitalsManage, Permission MedicationsAdminister, Permission NursingNotesManage, Permission AppointmentsManage, Permission AppointmentsView, Permission CheckInManage, Permission DemographicsManage, Permission BillingCreate, Permission BillingView, Permission BillingApply, Permission BillingExport, Permission PaymentsCreate, Permission PaymentsView, Permission InsuranceView, Permission InsuranceManage, Permission ClaimsManage, Permission ClaimsView, Permission PreauthorizationManage, Permission RefundsManage, Permission LabRequest, Permission LabProcess, Permission LabView, Permission LabManage, Permission LabSpecimenManage, Permission LabValidate, Permission RadiologyRequest, Permission RadiologySchedule, Permission RadiologyProcess, Permission RadiologyView, Permission PharmacyView, Permission PharmacyManage, Permission PharmacyCreate, Permission PharmacyDispense, Permission PharmacyInventoryManage, Permission PharmacyDelete, Permission InventoryView, Permission InventoryManage, Permission InventoryReceive, Permission InventoryDispense, Permission InventoryTransfer, Permission InventoryCount, Permission RecordsView, Permission RecordsManage, Permission DocumentsManage, Permission CodingManage, Permission CorrectionsManage, Permission StaffView, Permission StaffManage, Permission ContractsManage, Permission AttendanceManage, Permission LeaveManage, Permission ProcurementManage, Permission PurchaseOrdersManage, Permission SuppliersManage, Permission FinanceView, Permission FinanceManage, Permission LedgerManage, Permission ExpensesManage, Permission ReconciliationManage, Permission DepartmentApprovalsManage, Permission DepartmentStaffView, Permission DepartmentReportsView, Permission WardsView, Permission WardsManage, Permission BedsView, Permission BedsManage, Permission TheatreManage, Permission SchedulingManage, Permission OperationsKpiView, Permission AuditView, Permission PatientPortalView, Permission PatientPortalAppointments, Permission PatientPortalBilling, Permission PatientPortalPrescriptions, Permission PatientPortalResults, Permission PatientPortalRecords, Permission ReportsPatientsView, Permission ReportsProfilesView, Permission AdminDashboardView, Permission SystemPermissionsManage, Permission SystemMaintenanceManage, Permission LabChargeOnCredit, Permission PharmacyDispenseOnCredit, Permission ExpiryManage, Permission StockCountManage, Permission StockTransferManage)> EnsureBuiltInPermissionsAsync(AuthDbContext db)
        {
            var profileRead = await EnsurePermissionAsync(db, "PROFILE.READ", "Read user profiles");
            var profileUpdate = await EnsurePermissionAsync(db, "PROFILE.UPDATE", "Update user profiles");
            var profileManage = await EnsurePermissionAsync(db, "PROFILE.MANAGE", "Manage user profiles");
            var usersManage = await EnsurePermissionAsync(db, "users.manage", "Manage users");
            var rolesManage = await EnsurePermissionAsync(db, "roles.manage", "Manage roles");
            var authLogin = await EnsurePermissionAsync(db, "auth.login", "Login");
            var integrationsManage = await EnsurePermissionAsync(db, "integrations.manage", "Manage integrations");
            var securitySettingsManage = await EnsurePermissionAsync(db, "security.settings.manage", "Manage security settings");
            var tenantsManage = await EnsurePermissionAsync(db, "tenants.manage", "Manage tenants");
            var approvalsManage = await EnsurePermissionAsync(db, "approvals.manage", "Manage approvals");
            var patientsManage = await EnsurePermissionAsync(db, "patients.manage", "Manage patients");
            var patientsView = await EnsurePermissionAsync(db, "patients.view", "View patients");
            var clinicalNotesManage = await EnsurePermissionAsync(db, "clinical.notes.manage", "Manage clinical notes");
            var ordersManage = await EnsurePermissionAsync(db, "orders.manage", "Manage orders");
            var carePlansManage = await EnsurePermissionAsync(db, "careplans.manage", "Manage care plans");
            var vitalsManage = await EnsurePermissionAsync(db, "vitals.manage", "Manage vitals");
            var medicationsAdminister = await EnsurePermissionAsync(db, "medications.administer", "Administer medications");
            var nursingNotesManage = await EnsurePermissionAsync(db, "nursing.notes.manage", "Manage nursing notes");
            var appointmentsManage = await EnsurePermissionAsync(db, "appointments.manage", "Manage appointments");
            var appointmentsView = await EnsurePermissionAsync(db, "appointments.view", "View appointments");
            var checkInManage = await EnsurePermissionAsync(db, "checkin.manage", "Manage check-in and check-out");
            var demographicsManage = await EnsurePermissionAsync(db, "demographics.manage", "Manage demographics");
            var billingCreate = await EnsurePermissionAsync(db, "billing.create", "Create invoices");
            var billingView = await EnsurePermissionAsync(db, "billing.view", "View invoices");
            var billingApply = await EnsurePermissionAsync(db, "billing.applypayment", "Apply payments to invoices");
            var billingExport = await EnsurePermissionAsync(db, "billing.export", "Export invoices");
            var paymentsCreate = await EnsurePermissionAsync(db, "payments.create", "Create payments");
            var paymentsView = await EnsurePermissionAsync(db, "payments.view", "View payments");
            var insuranceView = await EnsurePermissionAsync(db, "insurance.view", "View insurance");
            var insuranceManage = await EnsurePermissionAsync(db, "insurance.manage", "Manage insurance");
            var claimsManage = await EnsurePermissionAsync(db, "claims.manage", "Manage claims");
            var claimsView = await EnsurePermissionAsync(db, "claims.view", "View claims");
            var preauthorizationManage = await EnsurePermissionAsync(db, "preauthorization.manage", "Manage preauthorizations");
            var refundsManage = await EnsurePermissionAsync(db, "refunds.manage", "Manage refunds");
            var labRequest = await EnsurePermissionAsync(db, "lab.request", "Create lab requests");
            var labProcess = await EnsurePermissionAsync(db, "lab.process", "Process lab requests");
            var labView = await EnsurePermissionAsync(db, "lab.view", "View lab tests and requests");
            var labManage = await EnsurePermissionAsync(db, "lab.manage", "Manage lab test catalog");
            var labSpecimenManage = await EnsurePermissionAsync(db, "lab.specimen.manage", "Manage laboratory specimens");
            var labValidate = await EnsurePermissionAsync(db, "lab.validate", "Validate lab results");
            var radiologyRequest = await EnsurePermissionAsync(db, "radiology.request", "Create radiology requests");
            var radiologySchedule = await EnsurePermissionAsync(db, "radiology.schedule", "Schedule radiology studies");
            var radiologyProcess = await EnsurePermissionAsync(db, "radiology.process", "Process radiology studies");
            var radiologyView = await EnsurePermissionAsync(db, "radiology.view", "View radiology studies and results");
            var pharmView = await EnsurePermissionAsync(db, "pharmacy.view", "View drugs and prescriptions");
            var pharmManage = await EnsurePermissionAsync(db, "pharmacy.manage", "Manage drug catalog");
            var pharmInventory = await EnsurePermissionAsync(db, "pharmacy.inventory.manage", "Manage pharmacy inventory");
            var pharmDelete = await EnsurePermissionAsync(db, "pharmacy.delete", "Delete pharmacy items (soft delete)");
            var pharmCreate = await EnsurePermissionAsync(db, "pharmacy.create", "Create prescriptions");
            var pharmDispense = await EnsurePermissionAsync(db, "pharmacy.dispense", "Dispense medications");
            var inventoryView = await EnsurePermissionAsync(db, "inventory.view", "View inventory and stock across stores");
            var inventoryManage = await EnsurePermissionAsync(db, "inventory.manage", "Manage inventory (receive/adjust)");
            var inventoryReceive = await EnsurePermissionAsync(db, "inventory.receive", "Receive purchase orders and create batches");
            var inventoryDispense = await EnsurePermissionAsync(db, "inventory.dispense", "Dispense items from inventory (department scoped)");
            var inventoryTransfer = await EnsurePermissionAsync(db, "inventory.transfer", "Transfer inventory between stores");
            var inventoryCount = await EnsurePermissionAsync(db, "inventory.count", "Count inventory and stock take");
            var recordsView = await EnsurePermissionAsync(db, "records.view", "View medical records");
            var recordsManage = await EnsurePermissionAsync(db, "records.manage", "Manage medical records");
            var documentsManage = await EnsurePermissionAsync(db, "documents.manage", "Manage documents");
            var codingManage = await EnsurePermissionAsync(db, "coding.manage", "Manage coding");
            var correctionsManage = await EnsurePermissionAsync(db, "corrections.manage", "Manage corrections");
            var staffView = await EnsurePermissionAsync(db, "staff.view", "View staff records");
            var staffManage = await EnsurePermissionAsync(db, "staff.manage", "Manage staff records");
            var contractsManage = await EnsurePermissionAsync(db, "contracts.manage", "Manage contracts");
            var attendanceManage = await EnsurePermissionAsync(db, "attendance.manage", "Manage attendance");
            var leaveManage = await EnsurePermissionAsync(db, "leave.manage", "Manage leave");
            var procurementManage = await EnsurePermissionAsync(db, "procurement.manage", "Manage procurement");
            var purchaseOrdersManage = await EnsurePermissionAsync(db, "purchase.orders.manage", "Manage purchase orders");
            var suppliersManage = await EnsurePermissionAsync(db, "suppliers.manage", "Manage suppliers");
            var financeView = await EnsurePermissionAsync(db, "finance.view", "View finance");
            var financeManage = await EnsurePermissionAsync(db, "finance.manage", "Manage finance");
            var ledgerManage = await EnsurePermissionAsync(db, "ledger.manage", "Manage ledger");
            var expensesManage = await EnsurePermissionAsync(db, "expenses.manage", "Manage expenses");
            var reconciliationManage = await EnsurePermissionAsync(db, "reconciliation.manage", "Manage reconciliation");
            var departmentApprovalsManage = await EnsurePermissionAsync(db, "department.approvals.manage", "Manage department approvals");
            var departmentStaffView = await EnsurePermissionAsync(db, "department.staff.view", "View department staff");
            var departmentReportsView = await EnsurePermissionAsync(db, "department.reports.view", "View department reports");
            var wardsView = await EnsurePermissionAsync(db, "wards.view", "View wards");
            var wardsManage = await EnsurePermissionAsync(db, "wards.manage", "Manage wards");
            var bedsView = await EnsurePermissionAsync(db, "beds.view", "View beds");
            var bedsManage = await EnsurePermissionAsync(db, "beds.manage", "Manage beds");
            var theatreManage = await EnsurePermissionAsync(db, "theatre.manage", "Manage theatre");
            var schedulingManage = await EnsurePermissionAsync(db, "scheduling.manage", "Manage scheduling");
            var operationsKpiView = await EnsurePermissionAsync(db, "operations.kpi.view", "View operations KPIs");
            var auditView = await EnsurePermissionAsync(db, "audit.view", "View audit trails");
            var patientPortalView = await EnsurePermissionAsync(db, "patient.portal.view", "View patient portal");
            var patientPortalAppointments = await EnsurePermissionAsync(db, "patient.portal.appointments", "View patient portal appointments");
            var patientPortalBilling = await EnsurePermissionAsync(db, "patient.portal.billing", "View patient portal billing");
            var patientPortalPrescriptions = await EnsurePermissionAsync(db, "patient.portal.prescriptions", "View patient portal prescriptions");
            var patientPortalResults = await EnsurePermissionAsync(db, "patient.portal.results", "View patient portal results");
            var patientPortalRecords = await EnsurePermissionAsync(db, "patient.portal.records", "View patient portal records");
            var reportsPatientsView = await EnsurePermissionAsync(db, "reports.patients.view", "View patient reports");
            var reportsProfilesView = await EnsurePermissionAsync(db, "reports.profiles.view", "View profile reports");
            var adminDashboardView = await EnsurePermissionAsync(db, "ADMIN.DASHBOARD.VIEW", "View admin dashboard");
            var systemPermissionsManage = await EnsurePermissionAsync(db, "system.permissions.manage", "Manage the permission catalog");
            var systemMaintenanceManage = await EnsurePermissionAsync(db, "system.maintenance.manage", "Run system maintenance and reseed tasks");
            var labChargeOnCredit = await EnsurePermissionAsync(db, "lab.charge.credit", "Allow charging lab items on credit");
            var pharmDispenseOnCredit = await EnsurePermissionAsync(db, "pharmacy.dispense.credit", "Allow dispensing pharmacy items on credit");
            var expiryManage = await EnsurePermissionAsync(db, "expiry.manage", "Manage expiry tracking");
            var stockCountManage = await EnsurePermissionAsync(db, "stock.count.manage", "Manage stock counts");
            var stockTransferManage = await EnsurePermissionAsync(db, "stock.transfer.manage", "Manage stock transfers");

            return (profileRead, profileUpdate, profileManage, usersManage, rolesManage, authLogin, integrationsManage, securitySettingsManage, tenantsManage, approvalsManage, patientsManage, patientsView, clinicalNotesManage, ordersManage, carePlansManage, vitalsManage, medicationsAdminister, nursingNotesManage, appointmentsManage, appointmentsView, checkInManage, demographicsManage, billingCreate, billingView, billingApply, billingExport, paymentsCreate, paymentsView, insuranceView, insuranceManage, claimsManage, claimsView, preauthorizationManage, refundsManage, labRequest, labProcess, labView, labManage, labSpecimenManage, labValidate, radiologyRequest, radiologySchedule, radiologyProcess, radiologyView, pharmView, pharmManage, pharmCreate, pharmDispense, pharmInventory, pharmDelete, inventoryView, inventoryManage, inventoryReceive, inventoryDispense, inventoryTransfer, inventoryCount, recordsView, recordsManage, documentsManage, codingManage, correctionsManage, staffView, staffManage, contractsManage, attendanceManage, leaveManage, procurementManage, purchaseOrdersManage, suppliersManage, financeView, financeManage, ledgerManage, expensesManage, reconciliationManage, departmentApprovalsManage, departmentStaffView, departmentReportsView, wardsView, wardsManage, bedsView, bedsManage, theatreManage, schedulingManage, operationsKpiView, auditView, patientPortalView, patientPortalAppointments, patientPortalBilling, patientPortalPrescriptions, patientPortalResults, patientPortalRecords, reportsPatientsView, reportsProfilesView, adminDashboardView, systemPermissionsManage, systemMaintenanceManage, labChargeOnCredit, pharmDispenseOnCredit, expiryManage, stockCountManage, stockTransferManage);
        }

        private static async Task<BuiltInRoles> EnsureBuiltInRolesAsync(AuthDbContext db)
        {
            var userRole = await EnsureRoleAsync(db, RoleCatalog.PatientPortalUser, "Patient portal user");
            var systemAdministratorRole = await EnsureRoleAsync(db, RoleCatalog.SystemAdministrator, "System administrator");
            var hospitalAdministratorRole = await EnsureRoleAsync(db, RoleCatalog.HospitalAdministrator, "Hospital administrator / super admin");
            var doctorRole = await EnsureRoleAsync(db, RoleCatalog.Doctor, "Doctor / physician");
            var nurseRole = await EnsureRoleAsync(db, RoleCatalog.Nurse, "Nurse");
            var pharmacistRole = await EnsureRoleAsync(db, RoleCatalog.Pharmacist, "Pharmacist");
            var laboratoryRole = await EnsureRoleAsync(db, RoleCatalog.LaboratoryStaff, "Laboratory staff");
            var radiologyRole = await EnsureRoleAsync(db, RoleCatalog.RadiologyStaff, "Radiology staff");
            var receptionistRole = await EnsureRoleAsync(db, RoleCatalog.Receptionist, "Receptionist / front desk");
            var billingRole = await EnsureRoleAsync(db, RoleCatalog.BillingAccountsOfficer, "Billing / accounts officer");
            var insuranceRole = await EnsureRoleAsync(db, RoleCatalog.InsuranceClaimsOfficer, "Insurance / claims officer");
            var cashierRole = await EnsureRoleAsync(db, RoleCatalog.Cashier, "Cashier");
            var recordsRole = await EnsureRoleAsync(db, RoleCatalog.MedicalRecordsOfficer, "Medical records / HIM officer");
            var hrRole = await EnsureRoleAsync(db, RoleCatalog.HrStaffManager, "HR / staff manager");
            var procurementRole = await EnsureRoleAsync(db, RoleCatalog.ProcurementOfficer, "Procurement officer");
            var inventoryRole = await EnsureRoleAsync(db, RoleCatalog.InventoryStoreManager, "Inventory / store manager");
            var financeRole = await EnsureRoleAsync(db, RoleCatalog.FinanceManager, "Finance manager / accountant");
            var departmentRole = await EnsureRoleAsync(db, RoleCatalog.DepartmentManager, "Department manager / head of department");
            var operationsRole = await EnsureRoleAsync(db, RoleCatalog.HospitalOperationsManager, "Hospital operations manager");
            var auditorRole = await EnsureRoleAsync(db, RoleCatalog.Auditor, "Auditor / compliance officer");

            return new BuiltInRoles(
                userRole,
                systemAdministratorRole,
                hospitalAdministratorRole,
                doctorRole,
                nurseRole,
                pharmacistRole,
                laboratoryRole,
                radiologyRole,
                receptionistRole,
                billingRole,
                insuranceRole,
                cashierRole,
                recordsRole,
                hrRole,
                procurementRole,
                inventoryRole,
                financeRole,
                departmentRole,
                operationsRole,
                auditorRole);
        }

        private static async Task GrantRolePermissionsAsync(AuthDbContext db, Role? role, params Permission[] permissions)
        {
            if (role == null)
            {
                return;
            }

            foreach (var permission in permissions)
            {
                await EnsureRolePermissionAsync(db, role.Id, permission.Id);
            }
        }

        private static async Task GrantUserRolesAsync(AuthDbContext db, User user, params Role[] roles)
        {
            foreach (var role in roles)
            {
                var exists = await db.UserRoles.IgnoreQueryFilters().AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id);
                if (!exists)
                {
                    db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
                }
            }

            await db.SaveChangesAsync();
        }

        private sealed record BuiltInRoles(
            Role User,
            Role SystemAdministrator,
            Role HospitalAdministrator,
            Role Doctor,
            Role Nurse,
            Role Pharmacist,
            Role LaboratoryStaff,
            Role RadiologyStaff,
            Role Receptionist,
            Role BillingAccountsOfficer,
            Role InsuranceClaimsOfficer,
            Role Cashier,
            Role MedicalRecordsOfficer,
            Role HrStaffManager,
            Role ProcurementOfficer,
            Role InventoryStoreManager,
            Role FinanceManager,
            Role DepartmentManager,
            Role HospitalOperationsManager,
            Role Auditor);

        private static async Task<Role> EnsureRoleAsync(AuthDbContext db, string name, string description)
        {
            var role = await db.Roles.IgnoreQueryFilters().SingleOrDefaultAsync(r => !r.IsDeleted && r.Name == name);
            if (role != null)
            {
                return role;
            }

            role = new Role { Name = name, Description = description };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            return role;
        }

        private static async Task<Permission> EnsurePermissionAsync(AuthDbContext db, string code, string description)
        {
            var permission = await db.Permissions.IgnoreQueryFilters().SingleOrDefaultAsync(p => !p.IsDeleted && p.Code == code);
            if (permission != null)
            {
                return permission;
            }

            permission = new Permission { Code = code, Description = description };
            db.Permissions.Add(permission);
            await db.SaveChangesAsync();
            return permission;
        }

        private static async Task EnsureRolePermissionAsync(AuthDbContext db, Guid roleId, Guid permissionId)
        {
            var exists = await db.RolePermissions.IgnoreQueryFilters().AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
            if (!exists)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
            }
        }
    }
}


