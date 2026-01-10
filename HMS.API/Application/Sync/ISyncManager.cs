using System.Threading.Tasks;

namespace HMS.API.Application.Sync
{
    public interface ISyncManager
    {
        Task RunOnceAsync(System.Threading.CancellationToken cancellationToken = default);
        Task TriggerAsync();
    }
}