using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Pharmacy.DTOs;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Domain.Pharmacy;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Controllers.Pharmacy
{
    [ApiController]
    [Route("pharmacy/procurement")]
    public class ProcurementController : ControllerBase
    {
        private readonly HmsDbContext _db;

        public ProcurementController(HmsDbContext db)
        {
            _db = db;
        }

        [HttpPost("orders")]
        [HasPermission("pharmacy.procurement.manage")]
        public async Task<IActionResult> CreateOrder([FromBody] CreatePurchaseOrderRequest req)
        {
            var supplier = await _db.Suppliers.SingleOrDefaultAsync(s => s.Id == req.SupplierId);
            if (supplier == null) return BadRequest(new { error = "Supplier not found" });

            var po = new PurchaseOrder { SupplierId = supplier.Id, OrderDate = req.OrderDate ?? DateTimeOffset.UtcNow };
            foreach (var li in req.Items)
            {
                po.Items.Add(new PurchaseOrderLine { ItemId = li.ItemId, Quantity = li.Quantity, UnitId = li.UnitId, UnitPrice = li.UnitPrice });
            }

            _db.PurchaseOrders.Add(po);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetOrder), new { id = po.Id }, po);
        }

        [HttpGet("orders/{id}")]
        [HasPermission("pharmacy.view")]
        public async Task<IActionResult> GetOrder(Guid id)
        {
            var po = await _db.PurchaseOrders.Include(p => p.Items).ThenInclude(li => li.Item).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
            if (po == null) return NotFound();
            return Ok(po);
        }

        [HttpPost("orders/{id}/receive")]
        [HasPermission("pharmacy.procurement.manage")]
        public async Task<IActionResult> ReceiveOrder(Guid id, [FromBody] ReceivePurchaseOrderRequest req)
        {
            var po = await _db.PurchaseOrders.Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == id);
            if (po == null) return NotFound();

            foreach (var r in req.ReceivedLines)
            {
                var line = po.Items.SingleOrDefault(l => l.Id == r.PurchaseOrderLineId);
                if (line == null) continue;

                // create batch and stock transaction
                var batch = new InventoryBatch
                {
                    ItemId = line.ItemId,
                    StoreId = r.StoreId,
                    BatchNumber = r.BatchNumber ?? string.Empty,
                    ExpiryDate = r.ExpiryDate,
                    ManufactureDate = r.ManufactureDate,
                    PurchasePrice = r.PurchasePrice,
                    SellingPrice = r.SellingPrice,
                    ReceivedQty = r.Quantity,
                    AvailableQty = r.Quantity
                };
                _db.InventoryBatches.Add(batch);

                var tx = new StockTransaction
                {
                    ItemId = line.ItemId,
                    Batch = batch,
                    StoreId = r.StoreId,
                    TransactionType = StockTransactionType.PURCHASE,
                    Quantity = r.Quantity,
                    UnitCost = r.PurchasePrice,
                    ReferenceType = "purchase",
                    ReferenceId = po.Id,
                    CreatedBy = Guid.Empty
                };
                _db.StockTransactions.Add(tx);

                // update item stock (legacy field kept for compatibility) - derived stock should be computed from ledger in future
                var item = await _db.InventoryItems.SingleOrDefaultAsync(i => i.Id == line.ItemId);
                if (item != null)
                {
                    item.Stock += r.Quantity;
                }
            }

            po.Status = PurchaseOrderStatus.RECEIVED;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
