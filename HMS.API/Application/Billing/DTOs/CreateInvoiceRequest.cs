using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HMS.API.Application.Billing.DTOs
{
    public class CreateInvoiceRequest
    {
        [Required]
        public Guid PatientId { get; set; }

        public Guid? VisitId { get; set; }

        [Required]
        public List<CreateInvoiceItemRequest> Items { get; set; } = new();
    }

    public class CreateInvoiceItemRequest
    {
        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public decimal UnitPrice { get; set; }

        [Required]
        public int Quantity { get; set; }

        public Guid? SourceId { get; set; }
        public string? SourceType { get; set; }
    }
}