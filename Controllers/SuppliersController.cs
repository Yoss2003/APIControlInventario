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
            var result = await _supplierService.ConsultarRucAsync(ruc);

            if (!result.Success)
            {
                if (result.Message.Contains("crítica"))
                {
                    return StatusCode(500, new { error = result.Message });
                }

                if (result.Message.Contains("no fue localizado"))
                {
                    return NotFound(new { error = result.Message });
                }

                return BadRequest(new { error = result.Message });
            }

            return Ok(result.Data);
        }

        // GET: api/Suppliers
        [HttpGet]
        public async Task<IActionResult> GetSuppliers()
        {
            var suppliers = await _supplierService.GetAllAsync();
            return Ok(suppliers);
        }

        // GET: api/Suppliers/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSupplier(int id)
        {
            var supplier = await _supplierService.GetByIdAsync(id);

            if (supplier == null)
            {
                return NotFound();
            }

            return Ok(supplier);
        }

        // PUT: api/Suppliers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSupplier(int id, [FromBody] Supplier supplier)
        {
            if (id != supplier.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingSupplier = await _supplierService.GetByIdAsync(id);
            if (existingSupplier == null)
            {
                return NotFound();
            }

            var success = await _supplierService.UpdateAsync(supplier);
            if (!success)
            {
                return BadRequest("No se pudo actualizar el proveedor.");
            }

            return NoContent();
        }

        // POST: api/Suppliers
        [HttpPost]
        public async Task<IActionResult> PostSupplier([FromBody] Supplier supplier)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var success = await _supplierService.CreateAsync(supplier);
            if (!success)
            {
                return BadRequest("No se pudo crear el proveedor.");
            }

            return CreatedAtAction(nameof(GetSupplier), new { id = supplier.Id }, supplier);
        }

        // DELETE: api/Suppliers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            var existingSupplier = await _supplierService.GetByIdAsync(id);
            if (existingSupplier == null)
            {
                return NotFound();
            }

            var success = await _supplierService.DeleteAsync(id);
            if (!success)
            {
                return BadRequest("No se pudo eliminar el proveedor.");
            }

            return NoContent();
        }
    }
}