using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class ActionItemsController(IActionItemService actionItemService) : BaseApiController
    {
        private readonly IActionItemService _actionItemService = actionItemService;

        [HttpGet]
        public async Task<IActionResult> GetActions()
        {
            try
            {
                var actionItems = await _actionItemService.GetAllAsync();
                return Ok(actionItems);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno del servidor: {ex.Message}"); }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetActionItem(int id)
        {
            try
            {
                var actionItem = await _actionItemService.GetByIdAsync(id);
                if (actionItem == null) return NotFound();
                return Ok(actionItem);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno del servidor: {ex.Message}"); }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutActionItem(int id, [FromBody] ActionItem actionItem)
        {
            if (id != actionItem.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var existingActionItem = await _actionItemService.GetByIdAsync(id);
                if (existingActionItem == null) return NotFound();

                var success = await _actionItemService.UpdateAsync(actionItem);
                if (!success) return BadRequest("No se pudo actualizar el Action Item.");

                return NoContent();
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno del servidor: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<IActionResult> PostActionItem([FromBody] ActionItem actionItem)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var success = await _actionItemService.CreateAsync(actionItem);
                if (!success) return BadRequest("No se pudo crear el Action Item.");

                return CreatedAtAction(nameof(GetActionItem), new { id = actionItem.Id }, actionItem);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno del servidor: {ex.Message}"); }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteActionItem(int id)
        {
            try
            {
                var existingActionItem = await _actionItemService.GetByIdAsync(id);
                if (existingActionItem == null) return NotFound();

                var success = await _actionItemService.DeleteAsync(id);
                if (!success) return BadRequest("No se pudo eliminar el Action Item.");

                return NoContent();
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno del servidor: {ex.Message}"); }
        }
    }
}