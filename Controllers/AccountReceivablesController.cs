using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class AccountReceivablesController(IAccountReceivableService accountReceivableService) : BaseApiController
    {
        private readonly IAccountReceivableService _accountReceivableService = accountReceivableService;

        [HttpGet]
        public async Task<IActionResult> GetAccountsReceivables()
        {
            try
            {
                int companyId = ObtenerEmpresaSegura();
                return Ok(await _accountReceivableService.GetAllByCompanyIdAsync(companyId));
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAccountReceivable(int id)
        {
            try
            {
                int companyId = ObtenerEmpresaSegura();
                var accountReceivable = await _accountReceivableService.GetByIdAsync(id);

                if (accountReceivable == null || accountReceivable.CompanyId != companyId) return NotFound();

                return Ok(accountReceivable);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<IActionResult> PostAccountReceivable([FromBody] AccountReceivable accountReceivable)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                accountReceivable.CompanyId = ObtenerEmpresaSegura();
                var success = await _accountReceivableService.CreateAsync(accountReceivable);

                if (!success) return BadRequest("No se pudo crear.");
                return CreatedAtAction(nameof(GetAccountReceivable), new { id = accountReceivable.Id }, accountReceivable);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccountReceivable(int id, [FromBody] AccountReceivable accountReceivable)
        {
            if (id != accountReceivable.Id) return BadRequest("El ID no coincide.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();
            accountReceivable.CompanyId = companyId;

            try
            {
                var existingAccountReceivable = await _accountReceivableService.GetByIdAsync(id);
                if (existingAccountReceivable == null || existingAccountReceivable.CompanyId != companyId) return NotFound();

                var success = await _accountReceivableService.UpdateAsync(accountReceivable);
                if (!success) return BadRequest("No se pudo actualizar.");

                return NoContent();
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccountReceivable(int id)
        {
            try
            {
                int companyId = ObtenerEmpresaSegura();
                var existingAccountReceivable = await _accountReceivableService.GetByIdAsync(id);

                if (existingAccountReceivable == null || existingAccountReceivable.CompanyId != companyId) return NotFound();

                var success = await _accountReceivableService.DeleteAsync(id);
                if (!success) return BadRequest("No se pudo eliminar.");

                return NoContent();
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }
    }
}