using InventoryAPI.Models;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers;

// Consulta pública exclusivamente del catálogo reducido para el selector de login.
// El CRUD administrativo se ejecuta solo en /api/dev/companies.
public sealed class CompaniesController(ICompanyService service) : BaseApiController
{
    [AllowAnonymous]
    [HttpGet("Active")]
    public async Task<IActionResult> GetActiveCompanies() =>
        Ok(await service.GetActiveCompaniesPublicAsync());

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (User.FindFirst("RoleId")?.Value != "1") return Forbid();
        return Ok(await service.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        if (User.FindFirst("RoleId")?.Value != "1") return Forbid();
        var company = await service.GetByIdAsync(id);
        return company is null ? NotFound() : Ok(company);
    }

    // Rutas antiguas deshabilitadas para no saltarse validaciones de licencia y dependencias.
    [HttpPost] public IActionResult OldCreate() => StatusCode(410, new { error = "Utiliza Gestión Dev." });
    [HttpPut("{id:int}")] public IActionResult OldUpdate(int id) => StatusCode(410, new { error = "Utiliza Gestión Dev." });
    [HttpDelete("{id:int}")] public IActionResult OldDelete(int id) => StatusCode(410, new { error = "Utiliza Gestión Dev." });
}
