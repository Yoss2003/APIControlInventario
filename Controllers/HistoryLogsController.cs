using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class HistoryLogsController(IHistoryLogService historyLogService) : BaseApiController
    {
        private readonly IHistoryLogService _historyLogService = historyLogService;

        [HttpGet]
        public async Task<IActionResult> GetHistoryLogs()
        {
            try
            {
                int companyId = ObtenerEmpresaSegura();
                return Ok(await _historyLogService.GetAllByCompanyIdAsync(companyId));
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetHistoryLog(int id)
        {
            try
            {
                int companyId = ObtenerEmpresaSegura();
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
                historyLog.CompanyId = ObtenerEmpresaSegura();
                var success = await _historyLogService.CreateAsync(historyLog);

                if (!success) return BadRequest("No se pudo crear.");
                return CreatedAtAction(nameof(GetHistoryLog), new { id = historyLog.Id }, historyLog);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }
    }
}