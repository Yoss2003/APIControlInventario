using ControlInventario.Shared.Models;
using InventoryAPI.Data;
using InventoryAPI.Models.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InventoriesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Inventories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Inventory>>> GetInventories()
        {
            return await _context.Inventories.ToListAsync();
        }

        // GET: api/Inventories/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Inventory>> GetInventory(int id)
        {
            var inventory = await _context.Inventories.FindAsync(id);

            if (inventory == null)
            {
                return NotFound();
            }

            return inventory;
        }

        // PUT: api/Inventories/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutInventory(int id, Inventory inventory)
        {
            if (id != inventory.Id)
            {
                return BadRequest();
            }

            _context.Entry(inventory).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryExists(id))
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

        // POST: api/Inventories
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Inventory>> PostInventory(Inventory inventory)
        {
            _context.Inventories.Add(inventory);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetInventory", new { id = inventory.Id }, inventory);
        }

        // DELETE: api/Inventories/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventory(int id)
        {
            var inventory = await _context.Inventories.FindAsync(id);
            if (inventory == null)
            {
                return NotFound();
            }

            _context.Inventories.Remove(inventory);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool InventoryExists(int id)
        {
            return _context.Inventories.Any(e => e.Id == id);
        }

        [HttpPost("Share")]
        public async Task<IActionResult> ShareInventory([FromBody] ShareRequestDTO request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.GuestIdentifier))
            {
                return BadRequest(new { mensaje = "Datos de invitación inválidos." });
            }

            if (!Enum.IsDefined(request.AccessLevel))
            {
                return BadRequest(new { mensaje = "El nivel de acceso enviado no es válido (Debe ser 1 para Lector o 2 para Editor)." });
            }

            var guestUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username!.ToLower() == request.GuestIdentifier.Trim().ToLower() ||
                                          u.Email!.ToLower() == request.GuestIdentifier.Trim().ToLower());

            if (guestUser == null)
            {
                return NotFound(new { mensaje = "El usuario o correo ingresado no existe en el sistema." });
            }

            var inventory = await _context.Inventories.FindAsync(request.InventoryId);
            if (inventory == null)
            {
                return NotFound(new { mensaje = "El inventario especificado no existe." });
            }

            if (inventory.UserId == guestUser.Id)
            {
                return BadRequest(new { mensaje = "No puedes compartir el inventario con el dueño del mismo." });
            }

            var yaCompartido = await _context.SharedInventories
                .AnyAsync(s => s.InventoryId == request.InventoryId && s.UserId == guestUser.Id);

            if (yaCompartido)
            {
                return BadRequest(new { mensaje = "Este inventario ya se encuentra compartido con este usuario." });
            }

            var sharedRelation = new SharedInventory
            {
                InventoryId = request.InventoryId,
                UserId = guestUser.Id,
                AccessLevel = request.AccessLevel,
                SharedDate = DateTime.Now
            };

            try
            {
                _context.SharedInventories.Add(sharedRelation);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = $"Inventario compartido con {guestUser.Username} en modo [{request.AccessLevel}] con éxito."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error interno al procesar la compartición.", detalle = ex.Message });
            }
        }
    }
}
