using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExportRoutesController(IExportRouteService exportRouteService) : ControllerBase
    {
        private readonly IExportRouteService _exportRouteService = exportRouteService;

        [HttpGet]
        public async Task<IActionResult> GetExportRoutes()
        {
            try
            {
                var routes = await _exportRouteService.GetAllAsync();
                return Ok(routes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetExportRoute(int id)
        {
            try
            {
                var route = await _exportRouteService.GetByIdAsync(id);
                if (route == null) return NotFound();
                return Ok(route);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutExportRoute(int id, [FromBody] ExportRoute exportRoute)
        {
            if (id != exportRoute.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var existing = await _exportRouteService.GetByIdAsync(id);
                if (existing == null) return NotFound();

                var success = await _exportRouteService.UpdateAsync(exportRoute);
                if (!success) return BadRequest("No se pudo actualizar la ruta de exportación.");

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<ActionResult<ExportRoute>> PostExportRoute([FromBody] ExportRoute exportRoute)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var success = await _exportRouteService.CreateAsync(exportRoute);
                if (!success) return BadRequest("No se pudo crear la ruta de exportación.");

                return CreatedAtAction(nameof(GetExportRoute), new { id = exportRoute.Id }, exportRoute);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExportRoute(int id)
        {
            try
            {
                var existing = await _exportRouteService.GetByIdAsync(id);
                if (existing == null) return NotFound();

                var success = await _exportRouteService.DeleteAsync(id);
                if (!success) return BadRequest("No se pudo eliminar la ruta de exportación.");

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }
    }
}