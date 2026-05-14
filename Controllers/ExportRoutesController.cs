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
    public class ExportRoutesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ExportRoutesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/ExportRoutes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExportRoute>>> GetExportRoutes()
        {
            return await _context.ExportRoutes.ToListAsync();
        }

        // GET: api/ExportRoutes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ExportRoute>> GetExportRoute(int id)
        {
            var exportRoute = await _context.ExportRoutes.FindAsync(id);

            if (exportRoute == null)
            {
                return NotFound();
            }

            return exportRoute;
        }

        // PUT: api/ExportRoutes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutExportRoute(int id, ExportRoute exportRoute)
        {
            if (id != exportRoute.Id)
            {
                return BadRequest();
            }

            _context.Entry(exportRoute).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ExportRouteExists(id))
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

        // POST: api/ExportRoutes
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ExportRoute>> PostExportRoute(ExportRoute exportRoute)
        {
            _context.ExportRoutes.Add(exportRoute);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetExportRoute", new { id = exportRoute.Id }, exportRoute);
        }

        // DELETE: api/ExportRoutes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExportRoute(int id)
        {
            var exportRoute = await _context.ExportRoutes.FindAsync(id);
            if (exportRoute == null)
            {
                return NotFound();
            }

            _context.ExportRoutes.Remove(exportRoute);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ExportRouteExists(int id)
        {
            return _context.ExportRoutes.Any(e => e.Id == id);
        }
    }
}
