using System.Collections.Generic;
using System.Threading.Tasks;

namespace HMS.API.Application.Common
{
    public interface INotificationService
    {
        Task NotifyAsync(string channel, object payload);
        Task<IEnumerable<string>> GetRecentAsync();
    }
}