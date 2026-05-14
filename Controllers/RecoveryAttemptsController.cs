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
    public class RecoveryAttemptsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RecoveryAttemptsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/RecoveryAttempts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RecoveryAttempt>>> GetRecoveryAttempts()
        {
            return await _context.RecoveryAttempts.ToListAsync();
        }

        // GET: api/RecoveryAttempts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<RecoveryAttempt>> GetRecoveryAttempt(int id)
        {
            var recoveryAttempt = await _context.RecoveryAttempts.FindAsync(id);

            if (recoveryAttempt == null)
            {
                return NotFound();
            }

            return recoveryAttempt;
        }

        // PUT: api/RecoveryAttempts/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutRecoveryAttempt(int id, RecoveryAttempt recoveryAttempt)
        {
            if (id != recoveryAttempt.Id)
            {
                return BadRequest();
            }

            _context.Entry(recoveryAttempt).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RecoveryAttemptExists(id))
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

        // POST: api/RecoveryAttempts
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<RecoveryAttempt>> PostRecoveryAttempt(RecoveryAttempt recoveryAttempt)
        {
            _context.RecoveryAttempts.Add(recoveryAttempt);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetRecoveryAttempt", new { id = recoveryAttempt.Id }, recoveryAttempt);
        }

        // DELETE: api/RecoveryAttempts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRecoveryAttempt(int id)
        {
            var recoveryAttempt = await _context.RecoveryAttempts.FindAsync(id);
            if (recoveryAttempt == null)
            {
                return NotFound();
            }

            _context.RecoveryAttempts.Remove(recoveryAttempt);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool RecoveryAttemptExists(int id)
        {
            return _context.RecoveryAttempts.Any(e => e.Id == id);
        }
    }
}
