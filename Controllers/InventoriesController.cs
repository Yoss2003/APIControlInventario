using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.DTO;

namespace InventoryAPI.Controllers
{
    public class InventoriesController(IInventoryService inventoryService) : BaseApiController
    {
        private readonly IInventoryService _inventoryService = inventoryService;

        [HttpGet]
        public async Task<IActionResult> GetInventories()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await _inventoryService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetInventory(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var inventory = await _inventoryService.GetByIdAsync(id);

            if (inventory == null || inventory.CompanyId != companyId) return NotFound();
            return Ok(inventory);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutInventory(int id, [FromBody] Inventory inventory)
        {
            if (id != inventory.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();
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

            inventory.CompanyId = ObtenerEmpresaSegura();
            var success = await _inventoryService.CreateAsync(inventory);

            if (!success) return BadRequest("No se pudo crear.");
            return CreatedAtAction(nameof(GetInventory), new { id = inventory.Id }, inventory);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteInventory(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            string deletedBy = ObtenerUsuarioSeguro().ToString();

            var existingInventory = await _inventoryService.GetByIdAsync(id);
            if (existingInventory == null || existingInventory.CompanyId != companyId) return NotFound();

            var success = await _inventoryService.DeleteAsync(id, deletedBy);
            if (!success) return BadRequest("No se pudo eliminar el inventario.");

            return NoContent();
        }

        [HttpPost("Share")]
        public async Task<IActionResult> ShareInventory([FromBody] ShareRequestDTO request)
        {
            int companyId = ObtenerEmpresaSegura();

            var existingInventory = await _inventoryService.GetByIdAsync(request.InventoryId);
            if (existingInventory == null || existingInventory.CompanyId != companyId)
                return NotFound(new { mensaje = "Inventario no encontrado." });

            var (Success, Message) = await _inventoryService.ShareInventoryAsync(request);
            if (!Success) return BadRequest(new { mensaje = Message });

            return Ok(new { mensaje = Message });
        }

        [HttpGet("{inventoryId:int}/Shared")]
        public async Task<IActionResult> GetSharedInventories(int inventoryId)
        {
            int companyId = ObtenerEmpresaSegura();
            var existingInventory = await _inventoryService.GetByIdAsync(inventoryId);

            if (existingInventory == null || existingInventory.CompanyId != companyId) return NotFound();

            var sharedList = await _inventoryService.GetSharedInventoriesAsync(inventoryId);
            return Ok(sharedList);
        }

        [HttpDelete("Revoke/{sharedInventoryId:int}")]
        public async Task<IActionResult> RevokeAccess(int sharedInventoryId)
        {
            var success = await _inventoryService.RevokeAccessAsync(sharedInventoryId);
            if (!success) return BadRequest(new { error = "No se pudo revocar el acceso." });

            return Ok(new { mensaje = "Acceso revocado correctamente." });
        }
    }
}