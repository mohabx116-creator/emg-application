namespace EMG.Domain.Entities;

public sealed class ConcreteRecord
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string AccountCategory { get; set; } = "المقاول";
    public string ConcreteType { get; set; } = "خرسانة مسلحة";
    public string ElementType { get; set; } = string.Empty;
    public string? MixGrade { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "م3";
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime PourDate { get; set; } = DateTime.UtcNow.Date;
    public string? DeliveryNoteNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "مسودة";
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
