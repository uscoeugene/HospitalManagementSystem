using System;

namespace HMS.UI.Models.Reports
{
    public class LabTurnaroundPageViewModel
    {
        public int Recent { get; set; }
        public LabTurnaroundViewModel[] Items { get; set; } = Array.Empty<LabTurnaroundViewModel>();
        public double AverageHours { get; set; }
        public double MinHours { get; set; }
        public double MaxHours { get; set; }
    }
}
