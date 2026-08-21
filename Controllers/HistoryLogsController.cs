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

        [HttpGet]
        public async Task<IActionResult> GetHistoryLogs()
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
                int companyId = int.Parse(companyIdHeader!);
                return Ok(await _historyLogService.GetAllByCompanyIdAsync(companyId));
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetHistoryLog(int id)
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
                int companyId = int.Parse(companyIdHeader!);

                var historyLog = await _historyLogService.GetByIdAsync(id);
                if (historyLog == null || historyLog.CompanyId != companyId) return NotFound();

                return Ok(historyLog);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<IActionResult> PostHistoryLog([FromBody] HistoryLog historyLog)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
                historyLog.CompanyId = int.Parse(companyIdHeader!);

                var success = await _historyLogService.CreateAsync(historyLog);
                if (!success) return BadRequest("No se pudo crear.");

                return CreatedAtAction(nameof(GetHistoryLog), new { id = historyLog.Id }, historyLog);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }
    }
}