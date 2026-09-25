using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class ParametersController(IParametersService parametersService) : BaseApiController
    {
        private readonly IParametersService _parametersService = parametersService;

        [HttpGet]
        public async Task<IActionResult> GetParameters()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await _parametersService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetParameter(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var parameter = await _parametersService.GetByIdAsync(id);

            if (parameter == null || (parameter.CompanyId != companyId && parameter.CompanyId != 1)) return NotFound();

            return Ok(parameter);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutParameter(int id, [FromBody] Parameters parameter)
        {
            if (id != parameter.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();
            parameter.CompanyId = companyId;

            var existingParameter = await _parametersService.GetByIdAsync(id);

            if (existingParameter != null && existingParameter.CompanyId == 1 && companyId != 1)
                return BadRequest("Solo la Administración Central puede modificar los parámetros maestros.");

            if (existingParameter == null || existingParameter.CompanyId != companyId) return NotFound();

            var success = await _parametersService.UpdateAsync(parameter);
            if (!success) return BadRequest("No se pudo actualizar el parámetro.");

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostParameter([FromBody] Parameters parameter)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            parameter.CompanyId = ObtenerEmpresaSegura();
            var success = await _parametersService.CreateAsync(parameter);

            if (!success) return BadRequest("No se pudo crear.");

            return CreatedAtAction(nameof(GetParameter), new { id = parameter.Id }, parameter);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteParameter(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            string deletedBy = ObtenerUsuarioSeguro().ToString();

            var existingParameter = await _parametersService.GetByIdAsync(id);

            if (existingParameter != null && existingParameter.CompanyId == 1 && companyId != 1)
                return BadRequest("Solo la Administración Central puede eliminar los parámetros maestros.");

            if (existingParameter == null || existingParameter.CompanyId != companyId) return NotFound();

            var success = await _parametersService.DeleteAsync(id, deletedBy);
            if (!success) return BadRequest("No se pudo eliminar el parámetro.");

            return NoContent();
        }
    }
}