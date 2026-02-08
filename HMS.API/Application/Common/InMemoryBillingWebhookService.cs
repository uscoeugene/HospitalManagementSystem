using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HMS.API.Application.Common
{
    public class InMemoryBillingWebhookService : IBillingWebhookService
    {
        private readonly ConcurrentQueue<BillingWebhookEvent> _events = new();

        public void AddEvent(BillingWebhookEvent ev)
        {
            _events.Enqueue(ev);
        }

        public IEnumerable<BillingWebhookEvent> ListEvents()
        {
            return _events.ToArray();
        }
    }
}
