using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required] public string FirstName { get; set; } = string.Empty;
        [Required] public string LastName { get; set; } = string.Empty;
        [Required] public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
        public string BirthDate { get; set; } = string.Empty;
        [Required] public string Username { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
        public int JobPositionId { get; set; }
        public int AreaId { get; set; }
        public string HireDate { get; set; } = string.Empty;
        public int ContractTypeId { get; set; }
        public int RoleId { get; set; }
    }
}