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

        // Vista administrativa de entradas/salidas de todos los inventarios personales.
        // Consulta los movimientos originales; no copia filas al inventario maestro.
        [HttpGet("master/history")]
        public async Task<IActionResult> MasterHistory(
            [FromQuery] int? inventoryId = null,
            [FromQuery] int? userId = null,
            [FromQuery] int? actorUserId = null,
            [FromQuery] int? actionId = null,
            [FromQuery(Name = "from")] DateTimeOffset? fromDate = null,
            [FromQuery(Name = "to")] DateTimeOffset? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (User.FindFirst("RoleId")?.Value is not ("1" or "2")) return Forbid();
            int companyId = ObtenerEmpresaSegura();
            if (companyId <= 0) return Unauthorized();
            if (actionId.HasValue && actionId.Value is not (1 or 2))
                return BadRequest(new { error = "Tipo válido: 1 entrada, 2 salida." });
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
                return BadRequest(new { error = "El rango de fechas no es válido." });

            page = Math.Clamp(page, 1, 1000000);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var rows =
                from movement in db.Movements.AsNoTracking()
                join article in db.Articles.AsNoTracking() on movement.ArticleId equals article.Id
                join inventory in db.Inventories.AsNoTracking() on article.InventoryId equals inventory.Id
                where movement.CompanyId == companyId && article.CompanyId == companyId &&
                    inventory.CompanyId == companyId && !inventory.IsMaster &&
                    (movement.ActionId == 1 || movement.ActionId == 2)
                select new
                {
                    movement.Id,
                    movement.MovementDate,
                    movement.ActionId,
                    movement.Amount,
                    movement.EmployeeId,
                    movement.Observation,
                    InventoryId = inventory.Id,
                    InventoryName = inventory.Alias ?? inventory.InventoryName,
                    UserId = inventory.UserId, // Propietario del inventario
                    Username = inventory.Username,
                    ActorUserId = db.Employees.Where(e => e.Id == movement.EmployeeId &&
                        e.CompanyId == companyId).Select(e => e.UserId).FirstOrDefault(),
                    ArticleId = article.Id,
                    ArticleName = article.Name,
                    ArticleCode = article.Code
                };

            if (inventoryId.HasValue) rows = rows.Where(x => x.InventoryId == inventoryId.Value);
            if (userId.HasValue) rows = rows.Where(x => x.UserId == userId.Value);
            if (actorUserId.HasValue) rows = rows.Where(x => x.ActorUserId == actorUserId.Value);
            if (actionId.HasValue) rows = rows.Where(x => x.ActionId == actionId.Value);
            if (fromDate.HasValue) rows = rows.Where(x => x.MovementDate >= fromDate.Value.UtcDateTime);
            if (toDate.HasValue) rows = rows.Where(x => x.MovementDate <= toDate.Value.UtcDateTime);

            var total = await rows.CountAsync();
            var items = await rows.OrderByDescending(x => x.MovementDate)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Ok(new { items, total, page, pageSize });
        }

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
