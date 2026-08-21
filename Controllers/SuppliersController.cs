using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SuppliersController(ISupplierService supplierService) : ControllerBase
    {
        private readonly ISupplierService _supplierService = supplierService;

        [HttpGet("ruc/{ruc}")]
        public async Task<IActionResult> ConsultarRuc(string ruc)
        {
            // Sin cambios, API externa no necesita verificación local.
            var result = await _supplierService.ConsultarRucAsync(ruc);

            if (!result.Success)
            {
                if (result.Message.Contains("crítica")) return StatusCode(500, new { error = result.Message });
                if (result.Message.Contains("no fue localizado")) return NotFound(new { error = result.Message });
                return BadRequest(new { error = result.Message });
            }
            return Ok(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> GetSuppliers()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);
            return Ok(await _supplierService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSupplier(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var supplier = await _supplierService.GetByIdAsync(id);
            if (supplier == null || supplier.CompanyId != companyId) return NotFound();

            return Ok(supplier);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutSupplier(int id, [FromBody] Supplier supplier)
        {
            if (id != supplier.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);
            supplier.CompanyId = companyId;

            var existingSupplier = await _supplierService.GetByIdAsync(id);
            if (existingSupplier == null || existingSupplier.CompanyId != companyId) return NotFound();

            var success = await _supplierService.UpdateAsync(supplier);
            if (!success) return BadRequest("No se pudo actualizar el proveedor.");

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostSupplier([FromBody] Supplier supplier)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            supplier.CompanyId = int.Parse(companyIdHeader!);

            var success = await _supplierService.CreateAsync(supplier);
            if (!success) return BadRequest("No se pudo crear.");

            return CreatedAtAction(nameof(GetSupplier), new { id = supplier.Id }, supplier);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var existingSupplier = await _supplierService.GetByIdAsync(id);
            if (existingSupplier == null || existingSupplier.CompanyId != companyId) return NotFound();

            var success = await _supplierService.DeleteAsync(id);
            if (!success) return BadRequest("No se pudo eliminar el proveedor.");

            return NoContent();
        }
    }
}