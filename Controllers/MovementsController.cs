using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MovementsController(IMovementService movementService) : ControllerBase
    {
        private readonly IMovementService _movementService = movementService;

        [HttpGet]
        public async Task<IActionResult> GetMovements()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);
            return Ok(await _movementService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMovement(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var movement = await _movementService.GetByIdAsync(id);
            if (movement == null || movement.CompanyId != companyId) return NotFound();

            return Ok(movement);
        }

        [HttpPost]
        public async Task<IActionResult> PostMovement([FromBody] Movement movement)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            movement.CompanyId = int.Parse(companyIdHeader!);

            var success = await _movementService.CreateAsync(movement);
            if (!success) return BadRequest("No se pudo crear.");

            return CreatedAtAction(nameof(GetMovement), new { id = movement.Id }, movement);
        }
    }
}