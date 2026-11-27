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
    public string HistoricalFundingBasis { get; init; } = "operational";
    public bool HistoricalFundingUnknown { get; init; }
    public IReadOnlyList<string> FinancialWarnings { get; init; } = [];
    public IReadOnlyList<EMG.Api.Modules.FinalAccounts.Dtos.SectionFinancialSummary> Sections { get; init; } = [];
    public string CashBalanceStatus { get; init; } = "الحساب متوازن";
}
