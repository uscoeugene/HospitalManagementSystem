using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HMS.UI.Models;
using HMS.UI.Models.Billing;
using HMS.UI.Models.Lab;
using HMS.UI.Models.Pharmacy;
using HMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers
{
    [Authorize]
    public class QueuesController : Controller
    {
        private readonly ApiClient _api;

        public QueuesController(ApiClient api)
        {
            _api = api;
        }

        public IActionResult Index()
        {
            var roles = GetRoles();
            var cards = BuildQueueHubCards(roles);
            var vm = new DashboardViewModel
            {
                DisplayName = GetDisplayName(),
                TenantName = GetTenantName(),
                Roles = roles,
                QueueCards = cards
            };

            return View(vm);
        }

        [HMS.UI.Security.HasPermission("lab.view")]
        public async Task<IActionResult> Lab(string? status = "PENDING", int page = 1, int pageSize = 10)
        {
            var normalizedStatus = NormalizeStatus(status, new[] { "PENDING", "PROCESSING", "READY", "COMPLETED" }, "PENDING");
            var pageResult = await _api.GetAsync<PagedResult<LabRequestViewModel>>($"/lab/requests?status={Uri.EscapeDataString(normalizedStatus)}&page={page}&pageSize={pageSize}")
                ?? new PagedResult<LabRequestViewModel> { Page = page, PageSize = pageSize };

            var items = (pageResult.Items ?? Array.Empty<LabRequestViewModel>())
                .Select(request => new QueueItemViewModel
                {
                    Title = request.RequestNumber,
                    Subtitle = request.PatientName ?? "Patient record",
                    Summary = $"{request.ItemsCount} test(s) • {FormatDate(request.CreatedAt)}",
                    Status = string.IsNullOrWhiteSpace(request.ResultsStatus) ? request.Status : request.ResultsStatus,
                    BadgeClass = MapLabBadge(request.Status),
                    LinkUrl = Url.Action("Details", "Lab", new { id = request.Id }),
                    LinkText = "Review",
                    Meta = string.IsNullOrWhiteSpace(request.InvoiceStatus) ? null : $"Invoice {request.InvoiceStatus}"
                })
                .ToArray();

            var vm = new QueuePageViewModel
            {
                Title = "Laboratory Queue",
                Description = "Review pending requests, validate results, and keep the lab moving.",
                Roles = new[] { "Laboratory Staff", "System Administrator", "Hospital Administrator / Super Admin" },
                FilterLabel = "Status",
                FilterValue = normalizedStatus,
                FilterOptions = new[] { "PENDING", "PROCESSING", "READY", "COMPLETED" },
                PrimaryActionUrl = Url.Action("Requests", "Lab"),
                PrimaryActionText = "Open Requests",
                EmptyMessage = "No lab requests are waiting right now.",
                ItemsPage = new PagedResult<QueueItemViewModel>
                {
                    Items = items,
                    Page = pageResult.Page,
                    PageSize = pageResult.PageSize,
                    TotalCount = pageResult.TotalCount
                }
            };

            return View(vm);
        }

        [HMS.UI.Security.HasPermission("pharmacy.view")]
        public async Task<IActionResult> Pharmacy(string? status = "PENDING", int page = 1, int pageSize = 10)
        {
            var normalizedStatus = NormalizeStatus(status, new[] { "PENDING", "PARTIAL", "READY", "DISPENSED" }, "PENDING");
            var pageResult = await _api.GetAsync<PagedResult<PrescriptionViewModel>>($"/pharmacy/prescriptions?status={Uri.EscapeDataString(normalizedStatus)}&page={page}&pageSize={pageSize}")
                ?? new PagedResult<PrescriptionViewModel> { Page = page, PageSize = pageSize };

            var items = (pageResult.Items ?? Array.Empty<PrescriptionViewModel>())
                .Select(prescription => new QueueItemViewModel
                {
                    Title = prescription.PatientDisplay ?? "Prescription",
                    Subtitle = prescription.VisitDisplay,
                    Summary = $"{prescription.Items.Length} item(s) • {FormatDate(prescription.CreatedAt)}",
                    Status = prescription.Status,
                    BadgeClass = MapPharmacyBadge(prescription.Status),
                    LinkUrl = Url.Action("PrescriptionDetails", "Pharmacy", new { id = prescription.Id }),
                    LinkText = "Open",
                    Meta = prescription.Items.Any() ? string.Join(", ", prescription.Items.Take(2).Select(i => i.MedicationName)) : null
                })
                .ToArray();

            var vm = new QueuePageViewModel
            {
                Title = "Pharmacy Queue",
                Description = "Dispense prescriptions, reconcile shortages, and keep medication flow moving.",
                Roles = new[] { "Pharmacist", "System Administrator", "Hospital Administrator / Super Admin" },
                FilterLabel = "Status",
                FilterValue = normalizedStatus,
                FilterOptions = new[] { "PENDING", "PARTIAL", "READY", "DISPENSED" },
                PrimaryActionUrl = Url.Action("Prescriptions", "Pharmacy"),
                PrimaryActionText = "Open Prescriptions",
                EmptyMessage = "No prescriptions are waiting for pharmacy action.",
                ItemsPage = new PagedResult<QueueItemViewModel>
                {
                    Items = items,
                    Page = pageResult.Page,
                    PageSize = pageResult.PageSize,
                    TotalCount = pageResult.TotalCount
                }
            };

            return View(vm);
        }

        [HMS.UI.Security.HasPermission("billing.view")]
        public async Task<IActionResult> Billing(string? status = "UNPAID", int page = 1, int pageSize = 10)
        {
            var normalizedStatus = NormalizeStatus(status, new[] { "UNPAID", "PARTIAL", "PAID", "OVERDUE" }, "UNPAID");
            var pageResult = await _api.GetAsync<PagedResult<InvoiceViewModel>>($"/billing?status={Uri.EscapeDataString(normalizedStatus)}&page={page}&pageSize={pageSize}")
                ?? new PagedResult<InvoiceViewModel> { Page = page, PageSize = pageSize };

            var items = (pageResult.Items ?? Array.Empty<InvoiceViewModel>())
                .Select(invoice => new QueueItemViewModel
                {
                    Title = invoice.InvoiceNumber,
                    Subtitle = invoice.PatientName ?? "Patient record",
                    Summary = $"{invoice.VisitType ?? "Visit"} • {FormatMoney(invoice.Balance, invoice.Currency)} • {FormatDate(invoice.CreatedAt)}",
                    Status = invoice.Status,
                    BadgeClass = MapBillingBadge(invoice.Status),
                    LinkUrl = Url.Action("Details", "Billing", new { id = invoice.Id }),
                    LinkText = "Review",
                    Meta = invoice.VisitAt.HasValue ? invoice.VisitAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : null
                })
                .ToArray();

            var vm = new QueuePageViewModel
            {
                Title = "Billing Queue",
                Description = "Clear unpaid invoices, manage balances, and keep revenue flowing.",
                Roles = new[] { "Billing / Accounts Officer", "Cashier", "System Administrator", "Hospital Administrator / Super Admin" },
                FilterLabel = "Status",
                FilterValue = normalizedStatus,
                FilterOptions = new[] { "UNPAID", "PARTIAL", "PAID", "OVERDUE" },
                PrimaryActionUrl = Url.Action("Invoices", "Billing"),
                PrimaryActionText = "Open Invoices",
                EmptyMessage = "No invoices are waiting in the billing queue.",
                ItemsPage = new PagedResult<QueueItemViewModel>
                {
                    Items = items,
                    Page = pageResult.Page,
                    PageSize = pageResult.PageSize,
                    TotalCount = pageResult.TotalCount
                }
            };

            return View(vm);
        }

        private DashboardCardViewModel[] BuildQueueHubCards(string[] roles)
        {
            var normalized = roles.Select(NormalizeRoleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cards = new System.Collections.Generic.List<DashboardCardViewModel>();

            if (HasRole(normalized, "doctor", "physician", "clinical"))
            {
                cards.Add(new DashboardCardViewModel
                {
                    Title = "Clinical Workspace",
                    Description = "Open patient charts, visits, notes, and care actions.",
                    IconClass = "bi bi-clipboard2-pulse",
                    BadgeText = "Clinical",
                    BadgeClass = "badge-soft-primary",
                    LinkUrl = Url.Action("Index", "Patients"),
                    LinkText = "Open Patients",
                    Footnote = "Best starting point for clinician workflows."
                });
            }

            if (HasRole(normalized, "laboratorystaff", "labtech", "labtechnician", "laboratory", "lab"))
            {
                cards.Add(new DashboardCardViewModel
                {
                    Title = "Laboratory Queue",
                    Description = "Pending requests, result validation, and sample follow-up.",
                    IconClass = "bi bi-heart-pulse-fill",
                    BadgeText = "Lab",
                    BadgeClass = "badge-soft-danger",
                    LinkUrl = Url.Action(nameof(Lab)),
                    LinkText = "Open Queue",
                    Footnote = "Shows the lab worklist with status filters."
                });
            }

            if (HasRole(normalized, "pharmacist", "pharmacy"))
            {
                cards.Add(new DashboardCardViewModel
                {
                    Title = "Pharmacy Queue",
                    Description = "Prescriptions ready for review, reconciliation, and dispensing.",
                    IconClass = "bi bi-capsule-pill",
                    BadgeText = "Pharmacy",
                    BadgeClass = "badge-soft-success",
                    LinkUrl = Url.Action(nameof(Pharmacy)),
                    LinkText = "Open Queue",
                    Footnote = "Use this for medication fulfillment."
                });
            }

            if (HasRole(normalized, "billing", "cashier", "accountsofficer", "billingaccountsofficer"))
            {
                cards.Add(new DashboardCardViewModel
                {
                    Title = "Billing Queue",
                    Description = "Open invoices and balances that need attention.",
                    IconClass = "bi bi-cash-stack",
                    BadgeText = "Billing",
                    BadgeClass = "badge-soft-warning",
                    LinkUrl = Url.Action(nameof(Billing)),
                    LinkText = "Open Queue",
                    Footnote = "Use this for invoice review and collection."
                });
            }

            if (HasRole(normalized, "systemadministrator", "hospitaladministrator", "admin", "superadmin", "hospitaladmin"))
            {
                cards.Add(new DashboardCardViewModel
                {
                    Title = "Administration",
                    Description = "User, role, and report administration tools.",
                    IconClass = "bi bi-shield-lock",
                    BadgeText = "Admin",
                    BadgeClass = "badge-soft-secondary",
                    LinkUrl = Url.Action("Index", "Users"),
                    LinkText = "Open Users",
                    Footnote = "Pairs well with role and permission management."
                });
            }

            if (!cards.Any())
            {
                cards.Add(new DashboardCardViewModel
                {
                    Title = "General Workspace",
                    Description = "Use the patient list to jump into chart-based workflows.",
                    IconClass = "bi bi-people-fill",
                    BadgeText = "General",
                    BadgeClass = "badge-soft-primary",
                    LinkUrl = Url.Action("Index", "Patients"),
                    LinkText = "Browse Patients",
                    Footnote = "You can still access module-specific workspaces from the menu."
                });
            }

            return cards.ToArray();
        }

        private string[] GetRoles()
        {
            return User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v)
                .ToArray();
        }

        private string GetDisplayName()
        {
            return User.FindFirst("display_name")?.Value
                ?? User.Identity?.Name
                ?? "User";
        }

        private string GetTenantName()
        {
            return User.FindFirst("tenant_name")?.Value
                ?? Request.Cookies["HmsTenantName"]
                ?? string.Empty;
        }

        private static string NormalizeRole(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static bool HasRole(System.Collections.Generic.ISet<string> normalizedRoles, params string[] roleNames)
        {
            return roleNames.Select(NormalizeRoleKey).Any(normalizedRoles.Contains);
        }

        private static string NormalizeRoleKey(string value)
        {
            var normalized = NormalizeRole(value);
            return normalized switch
            {
                "systemadministrator" or "admin" => "systemadministrator",
                "hospitaladministratorsuperadmin" or "hospitaladministrator" or "superadmin" or "hospitaladmin" => "hospitaladministrator",
                "doctorphysician" or "doctor" or "physician" => "doctor",
                "nurse" => "nurse",
                "pharmacist" => "pharmacist",
                "laboratorystaff" or "labtech" or "labtechnician" or "laboratory" or "lab" => "laboratory",
                "radiologystaff" or "radiology" => "radiology",
                "receptionistfrontdesk" or "receptionist" or "frontdesk" => "receptionist",
                "billingaccountsofficer" or "billing" or "accountsofficer" => "billing",
                "insuranceclaimsofficer" or "insurance" or "claims" or "claimsofficer" => "insurance",
                "cashier" => "cashier",
                "medicalrecordshimofficer" or "medicalrecords" or "him" or "himofficer" or "records" => "medicalrecords",
                "hrstaffmanager" or "hr" or "staffmanager" => "hr",
                "procurementofficer" or "procurement" => "procurement",
                "inventorystoremanager" or "inventory" or "storemanager" => "inventory",
                "financemanageraccountant" or "finance" or "accountant" => "finance",
                "departmentmanagerheadofdepartment" or "departmentmanager" or "headofdepartment" or "hod" => "departmentmanager",
                "hospitaloperationsmanager" or "operationsmanager" or "operations" => "hospitaloperations",
                "auditorcomplianceofficer" or "auditor" or "complianceofficer" => "auditor",
                "patientportaluser" or "patientportal" or "patient" or "user" => "patientportal",
                _ => normalized
            };
        }

        private static string NormalizeStatus(string? value, string[] allowed, string fallback)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
            return allowed.Contains(normalized) ? normalized : fallback;
        }

        private static string FormatDate(DateTimeOffset? value)
        {
            return value.HasValue ? value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "Recently";
        }

        private static string FormatMoney(decimal value, string? currency)
        {
            var ccy = string.IsNullOrWhiteSpace(currency) ? "NGN" : currency;
            return $"{value:0.##} {ccy}";
        }

        private static string MapLabBadge(string? status)
        {
            return NormalizeBadge(status, "badge-soft-primary", new (string, string)[]
            {
                ("PENDING", "badge-soft-warning"),
                ("PROCESSING", "badge-soft-info"),
                ("READY", "badge-soft-success"),
                ("COMPLETED", "badge-soft-secondary")
            });
        }

        private static string MapPharmacyBadge(string? status)
        {
            return NormalizeBadge(status, "badge-soft-primary", new (string, string)[]
            {
                ("PENDING", "badge-soft-warning"),
                ("PARTIAL", "badge-soft-info"),
                ("READY", "badge-soft-success"),
                ("DISPENSED", "badge-soft-secondary")
            });
        }

        private static string MapBillingBadge(string? status)
        {
            return NormalizeBadge(status, "badge-soft-primary", new (string, string)[]
            {
                ("UNPAID", "badge-soft-danger"),
                ("PARTIAL", "badge-soft-warning"),
                ("PAID", "badge-soft-success"),
                ("OVERDUE", "badge-soft-danger")
            });
        }

        private static string NormalizeBadge(string? value, string fallback, (string Status, string Badge)[] map)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
            return map.FirstOrDefault(x => x.Status == normalized).Badge ?? fallback;
        }
    }
}
