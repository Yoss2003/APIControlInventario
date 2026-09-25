using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class InstallmentPaymentsController(IInstallmentPaymentService installmentPaymentService) : BaseApiController
    {
        private readonly IInstallmentPaymentService _installmentPaymentService = installmentPaymentService;

        [HttpGet]
        public async Task<IActionResult> GetInstallmentPayments()
        {
            try
            {
                int companyId = ObtenerEmpresaSegura();
                return Ok(await _installmentPaymentService.GetAllByCompanyIdAsync(companyId));
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetInstallmentPayment(int id)
        {
            try
            {
                int companyId = ObtenerEmpresaSegura();
                var payment = await _installmentPaymentService.GetByIdAsync(id);

                if (payment == null || payment.CompanyId != companyId) return NotFound();
                return Ok(payment);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutInstallmentPayment(int id, [FromBody] InstallmentPayment installmentPayment)
        {
            if (id != installmentPayment.Id) return BadRequest("El ID no coincide.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();
            installmentPayment.CompanyId = companyId;

            try
            {
                var existingPayment = await _installmentPaymentService.GetByIdAsync(id);
                if (existingPayment == null || existingPayment.CompanyId != companyId) return NotFound();

                var success = await _installmentPaymentService.UpdateAsync(installmentPayment);
                if (!success) return BadRequest("No se pudo actualizar.");

                return NoContent();
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<IActionResult> PostInstallmentPayment([FromBody] InstallmentPayment installmentPayment)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                installmentPayment.CompanyId = ObtenerEmpresaSegura();
                var success = await _installmentPaymentService.CreateAsync(installmentPayment);

                if (!success) return BadRequest("No se pudo crear.");
                return CreatedAtAction(nameof(GetInstallmentPayment), new { id = installmentPayment.Id }, installmentPayment);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInstallmentPayment(int id)
        {
            try
            {
                int companyId = ObtenerEmpresaSegura();
                var existingPayment = await _installmentPaymentService.GetByIdAsync(id);

                if (existingPayment == null || existingPayment.CompanyId != companyId) return NotFound();

                var success = await _installmentPaymentService.DeleteAsync(id);
                if (!success) return BadRequest("No se pudo eliminar.");

                return NoContent();
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }
    }
}