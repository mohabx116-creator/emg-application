namespace EMG.Domain.Entities;

public sealed class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? OwnerName { get; set; }
    public string? PayerName { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = "نشط";
    public string? EngineerName { get; set; }
    public decimal OfficeSharePercentage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<SupplierPayment> SupplierPayments { get; set; } = [];
}
