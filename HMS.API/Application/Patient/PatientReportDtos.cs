using System;
using HMS.API.Application.Reporting;

namespace HMS.API.Application.Patient
{
    public class PatientSummaryReportDto : ReportDtoBase
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public DateTimeOffset DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string MedicalRecordNumber { get; set; } = string.Empty;
        public int VisitCount { get; set; }
        public DateTimeOffset? LastVisit { get; set; }
    }
}
