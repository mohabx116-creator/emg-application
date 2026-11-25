namespace EMG.Domain.Entities;

public sealed class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SupplierType { get; set; } = "مورد";
    public string? Phone { get; set; }
    public string? TaxNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<SupplierSupply> Supplies { get; set; } = [];
    public ICollection<SupplierPayment> Payments { get; set; } = [];
}
