using ControlInventario.Shared.Models;

namespace InventoryAPI.Models.DTO
{
    public class ShareRequestDTO
    {
        public int InventoryId { get; set; }
        public string GuestIdentifier { get; set; } = string.Empty;
        public SharedInventory.AccessMode AccessLevel { get; set; }
    }
}
