namespace EMG.Api.Modules.Advances.Dtos;

public sealed record AllocationTarget(string Kind, int Id, string Description, string Currency, decimal Amount, decimal Remaining);