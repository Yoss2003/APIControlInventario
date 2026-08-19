using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HistoryLogsController(IHistoryLogService historyLogService) : ControllerBase
    {
        private readonly IHistoryLogService _historyLogService = historyLogService;

        // GET: api/HistoryLogs
        [HttpGet]
        public async Task<IActionResult> GetHistoryLogs()
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal (X-Company-Id).");
                int companyId = int.Parse(companyIdHeader!);

                var historyLogs = await _historyLogService.GetAllByCompanyIdAsync(companyId);
                return Ok(historyLogs);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        // GET: api/HistoryLogs/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetHistoryLog(int id)
        {
            try
            {
                var historyLog = await _historyLogService.GetByIdAsync(id);

                if (historyLog == null)
                {
                    return NotFound($"No se encontró el registro de historial con ID {id}.");
                }

                return Ok(historyLog);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar el registro de historial: {ex.Message}");
            }
        }

        // POST: api/HistoryLogs
        [HttpPost]
        public async Task<IActionResult> PostHistoryLog([FromBody] HistoryLog historyLog)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                if (Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                    historyLog.CompanyId = int.Parse(companyIdHeader!);

                var success = await _historyLogService.CreateAsync(historyLog);
                if (!success) return BadRequest("No se pudo crear.");
                return CreatedAtAction(nameof(GetHistoryLog), new { id = historyLog.Id }, historyLog);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }
    }
}