using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController(ICustomerService customerService) : ControllerBase
    {
        private readonly ICustomerService _customerService = customerService;

        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);
            return Ok(await _customerService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomer(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null || customer.CompanyId != companyId) return NotFound();

            return Ok(customer);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutCustomer(int id, [FromBody] Customer customer)
        {
            if (id != customer.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            int companyId = int.Parse(companyIdHeader!);
            customer.CompanyId = companyId;

            var existingCustomer = await _customerService.GetByIdAsync(id);
            if (existingCustomer == null || existingCustomer.CompanyId != companyId) return NotFound();

            var success = await _customerService.UpdateAsync(customer);
            if (!success) return BadRequest("No se pudo actualizar el cliente.");

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<Customer>> PostCustomer([FromBody] Customer customer)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            customer.CompanyId = int.Parse(companyIdHeader!);
            var success = await _customerService.CreateAsync(customer);
            if (!success) return BadRequest("No se pudo crear el cliente.");

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            string deletedBy = Request.Headers.TryGetValue("X-User-Name", out var userHeader) ? userHeader.ToString() : "Usuario Desconocido";

            var existingCustomer = await _customerService.GetByIdAsync(id);
            if (existingCustomer == null || existingCustomer.CompanyId != companyId) return NotFound();

            var success = await _customerService.DeleteAsync(id, deletedBy);
            if (!success) return BadRequest("No se pudo eliminar el cliente.");
            return NoContent();
        }

        [HttpGet("dni/{dni}")]
        public async Task<IActionResult> ConsultarDniExterno(string dni)
        {
            var (IsSuccess, DataOrError) = await _customerService.ConsultarDniExternoAsync(dni);
            if (!IsSuccess) return BadRequest(new { error = DataOrError });
            return Content(DataOrError, "application/json");
        }
    }
}