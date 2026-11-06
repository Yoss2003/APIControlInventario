namespace InventoryAPI.Models
{
    public class CompanyPublicDTO
    {
        public int Id { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? PrimaryColorHex { get; set; }
    }
}