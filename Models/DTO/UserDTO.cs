namespace InventoryAPI.Models.DTO
{
    public class UserDTO
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int Age { get; set; }
        public string BirthDate { get; set; } = string.Empty;
        public string HireDate { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;
        public string ProfilePictureUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;

        public int JobPositionId { get; set; }
        public int AreaId { get; set; }
        public int ContractTypeId { get; set; }
    }
}
