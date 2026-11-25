using EMG.Api.Modules.Suppliers.Dtos;

namespace EMG.Api.Modules.Suppliers.Services;

public interface ISuppliersService
{
    Task<IReadOnlyList<SupplierSummary>> GetSuppliersAsync(int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<SupplierSummary?> GetSupplierAsync(int id, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<SupplierSummary> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupplySummary>> GetSuppliesAsync(int supplierId, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<SupplySummary?> AddSupplyAsync(int supplierId, CreateSupplyRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentSummary>> GetPaymentsAsync(int supplierId, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<PaymentSummary?> AddPaymentAsync(int supplierId, CreateSupplierPaymentRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken);
}
