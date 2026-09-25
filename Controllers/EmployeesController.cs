using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class EmployeesController(IEmployeeService employeeService) : BaseApiController
    {
        private readonly IEmployeeService _employeeService = employeeService;

        [HttpGet]
        public async Task<IActionResult> GetEmployees()
        {
            int companyId = ObtenerEmpresaSegura();

            if (companyId == 0)
                return Ok(await _employeeService.GetAllAsync());

            return Ok(await _employeeService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmployee(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var employee = await _employeeService.GetByIdAsync(id);

            if (employee == null || employee.CompanyId != companyId) return NotFound();
            return Ok(employee);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutEmployee(int id, [FromBody] Employee employee)
        {
            if (id != employee.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();

            if (employee.CompanyId <= 0)
                employee.CompanyId = companyId;

            var success = await _employeeService.UpdateAsync(employee);
            if (!success) return BadRequest("No se pudo actualizar.");

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostEmployee([FromBody] Employee employee)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();

            if (employee.CompanyId <= 0)
            {
                employee.CompanyId = companyId;
            }

            var success = await _employeeService.CreateAsync(employee);
            if (!success) return BadRequest("No se pudo crear.");

            return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, employee);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            string deletedBy = ObtenerUsuarioSeguro().ToString();

            var existingEmployee = await _employeeService.GetByIdAsync(id);
            if (existingEmployee == null || existingEmployee.CompanyId != companyId) return NotFound();

            var success = await _employeeService.DeleteAsync(id, deletedBy);
            if (!success) return BadRequest("No se pudo eliminar.");

            return NoContent();
        }
    }
}