using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class CustomersController(ICustomerService customerService) : BaseApiController
    {
        private readonly ICustomerService _customerService = customerService;

        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await _customerService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomer(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var customer = await _customerService.GetByIdAsync(id);

            if (customer == null || customer.CompanyId != companyId) return NotFound();
            return Ok(customer);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutCustomer(int id, [FromBody] Customer customer)
        {
            if (id != customer.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();
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

            customer.CompanyId = ObtenerEmpresaSegura();
            var success = await _customerService.CreateAsync(customer);

            if (!success) return BadRequest("No se pudo crear el cliente.");
            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            string deletedBy = ObtenerUsuarioSeguro().ToString();

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