using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryMeasurementUnitsController(ICategoryMeasurementUnitService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            return Ok(await service.GetAllByCompanyIdAsync(companyId));
        }
    }
}