using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers
{
    public class DateFormatsController(IDateFormatService dateFormatService) : BaseApiController
    {
        private readonly IDateFormatService _dateFormatService = dateFormatService;

        [HttpGet]
        public async Task<IActionResult> GetDateFormats()
        {
            try
            {
                var formats = await _dateFormatService.GetAllAsync();
                return Ok(formats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDateFormat(int id)
        {
            try
            {
                var format = await _dateFormatService.GetByIdAsync(id);
                if (format == null) return NotFound();
                return Ok(format);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}