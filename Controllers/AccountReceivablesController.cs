using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountReceivablesController : ControllerBase
    {
        private readonly IAccountReceivableService _accountReceivableService;

        public AccountReceivablesController(IAccountReceivableService accountReceivableService)
        {
            _accountReceivableService = accountReceivableService;
        }

        // GET: api/AccountReceivables
        [HttpGet]
        public async Task<IActionResult> GetAccountsReceivables()
        {
            try
            {
                // Usamos el método genérico GetAllAsync() heredado del WorkContainer
                var accountReceivables = await _accountReceivableService.GetAllAsync();
                return Ok(accountReceivables);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar las cuentas por cobrar: {ex.Message}");
            }
        }

        // GET: api/AccountReceivables/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAccountReceivable(int id)
        {
            try
            {
                // Usamos GetByIdAsync()
                var accountReceivable = await _accountReceivableService.GetByIdAsync(id);

                if (accountReceivable == null)
                {
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id}.");
                }

                return Ok(accountReceivable);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar la cuenta por cobrar: {ex.Message}");
            }
        }

        // POST: api/AccountReceivables
        [HttpPost]
        public async Task<IActionResult> PostAccountReceivable([FromBody] AccountReceivable accountReceivable)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Usamos CreateAsync()
                var success = await _accountReceivableService.CreateAsync(accountReceivable);
                if (!success)
                {
                    return BadRequest("No se pudo crear la cuenta por cobrar. Verifique los datos enviados.");
                }

                return CreatedAtAction(nameof(GetAccountReceivable), new { id = accountReceivable.Id }, accountReceivable);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al crear la cuenta por cobrar: {ex.Message}");
            }
        }

        // PUT: api/AccountReceivables/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccountReceivable(int id, [FromBody] AccountReceivable accountReceivable)
        {
            if (id != accountReceivable.Id)
            {
                return BadRequest("El ID de la URL no coincide con el ID de la cuenta por cobrar proporcionada.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var existingAccountReceivable = await _accountReceivableService.GetByIdAsync(id);
                if (existingAccountReceivable == null)
                {
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} para actualizar.");
                }

                // Usamos UpdateAsync()
                var success = await _accountReceivableService.UpdateAsync(accountReceivable);
                if (!success)
                {
                    return BadRequest("No se pudo actualizar la cuenta por cobrar.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al actualizar la cuenta por cobrar: {ex.Message}");
            }
        }

        // DELETE: api/AccountReceivables/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccountReceivable(int id)
        {
            try
            {
                var existingAccountReceivable = await _accountReceivableService.GetByIdAsync(id);
                if (existingAccountReceivable == null)
                {
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} para eliminar.");
                }

                // Usamos DeleteAsync()
                var success = await _accountReceivableService.DeleteAsync(id);
                if (!success)
                {
                    return BadRequest("No se pudo eliminar la cuenta por cobrar.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al eliminar la cuenta por cobrar: {ex.Message}");
            }
        }
    }
}