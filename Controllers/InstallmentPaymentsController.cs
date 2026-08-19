using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InstallmentPaymentsController(IInstallmentPaymentService installmentPaymentService) : ControllerBase
    {
        private readonly IInstallmentPaymentService _installmentPaymentService = installmentPaymentService;

        // GET: api/InstallmentPayments
        [HttpGet]
        public async Task<IActionResult> GetInstallmentPayments()
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
                int companyId = int.Parse(companyIdHeader!);

                var payments = await _installmentPaymentService.GetAllByCompanyIdAsync(companyId);
                return Ok(payments);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        // GET: api/InstallmentPayments/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetInstallmentPayment(int id)
        {
            try
            {
                var payment = await _installmentPaymentService.GetByIdAsync(id);

                if (payment == null)
                {
                    return NotFound($"No se encontró el pago en cuotas con ID {id}.");
                }

                return Ok(payment);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar el pago en cuotas: {ex.Message}");
            }
        }

        // PUT: api/InstallmentPayments/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutInstallmentPayment(int id, [FromBody] InstallmentPayment installmentPayment)
        {
            if (id != installmentPayment.Id)
            {
                return BadRequest("El ID de la URL no coincide con el ID del pago proporcionado.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var existingPayment = await _installmentPaymentService.GetByIdAsync(id);
                if (existingPayment == null)
                {
                    return NotFound($"No se encontró el pago en cuotas con ID {id} para actualizar.");
                }

                var success = await _installmentPaymentService.UpdateAsync(installmentPayment);
                if (!success)
                {
                    return BadRequest("No se pudo actualizar el pago en cuotas.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al actualizar el pago en cuotas: {ex.Message}");
            }
        }

        // POST: api/InstallmentPayments
        [HttpPost]
        public async Task<IActionResult> PostInstallmentPayment([FromBody] InstallmentPayment installmentPayment)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                if (Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                    installmentPayment.CompanyId = int.Parse(companyIdHeader!);

                var success = await _installmentPaymentService.CreateAsync(installmentPayment);
                if (!success) return BadRequest("No se pudo crear.");
                return CreatedAtAction(nameof(GetInstallmentPayment), new { id = installmentPayment.Id }, installmentPayment);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        // DELETE: api/InstallmentPayments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInstallmentPayment(int id)
        {
            try
            {
                var existingPayment = await _installmentPaymentService.GetByIdAsync(id);
                if (existingPayment == null)
                {
                    return NotFound($"No se encontró el pago en cuotas con ID {id} para eliminar.");
                }

                var success = await _installmentPaymentService.DeleteAsync(id);
                if (!success)
                {
                    return BadRequest("No se pudo eliminar el pago en cuotas.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al eliminar el pago en cuotas: {ex.Message}");
            }
        }
    }
}