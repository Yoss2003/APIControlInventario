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
    public class TimeZoneItemsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TimeZoneItemsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/TimeZoneItems
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TimeZoneItem>>> GetTimeZones()
        {
            return await _context.TimeZones.ToListAsync();
        }

        // GET: api/TimeZoneItems/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TimeZoneItem>> GetTimeZoneItem(int id)
        {
            var timeZoneItem = await _context.TimeZones.FindAsync(id);

            if (timeZoneItem == null)
            {
                return NotFound();
            }

            return timeZoneItem;
        }

        // PUT: api/TimeZoneItems/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTimeZoneItem(int id, TimeZoneItem timeZoneItem)
        {
            if (id != timeZoneItem.Id)
            {
                return BadRequest();
            }

            _context.Entry(timeZoneItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TimeZoneItemExists(id))
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

        // POST: api/TimeZoneItems
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TimeZoneItem>> PostTimeZoneItem(TimeZoneItem timeZoneItem)
        {
            _context.TimeZones.Add(timeZoneItem);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTimeZoneItem", new { id = timeZoneItem.Id }, timeZoneItem);
        }

        // DELETE: api/TimeZoneItems/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTimeZoneItem(int id)
        {
            var timeZoneItem = await _context.TimeZones.FindAsync(id);
            if (timeZoneItem == null)
            {
                return NotFound();
            }

            _context.TimeZones.Remove(timeZoneItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TimeZoneItemExists(int id)
        {
            return _context.TimeZones.Any(e => e.Id == id);
        }
    }
}
