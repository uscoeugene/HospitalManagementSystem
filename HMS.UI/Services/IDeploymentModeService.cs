using System.Threading.Tasks;

namespace HMS.UI.Services
{
    public interface IDeploymentModeService
    {
        Task<string> GetEffectiveModeAsync();
    }
}
