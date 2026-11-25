namespace EMG.Api.Modules.FinalAccounts.Dtos;

public sealed record FinalAccountSummary(
    int ProjectId,
    string ProjectName,
    string ClientName,
    string PayerName,
    decimal OfficeSharePercentage,
    decimal TotalIncoming,
    decimal TotalExpenses,
    decimal SupplierOutstanding,
    decimal NetBeforeOfficeShare,
    decimal OfficeShareAmount,
    decimal NetAfterOfficeShare,
    decimal PayerBalance,
    string BalanceStatus,
    string Status)
{
    public string ProjectCode { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public decimal TotalOutgoing { get; init; }
    public decimal OfficePaid { get; init; }
    public decimal OfficeNetDue { get; init; }
    public IReadOnlyDictionary<string, decimal> OutgoingByCategory { get; init; } = new Dictionary<string, decimal>();
    public IReadOnlyDictionary<string, decimal> IncomingByCategory { get; init; } = new Dictionary<string, decimal>();
}
