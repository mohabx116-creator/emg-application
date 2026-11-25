using EMG.Api.Modules.FinalAccounts.Dtos;

namespace EMG.Api.Modules.Reports.Dtos;

public sealed record ProjectReportSummary(
    FinalAccountSummary FinalAccount,
    IReadOnlyList<ProjectMovementRow> Movements,
    IReadOnlyList<ProjectAdvanceRow> Advances,
    IReadOnlyList<ProjectSupplierSupplyRow> SupplierSupplies,
    IReadOnlyList<ProjectSupplierPaymentRow> SupplierPayments,
    IReadOnlyList<ProjectWorkRow> Excavations,
    IReadOnlyList<ProjectWorkRow> Concretes,
    IReadOnlyList<ProjectWorkRow> Steels,
    IReadOnlyList<ProjectWorkRow> Finishings);

public sealed record ProjectMovementRow(int Id, DateTime Date, string Type, string Description, string SupplierName, string Unit, decimal? Quantity, decimal? UnitPrice, decimal Amount, string ExpenseCategory, string Status, string Reference);
public sealed record ProjectAdvanceRow(int Id, DateTime Date, string ClientName, string PayerName, decimal Amount, decimal Allocated, decimal Remaining, string Status, string Reference, IReadOnlyDictionary<string, decimal> AllocationsByCategory);
public sealed record ProjectSupplierSupplyRow(int Id, DateTime Date, string SupplierName, string SupplyType, string ItemName, string Unit, decimal Quantity, decimal UnitPrice, decimal TotalAmount, string InvoiceNumber);
public sealed record ProjectSupplierPaymentRow(int Id, DateTime Date, string SupplierName, decimal Amount, string Method, string Reference);
public sealed record ProjectWorkRow(int Id, DateTime Date, string SupplierName, string Category, string Item, string Description, string Unit, decimal? Quantity, decimal? UnitPrice, decimal TotalAmount, string Extra1, string Extra2, string Extra3, string Status, string Reference);
