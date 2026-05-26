using System;
using System.Collections.Generic;

namespace HMS.UI.Models.Lab
{
    public class LabRequestCreateViewModel
    {
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public HMS.UI.Models.PatientDetailsViewModel? Patient { get; set; }
        public LabTestViewModel[] AvailableTests { get; set; } = Array.Empty<LabTestViewModel>();
        public Guid[] SelectedTestIds { get; set; } = Array.Empty<Guid>();
        public bool AllowOnCredit { get; set; }
        public string? CreditReason { get; set; }
    }
}
