namespace EMG.Api.Modules.Suppliers.Services;

public sealed class DuplicateSupplierTransactionException(string message) : Exception(message);
