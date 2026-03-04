using System.Threading.Tasks;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Infrastructure.Auth;
using HMS.API.Application.Auth;

namespace HMS.API.Infrastructure.Persistence
{
    public static class HmsSeedData
    {
        public static async Task EnsureSeedDataAsync(HmsDbContext hdb, AuthDbContext authDb, IPasswordHasher hasher)
        {
            // Tenants and local auth entities are managed by AuthDbContext.
            // No cross-copying into HmsDbContext to avoid duplicated tables/migrations.

            // Place HMS-specific seed logic here if needed (e.g., sample invoices, drugs etc.).
            await Task.CompletedTask;
        }
    }
}
