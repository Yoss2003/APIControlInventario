using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services;
using InventoryAPI.Services.IServices;
using InventoryAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers
{
    public class MovementsController(IMovementService movementService, InventoryAPI.Data.AppDbContext db) : BaseApiController
    {
        private bool IsDeveloper => User.FindFirst("RoleId")?.Value == "1";
        private readonly IMovementService _movementService = movementService;

        private IQueryable<int> AccessibleInventories(int companyId, int userId, bool edit = false) =>
            db.Inventories.Where(i => i.CompanyId == companyId &&
                (IsDeveloper || i.UserId == userId || db.SharedInventories.Any(sh =>
                  sh.InventoryId == i.Id && sh.CompanyId == companyId && sh.UserId == userId &&
                  (!edit || sh.AccessLevel == SharedInventory.AccessMode.Editor)))).Select(i => i.Id);

        [HttpGet]
        public async Task<IActionResult> GetMovements()
        {
            int companyId = ObtenerEmpresaSegura();
            var accessible = AccessibleInventories(companyId, ObtenerUsuarioSeguro());
            return Ok(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                db.Movements.AsNoTracking().Where(m => m.CompanyId == companyId &&
                    db.Articles.Any(a => a.Id == m.ArticleId && accessible.Contains(a.InventoryId)))));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetMovement(int id)
        {
            var movement = await _movementService.GetByIdAsync(id);
            if (movement == null || movement.CompanyId != ObtenerEmpresaSegura()) return NotFound();
            var accessible = AccessibleInventories(movement.CompanyId, ObtenerUsuarioSeguro());
            return await db.Articles.AnyAsync(a => a.Id == movement.ArticleId && accessible.Contains(a.InventoryId)) ? Ok(movement) : NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> PostMovement([FromBody] Movement movement)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            movement.CompanyId = ObtenerEmpresaSegura();
            if (!await db.Articles.AnyAsync(a => a.Id == movement.ArticleId &&
                AccessibleInventories(movement.CompanyId, ObtenerUsuarioSeguro(), true).Contains(a.InventoryId))) return Forbid();
            if (movement.ActionId == 3)
                return BadRequest(new { error = "Las entregas se crean mediante /api/Assignments." });
            try
            {
                movement.EmployeeId = await db.Employees.Where(e => e.CompanyId == movement.CompanyId &&
                    e.UserId == ObtenerUsuarioSeguro()).Select(e => (int?)e.Id).FirstOrDefaultAsync();
                var success = await _movementService.CreateAsync(movement);
                return success ? CreatedAtAction(nameof(GetMovement), new { id = movement.Id }, movement)
                    : BadRequest(new { error = "No se pudo registrar el movimiento." });
            }
            catch (ArgumentException e) { return BadRequest(new { error = e.Message }); }
            catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
        }

        [HttpGet("cancellations")]
        public async Task<IActionResult> GetCancellations()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                db.MovementCancellations.Where(c => c.CompanyId == companyId &&
                    db.Movements.Any(m => m.Id == c.OriginalMovementId &&
                        db.Articles.Any(a => a.Id == m.ArticleId &&
                            AccessibleInventories(companyId, ObtenerUsuarioSeguro()).Contains(a.InventoryId))))
                    .Select(c => new { c.OriginalMovementId, c.ReversalMovementId, c.CancelledAt, c.Reason })));
        }

        public sealed record CancelMovementRequest(string Reason);

        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelMovementRequest body)
        {
            if (_movementService is not MovementService impl)
                return StatusCode(500, new { error = "Servicio de movimientos no disponible." });
            int companyId = ObtenerEmpresaSegura();
            if (!await db.Movements.AnyAsync(m => m.Id == id && m.CompanyId == companyId &&
                db.Articles.Any(a => a.Id == m.ArticleId &&
                    AccessibleInventories(companyId, ObtenerUsuarioSeguro(), true).Contains(a.InventoryId)))) return Forbid();
            var result = await impl.CancelStockMovementAsync(id, companyId, ObtenerUsuarioSeguro(), body.Reason);
            return result.Success ? Ok(new { message = "Movimiento revertido y stock actualizado." })
                : Conflict(new { error = result.Error });
        }
    }
}
