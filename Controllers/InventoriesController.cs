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

        // GET: api/Inventories
        [HttpGet]
        public async Task<IActionResult> GetInventories()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var inventories = await _inventoryService.GetAllByCompanyIdAsync(companyId);
            return Ok(inventories);
        }

        // GET: api/Inventories/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetInventory(int id)
        {
            var inventory = await _inventoryService.GetByIdAsync(id);

            if (inventory == null)
            {
                return NotFound();
            }

            return Ok(inventory);
        }

        // PUT: api/Inventories/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutInventory(int id, [FromBody] Inventory inventory)
        {
            if (id != inventory.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var success = await _inventoryService.UpdateAsync(inventory);
            if (!success)
            {
                return BadRequest("No se pudo actualizar el inventario.");
            }

            return NoContent();
        }

        // POST: api/Inventories
        [HttpPost]
        public async Task<IActionResult> PostInventory([FromBody] Inventory inventory)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                inventory.CompanyId = int.Parse(companyIdHeader!);

            var success = await _inventoryService.CreateAsync(inventory);
            if (!success) return BadRequest("No se pudo crear.");
            return CreatedAtAction(nameof(GetInventory), new { id = inventory.Id }, inventory);
        }

        // DELETE: api/Inventories/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteInventory(int id)
        {
            var success = await _inventoryService.DeleteAsync(id);
            if (!success)
            {
                return BadRequest("No se pudo eliminar el inventario.");
            }

            return NoContent();
        }

        // POST: api/Inventories/Share
        [HttpPost("Share")]
        public async Task<IActionResult> ShareInventory([FromBody] ShareRequestDTO request)
        {
            var result = await _inventoryService.ShareInventoryAsync(request);

            if (!result.Success)
            {
                if (result.Message.Contains("no existe") || result.Message.Contains("inválidos"))
                {
                    return NotFound(new { mensaje = result.Message });
                }
                return BadRequest(new { mensaje = result.Message });
            }

            return Ok(new { mensaje = result.Message });
        }

        // GET: api/Inventories/5/Shared
        [HttpGet("{inventoryId:int}/Shared")]
        public async Task<IActionResult> GetSharedInventories(int inventoryId)
        {
            try
            {
                var sharedList = await _inventoryService.GetSharedInventoriesAsync(inventoryId);
                return Ok(sharedList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al obtener la lista de accesos", detalle = ex.Message });
            }
        }

        // DELETE: api/Inventories/Revoke/5
        [HttpDelete("Revoke/{sharedInventoryId:int}")]
        public async Task<IActionResult> RevokeAccess(int sharedInventoryId)
        {
            try
            {
                var success = await _inventoryService.RevokeAccessAsync(sharedInventoryId);

                if (!success)
                    return BadRequest(new { error = "No se pudo revocar el acceso. Es posible que el registro ya no exista." });

                return Ok(new { mensaje = "Acceso revocado correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al revocar acceso", detalle = ex.Message });
            }
        }
    }
}