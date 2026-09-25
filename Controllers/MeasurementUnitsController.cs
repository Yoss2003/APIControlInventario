using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    public class MeasurementUnitsController(IMeasurementUnitService measurementUnitService) : BaseApiController
    {
        private readonly IMeasurementUnitService _measurementUnitService = measurementUnitService;

        [HttpGet]
        public async Task<IActionResult> GetMeasurementUnits()
        {
            var units = await _measurementUnitService.GetAllAsync();
            return Ok(units);
        }

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