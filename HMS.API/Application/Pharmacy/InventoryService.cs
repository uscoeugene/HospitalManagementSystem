using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HMS.API.Application.Pharmacy.DTOs;
using HMS.API.Domain.Pharmacy;
using System.Collections.Generic;
using HMS.API.Application.Common;

namespace HMS.API.Application.Pharmacy
{
    public interface IInventoryService
    {
        Task<InventoryItemDto[]> ListAsync(string? category = null);
        Task<InventoryItemDto> CreateAsync(CreateInventoryItemRequest req);
        Task<InventoryItemDto?> GetAsync(Guid id);
        Task UpdateAsync(Guid id, UpdateInventoryItemRequest req);
        Task DeleteAsync(Guid id);
        Task AdjustStockAsync(Guid id, int delta);
    }

    public class InventoryService : IInventoryService
    {
        private readonly HmsDbContext _db;
        private readonly ICurrentUserService _currentUserService;

        public InventoryService(HmsDbContext db, ICurrentUserService currentUserService)
        {
            _db = db;
            _currentUserService = currentUserService;
        }

        public async Task<InventoryItemDto[]> ListAsync(string? category = null)
        {
            var q = _db.InventoryItems.AsNoTracking().Where(i => !i.IsDeleted);
            if (!string.IsNullOrWhiteSpace(category)) q = q.Where(i => i.Category == category);
            return await q.Select(i => new InventoryItemDto { Id = i.Id, Code = i.Code, Name = i.Name, Description = i.Description, UnitPrice = i.UnitPrice, Currency = i.Currency, Stock = i.Stock, ReservedStock = i.ReservedStock, Category = i.Category, Unit = i.Unit }).ToArrayAsync();
        }

        public async Task<InventoryItemDto> CreateAsync(CreateInventoryItemRequest req)
        {
            var it = new InventoryItem { Code = req.Code, Name = req.Name, Description = req.Description, UnitPrice = req.UnitPrice, Currency = req.Currency, Stock = req.Stock, ReservedStock = 0, Category = req.Category ?? "general", Unit = req.Unit };
            _db.InventoryItems.Add(it);
            await _db.SaveChangesAsync();
            return new InventoryItemDto { Id = it.Id, Code = it.Code, Name = it.Name, Description = it.Description, UnitPrice = it.UnitPrice, Currency = it.Currency, Stock = it.Stock, ReservedStock = it.ReservedStock, Category = it.Category, Unit = it.Unit };
        }

        public async Task<InventoryItemDto?> GetAsync(Guid id)
        {
            var it = await _db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id);
            if (it == null) return null;
            return new InventoryItemDto { Id = it.Id, Code = it.Code, Name = it.Name, Description = it.Description, UnitPrice = it.UnitPrice, Currency = it.Currency, Stock = it.Stock, ReservedStock = it.ReservedStock, Category = it.Category, Unit = it.Unit };
        }

        public async Task UpdateAsync(Guid id, UpdateInventoryItemRequest req)
        {
            var it = await _db.InventoryItems.SingleOrDefaultAsync(i => i.Id == id);
            if (it == null) throw new InvalidOperationException("Inventory item not found");
            it.Code = req.Code ?? it.Code;
            it.Name = req.Name ?? it.Name;
            it.Description = req.Description ?? it.Description;
            if (req.UnitPrice.HasValue) it.UnitPrice = req.UnitPrice.Value;
            if (!string.IsNullOrWhiteSpace(req.Currency)) it.Currency = req.Currency;
            if (!string.IsNullOrWhiteSpace(req.Category)) it.Category = req.Category;
            if (!string.IsNullOrWhiteSpace(req.Unit)) it.Unit = req.Unit;
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var it = await _db.InventoryItems.SingleOrDefaultAsync(i => i.Id == id);
            if (it == null) throw new InvalidOperationException("Inventory item not found");
            it.SoftDelete();
            await _db.SaveChangesAsync();
        }

        public async Task AdjustStockAsync(Guid id, int delta)
        {
            var it = await _db.InventoryItems.SingleOrDefaultAsync(i => i.Id == id);
            if (it == null) throw new InvalidOperationException("Inventory item not found");
            it.Stock = Math.Max(0, it.Stock + delta);
            await _db.SaveChangesAsync();
        }
    }
}
