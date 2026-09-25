using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
namespace InventoryAPI.Controllers
{
    public class ThemesController(IThemeService service) : BaseApiController
    {
        [HttpGet] public async Task<IActionResult> GetThemes() => Ok(await service.GetAllAsync());
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTheme(int id)
        {
            var theme = await service.GetByIdAsync(id);
            return theme == null ? NotFound() : Ok(theme);
        }
    }
}