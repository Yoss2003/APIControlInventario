using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ThemesController(IThemeService themeService) : ControllerBase
    {
        private readonly IThemeService _themeService = themeService;

        // GET: api/Themes
        [HttpGet]
        public async Task<IActionResult> GetThemes()
        {
            var themes = await _themeService.GetAllAsync();
            return Ok(themes);
        }

        // GET: api/Themes/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTheme(int id)
        {
            var theme = await _themeService.GetByIdAsync(id);

            if (theme == null)
            {
                return NotFound();
            }

            return Ok(theme);
        }
    }
}