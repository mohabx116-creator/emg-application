using System.Text.Json;

namespace EMG.Api.Modules.Transactions.Dtos;

public sealed record TransactionCorrectionRequest(IReadOnlyDictionary<string, JsonElement> Values, string Reason);
public sealed record TransactionCancellationRequest(string Reason);
public sealed record TransactionOperationResult(string EntityType, int EntityId, string Status, string Action, DateTime ActionDateUtc);
