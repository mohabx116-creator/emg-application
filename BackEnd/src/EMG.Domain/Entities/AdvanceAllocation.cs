namespace EMG.Domain.Entities;

public sealed class AdvanceAllocation
{
    public int Id { get; set; }
    public int AdvancePaymentId { get; set; }
    public AdvancePayment AdvancePayment { get; set; } = null!;
    public int? SiteMovementId { get; set; }
    public SiteMovement? SiteMovement { get; set; }
    public int? SupplierPaymentId { get; set; }
    public SupplierPayment? SupplierPayment { get; set; }
    public string AccountCategory { get; set; } = "غير محدد";
    public decimal Amount { get; set; }
    public DateTime AllocationDate { get; set; } = DateTime.UtcNow.Date;
    public string? Notes { get; set; }
}
