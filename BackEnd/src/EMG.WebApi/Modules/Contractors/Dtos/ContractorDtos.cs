namespace EMG.Api.Modules.Contractors.Dtos;

public sealed record CreateContractorRequest(string Name, string? Phone, string? TaxNumber, string? Notes);
public sealed record ContractorSummary(int Id, string Name, string Phone, string TaxNumber, string Notes, bool IsActive);
public sealed record CreateContractRequest(int ProjectId, string ContractNumber, string Currency, DateTime? StartsOn, DateTime? EndsOn, string? Terms);
public sealed record ContractorContractSummary(int Id, int ProjectId, string ProjectName, int ContractorId, string ContractorName, string ContractNumber, string Currency, string Status, int ItemCount);
public sealed record CreateContractItemRequest(string Description, string Unit, decimal ApprovedQuantity, decimal UnitRate, string? SourceReference);
public sealed record ContractorContractItemSummary(int Id, int ContractId, string Description, string Unit, decimal ApprovedQuantity, decimal UnitRate, string SourceReference, decimal UsedQuantity, decimal RemainingQuantity);
public sealed record StatementLineRequest(int ContractItemId, string SourceEntityType, int SourceEntityId, decimal Quantity, decimal UnitRate, string SourceReference);
public sealed record CreateStatementRequest(int ProjectId, int ContractorId, int ContractId, DateTime PeriodFrom, DateTime PeriodTo, string Currency, string? SourceReference, IReadOnlyList<StatementLineRequest> Lines);
public sealed record ContractorStatementLineSummary(int Id, int ContractItemId, string Description, string SourceEntityType, int SourceEntityId, decimal Quantity, string Unit, decimal UnitRate, decimal GrossAmount, string SourceReference);
public sealed record ContractorStatementSummary(int Id, int ProjectId, string ProjectName, int ContractorId, string ContractorName, int ContractId, string ContractNumber, DateTime PeriodFrom, DateTime PeriodTo, string Currency, string Status, decimal GrossAmount, decimal PreviousPaymentsAmount, decimal DeductionsAmount, decimal NetAmount, string SourceReference, IReadOnlyList<ContractorStatementLineSummary> Lines);
public sealed record EligibleContractorWorkSummary(string SourceEntityType, int SourceEntityId, string Description, string Unit, decimal Quantity, decimal UnitRate, string Status, string SourceReference, bool AlreadyUsed);
