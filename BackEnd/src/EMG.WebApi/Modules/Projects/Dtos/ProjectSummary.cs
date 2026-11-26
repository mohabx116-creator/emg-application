namespace EMG.Api.Modules.Projects.Dtos;

public sealed record ProjectSummary(
    int Id,
    string Name,
    string Code,
    string ClientName,
    string Location,
    string Status,
    string EngineerName,
    int OpenMovements,
    decimal TotalIssued,
    decimal TotalReceived)
{
    public decimal TotalWorks { get; init; }
    public decimal TotalPayments { get; init; }
    public decimal CashBalance { get; init; }
    public string CashBalanceStatus { get; init; } = "الحساب متوازن";
}
