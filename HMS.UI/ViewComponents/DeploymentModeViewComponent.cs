using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HMS.UI.Services;

namespace HMS.UI.ViewComponents
{
    public class DeploymentModeViewComponent : ViewComponent
    {
        private readonly IDeploymentModeService _deploymentModeService;

        public DeploymentModeViewComponent(IDeploymentModeService deploymentModeService)
        {
            _deploymentModeService = deploymentModeService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var mode = await _deploymentModeService.GetEffectiveModeAsync();
            return View("Default", mode);
        }
    }
}
