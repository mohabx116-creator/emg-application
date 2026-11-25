namespace EMG.Api.Modules.Suppliers.Services;

public sealed class DuplicateSupplierException(IReadOnlyList<string> matches) : Exception
{
    public IReadOnlyList<string> Matches { get; } = matches;
}
