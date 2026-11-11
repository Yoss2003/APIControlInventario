using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InventoryAPI.Models;

// Cronograma propio de una venta; no modifica las cuentas por cobrar de movimientos.
public class SaleInstallment
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    [JsonIgnore] public Sale? Sale { get; set; }
    public int CompanyId { get; set; }
    public int InstallmentNumber { get; set; }
    public decimal InstallmentAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateOnly DueDate { get; set; }
    [MaxLength(20)] public string Frequency { get; set; } = "Mensual";
    [JsonIgnore] public List<SaleInstallmentReceipt> Receipts { get; set; } = [];
}

// Cada abono es inmutable y conserva su método real (Yape, Plin, Bim, etc.).
public class SaleInstallmentReceipt
{
    public int Id { get; set; }
    public int SaleInstallmentId { get; set; }
    [JsonIgnore] public SaleInstallment? SaleInstallment { get; set; }
    public int CompanyId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public PaymentType PaymentType { get; set; }
    public int ReceivedByUserId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public Guid OperationKey { get; set; }
}

public sealed class SaleInstallmentPlanRequest
{
    public int InstallmentCount { get; set; }
    public decimal DownPayment { get; set; }
    public string Frequency { get; set; } = "Mensual";
    public DateOnly? FirstDueDate { get; set; }
}
