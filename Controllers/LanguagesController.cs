using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    public class LanguagesController(ILanguageService languageService) : BaseApiController
    {
        private readonly ILanguageService _languageService = languageService;

        [HttpGet]
        public async Task<IActionResult> GetLanguages()
        {
            var languages = await _languageService.GetAllAsync();
            return Ok(languages);
        }

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