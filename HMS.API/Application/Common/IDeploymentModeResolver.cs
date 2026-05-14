using System.Threading.Tasks;

namespace HMS.API.Application.Common
{
    public enum DeploymentMode { Online, OnPrem }

    public interface IDeploymentModeResolver
    {
        Task<DeploymentMode> GetModeAsync();
    }
}
