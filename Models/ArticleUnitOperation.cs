using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models;

public sealed class ArticleUnitOperation
{
    [Key] public int Id { get; set; }
    public int CompanyId { get; set; }
    public int ArticleId { get; set; }
    public int ArticleDetailId { get; set; }
    public int? SaleId { get; set; }
    public int? AssignmentId { get; set; }
    public bool IsReversed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReversedAt { get; set; }
}
