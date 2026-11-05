using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class ParametersController(IParametersService parametersService) : BaseApiController
    {
        private readonly IParametersService _parametersService = parametersService;

        // LLAVE MAESTRA SUNAT (Decolecta)
        [HttpPut("GlobalToken")]
        public async Task<IActionResult> UpdateGlobalToken([FromBody] string newToken)
        {
            if (string.IsNullOrWhiteSpace(newToken)) return BadRequest("El token no puede estar vacío.");

            int companyId = ObtenerEmpresaSegura();

            if (companyId != 1)
                return BadRequest("Solo la Administración Central puede modificar el Token Maestro de SUNAT.");

            var parametrosCentral = await _parametersService.GetAllByCompanyIdAsync(1);
            var tokenParam = parametrosCentral.FirstOrDefault(p => p.ParameterType == "API_KEY" && p.Name == "Decolecta_Token");

            if (tokenParam != null)
            {
                tokenParam.Description = newToken;
                var success = await _parametersService.UpdateAsync(tokenParam);
                if (!success) return BadRequest("No se pudo actualizar el token maestro en la base de datos.");
            }
            else
            {
                var newParam = new Parameters
                {
                    InventoryId = 1,
                    CompanyId = 1,
                    ParameterType = "API_KEY",
                    Name = "Decolecta_Token",
                    Description = newToken
                };
                var success = await _parametersService.CreateAsync(newParam);
                if (!success) return BadRequest("No se pudo crear el token maestro en la base de datos.");
            }

            return Ok(new { message = "Llave maestra SUNAT actualizada correctamente." });
        }

        // ==========================================
        // ENDPOINTS ORIGINALES (CRUD ESTÁNDAR)
        // ==========================================

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