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
    public class InstallmentPaymentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InstallmentPaymentsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/InstallmentPayments
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InstallmentPayment>>> GetInstallmentPayments()
        {
            return await _context.InstallmentPayments.ToListAsync();
        }

        // GET: api/InstallmentPayments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<InstallmentPayment>> GetInstallmentPayment(int id)
        {
            var installmentPayment = await _context.InstallmentPayments.FindAsync(id);

            if (installmentPayment == null)
            {
                return NotFound();
            }

            return installmentPayment;
        }

        // PUT: api/InstallmentPayments/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutInstallmentPayment(int id, InstallmentPayment installmentPayment)
        {
            if (id != installmentPayment.Id)
            {
                return BadRequest();
            }

            _context.Entry(installmentPayment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InstallmentPaymentExists(id))
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

        // POST: api/InstallmentPayments
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<InstallmentPayment>> PostInstallmentPayment(InstallmentPayment installmentPayment)
        {
            _context.InstallmentPayments.Add(installmentPayment);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetInstallmentPayment", new { id = installmentPayment.Id }, installmentPayment);
        }

        // DELETE: api/InstallmentPayments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInstallmentPayment(int id)
        {
            var installmentPayment = await _context.InstallmentPayments.FindAsync(id);
            if (installmentPayment == null)
            {
                return NotFound();
            }

            _context.InstallmentPayments.Remove(installmentPayment);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool InstallmentPaymentExists(int id)
        {
            return _context.InstallmentPayments.Any(e => e.Id == id);
        }
    }
}
