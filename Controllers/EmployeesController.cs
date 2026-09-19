using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController(IEmployeeService employeeService) : ControllerBase
    {
        private readonly IEmployeeService _employeeService = employeeService;

        [HttpGet]
        public async Task<IActionResult> GetEmployees()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);
            return Ok(await _employeeService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmployee(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var employee = await _employeeService.GetByIdAsync(id);
            if (employee == null || employee.CompanyId != companyId) return NotFound();

            return Ok(employee);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutEmployee(int id, [FromBody] Employee employee)
        {
            if (id != employee.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            if (employee.CompanyId <= 0)
            {
                employee.CompanyId = int.Parse(companyIdHeader!);
            }

            var success = await _employeeService.UpdateAsync(employee);
            if (!success) return BadRequest("No se pudo actualizar.");

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostEmployee([FromBody] Employee employee)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            if (employee.CompanyId <= 0)
            {
                employee.CompanyId = int.Parse(companyIdHeader!);
            }

            var success = await _employeeService.CreateAsync(employee);
            if (!success) return BadRequest("No se pudo crear.");

            return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, employee);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            string deletedBy = Request.Headers.TryGetValue("X-User-Name", out var userHeader) ? userHeader.ToString() : "Usuario Desconocido";

            var success = await _employeeService.DeleteAsync(id, deletedBy);
            if (!success) return BadRequest("No se pudo eliminar.");

            return NoContent();
        }
    }
}