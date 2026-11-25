using EMG.Api.Modules.AccountingClassification.Dtos;

namespace EMG.Api.Modules.ConsolidatedStatements.Dtos;

public sealed record ConsolidatedStatementPreview(
    DateTime GeneratedAtUtc,
    string SnapshotRule,
    IReadOnlyList<ConsolidatedStatementRow> Projects);

public sealed record ConsolidatedStatementRow(
    int ProjectId,
    string ProjectCode,
    string ProjectName,
    IReadOnlyList<MasterStatementColumn> Columns,
    ConsolidatedFinancialDimensions Dimensions,
    OfficeAccrualPreview OfficeAccrual,
    IReadOnlyList<NetTransactionDiagnostic> NetTransactionDiagnostics,
    IReadOnlyList<UnresolvedRecordDiagnostic> UnresolvedRecords,
    bool IsComplete,
    string CompletenessStatus);

public sealed record MasterStatementColumn(
    string Column,
    string Heading,
    decimal? ImportedAmount,
    decimal? CurrentAmount,
    decimal? DiagnosticAmount,
    decimal UnresolvedAmount,
    int UnresolvedCount,
    bool IsOfficial,
    string Status,
    string Diagnostic);

public sealed record ConsolidatedFinancialDimensions(
    StatementDimension OperationalCosts,
    StatementDimension SupplierSupplies,
    StatementDimension SupplierPayments,
    StatementDimension OwnerFunding,
    StatementDimension AdvanceAllocations,
    StatementDimension OfficePayments,
    StatementDimension UnresolvedFinancialRecords);

public sealed record StatementDimension(
    decimal ImportedAmount,
    decimal CurrentAmount,
    int ImportedCount,
    int CurrentCount,
    string Meaning);

public sealed record OfficeAccrualPreview(
    decimal? DiagnosticAmount,
    decimal OfficeSharePercentage,
    decimal BasisAmount,
    bool IsOfficial,
    string Status,
    string Diagnostic);

public sealed record NetTransactionDiagnostic(
    string Scenario,
    decimal? Amount,
    string Formula,
    string Status);

public sealed record UnresolvedRecordDiagnostic(
    string EntityType,
    int EntityId,
    decimal Amount,
    string Status,
    string SourceText,
    string SourceSheet,
    int? SourceRow,
    string Diagnostic);
