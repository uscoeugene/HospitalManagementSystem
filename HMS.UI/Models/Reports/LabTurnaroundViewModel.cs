using System;

namespace HMS.UI.Models.Reports
{
    public class LabTurnaroundViewModel
    {
        public Guid LabRequestId { get; set; }
        public double TurnaroundHours { get; set; }
    }
}
