namespace EMG.Domain.Entities;

public sealed class ContractorContract
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int ContractorId { get; set; }
    public Contractor Contractor { get; set; } = null!;
    public string ContractNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "EGP";
    public DateTime? StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
    public string? Terms { get; set; }
    public string Status { get; set; } = "مسودة";
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<ContractorContractItem> Items { get; set; } = [];
    public ICollection<ContractorStatement> Statements { get; set; } = [];
}
