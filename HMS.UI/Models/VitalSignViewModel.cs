using System;

namespace HMS.UI.Models
{
    public class VitalSignViewModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid VisitId { get; set; }
        public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
        public decimal? Temperature { get; set; }
        public int? PulseRate { get; set; }
        public int? RespiratoryRate { get; set; }
        public int? SystolicBP { get; set; }
        public int? DiastolicBP { get; set; }
        public int? OxygenSaturation { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? BMI { get; set; }
        public decimal? BloodSugar { get; set; }
        public string? Notes { get; set; }
        public Guid? RecordedByUserId { get; set; }
    }
}
