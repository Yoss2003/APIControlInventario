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

        // GET: api/Movements
        [HttpGet]
        public async Task<IActionResult> GetMovements()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var movements = await _movementService.GetAllByCompanyIdAsync(companyId);
            return Ok(movements);
        }

        // GET: api/Movements/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMovement(int id)
        {
            var movement = await _movementService.GetByIdAsync(id);

            if (movement == null)
            {
                return NotFound();
            }

            return Ok(movement);
        }

        // POST: api/Movements
        [HttpPost]
        public async Task<IActionResult> PostMovement([FromBody] Movement movement)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                movement.CompanyId = int.Parse(companyIdHeader!);

            var success = await _movementService.CreateAsync(movement);
            if (!success) return BadRequest("No se pudo crear.");
            return CreatedAtAction(nameof(GetMovement), new { id = movement.Id }, movement);
        }
    }
}