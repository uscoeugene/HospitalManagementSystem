using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HMS.UI.Services;
using System.Text.Json;
using HMS.UI.Models;
using HMS.UI.Models.Lab;
using HMS.UI.Models.Lab;
using System.Net.Http.Json;
using HMS.UI.Security;
using HMS.UI.Models.Profile;

namespace HMS.UI.Controllers
{
    [HasPermission("patients.view")]
    public class PatientsController : Controller
    {
        private readonly ApiClient _api;
        private readonly StaticDataService _static;

        public PatientsController(ApiClient api, StaticDataService staticData)
        {
            _api = api;
            _static = staticData;
        }

        [HasPermission("patients.view")]
        public async Task<IActionResult> VisitDetails(Guid id)
        {
            try
            {
                // id is visit id
                var v = await _api.GetAsync<HMS.UI.Models.VisitViewModel>($"/patients/visits/{id}");
                if (v == null) return NotFound();

                // fetch patient
                var p = await _api.GetAsync<PatientDetailsViewModel>($"/patients/{v.PatientId}");

                // fetch recent vitals for visit (API returns ApiResponse wrapper -> use GetAsync on array type via client)
                var vitals = await _api.GetAsync<HMS.UI.Models.VitalSignViewModel[]>($"/patients/visits/{v.Id}/vitals");
                var consultations = await _api.GetAsync<HMS.UI.Models.ConsultationViewModel[]>($"/patients/visits/{v.Id}/consultations");

                // load providers (medical staff)
                try
                {
                    var providers = await _api.GetAsync<HMS.UI.Models.Profile.ProviderViewModel[]>($"/api/profile/providers");
                    var providerMap = new System.Collections.Generic.Dictionary<Guid, HMS.UI.Models.Profile.ProviderViewModel>();
                    if (providers != null)
                    {
                        foreach (var prov in providers) providerMap[prov.UserId] = prov;
                    }
                    ViewBag.ProvidersMap = providerMap;
                }
                catch { ViewBag.ProvidersMap = null; }
                var vm = new HMS.UI.Models.VisitDetailsViewModel
                {
                    Visit = v,
                    Patient = p,
                    RecentVitals = (vitals ?? Array.Empty<HMS.UI.Models.VitalSignViewModel>()).Select(x => new HMS.UI.Models.VitalSignListItem
                    {
                        Id = x.GetType().GetProperty("Id") != null ? (Guid)x.GetType().GetProperty("Id").GetValue(x)! : Guid.Empty,
                        PatientId = x.PatientId,
                        VisitId = x.VisitId,
                        RecordedAt = x.RecordedAt,
                        Temperature = x.Temperature,
                        PulseRate = x.PulseRate,
                        RespiratoryRate = x.RespiratoryRate,
                        SystolicBP = x.SystolicBP,
                        DiastolicBP = x.DiastolicBP,
                        OxygenSaturation = x.OxygenSaturation,
                        WeightKg = x.WeightKg,
                        HeightCm = x.HeightCm,
                        BMI = x.BMI,
                        BloodSugar = x.BloodSugar,
                        Notes = x.Notes,
                        RecordedByUserId = x.GetType().GetProperty("RecordedByUserId") != null ? (Guid?)x.GetType().GetProperty("RecordedByUserId").GetValue(x) : null
                    }).ToArray(),
                Consultations = consultations ?? Array.Empty<HMS.UI.Models.ConsultationViewModel>()
                };

                // load invoices for this visit so UI can display them in invoices tab
                try
                {
                    var invoicePage = await _api.GetAsync<PagedResult<HMS.UI.Models.Billing.InvoiceViewModel>>($"/billing?visitId={v.Id}&page=1&pageSize=50");
                    vm.Invoices = invoicePage?.Items ?? Array.Empty<HMS.UI.Models.Billing.InvoiceViewModel>();
                }
                catch { vm.Invoices = Array.Empty<HMS.UI.Models.Billing.InvoiceViewModel>(); }

                try
                {
                    var labPage = await _api.GetAsync<PagedResult<HMS.UI.Models.Lab.LabRequestViewModel>>($"/lab/requests?visitId={v.Id}&page=1&pageSize=50");
                    var items = labPage?.Items ?? Array.Empty<HMS.UI.Models.Lab.LabRequestViewModel>();

                    // Enhance display metadata where possible
                    foreach (var lr in items)
                    {
                        lr.ItemsCount = lr.Items != null ? lr.Items.Count() : 0;
                        lr.ResultsStatus = lr.Items != null && lr.Items.Any(i => !string.Equals(i.ResultStatus, "PENDING", StringComparison.OrdinalIgnoreCase)) ? "Has Results" : "Pending";
                        // use invoice summary returned by API when available
                        lr.InvoiceStatus = lr.InvoiceSummary != null ? lr.InvoiceSummary.Status : (lr.Items?.Any(i => i.ChargeInvoiceItemId.HasValue) == true ? "CHARGED" : "UNPAID");
                        lr.PatientName = vm.Patient != null ? (vm.Patient.FirstName + " " + vm.Patient.LastName).Trim() : lr.PatientName;
                    }

                    vm.LabRequests = items;
                }
                catch { vm.LabRequests = Array.Empty<HMS.UI.Models.Lab.LabRequestViewModel>(); }

                try
                {
                    var prescriptionPage = await _api.GetAsync<PagedResult<HMS.UI.Models.Pharmacy.PrescriptionViewModel>>($"/pharmacy/prescriptions?visitId={v.Id}&page=1&pageSize=100");
                    vm.Prescriptions = prescriptionPage?.Items ?? Array.Empty<HMS.UI.Models.Pharmacy.PrescriptionViewModel>();
                }
                catch
                {
                    vm.Prescriptions = Array.Empty<HMS.UI.Models.Pharmacy.PrescriptionViewModel>();
                }

                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HasPermission("patients.view")]
        public async Task<IActionResult> ConsultationDetails(Guid id)
        {
            try
            {
                var c = await _api.GetAsync<HMS.UI.Models.ConsultationViewModel>($"/patients/consultations/{id}");
                if (c == null) return NotFound();
                // populate patient header
                try { ViewBag.Patient = await _api.GetAsync<HMS.UI.Models.PatientDetailsViewModel>($"/patients/{c.PatientId}"); } catch { ViewBag.Patient = null; }

                // providers map
                try
                {
                    var profilesPage = await _api.GetAsync<HMS.UI.Models.Reporting.PagedReportResult<HMS.UI.Models.Reporting.ProfileSummaryDto>>("/reports/Profile/summary?page=1&pageSize=200");
                    var providerMap = new System.Collections.Generic.Dictionary<Guid, string>();
                    if (profilesPage != null && profilesPage.Items != null)
                    {
                        foreach (var prof in profilesPage.Items) providerMap[prof.UserId] = prof.FullName;
                    }
                    ViewBag.ProvidersMap = providerMap;
                }
                catch { ViewBag.ProvidersMap = null; }

                return View(c);
            }
            catch
            {
                return NotFound();
            }
        }

        [HasPermission("patients.manage")]
        public async Task<IActionResult> EditConsultation(Guid id)
        {
            try
            {
                var c = await _api.GetAsync<HMS.UI.Models.ConsultationViewModel>($"/patients/consultations/{id}");
                if (c == null) return NotFound();
                var vm = new HMS.UI.Models.CreateConsultationViewModel
                {
                    PatientId = c.PatientId,
                    VisitId = c.VisitId,
                    DoctorId = c.DoctorId,
                    ConsultationAt = c.ConsultationAt,
                    FollowUpAt = c.FollowUpAt,
                    ChiefComplaint = c.ChiefComplaint,
                    HistoryOfPresentIllness = c.HistoryOfPresentIllness,
                    PhysicalExamination = c.PhysicalExamination,
                    DiagnosisCodes = c.DiagnosisCodes,
                    Procedures = c.Procedures,
                    Notes = c.Notes,
                    Status = c.Status
                };
                ViewBag.ConsultationId = id;
                return View(vm);
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> EditConsultation(Guid id, HMS.UI.Models.CreateConsultationViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            try
            {
                var payload = new
                {
                    PatientId = vm.PatientId,
                    VisitId = vm.VisitId,
                    DoctorId = vm.DoctorId,
                    ConsultationAt = vm.ConsultationAt,
                    FollowUpAt = vm.FollowUpAt,
                    ChiefComplaint = vm.ChiefComplaint,
                    HistoryOfPresentIllness = vm.HistoryOfPresentIllness,
                    PhysicalExamination = vm.PhysicalExamination,
                    DiagnosisCodes = vm.DiagnosisCodes,
                    Procedures = vm.Procedures,
                    Notes = vm.Notes,
                    Status = vm.Status
                };
                var raw = await _api.PutRawAsync($"/patients/consultations/{id}", payload);
                if (raw.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Consultation updated";
                    return RedirectToAction("ConsultationDetails", new { id = id });
                }
                var body = await raw.Content.ReadAsStringAsync();
                TempData["Error"] = body;
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> DeleteConsultation(Guid id, Guid? visitId)
        {
            try
            {
                var raw = await _api.DeleteRawAsync($"/patients/consultations/{id}");
                if (raw.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Consultation deleted";
                    if (visitId.HasValue && visitId.Value != Guid.Empty) return RedirectToAction("VisitDetails", new { id = visitId.Value });
                    return RedirectToAction("Index");
                }
                var body = await raw.Content.ReadAsStringAsync();
                TempData["Error"] = body;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HasPermission("patients.view")]
        public async Task<IActionResult> Consultations(Guid visitId)
        {
            try
            {
                var list = await _api.GetAsync<HMS.UI.Models.ConsultationViewModel[]>($"/patients/visits/{visitId}/consultations");
                return View(list ?? Array.Empty<HMS.UI.Models.ConsultationViewModel>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("VisitDetails", new { id = visitId });
            }
        }

        [HasPermission("patients.manage")]
        public async Task<IActionResult> CreateConsultation(Guid visitId)
        {
            try
            {
                var visit = await _api.GetAsync<HMS.UI.Models.VisitViewModel>($"/patients/visits/{visitId}");
                if (visit == null) return NotFound();
                var vm = new HMS.UI.Models.CreateConsultationViewModel { PatientId = visit.PatientId, VisitId = visit.Id, ConsultationAt = DateTimeOffset.UtcNow };
                await PopulateConsultationViewDataAsync(vm);
                return View(vm);
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> CreateConsultation(HMS.UI.Models.CreateConsultationViewModel vm)
        {
            await ApplyCurrentDoctorDefaultsAsync(vm);
            if (!ModelState.IsValid)
            {
                await PopulateConsultationViewDataAsync(vm);
                return View(vm);
            }
            try
            {
                var payload = new { PatientId = vm.PatientId, VisitId = vm.VisitId, DoctorId = vm.DoctorId, ConsultationAt = vm.ConsultationAt, Notes = vm.Notes, ChiefComplaint = vm.ChiefComplaint };
                var resp = await _api.PostAsync<object>($"/patients/{vm.PatientId}/visits/{vm.VisitId}/consultations", payload);
                if (resp != null)
                {
                    TempData["Success"] = "Consultation created";
                    return RedirectToAction("VisitDetails", new { id = vm.VisitId });
                }
                TempData["Error"] = "Failed to create consultation";
                await PopulateConsultationViewDataAsync(vm);
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                await PopulateConsultationViewDataAsync(vm);
                return View(vm);
            }
        }

        private async Task PopulateConsultationViewDataAsync(HMS.UI.Models.CreateConsultationViewModel vm)
        {
            ProviderViewModel[] providers;
            try
            {
                providers = await _api.GetAsync<ProviderViewModel[]>("/api/profile/providers") ?? Array.Empty<ProviderViewModel>();
            }
            catch
            {
                providers = Array.Empty<ProviderViewModel>();
            }

            ViewBag.Providers = providers;

            try
            {
                ViewBag.Patient = await _api.GetAsync<HMS.UI.Models.PatientDetailsViewModel>($"/patients/{vm.PatientId}");
            }
            catch
            {
                ViewBag.Patient = null;
            }

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(currentUserId, out var parsedUserId))
            {
                var me = providers.FirstOrDefault(p => p.UserId == parsedUserId);
                ViewBag.CurrentDoctor = me;
                ViewBag.LockDoctorSelection = me?.IsDoctor == true;
                if (me?.IsDoctor == true && !vm.DoctorId.HasValue)
                {
                    vm.DoctorId = me.UserId;
                }
            }
            else
            {
                ViewBag.CurrentDoctor = null;
                ViewBag.LockDoctorSelection = false;
            }
        }

        private async Task ApplyCurrentDoctorDefaultsAsync(HMS.UI.Models.CreateConsultationViewModel vm)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(currentUserId, out var parsedUserId))
            {
                return;
            }

            var providers = await _api.GetAsync<ProviderViewModel[]>("/api/profile/providers") ?? Array.Empty<ProviderViewModel>();
            var me = providers.FirstOrDefault(p => p.UserId == parsedUserId);
            if (me?.IsDoctor == true)
            {
                vm.DoctorId = me.UserId;
            }
        }

        private async Task PopulateStaticListsAsync()
        {
            var med = await _static.GetMedicalDataAsync();
            ViewBag.BloodGroups = med?.BloodGroups ?? Enumerable.Empty<string>();
            ViewBag.Genotypes = med?.Genotypes ?? Enumerable.Empty<string>();

            var countries = await _static.GetCountriesAsync() ?? Array.Empty<StaticDataService.CountryEntry>();
            ViewData["Countries"] = countries.Select(c => c.Name).ToArray();
            ViewData["CountryStatesJson"] = JsonSerializer.Serialize(countries);
        }
        [Route("patients")]
        public async Task<IActionResult> Index(string q = null, int page = 1)
        {
            try
            {
                var res = await _api.GetAsync<PagedResult<PatientListItemViewModel>>($"/patients?q={System.Net.WebUtility.UrlEncode(q ?? string.Empty)}&page={page}");

                if (res == null)
                {
                    // Inspect API debug info to provide a helpful message instead of silently showing empty list
                    try
                    {
                        var dbg = _api.GetLastDebug();
                        if (dbg != null && dbg.ResponseStatus.HasValue)
                        {
                            var status = dbg.ResponseStatus.Value;
                            var body = dbg.ResponseBody ?? string.Empty;
                            if (status == 401)
                            {
                                // Not authenticated - redirect to login
                                return RedirectToAction("Login", "Account");
                            }
                            if (status == 403)
                            {
                                TempData["Error"] = "Access denied when fetching patients.";
                                return View(new PagedResult<PatientListItemViewModel>());
                            }

                            // Try parse common shapes: { error: ".." } or ProblemDetails
                            string friendly = null;
                            try
                            {
                                using var jd = System.Text.Json.JsonDocument.Parse(body);
                                var root = jd.RootElement;
                                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    if (root.TryGetProperty("error", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String) friendly = e.GetString();
                                    else if (root.TryGetProperty("detail", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String) friendly = d.GetString();
                                    else if (root.TryGetProperty("title", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String) friendly = t.GetString();
                                }
                            }
                            catch { }

                            TempData["Error"] = string.IsNullOrWhiteSpace(friendly) ? ($"Failed to load patients (API {status})") : friendly;
                        }
                    }
                    catch { }

                    return View(new PagedResult<PatientListItemViewModel>());
                }

                return View(res);
            }
            catch
            {
                return View(new PagedResult<PatientListItemViewModel>());
            }
        }

        [HasPermission("patients.manage")]
        public async Task<IActionResult> Create()
        {
            await PopulateStaticListsAsync();
            return View(new PatientCreateViewModel { DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> Create(PatientCreateViewModel vm)
        {
            // If model binding failed or validation invalid, attempt to read raw form values as a fallback.
            if (!ModelState.IsValid || vm == null)
            {
                try
                {
                    var f = Request.HasFormContentType ? Request.Form : null;
                    if (f != null)
                    {
                        // build payload from form values (use ISO date string)
                        var dob = f["DateOfBirth"].ToString();
                        var payload = new
                        {
                            FirstName = f["FirstName"].ToString(),
                            MiddleName = string.IsNullOrWhiteSpace(f["MiddleName"]) ? null : f["MiddleName"].ToString(),
                            LastName = f["LastName"].ToString(),
                            DateOfBirth = dob, // yyyy-MM-dd from date input
                            Gender = f["Gender"].ToString(),
                            Phone = string.IsNullOrWhiteSpace(f["Phone"]) ? null : f["Phone"].ToString(),
                            AlternatePhone = string.IsNullOrWhiteSpace(f["AlternatePhone"]) ? null : f["AlternatePhone"].ToString(),
                            Email = string.IsNullOrWhiteSpace(f["Email"]) ? null : f["Email"].ToString(),
                            MedicalRecordNumber = string.IsNullOrWhiteSpace(f["MedicalRecordNumber"]) ? null : f["MedicalRecordNumber"].ToString(),
                        };

                        var resp2 = await _api.PostAsync<object>("/patients", payload);
                        if (resp2 != null)
                        {
                            TempData["Success"] = "Patient created successfully.";
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, "Failed to create patient.");
                            TempData["Error"] = "Failed to create patient.";
                            return View(vm ?? new PatientCreateViewModel());
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = ex.Message;
                    // fall through to show view with errors
                }
            }

            // Normal binding path
            try
            {
                var resp = await _api.PostAsync<object>("/patients", vm!);
                if (resp != null)
                {
                    TempData["Success"] = "Patient created successfully.";
                    return RedirectToAction("Index");
                }
                ModelState.AddModelError(string.Empty, "Failed to create patient.");
                TempData["Error"] = "Failed to create patient.";
                await PopulateStaticListsAsync();
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(vm);
            }

        }


        public async Task<IActionResult> Details(Guid id)
        {
            try
            {
                var p = await _api.GetAsync<PatientDetailsViewModel>($"/patients/{id}");
                if (p == null) return NotFound();
                return View(p);
            }
            catch
            {
                return NotFound();
            }
        }

        [HasPermission("patients.view")]
        public async Task<IActionResult> Visits(Guid id)
        {
            try
            {
                var visits = await _api.GetAsync<HMS.UI.Models.VisitViewModel[]>($"/patients/{id}/visits");
                if (visits == null) visits = Array.Empty<HMS.UI.Models.VisitViewModel>();
                ViewData["PatientId"] = id;
                return View(visits);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", new { id = id });
            }
        }

        [HasPermission("patients.manage")]
        public async Task<IActionResult> CreateVisit(Guid patientId)
        {
            var vm = new HMS.UI.Models.VisitCreateViewModel { PatientId = patientId, VisitAt = DateTimeOffset.UtcNow };
            try
            {
                var types = await _api.GetAsync<string[]>("/visittypes");
                ViewBag.VisitTypes = types ?? Array.Empty<string>();
            }
            catch { ViewBag.VisitTypes = Array.Empty<string>(); }
            return View(vm);
        }

        [HasPermission("patients.manage")]
        public async Task<IActionResult> EnterVitals(Guid id)
        {
            // id is visit id
            try
            {
                var visit = await _api.GetAsync<HMS.UI.Models.VisitViewModel>($"/patients/visits/{id}");
                if (visit == null) return NotFound();

                var patient = await _api.GetAsync<PatientDetailsViewModel>($"/patients/{visit.PatientId}");

                // fetch recent vitals
                var vitals = await _api.GetAsync<HMS.UI.Models.VitalSignViewModel[]>($"/patients/visits/{visit.Id}/vitals");

                var vm = new HMS.UI.Models.EnterVitalsPageViewModel
                {
                    Form = new HMS.UI.Models.VitalSignViewModel { PatientId = visit.PatientId, VisitId = visit.Id, RecordedAt = DateTimeOffset.UtcNow },
                    Patient = patient,
                    Visit = visit,
                    RecentVitals = (vitals ?? Array.Empty<HMS.UI.Models.VitalSignViewModel>()).Select(x => new HMS.UI.Models.VitalSignListItem
                    {
                        Id = x.Id,
                        PatientId = x.PatientId,
                        VisitId = x.VisitId,
                        RecordedAt = x.RecordedAt,
                        Temperature = x.Temperature,
                        PulseRate = x.PulseRate,
                        RespiratoryRate = x.RespiratoryRate,
                        SystolicBP = x.SystolicBP,
                        DiastolicBP = x.DiastolicBP,
                        OxygenSaturation = x.OxygenSaturation,
                        WeightKg = x.WeightKg,
                        HeightCm = x.HeightCm,
                        BMI = x.BMI,
                        BloodSugar = x.BloodSugar,
                        Notes = x.Notes,
                        RecordedByUserId = x.RecordedByUserId
                    }).ToArray()
                };

                return View(vm);
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> EnterVitals(HMS.UI.Models.VitalSignViewModel vm)
        {
            if (!ModelState.IsValid) 
            {
                // rebuild page vm to include patient and recent vitals
                var visit = await _api.GetAsync<HMS.UI.Models.VisitViewModel>($"/patients/visits/{vm.VisitId}");
                var patient = visit != null ? await _api.GetAsync<PatientDetailsViewModel>($"/patients/{visit.PatientId}") : null;
                var vitals = await _api.GetAsync<HMS.UI.Models.VitalSignViewModel[]>($"/patients/visits/{vm.VisitId}/vitals");
                var pageVm = new HMS.UI.Models.EnterVitalsPageViewModel
                {
                    Form = vm,
                    Patient = patient,
                    Visit = visit,
                    RecentVitals = (vitals ?? Array.Empty<HMS.UI.Models.VitalSignViewModel>()).Select(x => new HMS.UI.Models.VitalSignListItem
                    {
                        Id = x.Id,
                        PatientId = x.PatientId,
                        VisitId = x.VisitId,
                        RecordedAt = x.RecordedAt,
                        Temperature = x.Temperature,
                        PulseRate = x.PulseRate,
                        RespiratoryRate = x.RespiratoryRate,
                        SystolicBP = x.SystolicBP,
                        DiastolicBP = x.DiastolicBP,
                        OxygenSaturation = x.OxygenSaturation,
                        WeightKg = x.WeightKg,
                        HeightCm = x.HeightCm,
                        BMI = x.BMI,
                        BloodSugar = x.BloodSugar,
                        Notes = x.Notes,
                        RecordedByUserId = x.RecordedByUserId
                    }).ToArray()
                };
                return View(pageVm);
            }

            try
            {
                var payload = new
                {
                    PatientId = vm.PatientId,
                    VisitId = vm.VisitId,
                    RecordedAt = vm.RecordedAt,
                    Temperature = vm.Temperature,
                    PulseRate = vm.PulseRate,
                    RespiratoryRate = vm.RespiratoryRate,
                    SystolicBP = vm.SystolicBP,
                    DiastolicBP = vm.DiastolicBP,
                    OxygenSaturation = vm.OxygenSaturation,
                    WeightKg = vm.WeightKg,
                    HeightCm = vm.HeightCm,
                    BMI = vm.BMI,
                    BloodSugar = vm.BloodSugar,
                    Notes = vm.Notes
                };

                var resp = await _api.PostRawAsync($"/patients/{vm.PatientId}/visits/{vm.VisitId}/vitals", payload);
                if (resp.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Vitals saved";
                    return RedirectToAction("Visits", new { id = vm.PatientId });
                }
                var txt = await resp.Content.ReadAsStringAsync();

                ModelState.AddModelError(string.Empty, "Failed to save vitals: " + txt);

                // rebuild page viewmodel to return proper context
                var visit2 = await _api.GetAsync<HMS.UI.Models.VisitViewModel>($"/patients/visits/{vm.VisitId}");
                var patient2 = visit2 != null ? await _api.GetAsync<PatientDetailsViewModel>($"/patients/{visit2.PatientId}") : null;
                var vitals2 = await _api.GetAsync<HMS.UI.Models.VitalSignViewModel[]>($"/patients/visits/{vm.VisitId}/vitals");
                var pageVm2 = new HMS.UI.Models.EnterVitalsPageViewModel
                {
                    Form = vm,
                    Patient = patient2,
                    Visit = visit2,
                    RecentVitals = (vitals2 ?? Array.Empty<HMS.UI.Models.VitalSignViewModel>()).Select(x => new HMS.UI.Models.VitalSignListItem
                    {
                        Id = x.Id,
                        PatientId = x.PatientId,
                        VisitId = x.VisitId,
                        RecordedAt = x.RecordedAt,
                        Temperature = x.Temperature,
                        PulseRate = x.PulseRate,
                        RespiratoryRate = x.RespiratoryRate,
                        SystolicBP = x.SystolicBP,
                        DiastolicBP = x.DiastolicBP,
                        OxygenSaturation = x.OxygenSaturation,
                        WeightKg = x.WeightKg,
                        HeightCm = x.HeightCm,
                        BMI = x.BMI,
                        BloodSugar = x.BloodSugar,
                        Notes = x.Notes,
                        RecordedByUserId = x.RecordedByUserId
                    }).ToArray()
                };

                return View(pageVm2);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> CreateVisit(HMS.UI.Models.VisitCreateViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            // Ensure VisitType and VisitAt are provided
            if (string.IsNullOrWhiteSpace(vm.VisitType))
            {
                ModelState.AddModelError(nameof(vm.VisitType), "Visit type is required");
                return View(vm);
            }

            try
            {
                // payload expects VisitAt as DateTimeOffset
                var payload = new { VisitAt = vm.VisitAt, VisitType = vm.VisitType, Notes = vm.Notes };
                var resp = await _api.PostAsync<object>($"/patients/{vm.PatientId}/visits", payload);
                if (resp != null)
                {
                    TempData["Success"] = "Visit created";
                    return RedirectToAction("Details", new { id = vm.PatientId });
                }
                TempData["Error"] = "Failed to create visit";
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(vm);
            }
        }

        [HasPermission("patients.manage")]
        public async Task<IActionResult> VisitEdit(Guid id)
        {
            try
            {
                var v = await _api.GetAsync<HMS.UI.Models.VisitViewModel>($"/patients/visits/{id}");
                if (v == null) return NotFound();
                var vm = new HMS.UI.Models.VisitCreateViewModel { Id = v.Id, PatientId = v.PatientId, VisitAt = v.VisitAt, VisitType = v.VisitType, Notes = v.Notes };
                // try to discover patientId via visit details API not provided; user will be redirected back to Details after edit
                try
                {
                    var types = await _api.GetAsync<string[]>("/visittypes");
                    ViewBag.VisitTypes = types ?? Array.Empty<string>();
                }
                catch { ViewBag.VisitTypes = Array.Empty<string>(); }
                return View(vm);
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> VisitEdit(HMS.UI.Models.VisitCreateViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (string.IsNullOrWhiteSpace(vm.VisitType))
            {
                ModelState.AddModelError(nameof(vm.VisitType), "Visit type is required");
                return View(vm);
            }

            try
            {
                var payload = new { VisitAt = vm.VisitAt, VisitType = vm.VisitType, Notes = vm.Notes };
                var resp = await _api.PutRawAsync($"/patients/visits/{vm.Id}", payload);
                if (resp.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Visit updated";
                    if (vm.PatientId != Guid.Empty) return RedirectToAction("Details", new { id = vm.PatientId });
                    return RedirectToAction("Index");
                }
                TempData["Error"] = "Failed to update visit";
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> DeleteVisit(Guid id, Guid? patientId)
        {
            try
            {
                var resp = await _api.DeleteRawAsync($"/patients/visits/{id}");
                if (resp.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Visit deleted";
                    if (patientId.HasValue) return RedirectToAction("Details", new { id = patientId.Value });
                    return RedirectToAction("Index");
                }
                TempData["Error"] = "Failed to delete visit";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HasPermission("patients.manage")]
        public async Task<IActionResult> Edit(Guid id)
        {
            try
            {
                var p = await _api.GetAsync<PatientDetailsViewModel>($"/patients/{id}");
                if (p == null) return NotFound();
                var vm = new PatientCreateViewModel
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    MiddleName = p.MiddleName,
                    LastName = p.LastName,
                    DateOfBirth = p.DateOfBirth,
                    Gender = p.Gender,
                    Phone = p.Phone,
                    AlternatePhone = p.AlternatePhone,
                    Email = p.Email,
                    MedicalRecordNumber = p.MedicalRecordNumber,
                    AddressLine1 = p.AddressLine1,
                    AddressLine2 = p.AddressLine2,
                    City = p.City,
                    State = p.State,
                    PostalCode = p.PostalCode,
                    Country = p.Country,
                    Nationality = p.Nationality,
                    NationalIdNumber = p.NationalIdNumber,
                    BloodGroup = p.BloodGroup,
                    Genotype = p.Genotype,
                    EmergencyContactName = p.EmergencyContactName,
                    EmergencyContactRelationship = p.EmergencyContactRelationship,
                    EmergencyContactPhone = p.EmergencyContactPhone,
                    InsuranceProvider = p.InsuranceProvider,
                    InsuranceNumber = p.InsuranceNumber,
                    Occupation = p.Occupation,
                    IsActive = p.IsActive
                };
                return View(vm);
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> Edit(PatientCreateViewModel vm)
        {
            if (vm == null || !ModelState.IsValid)
            {
                return View(vm ?? new PatientCreateViewModel());
            }

            try
            {
                var id = vm.Id ?? Guid.Empty;
                if (id == Guid.Empty) return BadRequest();

                // build payload similar to create
                var payload = new
                {
                    FirstName = vm.FirstName.Trim(),
                    MiddleName = string.IsNullOrWhiteSpace(vm.MiddleName) ? null : vm.MiddleName,
                    LastName = vm.LastName,
                    DateOfBirth = vm.DateOfBirth,
                    Gender = vm.Gender,
                    Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone,
                    AlternatePhone = string.IsNullOrWhiteSpace(vm.AlternatePhone) ? null : vm.AlternatePhone,
                    Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email,
                    MedicalRecordNumber = string.IsNullOrWhiteSpace(vm.MedicalRecordNumber) ? null : vm.MedicalRecordNumber,
                    AddressLine1 = vm.AddressLine1,
                    AddressLine2 = vm.AddressLine2,
                    City = vm.City,
                    State = vm.State,
                    PostalCode = vm.PostalCode,
                    Country = vm.Country,
                    Nationality = vm.Nationality,
                    NationalIdNumber = vm.NationalIdNumber,
                    BloodGroup = vm.BloodGroup,
                    Genotype = vm.Genotype,
                    EmergencyContactName = vm.EmergencyContactName,
                    EmergencyContactRelationship = vm.EmergencyContactRelationship,
                    EmergencyContactPhone = vm.EmergencyContactPhone,
                    InsuranceProvider = vm.InsuranceProvider,
                    InsuranceNumber = vm.InsuranceNumber,
                    Occupation = vm.Occupation
                    ,
                    IsActive = vm.IsActive
                };

                var resp = await _api.PutAsync<object>($"/patients/{id}", payload);
                if (resp != null)
                {
                    TempData["Success"] = "Patient updated successfully.";
                    return RedirectToAction("Details", new { id = id });
                }

                TempData["Error"] = "Failed to update patient.";
                ModelState.AddModelError(string.Empty, "Failed to update patient.");
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Search(string? q, int page = 1)
        {
            try
            {
                var res = await _api.GetAsync<PagedResult<PatientListItemViewModel>>(
                    $"/patients?q={System.Net.WebUtility.UrlEncode(q ?? string.Empty)}&page={page}");

                if (res == null)
                {
                    return PartialView(
                        "_PatientsTable",
                        new PagedResult<PatientListItemViewModel>());
                }

                return PartialView("_PatientsTable", res);
            }
            catch
            {
                return PartialView(
                    "_PatientsTable",
                    new PagedResult<PatientListItemViewModel>());
            }
        }
    }
}
