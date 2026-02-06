using System;

namespace HMS.API.Application.Lab
{
    public class LabStatusBreakdownDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class LabTurnaroundDto
    {
        public Guid LabRequestId { get; set; }
        public double TurnaroundHours { get; set; }
    }

    public class TopTestDto
    {
        public Guid TestId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public int Requests { get; set; }
    }
}
