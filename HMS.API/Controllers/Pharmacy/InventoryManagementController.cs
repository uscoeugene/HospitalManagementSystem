using System;
using System.Threading.Tasks;
using HMS.API.Application.Pharmacy;
using HMS.API.Domain.Pharmacy;
using HMS.API.Security;
using HMS.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace HMS.API.Controllers.Pharmacy
{
    [ApiController]
    [Route("pharmacy/management")]
    public class InventoryManagementController : ControllerBase
    {
        private readonly HmsDbContext _db;
        public InventoryManagementController(HmsDbContext db)
        {
            _db = db;
        }

        [HttpPost("batches")]
        [HasPermission("pharmacy.inventory.manage")]
        public async Task<IActionResult> CreateBatch([FromBody] CreateBatchRequest req)
        {
            var item = await _db.InventoryItems.SingleOrDefaultAsync(i => i.Id == req.ItemId);
            if (item == null) return BadRequest(new { error = "Item not found" });
            var store = await _db.Stores.SingleOrDefaultAsync(s => s.Id == req.StoreId);
            if (store == null) return BadRequest(new { error = "Store not found" });

            var batch = new InventoryBatch
            {
                ItemId = item.Id,
                StoreId = store.Id,
                BatchNumber = req.BatchNumber ?? string.Empty,
                ExpiryDate = req.ExpiryDate,
                ManufactureDate = req.ManufactureDate,
                PurchasePrice = req.PurchasePrice,
                SellingPrice = req.SellingPrice,
                ReceivedQty = req.Quantity,
                AvailableQty = req.Quantity
            };

            _db.InventoryBatches.Add(batch);

            // create stock transaction
            var tx = new StockTransaction
            {
                ItemId = item.Id,
                Batch = batch,
                StoreId = store.Id,
                TransactionType = StockTransactionType.PURCHASE,
                Quantity = req.Quantity,
                UnitCost = req.PurchasePrice,
                ReferenceType = "purchase",
                ReferenceId = null,
                CreatedBy = Guid.Empty
            };
            _db.StockTransactions.Add(tx);

            // update item stock
            item.Stock += req.Quantity;

            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBatch), new { id = batch.Id }, batch);
        }

        [HttpGet("batches/{id}")]
        [HasPermission("pharmacy.view")]
        public async Task<IActionResult> GetBatch(Guid id)
        {
            var batch = await _db.InventoryBatches.Include(b => b.Item).Include(b => b.Store).AsNoTracking().SingleOrDefaultAsync(b => b.Id == id);
            if (batch == null) return NotFound();
            return Ok(batch);
        }

        [HttpGet("ledger")]
        [HasPermission("pharmacy.view")]
        public async Task<IActionResult> GetLedger([FromQuery] Guid? itemId)
        {
            var q = _db.StockTransactions.AsNoTracking().Include(s => s.Item).Include(s => s.Batch).Include(s => s.Store).OrderByDescending(s => s.Date).AsQueryable();
            if (itemId.HasValue) q = q.Where(s => s.ItemId == itemId.Value);
            var list = await q.Take(200).ToArrayAsync();
            return Ok(list);
        }
    }

    public class CreateBatchRequest
    {
        public Guid ItemId { get; set; }
        public Guid StoreId { get; set; }
        public string? BatchNumber { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public DateOnly? ManufactureDate { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int Quantity { get; set; }
    }
}
