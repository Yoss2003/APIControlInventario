namespace InventoryAPI.Models.DTO
{
    public class ChangePasswordRequest
    {
        public int UserId { get; set; }
        public required string NewPassword { get; set; }
    }
}
