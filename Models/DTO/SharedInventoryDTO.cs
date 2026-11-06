namespace InventoryAPI.Models
{
    public class SharedInventoryDTO
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public SharedInventory.AccessMode AccessLevel { get; set; }
        public DateTime SharedDate { get; set; }
        public string GrantedBy { get; set; } = string.Empty;
    }
}