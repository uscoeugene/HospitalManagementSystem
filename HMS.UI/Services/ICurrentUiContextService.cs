using System;

namespace HMS.UI.Services
{
    public interface ICurrentUiContextService
    {
        Guid? GetCurrentTenantId();
    }
}
