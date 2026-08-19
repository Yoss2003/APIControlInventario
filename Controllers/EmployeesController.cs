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

        // GET: api/Employees
        [HttpGet]
        public async Task<IActionResult> GetEmployees()
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                    return BadRequest("Falta indicar la sucursal (X-Company-Id).");
                
                int companyId = int.Parse(companyIdHeader!);
                var employees = await _employeeService.GetAllByCompanyIdAsync(companyId);
                return Ok(employees);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar los empleados: {ex.Message}");
            }
        }

        // GET: api/Employees/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmployee(int id)
        {
            try
            {
                var employee = await _employeeService.GetByIdAsync(id);

                if (employee == null)
                {
                    return NotFound();
                }

                return Ok(employee);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar el empleado: {ex.Message}");
            }
        }

        // PUT: api/Employees/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEmployee(int id, [FromBody] Employee employee)
        {
            if (id != employee.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var existingEmployee = await _employeeService.GetByIdAsync(id);
                if (existingEmployee == null)
                {
                    return NotFound();
                }

                var success = await _employeeService.UpdateAsync(employee);
                if (!success)
                {
                    return BadRequest("No se pudo actualizar el empleado.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al actualizar el empleado: {ex.Message}");
            }
        }

        // POST: api/Employees
        [HttpPost]
        public async Task<IActionResult> PostEmployee([FromBody] Employee employee)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _employeeService.CreateAsync(employee);
                if (!success)
                {
                    return BadRequest("No se pudo crear el empleado.");
                }

                return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, employee);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al crear el empleado: {ex.Message}");
            }
        }

        // DELETE: api/Employees/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            try
            {
                var existingEmployee = await _employeeService.GetByIdAsync(id);
                if (existingEmployee == null)
                {
                    return NotFound();
                }

                var success = await _employeeService.DeleteAsync(id);
                if (!success)
                {
                    return BadRequest("No se pudo eliminar el empleado.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al eliminar el empleado: {ex.Message}");
            }
        }
    }
}