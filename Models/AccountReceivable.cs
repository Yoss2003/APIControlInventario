using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class AccountReceivable
    {
        [Key]
        public int Id { get; set; }
        [Required] public int MovementId { get; set; }
        public int InstallmentNumber { get; set; }
        public decimal InstallmentAmount { get; set; }
        public decimal PaidAmount { get; set; } = 0;
        public decimal LateFeeAmount { get; set; } = 0;
        [Required] public string DueDate { get; set; } = string.Empty;
        public string? PaymentDate { get; set; }
        [Required] public string Status { get; set; } = "Pending";
        [Required] public string Frequency { get; set; } = string.Empty;
        public int CompanyId { get; set; }
    }
}