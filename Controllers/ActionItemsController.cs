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
    public class ActionItemsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ActionItemsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/ActionItems
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ActionItem>>> GetActions()
        {
            return await _context.Actions.ToListAsync();
        }

        // GET: api/ActionItems/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ActionItem>> GetActionItem(int id)
        {
            var actionItem = await _context.Actions.FindAsync(id);

            if (actionItem == null)
            {
                return NotFound();
            }

            return actionItem;
        }

        // PUT: api/ActionItems/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutActionItem(int id, ActionItem actionItem)
        {
            if (id != actionItem.Id)
            {
                return BadRequest();
            }

            _context.Entry(actionItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ActionItemExists(id))
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

        // POST: api/ActionItems
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ActionItem>> PostActionItem(ActionItem actionItem)
        {
            _context.Actions.Add(actionItem);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetActionItem", new { id = actionItem.Id }, actionItem);
        }

        // DELETE: api/ActionItems/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteActionItem(int id)
        {
            var actionItem = await _context.Actions.FindAsync(id);
            if (actionItem == null)
            {
                return NotFound();
            }

            _context.Actions.Remove(actionItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ActionItemExists(int id)
        {
            return _context.Actions.Any(e => e.Id == id);
        }
    }
}
