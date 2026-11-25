namespace EMG.Api.Modules.AccountingClassification.Dtos;

public enum AccountingClassificationCode
{
    SiteExpense,
    Excavation,
    ContractorWorkmanship,
    ConcreteStation,
    Steel,
    Buildings,
    Plaster,
    Insulation,
    Electrical,
    Plumbing,
    OfficePayment,
    OwnerFunding,
    FundingAllocation,
    SupplierSupply,
    SupplierPayment,
    Unresolved
}

public enum ClassificationResolution
{
    Resolved,
    Unresolved
}

public sealed record ClassificationProvenance(
    bool IsImported,
    string SourceText,
    string SourceSheet,
    int? SourceRow,
    string Evidence);

public sealed record AccountingClassificationLine(
    string EntityType,
    int EntityId,
    AccountingClassificationCode Classification,
    ClassificationResolution Resolution,
    decimal Amount,
    string Status,
    bool IncludedInCurrentTotals,
    ClassificationProvenance Provenance,
    string Diagnostic,
    DateTime? TransactionDate,
    string PartyName);

public sealed record AccountingClassificationTotal(
    AccountingClassificationCode Classification,
    decimal ImportedApprovedPostedAmount,
    decimal CurrentApprovedPostedAmount,
    int ImportedRecordCount,
    int CurrentRecordCount,
    int UnresolvedRecordCount);

public sealed record AccountingClassificationPreview(
    int ProjectId,
    string ProjectCode,
    string ProjectName,
    decimal OfficeSharePercentage,
    string SnapshotRule,
    IReadOnlyList<AccountingClassificationTotal> Totals,
    IReadOnlyList<AccountingClassificationLine> Lines,
    IReadOnlyList<AccountingClassificationLine> UnresolvedRecords);
