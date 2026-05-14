using System.Threading.Tasks;

namespace HMS.API.Application.Common
{
    public interface IAppSettingsService
    {
        Task<string?> GetAsync(string key);
        Task SetAsync(string key, string value);
        Task InvalidateAsync(string key);
    }
}
