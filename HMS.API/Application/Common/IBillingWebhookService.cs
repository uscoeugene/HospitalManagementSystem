using System.Collections.Generic;

namespace HMS.API.Application.Common
{
    public interface IBillingWebhookService
    {
        void AddEvent(BillingWebhookEvent ev);
        IEnumerable<BillingWebhookEvent> ListEvents();
    }

    public record BillingWebhookEvent(string EventType, string Payload, System.DateTimeOffset ReceivedAt);
}
