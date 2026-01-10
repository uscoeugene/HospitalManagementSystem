using System;

namespace HMS.API.Application.Pharmacy.Events
{
    public class PrescriptionChargedEvent
    {
        public Guid PrescriptionId { get; set; }
        public Guid InvoiceId { get; set; }
        public Guid PatientId { get; set; }
    }
}