using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers;

// Proyectar solo campos públicos de SUNAT: el servicio legado puede contener
// registros internos de Supplier de otras empresas.
public sealed class ConsultasController(ISupplierService suppliers) : BaseApiController
{
    [HttpGet("ruc/{ruc}")]
    public async Task<IActionResult> Ruc(string ruc)
    {
        if (ruc.Length != 11 || !ruc.All(char.IsDigit))
            return BadRequest(new { error = "El RUC debe tener 11 dígitos." });
        var result = await suppliers.ConsultarRucAsync(ruc);
        if (!result.Success || result.Data is null)
            return BadRequest(new { error = "No fue posible consultar el RUC." });
        var d = result.Data;
        return Ok(new { d.Ruc, d.BusinessName, d.Address, d.Estado, d.Condicion, d.Distrito, d.Provincia, d.Departamento });
    }
}
