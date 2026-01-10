using System;
using System.Threading.Tasks;

namespace HMS.API.Application.Sync
{
    public interface ICloudSyncClient
    {
        Task PushAsync(string entityName, object[] records);
        Task<object[]> PullAsync(string entityName, DateTimeOffset? since);
    }
}