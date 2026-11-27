namespace EMG.Api.Modules.Advances.Dtos;

public sealed record AdvanceLedger(IReadOnlyList<AdvanceSummary> Receipts, IReadOnlyList<HistoricalAdvanceRow> Historical,
    IReadOnlyList<DirectFundingRow> DirectFunding, IReadOnlyList<AdvanceCurrencyTotal> Totals, IReadOnlyList<string> Warnings);
public sealed record AdvanceCurrencyTotal(string Currency, decimal Works, decimal? Payments, decimal? Allocated, decimal? CashBalance);
public sealed record HistoricalAdvanceRow(long Id, int ProjectId, string ProjectName, string Workbook, string Sheet, string Cell,
    DateTime? Date, string Description, string Category, decimal Amount, string Currency, string Basis, bool FundsProject,
    bool IsAllocation, int? ReceiptId);

public sealed record DirectFundingRow(int Id, int ProjectId, string ProjectName, string Source, string Party, DateTime? Date, decimal Amount, string Currency, string Reference);
