using System.Text.Json.Serialization;

namespace InventoryAPI.Models
{
    public class SharedInventory
    {
        public enum AccessMode
        {
            Lector = 1,
            Editor = 2
        }

        public int Id { get; set; }
        public int InventoryId { get; set; }
        public int UserId { get; set; }
        public AccessMode AccessLevel { get; set; }
        public DateTime SharedDate { get; set; } = DateTime.Now;
        [JsonIgnore]
        public Inventory? Inventory { get; set; }
        public User? User { get; set; }
        public int CompanyId { get; set; }
    }
}