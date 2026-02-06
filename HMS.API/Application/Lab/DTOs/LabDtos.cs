using System;
using System.Collections.Generic;

namespace HMS.API.Application.Lab.DTOs
{
    public class LabTestDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
    }

    public class CreateLabRequestItem
    {
        public Guid LabTestId { get; set; }
    }

    public class CreateLabRequest
    {
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public List<CreateLabRequestItem> Items { get; set; } = new();

        // Allow creating lab requests and charging items on credit (create debts) when true.
        public bool AllowOnCredit { get; set; } = false;
        public string? CreditReason { get; set; }
    }

    public class LabRequestDto
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public string Status { get; set; } = string.Empty;
        public IEnumerable<LabTestDto> Tests { get; set; } = Array.Empty<LabTestDto>();
    }
}