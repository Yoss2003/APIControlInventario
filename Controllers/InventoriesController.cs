using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.DTO;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoriesController(IInventoryService inventoryService) : ControllerBase
    {
        private readonly IInventoryService _inventoryService = inventoryService;

        [HttpGet]
        public async Task<IActionResult> GetInventories()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);
            return Ok(await _inventoryService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetInventory(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var inventory = await _inventoryService.GetByIdAsync(id);
            if (inventory == null || inventory.CompanyId != companyId) return NotFound();

            return Ok(inventory);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutInventory(int id, [FromBody] Inventory inventory)
        {
            if (id != inventory.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            int companyId = int.Parse(companyIdHeader!);
            inventory.CompanyId = companyId;

            var existingInventory = await _inventoryService.GetByIdAsync(id);
            if (existingInventory == null || existingInventory.CompanyId != companyId) return NotFound();

            var success = await _inventoryService.UpdateAsync(inventory);
            if (!success) return BadRequest("No se pudo actualizar el inventario.");

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostInventory([FromBody] Inventory inventory)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            inventory.CompanyId = int.Parse(companyIdHeader!);
            var success = await _inventoryService.CreateAsync(inventory);
            if (!success) return BadRequest("No se pudo crear.");

            return CreatedAtAction(nameof(GetInventory), new { id = inventory.Id }, inventory);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteInventory(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var existingInventory = await _inventoryService.GetByIdAsync(id);
            if (existingInventory == null || existingInventory.CompanyId != companyId) return NotFound();

            var success = await _inventoryService.DeleteAsync(id);
            if (!success) return BadRequest("No se pudo eliminar el inventario.");

            return NoContent();
        }

        // POST: api/Inventories/Share
        [HttpPost("Share")]
        public async Task<IActionResult> ShareInventory([FromBody] ShareRequestDTO request)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var existingInventory = await _inventoryService.GetByIdAsync(request.InventoryId);
            if (existingInventory == null || existingInventory.CompanyId != companyId) return NotFound(new { mensaje = "Inventario no encontrado." });

            var result = await _inventoryService.ShareInventoryAsync(request);
            if (!result.Success) return BadRequest(new { mensaje = result.Message });

            return Ok(new { mensaje = result.Message });
        }

        // GET: api/Inventories/5/Shared
        [HttpGet("{inventoryId:int}/Shared")]
        public async Task<IActionResult> GetSharedInventories(int inventoryId)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var existingInventory = await _inventoryService.GetByIdAsync(inventoryId);
            if (existingInventory == null || existingInventory.CompanyId != companyId) return NotFound();

            var sharedList = await _inventoryService.GetSharedInventoriesAsync(inventoryId);
            return Ok(sharedList);
        }

        // DELETE: api/Inventories/Revoke/5
        [HttpDelete("Revoke/{sharedInventoryId:int}")]
        public async Task<IActionResult> RevokeAccess(int sharedInventoryId)
        {
            var success = await _inventoryService.RevokeAccessAsync(sharedInventoryId);
            if (!success) return BadRequest(new { error = "No se pudo revocar el acceso." });
            return Ok(new { mensaje = "Acceso revocado correctamente." });
        }
    }
}