namespace InventoryAPI.Models
{
    public class Company
    {
        public int Id { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string Ruc { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }
        public string? PrimaryColorHex { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime RegistrationDate { get; set; } = DateTime.Now;
        public List<User>? Users { get; set; }
        public DateTime? DeletionDate { get; set; }
        public string? DeletionUser { get; set; }
    }
}