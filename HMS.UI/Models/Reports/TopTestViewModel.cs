using System;

namespace HMS.UI.Models.Reports
{
    public class TopTestViewModel
    {
        public Guid TestId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public int Requests { get; set; }
    }
}
