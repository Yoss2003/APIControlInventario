using ControlInventario.Shared.Models;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompaniesController(ICompanyService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get() => Ok(await service.GetAllAsync());

        [HttpGet("Active")]
        public async Task<IActionResult> GetActiveCompanies()
        {
            var companies = await service.GetActiveCompaniesPublicAsync();
            return Ok(companies);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var company = await service.GetByIdAsync(id);
            if (company == null) return NotFound();
            return Ok(company);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Company company)
        {
            await service.CreateAsync(company);
            return Ok(company);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Company company)
        {
            if (id != company.Id) return BadRequest();
            await service.UpdateAsync(company);
            return Ok(company);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var company = await service.GetByIdAsync(id);
            if (company == null) return NotFound();
            await service.DeleteAsync(id);
            return Ok();
        }
    }
}