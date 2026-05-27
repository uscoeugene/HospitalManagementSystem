using System.Threading.Tasks;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Infrastructure.Auth;
using HMS.API.Application.Auth;

namespace HMS.API.Infrastructure.Persistence
{
    public static class HmsSeedData
    {
        public static async Task EnsureSeedDataAsync(HmsDbContext hdb, AuthDbContext authDb, IPasswordHasher hasher)
        {
            // Tenants and local auth entities are managed by AuthDbContext.
            // No cross-copying into HmsDbContext to avoid duplicated tables/migrations.

            // Seed some sample data for development: patients, visits, lab tests/requests, inventory, drugs, invoices
            if (!hdb.Patients.Any())
            {
                var p1 = new HMS.API.Domain.Patient.Patient { FirstName = "John", LastName = "Doe", DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)), Gender = "Male", MedicalRecordNumber = "MRN-1001" };
                var p2 = new HMS.API.Domain.Patient.Patient { FirstName = "Jane", LastName = "Smith", DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25)), Gender = "Female", MedicalRecordNumber = "MRN-1002" };
                hdb.Patients.AddRange(p1, p2);
                await hdb.SaveChangesAsync();

                // visits
                var v1 = new HMS.API.Domain.Patient.Visit { PatientId = p1.Id, VisitAt = DateTimeOffset.UtcNow.AddDays(-5), VisitType = "Outpatient" };
                var v2 = new HMS.API.Domain.Patient.Visit { PatientId = p2.Id, VisitAt = DateTimeOffset.UtcNow.AddDays(-2), VisitType = "Emergency" };
                hdb.Visits.AddRange(v1, v2);
                await hdb.SaveChangesAsync();

                // lab tests
                var t1 = new HMS.API.Domain.Lab.LabTest { Code = "CBC", Name = "Complete Blood Count", Price = 12.5m, Currency = "NGN" };
                var t2 = new HMS.API.Domain.Lab.LabTest { Code = "LFT", Name = "Liver Function Test", Price = 25m, Currency = "NGN" };
                hdb.LabTests.AddRange(t1, t2);
                await hdb.SaveChangesAsync();

                // sample lab request
                var lr = new HMS.API.Domain.Lab.LabRequest { PatientId = p1.Id, VisitId = v1.Id, RequestNumber = "LR-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss"), Status = HMS.API.Domain.Lab.LabRequestStatus.CHARGED };
                var lri = new HMS.API.Domain.Lab.LabRequestItem { LabTestId = t1.Id, Price = t1.Price, Currency = t1.Currency };
                lr.Items.Add(lri);
                hdb.LabRequests.Add(lr);
                await hdb.SaveChangesAsync();

                // inventory items
                var inv1 = new HMS.API.Domain.Pharmacy.InventoryItem { Code = "IV-001", Name = "Normal Saline 500ml", UnitPrice = 3.5m, Stock = 100, Unit = "bag" };
                var inv2 = new HMS.API.Domain.Pharmacy.InventoryItem { Code = "PARA-500", Name = "Paracetamol 500mg", UnitPrice = 0.05m, Stock = 1000, Unit = "tablet" };
                hdb.InventoryItems.AddRange(inv1, inv2);
                await hdb.SaveChangesAsync();

                // legacy drug catalog removed - inventory items seeded above replace drug catalog

                // create sample invoice for lr
                var invoice = new HMS.API.Domain.Billing.Invoice { PatientId = p1.Id, VisitId = v1.Id, InvoiceNumber = "INV-INIT-" + Guid.NewGuid().ToString("N").Substring(0,6).ToUpper(), Status = HMS.API.Domain.Billing.InvoiceStatus.UNPAID, TotalAmount = lri.Price, AmountPaid = 0m, Currency = lri.Currency };
                var ii = new HMS.API.Domain.Billing.InvoiceItem { Description = t1.Name, UnitPrice = lri.Price, Quantity = 1 };
                invoice.Items.Add(ii);
                hdb.Invoices.Add(invoice);
                await hdb.SaveChangesAsync();
            }
        }
    }
}
