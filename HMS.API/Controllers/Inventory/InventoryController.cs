using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Pharmacy.DTOs;
using HMS.API.Application.Pharmacy;
using HMS.API.Domain.Pharmacy;
using HMS.API.Security;
using HMS.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Controllers.Inventory
{
    [ApiController]
    [Route("inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly HmsDbContext _db;

        public InventoryController(HmsDbContext db)
        {
            _db = db;
        }

        [HttpPost("items")]
        [HasPermission("inventory.manage")]
        public async Task<IActionResult> CreateItem([FromBody] CreateInventoryItemRequest req)
        {
            var it = new InventoryItem
            {
                Code = req.Code,
                Name = req.Name,
                Description = req.Description,
                UnitPrice = req.UnitPrice,
                Currency = req.Currency,
                Stock = req.Stock,
                Unit = req.Unit
            };
            if (req.CategoryId.HasValue) it.CategoryId = req.CategoryId.Value;
            _db.InventoryItems.Add(it);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetItem), new { id = it.Id }, it);
        }

        [HttpGet("items")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> ListItems([FromQuery] string? search, [FromQuery] string? category)
        {
            var q = _db.InventoryItems.AsNoTracking().Where(i => !i.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(i => i.Name.Contains(search) || i.Code.Contains(search));
            if (!string.IsNullOrWhiteSpace(category)) q = q.Where(i => i.Category != null && i.Category.Name == category);
            var list = await q.ToArrayAsync();
            return Ok(list);
        }

        [HttpGet("items/{id}")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> GetItem(Guid id)
        {
            var it = await _db.InventoryItems.AsNoTracking().Include(i => i.Category).Include(i => i.BaseUnit).SingleOrDefaultAsync(i => i.Id == id);
            if (it == null) return NotFound();
            return Ok(it);
        }

        [HttpPut("items/{id}")]
        [HasPermission("inventory.manage")]
        public async Task<IActionResult> UpdateItem(Guid id, [FromBody] UpdateInventoryItemRequest req)
        {
            var it = await _db.InventoryItems.SingleOrDefaultAsync(i => i.Id == id);
            if (it == null) return NotFound();
            if (req.Code != null) it.Code = req.Code;
            if (req.Name != null) it.Name = req.Name;
            if (req.Description != null) it.Description = req.Description;
            if (req.UnitPrice.HasValue) it.UnitPrice = req.UnitPrice.Value;
            if (req.Currency != null) it.Currency = req.Currency;
            if (req.CategoryId.HasValue) it.CategoryId = req.CategoryId.Value;
            if (req.Unit != null) it.Unit = req.Unit;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Batches for an item
        [HttpGet("items/{id}/batches")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> GetItemBatches(Guid id)
        {
            var list = await _db.InventoryBatches.AsNoTracking().Include(b => b.Store).Where(b => b.ItemId == id).ToArrayAsync();
            var dto = list.Select(b => new BatchDto
            {
                Id = b.Id,
                ItemId = b.ItemId,
                StoreId = b.StoreId,
                BatchNumber = b.BatchNumber ?? string.Empty,
                ExpiryDate = b.ExpiryDate,
                ManufactureDate = b.ManufactureDate,
                ReceivedQty = b.ReceivedQty,
                AvailableQty = b.AvailableQty,
                PurchasePrice = b.PurchasePrice,
                SellingPrice = b.SellingPrice
            }).ToArray();

            return Ok(dto);
        }

        [HttpGet("batches/{batchId}")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> GetBatch(Guid batchId)
        {
            var b = await _db.InventoryBatches.AsNoTracking().Include(x => x.Store).Include(x => x.Item).SingleOrDefaultAsync(x => x.Id == batchId);
            if (b == null) return NotFound();
            var dto = new BatchDto
            {
                Id = b.Id,
                ItemId = b.ItemId,
                StoreId = b.StoreId,
                BatchNumber = b.BatchNumber ?? string.Empty,
                ExpiryDate = b.ExpiryDate,
                ManufactureDate = b.ManufactureDate,
                ReceivedQty = b.ReceivedQty,
                AvailableQty = b.AvailableQty,
                PurchasePrice = b.PurchasePrice,
                SellingPrice = b.SellingPrice
            };
            return Ok(dto);
        }

        // Stock lookup across stores or by filters
        [HttpGet("stock")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> StockLookup([FromQuery] Guid? storeId, [FromQuery] Guid? itemId)
        {
            var q = _db.InventoryBatches.AsNoTracking().Include(b => b.Store).Where(b => !b.IsDeleted);
            if (storeId.HasValue) q = q.Where(b => b.StoreId == storeId.Value);
            if (itemId.HasValue) q = q.Where(b => b.ItemId == itemId.Value);

            var list = await q.GroupBy(b => new { b.StoreId, StoreName = b.Store != null ? b.Store.StoreName : string.Empty })
                              .Select(g => new StockLookupDto { StoreId = g.Key.StoreId, StoreName = g.Key.StoreName ?? string.Empty, Quantity = g.Sum(b => b.AvailableQty) })
                              .ToArrayAsync();

            return Ok(list);
        }

        [HttpGet("items/{id}/stock")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> ItemStockAcrossStores(Guid id)
        {
            var list = await _db.InventoryBatches.AsNoTracking().Include(b => b.Store).Where(b => b.ItemId == id && !b.IsDeleted)
                              .GroupBy(b => new { b.StoreId, StoreName = b.Store != null ? b.Store.StoreName : string.Empty })
                              .Select(g => new StockLookupDto { StoreId = g.Key.StoreId, StoreName = g.Key.StoreName ?? string.Empty, Quantity = g.Sum(b => b.AvailableQty) })
                              .ToArrayAsync();

            return Ok(list);
        }

        // Expiring batches report
        [HttpGet("reports/expiring")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> ExpiringBatches([FromQuery] int days = 90)
        {
            var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(days));
            var q = _db.InventoryBatches.AsNoTracking().Where(b => b.ExpiryDate.HasValue && b.ExpiryDate.Value <= cutoff);
            var list = await q.Select(b => new BatchDto
            {
                Id = b.Id,
                ItemId = b.ItemId,
                StoreId = b.StoreId,
                BatchNumber = b.BatchNumber ?? string.Empty,
                ExpiryDate = b.ExpiryDate,
                ManufactureDate = b.ManufactureDate,
                ReceivedQty = b.ReceivedQty,
                AvailableQty = b.AvailableQty,
                PurchasePrice = b.PurchasePrice,
                SellingPrice = b.SellingPrice
            }).ToArrayAsync();

            return Ok(list);
        }

        // Stock transactions ledger
        [HttpGet("transactions")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> Transactions([FromQuery] Guid? itemId, [FromQuery] Guid? batchId, [FromQuery] Guid? storeId)
        {
            var q = _db.StockTransactions.AsNoTracking().Include(st => st.Batch).Include(st => st.Store).Include(st => st.Item).Where(st => !st.IsDeleted);
            if (itemId.HasValue) q = q.Where(st => st.ItemId == itemId.Value);
            if (batchId.HasValue) q = q.Where(st => st.BatchId == batchId.Value);
            if (storeId.HasValue) q = q.Where(st => st.StoreId == storeId.Value);

            var list = await q.OrderByDescending(st => st.Date).Select(st => new
            {
                st.Id,
                st.ItemId,
                ItemName = st.Item != null ? st.Item.Name : string.Empty,
                st.BatchId,
                st.StoreId,
                StoreName = st.Store != null ? st.Store.StoreName : string.Empty,
                TransactionType = st.TransactionType.ToString(),
                st.Quantity,
                st.UnitCost,
                st.Date,
                st.ReferenceType,
                st.ReferenceId,
                st.CreatedBy
            }).ToArrayAsync();

            return Ok(list);
        }

        [HttpDelete("items/{id}")]
        [HasPermission("inventory.manage")]
        public async Task<IActionResult> DeleteItem(Guid id)
        {
            var it = await _db.InventoryItems.SingleOrDefaultAsync(i => i.Id == id);
            if (it == null) return NotFound();
            it.SoftDelete();
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Units
        [HttpPost("units")]
        [HasPermission("inventory.manage")]
        public async Task<IActionResult> CreateUnit([FromBody] CreateUnitRequest req)
        {
            var u = new HMS.API.Domain.Pharmacy.UnitOfMeasure { Code = req.Code, Name = req.Name };
            _db.Units.Add(u);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUnit), new { id = u.Id }, u);
        }

        [HttpGet("units")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> ListUnits()
        {
            return Ok(await _db.Units.AsNoTracking().ToArrayAsync());
        }

        [HttpGet("units/{id}")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> GetUnit(Guid id)
        {
            var u = await _db.Units.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound();
            return Ok(u);
        }

        // Conversions
        [HttpPost("items/{id}/conversions")]
        [HasPermission("inventory.manage")]
        public async Task<IActionResult> AddConversion(Guid id, [FromBody] CreateConversionRequest req)
        {
            var item = await _db.InventoryItems.SingleOrDefaultAsync(i => i.Id == id);
            if (item == null) return NotFound();
            var conv = new HMS.API.Domain.Pharmacy.ItemUnitConversion { ItemId = id, UnitId = req.UnitId, BaseUnitQty = req.BaseQty };
            _db.ItemUnitConversions.Add(conv);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetConversions), new { id = id }, conv);
        }

        [HttpGet("items/{id}/conversions")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> GetConversions(Guid id)
        {
            var list = await _db.ItemUnitConversions.AsNoTracking().Where(c => c.ItemId == id).ToArrayAsync();
            return Ok(list);
        }

        // Stores
        [HttpPost("stores")]
        [HasPermission("inventory.manage")]
        public async Task<IActionResult> CreateStore([FromBody] CreateStoreRequest req)
        {
            var s = new HMS.API.Domain.Pharmacy.Store { StoreName = req.StoreName, StoreType = req.StoreType, DepartmentId = req.DepartmentId };
            _db.Stores.Add(s);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetStore), new { id = s.Id }, s);
        }

        [HttpGet("stores")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> ListStores()
        {
            return Ok(await _db.Stores.AsNoTracking().ToArrayAsync());
        }

        [HttpGet("stores/{id}")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> GetStore(Guid id)
        {
            var s = await _db.Stores.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            return Ok(s);
        }

        // Suppliers
        [HttpPost("suppliers")]
        [HasPermission("inventory.manage")]
        public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierRequest req)
        {
            var sp = new Supplier { SupplierName = req.SupplierName, ContactInfo = req.ContactInfo };
            _db.Suppliers.Add(sp);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetSupplier), new { id = sp.Id }, sp);
        }

        [HttpGet("suppliers")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> ListSuppliers()
        {
            return Ok(await _db.Suppliers.AsNoTracking().ToArrayAsync());
        }

        [HttpGet("suppliers/{id}")]
        [HasPermission("inventory.view")]
        public async Task<IActionResult> GetSupplier(Guid id)
        {
            var sp = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (sp == null) return NotFound();
            return Ok(sp);
        }

        // Receive goods (simplified)
        [HttpPost("receipts")]
        [HasPermission("inventory.receive")]
        public async Task<IActionResult> ReceiveGoods([FromBody] ReceiveGoodsRequest req)
        {
            foreach (var r in req.ReceivedItems)
            {
                var item = await _db.InventoryItems.SingleOrDefaultAsync(i => i.Id == r.ItemId);
                if (item == null) continue;
                var batch = new InventoryBatch
                {
                    ItemId = r.ItemId,
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

                var st = new StockTransaction
                {
                    ItemId = r.ItemId,
                    Batch = batch,
                    StoreId = r.StoreId,
                    TransactionType = StockTransactionType.PURCHASE,
                    Quantity = r.Quantity,
                    UnitCost = r.PurchasePrice,
                    ReferenceType = req.PurchaseOrderId.HasValue ? "purchase" : "goods_receipt",
                    ReferenceId = req.PurchaseOrderId
                };
                _db.StockTransactions.Add(st);

                item.Stock += r.Quantity;
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
