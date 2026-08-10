using System;
using System.Collections.Generic;
using System.Linq;

namespace HMS.API.Infrastructure.Auth
{
    public static class RoleCatalog
    {
        public const string SystemAdministrator = "System Administrator";
        public const string HospitalAdministrator = "Hospital Administrator / Super Admin";
        public const string Doctor = "Doctor / Physician";
        public const string Nurse = "Nurse";
        public const string Pharmacist = "Pharmacist";
        public const string LaboratoryStaff = "Laboratory Staff";
        public const string RadiologyStaff = "Radiology Staff";
        public const string Receptionist = "Receptionist / Front Desk";
        public const string BillingAccountsOfficer = "Billing / Accounts Officer";
        public const string InsuranceClaimsOfficer = "Insurance / Claims Officer";
        public const string Cashier = "Cashier";
        public const string MedicalRecordsOfficer = "Medical Records / HIM Officer";
        public const string HrStaffManager = "HR / Staff Manager";
        public const string ProcurementOfficer = "Procurement Officer";
        public const string InventoryStoreManager = "Inventory / Store Manager";
        public const string FinanceManager = "Finance Manager / Accountant";
        public const string DepartmentManager = "Department Manager / Head of Department";
        public const string HospitalOperationsManager = "Hospital Operations Manager";
        public const string Auditor = "Auditor / Compliance Officer";
        public const string PatientPortalUser = "Patient Portal User";

        public static readonly string[] CoreRoleNames =
        {
            SystemAdministrator,
            HospitalAdministrator,
            Doctor,
            Nurse,
            Pharmacist,
            LaboratoryStaff,
            RadiologyStaff,
            Receptionist,
            BillingAccountsOfficer,
            InsuranceClaimsOfficer,
            Cashier,
            MedicalRecordsOfficer,
            HrStaffManager,
            ProcurementOfficer,
            InventoryStoreManager,
            FinanceManager,
            DepartmentManager,
            HospitalOperationsManager,
            Auditor,
            PatientPortalUser
        };

        public static readonly string[] LegacyRoleAliases =
        {
            "Admin",
            "User",
            "Doctor",
            "Cashier",
            "LabTech",
            "Pharmacist"
        };

        public static readonly string[] ReservedRoleNames = CoreRoleNames
            .Concat(LegacyRoleAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        private static readonly HashSet<string> ReservedRoleNameSet = new(ReservedRoleNames, StringComparer.OrdinalIgnoreCase);

        private const string SystemAdministratorKey = "systemadministrator";
        private const string HospitalAdministratorKey = "hospitaladministrator";
        private const string DoctorKey = "doctor";
        private const string NurseKey = "nurse";
        private const string PharmacistKey = "pharmacist";
        private const string LaboratoryKey = "laboratory";
        private const string RadiologyKey = "radiology";
        private const string ReceptionistKey = "receptionist";
        private const string BillingKey = "billing";
        private const string InsuranceKey = "insurance";
        private const string CashierKey = "cashier";
        private const string MedicalRecordsKey = "medicalrecords";
        private const string HrKey = "hr";
        private const string ProcurementKey = "procurement";
        private const string InventoryKey = "inventory";
        private const string FinanceKey = "finance";
        private const string DepartmentManagerKey = "departmentmanager";
        private const string OperationsKey = "hospitaloperations";
        private const string AuditorKey = "auditor";
        private const string PatientPortalKey = "patientportal";

        private static readonly Dictionary<string, string> RoleKeyMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["systemadministrator"] = SystemAdministratorKey,
            ["admin"] = SystemAdministratorKey,
            ["hospitaladministrator"] = HospitalAdministratorKey,
            ["hospitaladministratorsuperadmin"] = HospitalAdministratorKey,
            ["superadmin"] = HospitalAdministratorKey,
            ["hospitaladmin"] = HospitalAdministratorKey,
            ["doctor"] = DoctorKey,
            ["physician"] = DoctorKey,
            ["doctorphysician"] = DoctorKey,
            ["nurse"] = NurseKey,
            ["pharmacist"] = PharmacistKey,
            ["laboratorystaff"] = LaboratoryKey,
            ["labtech"] = LaboratoryKey,
            ["labtechnician"] = LaboratoryKey,
            ["lab"] = LaboratoryKey,
            ["radiologystaff"] = RadiologyKey,
            ["radiology"] = RadiologyKey,
            ["receptionist"] = ReceptionistKey,
            ["frontdesk"] = ReceptionistKey,
            ["billingaccountsofficer"] = BillingKey,
            ["billing"] = BillingKey,
            ["accountsofficer"] = BillingKey,
            ["insuranceclaimsofficer"] = InsuranceKey,
            ["insurance"] = InsuranceKey,
            ["claims"] = InsuranceKey,
            ["claimsofficer"] = InsuranceKey,
            ["cashier"] = CashierKey,
            ["medicalrecordshimofficer"] = MedicalRecordsKey,
            ["medicalrecords"] = MedicalRecordsKey,
            ["him"] = MedicalRecordsKey,
            ["himofficer"] = MedicalRecordsKey,
            ["records"] = MedicalRecordsKey,
            ["hrstaffmanager"] = HrKey,
            ["hr"] = HrKey,
            ["staffmanager"] = HrKey,
            ["procurementofficer"] = ProcurementKey,
            ["procurement"] = ProcurementKey,
            ["inventorystoremanager"] = InventoryKey,
            ["inventory"] = InventoryKey,
            ["storemanager"] = InventoryKey,
            ["financemanageraccountant"] = FinanceKey,
            ["finance"] = FinanceKey,
            ["accountant"] = FinanceKey,
            ["departmentmanagerheadofdepartment"] = DepartmentManagerKey,
            ["departmentmanager"] = DepartmentManagerKey,
            ["headofdepartment"] = DepartmentManagerKey,
            ["hod"] = DepartmentManagerKey,
            ["hospitaloperationsmanager"] = OperationsKey,
            ["operationsmanager"] = OperationsKey,
            ["operations"] = OperationsKey,
            ["auditorcomplianceofficer"] = AuditorKey,
            ["auditor"] = AuditorKey,
            ["complianceofficer"] = AuditorKey,
            ["patientportaluser"] = PatientPortalKey,
            ["patientportal"] = PatientPortalKey,
            ["patient"] = PatientPortalKey,
            ["user"] = PatientPortalKey
        };

        private static readonly HashSet<string> ReservedRoleKeySet = new(
            CoreRoleNames.Select(GetRoleKey)
                .Concat(LegacyRoleAliases.Select(GetRoleKey))
                .Where(v => !string.IsNullOrWhiteSpace(v)),
            StringComparer.OrdinalIgnoreCase);

        public static bool IsCoreRoleName(string? roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            var value = roleName.Trim();
            return ReservedRoleNameSet.Contains(value) || ReservedRoleKeySet.Contains(GetRoleKey(value));
        }

        public static string GetRoleKey(string? roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return string.Empty;
            }

            var normalized = Normalize(roleName);
            if (RoleKeyMap.TryGetValue(normalized, out var key))
            {
                return key;
            }

            return normalized;
        }

        public static bool MatchesRole(string? roleName, params string[] roleKeys)
        {
            if (string.IsNullOrWhiteSpace(roleName) || roleKeys.Length == 0)
            {
                return false;
            }

            var actual = GetRoleKey(roleName);
            return roleKeys.Select(GetRoleKey).Contains(actual, StringComparer.OrdinalIgnoreCase);
        }

        public static bool HasAnyRole(IEnumerable<string> roles, params string[] roleKeys)
        {
            if (roles == null)
            {
                return false;
            }

            var target = roleKeys.Select(GetRoleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return roles.Any(role => target.Contains(GetRoleKey(role)));
        }

        public static bool IsSystemAdministratorRole(string? roleName) => MatchesRole(roleName, SystemAdministrator, "Admin");
        public static bool IsHospitalAdministratorRole(string? roleName) => MatchesRole(roleName, HospitalAdministrator, "Super Admin", "Hospital Admin");
        public static bool IsDoctorRole(string? roleName) => MatchesRole(roleName, Doctor, "Physician");
        public static bool IsNurseRole(string? roleName) => MatchesRole(roleName, Nurse);
        public static bool IsPharmacistRole(string? roleName) => MatchesRole(roleName, Pharmacist);
        public static bool IsLaboratoryRole(string? roleName) => MatchesRole(roleName, LaboratoryStaff, "LabTech", "Lab Technician", "Lab");
        public static bool IsRadiologyRole(string? roleName) => MatchesRole(roleName, RadiologyStaff, "Radiology");
        public static bool IsReceptionRole(string? roleName) => MatchesRole(roleName, Receptionist, "Front Desk");
        public static bool IsBillingRole(string? roleName) => MatchesRole(roleName, BillingAccountsOfficer, "Billing", "Accounts Officer");
        public static bool IsInsuranceRole(string? roleName) => MatchesRole(roleName, InsuranceClaimsOfficer, "Insurance", "Claims Officer");
        public static bool IsCashierRole(string? roleName) => MatchesRole(roleName, Cashier);
        public static bool IsMedicalRecordsRole(string? roleName) => MatchesRole(roleName, MedicalRecordsOfficer, "HIM", "Medical Records");
        public static bool IsHrRole(string? roleName) => MatchesRole(roleName, HrStaffManager, "Staff Manager", "HR");
        public static bool IsProcurementRole(string? roleName) => MatchesRole(roleName, ProcurementOfficer);
        public static bool IsInventoryRole(string? roleName) => MatchesRole(roleName, InventoryStoreManager, "Store Manager", "Inventory");
        public static bool IsFinanceRole(string? roleName) => MatchesRole(roleName, FinanceManager, "Accountant", "Finance");
        public static bool IsDepartmentManagerRole(string? roleName) => MatchesRole(roleName, DepartmentManager, "Head of Department", "HOD");
        public static bool IsOperationsRole(string? roleName) => MatchesRole(roleName, HospitalOperationsManager, "Operations Manager", "Operations");
        public static bool IsAuditorRole(string? roleName) => MatchesRole(roleName, Auditor, "Compliance Officer", "Auditor");
        public static bool IsPatientPortalRole(string? roleName) => MatchesRole(roleName, PatientPortalUser, "Patient Portal");

        private static string Normalize(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }
    }
}
