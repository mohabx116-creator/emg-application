namespace EMG.Api.Modules.Reports.Dtos;

public sealed record OperationalAuditSummary(
    int ProjectId,
    decimal ExcavationTotal,
    decimal ConcreteTotal,
    decimal SteelTotal,
    decimal FinishingTotal,
    decimal SupplierSuppliesTotal,
    decimal SupplierPaymentsTotal,
    IReadOnlyList<AuditCheck> Checks);

public sealed record AuditCheck(string Name, bool IsValid, decimal Difference, string Message);
