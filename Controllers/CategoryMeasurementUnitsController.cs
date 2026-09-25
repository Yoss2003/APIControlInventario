using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    public class CategoryMeasurementUnitsController(ICategoryMeasurementUnitService service) : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await service.GetAllByCompanyIdAsync(companyId));
        }
    }
}