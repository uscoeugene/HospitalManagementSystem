using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.UI.Models.Reports;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers
{
    [HMS.UI.Security.HasPermission("SERVICE.REPORT.VIEW")]
    public class ReportsController : Controller
    {
        private readonly ApiClient _api;
        public ReportsController(ApiClient api) { _api = api; }

        public async Task<IActionResult> LabStatusBreakdown()
        {
            try
            {
                var data = await _api.GetAsync<LabStatusBreakdownViewModel[]>("/api/reports/lab/status-breakdown");
                return View(data ?? Array.Empty<LabStatusBreakdownViewModel>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(Array.Empty<LabStatusBreakdownViewModel>());
            }
        }

        public async Task<IActionResult> LabTurnaround(int recent = 100)
        {
            try
            {
                var items = await _api.GetAsync<LabTurnaroundViewModel[]>($"/api/reports/lab/turnaround?recent={recent}");
                var arr = items ?? Array.Empty<LabTurnaroundViewModel>();
                var model = new LabTurnaroundPageViewModel
                {
                    Recent = recent,
                    Items = arr.OrderByDescending(i => i.TurnaroundHours).ToArray(),
                    AverageHours = arr.Any() ? arr.Average(i => i.TurnaroundHours) : 0.0,
                    MaxHours = arr.Any() ? arr.Max(i => i.TurnaroundHours) : 0.0,
                    MinHours = arr.Any() ? arr.Min(i => i.TurnaroundHours) : 0.0
                };
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new LabTurnaroundPageViewModel { Items = Array.Empty<LabTurnaroundViewModel>(), Recent = recent });
            }
        }

        public async Task<IActionResult> LabTopTests(int top = 10)
        {
            try
            {
                var items = await _api.GetAsync<TopTestViewModel[]>($"/api/reports/lab/top-tests?top={top}");
                return View(items ?? Array.Empty<TopTestViewModel>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(Array.Empty<TopTestViewModel>());
            }

        }

        // Dashboard index with links to available report pages
        public IActionResult Index()
        {
            return View();
        }
    }
}
