namespace InventoryAPI.Models.DTO
{
    public class EmployeeUpdateDTO
    {
        public Employee Employee { get; set; } = null!;
        public string? UpdatedEmail { get; set; }
    }
}