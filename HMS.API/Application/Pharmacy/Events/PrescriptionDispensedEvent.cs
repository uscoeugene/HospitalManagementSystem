using System;

namespace HMS.API.Application.Pharmacy.Events
{
    public class PrescriptionDispensedEvent
    {
        public Guid PrescriptionId { get; set; }
        public Guid PrescriptionItemId { get; set; }
        public Guid DispensedBy { get; set; }
        public int Quantity { get; set; }
        public DateTimeOffset DispensedAt { get; set; }
    }
}