using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HMS.API.Infrastructure.Persistence.Migrations
{
    public partial class AddReportingIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Visits_PatientId",
                table: "Visits",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_VisitAt",
                table: "Visits",
                column: "VisitAt");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreatedAt",
                table: "Invoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId",
                table: "InvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_InvoiceId",
                table: "InvoicePayments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_PaidAt",
                table: "InvoicePayments",
                column: "PaidAt");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_DrugId",
                table: "PrescriptionItems",
                column: "DrugId");

            migrationBuilder.CreateIndex(
                name: "IX_DispenseLogs_DispensedAt",
                table: "DispenseLogs",
                column: "DispensedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_CreatedAt",
                table: "Refunds",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_Status",
                table: "LabRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_CreatedAt",
                table: "LabRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequestItems_LabTestId",
                table: "LabRequestItems",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_Code",
                table: "Drugs",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UpdatedAt",
                table: "UserProfiles",
                column: "UpdatedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Visits_PatientId", table: "Visits");
            migrationBuilder.DropIndex(name: "IX_Visits_VisitAt", table: "Visits");
            migrationBuilder.DropIndex(name: "IX_Invoices_CreatedAt", table: "Invoices");
            migrationBuilder.DropIndex(name: "IX_Invoices_Status", table: "Invoices");
            migrationBuilder.DropIndex(name: "IX_InvoiceItems_InvoiceId", table: "InvoiceItems");
            migrationBuilder.DropIndex(name: "IX_InvoicePayments_InvoiceId", table: "InvoicePayments");
            migrationBuilder.DropIndex(name: "IX_InvoicePayments_PaidAt", table: "InvoicePayments");
            migrationBuilder.DropIndex(name: "IX_PrescriptionItems_DrugId", table: "PrescriptionItems");
            migrationBuilder.DropIndex(name: "IX_DispenseLogs_DispensedAt", table: "DispenseLogs");
            migrationBuilder.DropIndex(name: "IX_Refunds_CreatedAt", table: "Refunds");
            migrationBuilder.DropIndex(name: "IX_LabRequests_Status", table: "LabRequests");
            migrationBuilder.DropIndex(name: "IX_LabRequests_CreatedAt", table: "LabRequests");
            migrationBuilder.DropIndex(name: "IX_LabRequestItems_LabTestId", table: "LabRequestItems");
            migrationBuilder.DropIndex(name: "IX_Drugs_Code", table: "Drugs");
            migrationBuilder.DropIndex(name: "IX_UserProfiles_UpdatedAt", table: "UserProfiles");
        }
    }
}
