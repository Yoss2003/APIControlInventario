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
    public class ParametersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ParametersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Parameters
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Parameter>>> GetParameters()
        {
            return await _context.Parameters.ToListAsync();
        }

        // GET: api/Parameters/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Parameter>> GetParameter(int id)
        {
            var parameter = await _context.Parameters.FindAsync(id);

            if (parameter == null)
            {
                return NotFound();
            }

            return parameter;
        }

        // PUT: api/Parameters/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutParameter(int id, Parameter parameter)
        {
            if (id != parameter.Id)
            {
                return BadRequest();
            }

            _context.Entry(parameter).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ParameterExists(id))
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

        // POST: api/Parameters
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Parameter>> PostParameter(Parameter parameter)
        {
            _context.Parameters.Add(parameter);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetParameter", new { id = parameter.Id }, parameter);
        }

        // DELETE: api/Parameters/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteParameter(int id)
        {
            var parameter = await _context.Parameters.FindAsync(id);
            if (parameter == null)
            {
                return NotFound();
            }

            _context.Parameters.Remove(parameter);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ParameterExists(int id)
        {
            return _context.Parameters.Any(e => e.Id == id);
        }
    }
}
