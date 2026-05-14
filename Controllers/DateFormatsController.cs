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
    public class DateFormatsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DateFormatsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/DateFormats
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DateFormat>>> GetDateFormats()
        {
            return await _context.DateFormats.ToListAsync();
        }

        // GET: api/DateFormats/5
        [HttpGet("{id}")]
        public async Task<ActionResult<DateFormat>> GetDateFormat(int id)
        {
            var dateFormat = await _context.DateFormats.FindAsync(id);

            if (dateFormat == null)
            {
                return NotFound();
            }

            return dateFormat;
        }

        // PUT: api/DateFormats/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDateFormat(int id, DateFormat dateFormat)
        {
            if (id != dateFormat.Id)
            {
                return BadRequest();
            }

            _context.Entry(dateFormat).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DateFormatExists(id))
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

        // POST: api/DateFormats
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<DateFormat>> PostDateFormat(DateFormat dateFormat)
        {
            _context.DateFormats.Add(dateFormat);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetDateFormat", new { id = dateFormat.Id }, dateFormat);
        }

        // DELETE: api/DateFormats/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDateFormat(int id)
        {
            var dateFormat = await _context.DateFormats.FindAsync(id);
            if (dateFormat == null)
            {
                return NotFound();
            }

            _context.DateFormats.Remove(dateFormat);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool DateFormatExists(int id)
        {
            return _context.DateFormats.Any(e => e.Id == id);
        }
    }
}
