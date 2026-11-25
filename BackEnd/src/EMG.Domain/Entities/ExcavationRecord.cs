namespace EMG.Domain.Entities;

public sealed class ExcavationRecord
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string AccountCategory { get; set; } = "العرب";
    public string CalculationMethod { get; set; } = "كمية مباشرة";
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Depth { get; set; }
    public decimal CalculatedQuantity { get; set; }
    public string Unit { get; set; } = "م3";
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string? SoilType { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime ExcavationDate { get; set; } = DateTime.UtcNow.Date;
    public string Status { get; set; } = "مسودة";
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
