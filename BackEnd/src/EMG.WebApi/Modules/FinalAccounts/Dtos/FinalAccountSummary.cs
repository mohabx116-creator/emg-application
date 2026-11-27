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
    public decimal HistoricalFundingAdjustments { get; init; }
    public bool HasRecoveredWork { get; init; }
    public IReadOnlyList<CurrencyFinancialSummary> Currencies { get; init; } = [];
    public IReadOnlyList<SectionFinancialSummary> Sections { get; init; } = [];
    public decimal TotalOutgoing { get; init; }
    public decimal OfficePaid { get; init; }
    public decimal OfficeNetDue { get; init; }
    public decimal TotalWorks { get; init; }
    public decimal TotalPayments { get; init; }
    public decimal CashBalance { get; init; }
    public string CashBalanceStatus { get; init; } = "متزن";
    public bool HasUnresolvedImportRecords { get; init; }
    public IReadOnlyDictionary<string, decimal> OutgoingByCategory { get; init; } = new Dictionary<string, decimal>();
    public IReadOnlyDictionary<string, decimal> IncomingByCategory { get; init; } = new Dictionary<string, decimal>();
}

public sealed record SectionFinancialSummary(string Category, string Currency, decimal Works,
    decimal AllocatedFunding, decimal FundingLessWorks);

public sealed record CurrencyFinancialSummary(string Currency, decimal Works, decimal Payments, decimal CashBalance,
    decimal OfficeFee, decimal OfficePayments, decimal OfficeOutstanding, decimal ReferenceTransfers);
