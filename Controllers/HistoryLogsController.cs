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
                var historyLogs = await _historyLogService.GetAllAsync();
                return Ok(historyLogs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar los registros de historial: {ex.Message}");
            }
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _historyLogService.CreateAsync(historyLog);
                if (!success)
                {
                    return BadRequest("No se pudo crear el registro de historial.");
                }

                return CreatedAtAction(nameof(GetHistoryLog), new { id = historyLog.Id }, historyLog);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al crear el registro de historial: {ex.Message}");
            }
        }
    }
}