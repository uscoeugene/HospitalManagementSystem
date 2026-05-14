using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HMS.UI.Models.Profile;
using System.Collections.Generic;

namespace HMS.UI.Pages.Reports
{
    public class ProfileSummaryModel : PageModel
    {
        private readonly ApiClient _api;

        public ProfileSummaryModel(ApiClient api)
        {
            _api = api;
        }

        public List<UserProfileViewModel> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int Total { get; set; }

        public async Task OnGetAsync(int page = 1, int pageSize = 50)
        {
            Page = page;
            PageSize = pageSize;
            var res = await _api.GetAsync<HMS.UI.Models.Reporting.PagedReportResult<HMS.UI.Models.Reporting.ProfileSummaryDto>>("/reports/Profile/summary?page=" + page + "&pageSize=" + pageSize);
            if (res != null)
            {
                // Map to view models
                Items = new List<UserProfileViewModel>();
                foreach (var it in res.Items)
                {
                    Items.Add(new UserProfileViewModel { UserId = it.UserId, FirstName = it.FullName, Email = it.Email, Department = it.Department, JobTitle = it.JobTitle });
                }
                Total = res.TotalCount;
            }
        }
    }
}
