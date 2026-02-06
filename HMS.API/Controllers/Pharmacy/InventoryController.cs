using System;
using System.Threading.Tasks;
using HMS.API.Application.Pharmacy;
using HMS.API.Application.Pharmacy.DTOs;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers.Pharmacy
{
    [ApiController]
    [Route("pharmacy/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventory;

        public InventoryController(IInventoryService inventory)
        {
            _inventory = inventory;
        }

        [HttpGet]
        [HasPermission("pharmacy.view")]
        public async Task<IActionResult> List([FromQuery] string? category)
        {
            var items = await _inventory.ListAsync(category);
            return Ok(items);
        }

        [HttpGet("{id}")]
        [HasPermission("pharmacy.view")]
        public async Task<IActionResult> Get(Guid id)
        {
            var it = await _inventory.GetAsync(id);
            if (it == null) return NotFound();
            return Ok(it);
        }

        [HttpPost]
        [HasPermission("pharmacy.inventory.manage")]
        public async Task<IActionResult> Create([FromBody] CreateInventoryItemRequest req)
        {
            var it = await _inventory.CreateAsync(req);
            return CreatedAtAction(nameof(Get), new { id = it.Id }, it);
        }

        [HttpPut("{id}")]
        [HasPermission("pharmacy.inventory.manage")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInventoryItemRequest req)
        {
            await _inventory.UpdateAsync(id, req);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [HasPermission("pharmacy.delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _inventory.DeleteAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/adjust-stock")]
        [HasPermission("pharmacy.inventory.manage")]
        public async Task<IActionResult> AdjustStock(Guid id, [FromBody] AdjustStockRequest req)
        {
            await _inventory.AdjustStockAsync(id, req.Delta);
            return NoContent();
        }
    }

    public class AdjustStockRequest { public int Delta { get; set; } }
}
