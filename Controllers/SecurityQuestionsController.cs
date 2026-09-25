using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
namespace InventoryAPI.Controllers
{
    public class SecurityQuestionsController(ISecurityQuestionService service) : BaseApiController
    {
        [HttpGet] public async Task<IActionResult> GetSecurityQuestions() => Ok(await service.GetAllAsync());
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSecurityQuestion(int id)
        {
            var question = await service.GetByIdAsync(id);
            return question == null ? NotFound() : Ok(question);
        }
    }
}