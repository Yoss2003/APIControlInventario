using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MeasurementUnitsController(IMeasurementUnitService measurementUnitService) : ControllerBase
    {
        private readonly IMeasurementUnitService _measurementUnitService = measurementUnitService;

        // GET: api/MeasurementUnits
        [HttpGet]
        public async Task<IActionResult> GetMeasurementUnits()
        {
            var units = await _measurementUnitService.GetAllAsync();
            return Ok(units);
        }

        // GET: api/MeasurementUnits/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMeasurementUnit(int id)
        {
            var unit = await _measurementUnitService.GetByIdAsync(id);

            if (unit == null)
            {
                return NotFound();
            }

            return Ok(unit);
        }
    }
}