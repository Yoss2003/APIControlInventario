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

        [HttpGet("test-conexion")]
        public IActionResult TestConexioMultiTenant()
        {
            return Ok(new { mensaje = "¡ESTE ES EL CÓDIGO NUEVO 2026!" });
        }

        // GET: api/Customers
        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                    return BadRequest("Falta indicar la sucursal (X-Company-Id).");

                int companyId = int.Parse(companyIdHeader!);
                var customers = await _customerService.GetAllByCompanyIdAsync(companyId);

                return Ok(customers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar los clientes: {ex.Message}");
            }
        }

        // GET: api/Customers/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomer(int id)
        {
            try
            {
                var customer = await _customerService.GetByIdAsync(id);

                if (customer == null)
                {
                    return NotFound();
                }

                return Ok(customer);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar el cliente: {ex.Message}");
            }
        }

        // PUT: api/Customers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCustomer(int id, [FromBody] Customer customer)
        {
            if (id != customer.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var existingCustomer = await _customerService.GetByIdAsync(id);
                if (existingCustomer == null)
                {
                    return NotFound();
                }

                var success = await _customerService.UpdateAsync(customer);
                if (!success)
                {
                    return BadRequest("No se pudo actualizar el cliente.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al actualizar el cliente: {ex.Message}");
            }
        }

        // POST: api/Customers
        [HttpPost]
        public async Task<ActionResult<Customer>> PostCustomer([FromBody] Customer customer)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _customerService.CreateAsync(customer);
                if (!success)
                {
                    return BadRequest("No se pudo crear el cliente.");
                }

                return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al crear el cliente: {ex.Message}");
            }
        }

        // DELETE: api/Customers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            try
            {
                var existingCustomer = await _customerService.GetByIdAsync(id);
                if (existingCustomer == null)
                {
                    return NotFound();
                }

                var success = await _customerService.DeleteAsync(id);
                if (!success)
                {
                    return BadRequest("No se pudo eliminar el cliente.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al eliminar el cliente: {ex.Message}");
            }
        }

        // 🌟 ENDPOINT EXTERNO DNI MIGRADO AL SERVICIO
        [HttpGet("dni/{dni}")]
        public async Task<IActionResult> ConsultarDniExterno(string dni)
        {
            try
            {
                var result = await _customerService.ConsultarDniExternoAsync(dni);

                if (!result.IsSuccess)
                {
                    if (result.DataOrError.Contains("exactamente"))
                    {
                        return BadRequest(new { error = result.DataOrError });
                    }
                    if (result.DataOrError.Contains("no fue localizado"))
                    {
                        return NotFound(new { error = result.DataOrError });
                    }

                    return StatusCode(500, new { error = "Falla de red al intentar conectar con RENIEC.", detalle = result.DataOrError });
                }

                return Content(result.DataOrError, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error inesperado al consultar el DNI.", detalle = ex.Message });
            }
        }
    }
}