namespace EMG.Domain.Entities;

public sealed class FinishingRecord
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string AccountCategory { get; set; } = "غير محدد";
    public string WorkType { get; set; } = string.Empty;
    public string? AreaName { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "م2";
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime WorkDate { get; set; } = DateTime.UtcNow.Date;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "مسودة";
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
