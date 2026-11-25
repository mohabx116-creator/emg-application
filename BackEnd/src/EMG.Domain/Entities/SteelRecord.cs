namespace EMG.Domain.Entities;

public sealed class SteelRecord
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string AccountCategory { get; set; } = "الحديد";
    public string OperationType { get; set; } = "توريد حديد";
    public string? SteelType { get; set; }
    public string? BarDiameter { get; set; }
    public string? Grade { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "طن";
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OperationDate { get; set; } = DateTime.UtcNow.Date;
    public string? DeliveryNoteNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "مسودة";
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
