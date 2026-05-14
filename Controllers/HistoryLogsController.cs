using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryAPI.Data;
using InventoryAPI.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HistoryLogsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HistoryLogsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/HistoryLogs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<HistoryLog>>> GetHistoryLogs()
        {
            return await _context.HistoryLogs.ToListAsync();
        }

        // GET: api/HistoryLogs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<HistoryLog>> GetHistoryLog(int id)
        {
            var historyLog = await _context.HistoryLogs.FindAsync(id);

            if (historyLog == null)
            {
                return NotFound();
            }

            return historyLog;
        }

        // PUT: api/HistoryLogs/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutHistoryLog(int id, HistoryLog historyLog)
        {
            if (id != historyLog.Id)
            {
                return BadRequest();
            }

            _context.Entry(historyLog).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HistoryLogExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/HistoryLogs
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<HistoryLog>> PostHistoryLog(HistoryLog historyLog)
        {
            _context.HistoryLogs.Add(historyLog);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetHistoryLog", new { id = historyLog.Id }, historyLog);
        }

        // DELETE: api/HistoryLogs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHistoryLog(int id)
        {
            var historyLog = await _context.HistoryLogs.FindAsync(id);
            if (historyLog == null)
            {
                return NotFound();
            }

            _context.HistoryLogs.Remove(historyLog);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool HistoryLogExists(int id)
        {
            return _context.HistoryLogs.Any(e => e.Id == id);
        }
    }
}
