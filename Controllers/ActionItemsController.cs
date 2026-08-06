using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActionItemsController : ControllerBase
    {
        private readonly IActionItemService _actionItemService;

        public ActionItemsController(IActionItemService actionItemService)
        {
            _actionItemService = actionItemService;
        }

        // GET: api/ActionItems
        [HttpGet]
        public async Task<IActionResult> GetActions()
        {
            try
            {
                // Actualizado a GetAllAsync()
                var actionItems = await _actionItemService.GetAllAsync();
                return Ok(actionItems);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        // GET: api/ActionItems/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetActionItem(int id)
        {
            try
            {
                // Actualizado a GetByIdAsync()
                var actionItem = await _actionItemService.GetByIdAsync(id);

                if (actionItem == null)
                {
                    return NotFound();
                }

                return Ok(actionItem);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        // PUT: api/ActionItems/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutActionItem(int id, [FromBody] ActionItem actionItem)
        {
            if (id != actionItem.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var existingActionItem = await _actionItemService.GetByIdAsync(id);
                if (existingActionItem == null)
                {
                    return NotFound();
                }

                // Actualizado a UpdateAsync()
                var success = await _actionItemService.UpdateAsync(actionItem);
                if (!success)
                {
                    return BadRequest("No se pudo actualizar el Action Item.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        // POST: api/ActionItems
        [HttpPost]
        public async Task<IActionResult> PostActionItem([FromBody] ActionItem actionItem)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Actualizado a CreateAsync()
                var success = await _actionItemService.CreateAsync(actionItem);
                if (!success)
                {
                    return BadRequest("No se pudo crear el Action Item.");
                }

                return CreatedAtAction(nameof(GetActionItem), new { id = actionItem.Id }, actionItem);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        // DELETE: api/ActionItems/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteActionItem(int id)
        {
            try
            {
                var existingActionItem = await _actionItemService.GetByIdAsync(id);
                if (existingActionItem == null)
                {
                    return NotFound();
                }

                // Actualizado a DeleteAsync()
                var success = await _actionItemService.DeleteAsync(id);
                if (!success)
                {
                    return BadRequest("No se pudo eliminar el Action Item.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }
    }
}