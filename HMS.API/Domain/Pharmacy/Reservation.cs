using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class Reservation : BaseEntity
    {
        public Guid DrugId { get; set; }
        public int Quantity { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public Guid? PrescriptionItemId { get; set; }
        public bool Processed { get; set; } = false;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}