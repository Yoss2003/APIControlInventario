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
    public class SecurityQuestionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SecurityQuestionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/SecurityQuestions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SecurityQuestion>>> GetSecurityQuestions()
        {
            return await _context.SecurityQuestions.ToListAsync();
        }

        // GET: api/SecurityQuestions/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SecurityQuestion>> GetSecurityQuestion(int id)
        {
            var securityQuestion = await _context.SecurityQuestions.FindAsync(id);

            if (securityQuestion == null)
            {
                return NotFound();
            }

            return securityQuestion;
        }

        // PUT: api/SecurityQuestions/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSecurityQuestion(int id, SecurityQuestion securityQuestion)
        {
            if (id != securityQuestion.Id)
            {
                return BadRequest();
            }

            _context.Entry(securityQuestion).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SecurityQuestionExists(id))
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

        // POST: api/SecurityQuestions
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<SecurityQuestion>> PostSecurityQuestion(SecurityQuestion securityQuestion)
        {
            _context.SecurityQuestions.Add(securityQuestion);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSecurityQuestion", new { id = securityQuestion.Id }, securityQuestion);
        }

        // DELETE: api/SecurityQuestions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSecurityQuestion(int id)
        {
            var securityQuestion = await _context.SecurityQuestions.FindAsync(id);
            if (securityQuestion == null)
            {
                return NotFound();
            }

            _context.SecurityQuestions.Remove(securityQuestion);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool SecurityQuestionExists(int id)
        {
            return _context.SecurityQuestions.Any(e => e.Id == id);
        }
    }
}
