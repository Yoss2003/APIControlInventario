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
    public class AccountReceivablesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AccountReceivablesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/AccountReceivables
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AccountReceivable>>> GetAccountsReceivables()
        {
            return await _context.AccountsReceivables.ToListAsync();
        }

        // GET: api/AccountReceivables/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AccountReceivable>> GetAccountReceivable(int id)
        {
            var accountReceivable = await _context.AccountsReceivables.FindAsync(id);

            if (accountReceivable == null)
            {
                return NotFound();
            }

            return accountReceivable;
        }

        // PUT: api/AccountReceivables/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccountReceivable(int id, AccountReceivable accountReceivable)
        {
            if (id != accountReceivable.Id)
            {
                return BadRequest();
            }

            _context.Entry(accountReceivable).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AccountReceivableExists(id))
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

        // POST: api/AccountReceivables
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<AccountReceivable>> PostAccountReceivable(AccountReceivable accountReceivable)
        {
            _context.AccountsReceivables.Add(accountReceivable);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAccountReceivable", new { id = accountReceivable.Id }, accountReceivable);
        }

        // DELETE: api/AccountReceivables/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccountReceivable(int id)
        {
            var accountReceivable = await _context.AccountsReceivables.FindAsync(id);
            if (accountReceivable == null)
            {
                return NotFound();
            }

            _context.AccountsReceivables.Remove(accountReceivable);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AccountReceivableExists(int id)
        {
            return _context.AccountsReceivables.Any(e => e.Id == id);
        }
    }
}
