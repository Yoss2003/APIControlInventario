using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SecurityQuestionsController(ISecurityQuestionService securityQuestionService) : ControllerBase
    {
        private readonly ISecurityQuestionService _securityQuestionService = securityQuestionService;

        // GET: api/SecurityQuestions
        [HttpGet]
        public async Task<IActionResult> GetSecurityQuestions()
        {
            var questions = await _securityQuestionService.GetAllAsync();
            return Ok(questions);
        }

        // GET: api/SecurityQuestions/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSecurityQuestion(int id)
        {
            var question = await _securityQuestionService.GetByIdAsync(id);

            if (question == null)
            {
                return NotFound();
            }

            return Ok(question);
        }
    }
}