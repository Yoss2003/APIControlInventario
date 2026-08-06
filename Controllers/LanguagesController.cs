using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LanguagesController(ILanguageService languageService) : ControllerBase
    {
        private readonly ILanguageService _languageService = languageService;

        // GET: api/Languages
        [HttpGet]
        public async Task<IActionResult> GetLanguages()
        {
            var languages = await _languageService.GetAllAsync();
            return Ok(languages);
        }

        // GET: api/Languages/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetLanguage(int id)
        {
            var language = await _languageService.GetByIdAsync(id);

            if (language == null)
            {
                return NotFound();
            }

            return Ok(language);
        }
    }
}