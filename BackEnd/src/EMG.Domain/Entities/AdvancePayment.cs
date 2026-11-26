namespace EMG.Domain.Entities;

public sealed class AdvancePayment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string ClientName { get; set; } = string.Empty;
    public string PayerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EGP";
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow.Date;
    public string PaymentMethod { get; set; } = "تحويل";
    public string? Reference { get; set; }
    public string Status { get; set; } = "مسودة";
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<AdvanceAllocation> Allocations { get; set; } = [];
}
