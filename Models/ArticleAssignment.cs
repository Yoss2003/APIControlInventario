using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryAPI.Models;

// Custodia de stock: NO da acceso al inventario completo del remitente.
public sealed class ArticleAssignment
{
    [Key] public int Id { get; set; }
    public Guid OperationKey { get; set; }
    public int CompanyId { get; set; }
    public int InventoryId { get; set; }
    public int ArticleId { get; set; }
    public int AssignedByUserId { get; set; }
    public int? RecipientUserId { get; set; }
    [Required, MaxLength(180)] public string RecipientName { get; set; } = string.Empty;
    [Column(TypeName = "numeric(18,2)")] public decimal Quantity { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal ReturnedQuantity { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(2000)] public string? Observation { get; set; }
    [MaxLength(400)] public string? EvidencePath { get; set; }
    public List<ArticleAssignmentEvent> Events { get; set; } = [];
}

public sealed class ArticleAssignmentEvent
{
    [Key] public int Id { get; set; }
    public Guid OperationKey { get; set; }
    public int AssignmentId { get; set; }
    public int CompanyId { get; set; }
    public int ActorUserId { get; set; }
    [Required, MaxLength(24)] public string Kind { get; set; } = string.Empty; // Entrega / Devolucion
    [Column(TypeName = "numeric(18,2)")] public decimal Quantity { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    [MaxLength(2000)] public string? Observation { get; set; }
    public int MovementId { get; set; }
    public ArticleAssignment Assignment { get; set; } = null!;
}

public sealed class MovementCancellation
{
    [Key] public int Id { get; set; }
    public int OriginalMovementId { get; set; }
    public int ReversalMovementId { get; set; }
    public int CompanyId { get; set; }
    public int CancelledByUserId { get; set; }
    public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
    [Required, MaxLength(1200)] public string Reason { get; set; } = string.Empty;
}

public sealed class SaleOperation
{
    [Key] public int Id { get; set; }
    public Guid OperationKey { get; set; }
    public int SaleId { get; set; }
    public int CompanyId { get; set; }
}

public sealed class SaleCancellation
{
    [Key] public int Id { get; set; }
    public int SaleId { get; set; }
    public int CompanyId { get; set; }
    public int CancelledByUserId { get; set; }
    public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
    [Required, MaxLength(1200)] public string Reason { get; set; } = string.Empty;
}
