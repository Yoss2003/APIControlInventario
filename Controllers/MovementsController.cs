using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class MovementsController(IMovementService movementService) : BaseApiController
    {
        private readonly IMovementService _movementService = movementService;

        [HttpGet]
        public async Task<IActionResult> GetMovements()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await _movementService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMovement(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var movement = await _movementService.GetByIdAsync(id);

            if (movement == null || movement.CompanyId != companyId) return NotFound();
            return Ok(movement);
        }

        [HttpPost]
        public async Task<IActionResult> PostMovement([FromBody] Movement movement)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            movement.CompanyId = ObtenerEmpresaSegura();
            var success = await _movementService.CreateAsync(movement);

            if (!success) return BadRequest("No se pudo crear.");
            return CreatedAtAction(nameof(GetMovement), new { id = movement.Id }, movement);
        }
    }
}