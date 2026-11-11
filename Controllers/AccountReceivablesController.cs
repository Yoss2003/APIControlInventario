using InventoryAPI.Data;
using InventoryAPI.Models;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

// Consulta histórica compatible con cuentas de movimientos previos.
// Los nuevos créditos se registran exclusivamente en SaleInstallments.
public sealed class AccountReceivablesController(
    IAccountReceivableService accountReceivableService, AppDbContext db) : BaseApiController
{
    private bool IsDeveloper => User.FindFirst("RoleId")?.Value == "1";

    private IQueryable<int> ReadableInventories(int companyId, int userId) =>
        db.Inventories.Where(i => i.CompanyId == companyId &&
            (IsDeveloper || i.UserId == userId || db.SharedInventories.Any(s =>
                s.CompanyId == companyId && s.InventoryId == i.Id && s.UserId == userId)))
            .Select(i => i.Id);

    private IQueryable<int> AllowedMovements(int companyId, int userId)
    {
        var readable = ReadableInventories(companyId, userId);
        return db.Movements.Where(m => m.CompanyId == companyId &&
            db.Articles.Any(a => a.CompanyId == companyId && a.Id == m.ArticleId &&
                readable.Contains(a.InventoryId)))
            .Select(m => m.Id);
    }

    [HttpGet]
    public async Task<IActionResult> GetAccountsReceivables()
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        if (companyId <= 0 || userId <= 0) return Unauthorized();
        var ids = await AllowedMovements(companyId, userId).ToArrayAsync();
        var accounts = await accountReceivableService.GetAllByCompanyIdAsync(companyId);
        return Ok(accounts.Where(a => ids.Contains(a.MovementId)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAccountReceivable(int id)
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        if (companyId <= 0 || userId <= 0) return Unauthorized();
        var account = await accountReceivableService.GetByIdAsync(id);
        if (account == null || account.CompanyId != companyId) return NotFound();
        return await AllowedMovements(companyId, userId).ContainsAsync(account.MovementId)
            ? Ok(account) : Forbid();
    }

    // No admitir escrituras arbitrarias: alterarían el histórico contable.
    // La versión nueva utiliza los endpoints de ventas y abonos auditables.
    [HttpPost]
    public IActionResult PostAccountReceivable() =>
        Conflict(new { error = "Las cuotas se crean con la venta; no se permite generar cuentas por cobrar arbitrarias." });

    [HttpPut("{id:int}")]
    public IActionResult PutAccountReceivable(int id) =>
        Conflict(new { error = "No se permiten modificaciones directas de cuotas. Registra abonos mediante el flujo de cobros." });

    [HttpDelete("{id:int}")]
    public IActionResult DeleteAccountReceivable(int id) =>
        Conflict(new { error = "No se permite borrar cuentas por cobrar. Utiliza el proceso de anulación auditada." });
}
