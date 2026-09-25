using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    public class NotificationsController(INotificationService notificationService) : BaseApiController
    {
        private readonly INotificationService _notificationService = notificationService;

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            int companyId = ObtenerEmpresaSegura();
            var notifications = await _notificationService.GetAllByCompanyIdAsync(companyId);
            return Ok(notifications);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetNotification(int id)
        {
            var notification = await _notificationService.GetByIdAsync(id);
            if (notification == null) return NotFound();

            return Ok(notification);
        }
    }
}