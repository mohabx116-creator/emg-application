using EMG.Api.Modules.Suppliers.Dtos;

namespace EMG.Api.Modules.Suppliers.Services;

public interface ISupplierInvoiceService
{
    Task<SupplierInvoiceSummary?> CreateAsync(CreateSupplierInvoiceRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<SupplierInvoiceSummary?> GetAsync(int invoiceId, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<SupplierInvoiceSummary?> UpdateAsync(int invoiceId, UpdateSupplierInvoiceRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int invoiceId, int? userId, bool globalAccess, CancellationToken cancellationToken);
}
