using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using InventoryAPI.Models;

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

        // Las cuotas se generan atómicamente al registrar la venta;
        // cambios manuales aquí alterarían el saldo contable y permitirían cobros inconsistentes.
        [HttpPost]
        public IActionResult PostAccountReceivable() =>
            Conflict(new { error = "El cronograma solo se genera mediante POST /api/Sales/quick." });

        [HttpPut("{id:int}")]
        public IActionResult PutAccountReceivable(int id) =>
            Conflict(new { error = "No se permite editar cuotas directamente. Los abonos deben registrarse mediante InstallmentPayments." });

        [HttpDelete("{id:int}")]
        public IActionResult DeleteAccountReceivable(int id) =>
            Conflict(new { error = "No se permite borrar cuotas; anula la venta sin cobros o utiliza un proceso financiero auditado." });
    }
}
