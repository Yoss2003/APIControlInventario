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
    public class RegistrationGroupsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RegistrationGroupsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/RegistrationGroups
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RegistrationGroup>>> GetRegistrationGroups()
        {
            return await _context.RegistrationGroups.ToListAsync();
        }

        // GET: api/RegistrationGroups/5
        [HttpGet("{id}")]
        public async Task<ActionResult<RegistrationGroup>> GetRegistrationGroup(int id)
        {
            var registrationGroup = await _context.RegistrationGroups.FindAsync(id);

            if (registrationGroup == null)
            {
                return NotFound();
            }

            return registrationGroup;
        }

        // PUT: api/RegistrationGroups/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutRegistrationGroup(int id, RegistrationGroup registrationGroup)
        {
            if (id != registrationGroup.Id)
            {
                return BadRequest();
            }

            _context.Entry(registrationGroup).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RegistrationGroupExists(id))
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

        // POST: api/RegistrationGroups
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<RegistrationGroup>> PostRegistrationGroup(RegistrationGroup registrationGroup)
        {
            _context.RegistrationGroups.Add(registrationGroup);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetRegistrationGroup", new { id = registrationGroup.Id }, registrationGroup);
        }

        // DELETE: api/RegistrationGroups/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRegistrationGroup(int id)
        {
            var registrationGroup = await _context.RegistrationGroups.FindAsync(id);
            if (registrationGroup == null)
            {
                return NotFound();
            }

            _context.RegistrationGroups.Remove(registrationGroup);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool RegistrationGroupExists(int id)
        {
            return _context.RegistrationGroups.Any(e => e.Id == id);
        }
    }
}
