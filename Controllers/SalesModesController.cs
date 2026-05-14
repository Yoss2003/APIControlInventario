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
    public class SalesModesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalesModesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/SalesModes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SalesMode>>> GetSalesModes()
        {
            return await _context.SalesModes.ToListAsync();
        }

        // GET: api/SalesModes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SalesMode>> GetSalesMode(int id)
        {
            var salesMode = await _context.SalesModes.FindAsync(id);

            if (salesMode == null)
            {
                return NotFound();
            }

            return salesMode;
        }

        // PUT: api/SalesModes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSalesMode(int id, SalesMode salesMode)
        {
            if (id != salesMode.Id)
            {
                return BadRequest();
            }

            _context.Entry(salesMode).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SalesModeExists(id))
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

        // POST: api/SalesModes
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<SalesMode>> PostSalesMode(SalesMode salesMode)
        {
            _context.SalesModes.Add(salesMode);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSalesMode", new { id = salesMode.Id }, salesMode);
        }

        // DELETE: api/SalesModes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSalesMode(int id)
        {
            var salesMode = await _context.SalesModes.FindAsync(id);
            if (salesMode == null)
            {
                return NotFound();
            }

            _context.SalesModes.Remove(salesMode);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool SalesModeExists(int id)
        {
            return _context.SalesModes.Any(e => e.Id == id);
        }
    }
}
