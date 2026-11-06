using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models;

public sealed class PasswordRecoveryRequest
{
    [Key] public int Id { get; set; }
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    [MaxLength(100)] public string? TokenHash { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Rejected { get; set; }
}
