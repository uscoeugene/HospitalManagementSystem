using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using HMS.API.Application.Common;
using HMS.API.Application.Payments;
using HMS.API.Application.Payments.DTOs;
using HMS.API.Domain.Billing;
using HMS.API.Domain.Payments;
using HMS.API.Domain.Common;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HMS.API.IntegrationTests
{
    public class PaymentServiceUnitTests
    {
        private HmsDbContext CreateInMemoryDb(string name)
        {
            var options = new DbContextOptionsBuilder<HmsDbContext>().UseInMemoryDatabase(name).Options;
            return new HmsDbContext(options, new TestCurrentUserService());
        }

        private class TestCurrentUserService : ICurrentUserService
        {
            public Guid? UserId { get; set; } = Guid.NewGuid();
        }

        [Fact]
        public async Task CreatePayment_WhenInvoiceMissing_Throws()
        {
            var db = CreateInMemoryDb("pm_missing_inv");
            var svc = new PaymentService(db, new TestCurrentUserService());

            var req = new CreatePaymentRequest { InvoiceId = Guid.NewGuid(), PatientId = Guid.NewGuid(), Amount = 50m };
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreatePaymentAsync(req));
        }

        [Fact]
        public async Task CreatePayment_UpdatesInvoiceStatusToPaid_WhenAmountCoversTotal()
        {
            var db = CreateInMemoryDb("pm_invoice_paid");

            var invoice = new Invoice { PatientId = Guid.NewGuid(), InvoiceNumber = "INV-TEST", TotalAmount = 100m, AmountPaid = 0m, Status = InvoiceStatus.UNPAID };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            var currentUser = new TestCurrentUserService();
            var svc = new PaymentService(db, currentUser);

            var req = new CreatePaymentRequest { InvoiceId = invoice.Id, PatientId = invoice.PatientId, Amount = 100m };
            var payment = await svc.CreatePaymentAsync(req);

            payment.Should().NotBeNull();
            var invAfter = await db.Invoices.FindAsync(invoice.Id);
            invAfter.Should().NotBeNull();
            invAfter!.AmountPaid.Should().Be(100m);
            invAfter.Status.Should().Be(InvoiceStatus.PAID);
        }

        [Fact]
        public async Task CreateRefund_WhenPaymentNotConfirmed_Throws()
        {
            var db = CreateInMemoryDb("pm_refund_edge");

            var invoice = new Invoice { PatientId = Guid.NewGuid(), InvoiceNumber = "INV-TEST2", TotalAmount = 200m, AmountPaid = 0m, Status = InvoiceStatus.UNPAID };
            db.Invoices.Add(invoice);

            var payment = new Payment { InvoiceId = invoice.Id, PatientId = invoice.PatientId, Amount = 50m, Currency = "USD", Status = PaymentStatus.PENDING, CreatedByUserId = Guid.NewGuid() };
            db.Payments.Add(payment);

            await db.SaveChangesAsync();

            var svc = new PaymentService(db, new TestCurrentUserService());

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateRefundAsync(payment.Id, new RefundRequest { Amount = 10m, Reason = "test" }));
        }

        [Fact]
        public async Task CreateRefund_And_Reversal_Workflow()
        {
            var db = CreateInMemoryDb("pm_refund_and_reverse");

            var invoice = new Invoice { PatientId = Guid.NewGuid(), InvoiceNumber = "INV-TEST3", TotalAmount = 150m, AmountPaid = 0m, Status = InvoiceStatus.UNPAID };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            var svc = new PaymentService(db, new TestCurrentUserService());

            // create payment (confirmed)
            var payReq = new CreatePaymentRequest { InvoiceId = invoice.Id, PatientId = invoice.PatientId, Amount = 150m };
            var paymentDto = await svc.CreatePaymentAsync(payReq);
            paymentDto.Should().NotBeNull();

            var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == paymentDto.Id);
            payment.Should().NotBeNull();
            payment!.Status.Should().Be(PaymentStatus.CONFIRMED);

            // create refund
            var refundDto = await svc.CreateRefundAsync(payment.Id, new RefundRequest { Amount = 50m, Reason = "Overcharge" });
            refundDto.Should().NotBeNull();

            var refund = await db.Refunds.SingleOrDefaultAsync(r => r.Id == refundDto.Id);
            refund.Should().NotBeNull();
            refund!.IsReversed.Should().BeFalse();

            // outbox message should exist
            var outbox = await db.OutboxMessages.Where(o => o.Type == "PaymentRefunded" || o.Type == "PaymentRefunded" ).ToListAsync();
            outbox.Should().NotBeEmpty();

            // reverse refund
            var reversalDto = await svc.CreateRefundReversalAsync(refund.Id, new RefundReversalRequest { Reason = "Customer returned" });
            reversalDto.Should().NotBeNull();

            var refundAfter = await db.Refunds.SingleOrDefaultAsync(r => r.Id == refund.Id);
            refundAfter!.IsReversed.Should().BeTrue();
            refundAfter.ReversedAt.Should().NotBeNull();

            // refund reversal outbox message exists
            var outbox2 = await db.OutboxMessages.Where(o => o.Type == "RefundReversed").ToListAsync();
            outbox2.Should().NotBeEmpty();
        }
    }
}