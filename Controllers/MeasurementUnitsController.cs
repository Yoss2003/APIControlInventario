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
    public class MeasurementUnitsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MeasurementUnitsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/MeasurementUnits
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MeasurementUnit>>> GetMeasurementUnits()
        {
            return await _context.MeasurementUnits.ToListAsync();
        }

        // GET: api/MeasurementUnits/5
        [HttpGet("{id}")]
        public async Task<ActionResult<MeasurementUnit>> GetMeasurementUnit(int id)
        {
            var measurementUnit = await _context.MeasurementUnits.FindAsync(id);

            if (measurementUnit == null)
            {
                return NotFound();
            }

            return measurementUnit;
        }

        // PUT: api/MeasurementUnits/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMeasurementUnit(int id, MeasurementUnit measurementUnit)
        {
            if (id != measurementUnit.Id)
            {
                return BadRequest();
            }

            _context.Entry(measurementUnit).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MeasurementUnitExists(id))
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

        // POST: api/MeasurementUnits
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<MeasurementUnit>> PostMeasurementUnit(MeasurementUnit measurementUnit)
        {
            _context.MeasurementUnits.Add(measurementUnit);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMeasurementUnit", new { id = measurementUnit.Id }, measurementUnit);
        }

        // DELETE: api/MeasurementUnits/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMeasurementUnit(int id)
        {
            var measurementUnit = await _context.MeasurementUnits.FindAsync(id);
            if (measurementUnit == null)
            {
                return NotFound();
            }

            _context.MeasurementUnits.Remove(measurementUnit);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MeasurementUnitExists(int id)
        {
            return _context.MeasurementUnits.Any(e => e.Id == id);
        }
    }
}
