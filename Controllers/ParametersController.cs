using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ParametersController(IParametersService parametersService) : ControllerBase
    {
        private readonly IParametersService _parametersService = parametersService;

        [HttpGet]
        public async Task<IActionResult> GetParameters()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);
            return Ok(await _parametersService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetParameter(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var parameter = await _parametersService.GetByIdAsync(id);
            if (parameter == null || parameter.CompanyId != companyId) return NotFound();

            return Ok(parameter);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutParameter(int id, [FromBody] Parameters parameter)
        {
            if (id != parameter.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);
            parameter.CompanyId = companyId;

            var existingParameter = await _parametersService.GetByIdAsync(id);
            if (existingParameter == null || existingParameter.CompanyId != companyId) return NotFound();

            var success = await _parametersService.UpdateAsync(parameter);
            if (!success) return BadRequest("No se pudo actualizar el parámetro.");

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostParameter([FromBody] Parameters parameter)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            parameter.CompanyId = int.Parse(companyIdHeader!);

            var success = await _parametersService.CreateAsync(parameter);
            if (!success) return BadRequest("No se pudo crear.");

            return CreatedAtAction(nameof(GetParameter), new { id = parameter.Id }, parameter);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteParameter(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var existingParameter = await _parametersService.GetByIdAsync(id);
            if (existingParameter == null || existingParameter.CompanyId != companyId) return NotFound();

            var success = await _parametersService.DeleteAsync(id);
            if (!success) return BadRequest("No se pudo eliminar el parámetro.");

            return NoContent();
        }
    }
}