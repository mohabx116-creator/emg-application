using EMG.Api.Modules.Suppliers.Dtos;
using EMG.Api.Modules.Suppliers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;
using System.Security.Claims;

namespace EMG.Api.Modules.Suppliers.Controllers;

[ApiController]
[Authorize]
[Route("api/suppliers")]
public sealed class SuppliersController(ISuppliersService suppliersService, ISupplierInvoiceService invoiceService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("suppliers.view")]
    public async Task<ActionResult<IReadOnlyList<SupplierSummary>>> GetSuppliers(CancellationToken cancellationToken) =>
        Ok(await suppliersService.GetSuppliersAsync(CurrentUserId(), HasGlobalProjectAccess(), cancellationToken));

    [HttpGet("{id:int}")]
    [PermissionAuthorize("suppliers.view")]
    public async Task<ActionResult<SupplierSummary>> GetSupplier(int id, CancellationToken cancellationToken)
    {
        var supplier = await suppliersService.GetSupplierAsync(id, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpPost]
    [PermissionAuthorize("suppliers.create")]
    public async Task<ActionResult<SupplierSummary>> CreateSupplier(CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "اسم المورد مطلوب." });
        SupplierSummary supplier;
        try
        {
            supplier = await suppliersService.CreateAsync(request, cancellationToken);
        }
        catch (DuplicateSupplierException exception)
        {
            return Conflict(new
            {
                message = "يوجد مورد مسجل بالفعل بنفس الاسم أو رقم الهاتف أو الرقم الضريبي.",
                matches = exception.Matches
            });
        }
        return CreatedAtAction(nameof(GetSupplier), new { id = supplier.Id }, supplier);
    }

    [HttpGet("{supplierId:int}/supplies")]
    [PermissionAuthorize("suppliers.view")]
    public async Task<ActionResult<IReadOnlyList<SupplySummary>>> GetSupplies(int supplierId, CancellationToken cancellationToken) =>
        Ok(await suppliersService.GetSuppliesAsync(supplierId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken));

    [HttpPost("{supplierId:int}/supplies")]
    [PermissionAuthorize("suppliers.supplies-create")]
    public async Task<ActionResult<SupplySummary>> AddSupply(int supplierId, CreateSupplyRequest request, CancellationToken cancellationToken)
    {
        if (request.ProjectId <= 0 || string.IsNullOrWhiteSpace(request.ItemName) || string.IsNullOrWhiteSpace(request.Unit) || request.Quantity <= 0 || request.UnitPrice < 0)
            return BadRequest(new { message = "المشروع والبند والوحدة والكمية والسعر مطلوبة بشكل صحيح." });
        SupplySummary? supply;
        try { supply = await suppliersService.AddSupplyAsync(supplierId, request, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken); }
        catch (DuplicateSupplierTransactionException exception) { return Conflict(new { message = exception.Message }); }
        return supply is null ? NotFound() : Ok(supply);
    }

    [HttpPost("inline-supply")]
    [PermissionAuthorize("suppliers.supplies-create")]
    public async Task<ActionResult<InlineSupplierSupplySummary>> AddInlineSupply(CreateInlineSupplierSupplyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierName) || request.ProjectId <= 0 ||
            string.IsNullOrWhiteSpace(request.ItemName) || string.IsNullOrWhiteSpace(request.Unit) ||
            request.Quantity <= 0 || request.UnitPrice < 0)
            return BadRequest(new { message = "بيانات المورد والمشروع والبند والكمية والسعر مطلوبة بشكل صحيح." });
        try
        {
            var result = await suppliersService.AddInlineSupplyAsync(request, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
            return result is null ? BadRequest(new { message = "بيانات التوريدة أو صلاحية المشروع غير صحيحة." }) : Ok(result);
        }
        catch (DuplicateSupplierException exception)
        {
            return Conflict(new { message = "يوجد مورد مسجل بالفعل بنفس الاسم أو رقم الهاتف أو الرقم الضريبي.", matches = exception.Matches });
        }
        catch (DuplicateSupplierTransactionException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("invoices")]
    [PermissionAuthorize("suppliers.supplies-create")]
    public async Task<ActionResult<SupplierInvoiceSummary>> CreateInvoice(CreateSupplierInvoiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await invoiceService.CreateAsync(request, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
            return invoice is null ? BadRequest(new { message = "بيانات الفاتورة أو صلاحية المشروع غير صحيحة." }) : Ok(invoice);
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (DuplicateSupplierTransactionException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpGet("invoices/{invoiceId:int}")]
    [PermissionAuthorize("suppliers.view")]
    public async Task<ActionResult<SupplierInvoiceSummary>> GetInvoice(int invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await invoiceService.GetAsync(invoiceId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return invoice is null ? NotFound() : Ok(invoice);
    }

    [HttpPut("invoices/{invoiceId:int}")]
    [Authorize]
    public async Task<ActionResult<SupplierInvoiceSummary>> UpdateInvoice(int invoiceId, UpdateSupplierInvoiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await invoiceService.UpdateAsync(invoiceId, request, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
            return invoice is null ? Forbid() : Ok(invoice);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpDelete("invoices/{invoiceId:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteInvoice(int invoiceId, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await invoiceService.DeleteAsync(invoiceId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpGet("{supplierId:int}/payments")]
    [PermissionAuthorize("suppliers.view")]
    public async Task<ActionResult<IReadOnlyList<PaymentSummary>>> GetPayments(int supplierId, CancellationToken cancellationToken) =>
        Ok(await suppliersService.GetPaymentsAsync(supplierId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken));

    [HttpPost("{supplierId:int}/payments")]
    [PermissionAuthorize("suppliers.payments-create")]
    public async Task<ActionResult<PaymentSummary>> AddPayment(int supplierId, CreateSupplierPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return BadRequest(new { message = "قيمة الدفع يجب أن تكون أكبر من صفر." });
        PaymentSummary? payment;
        try { payment = await suppliersService.AddPaymentAsync(supplierId, request, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken); }
        catch (DuplicateSupplierTransactionException exception) { return Conflict(new { message = exception.Message }); }
        return payment is null
            ? BadRequest(new { message = "بيانات الدفع غير صحيحة." })
            : Ok(payment);
    }

    private int? CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private bool HasGlobalProjectAccess() =>
        User.IsInRole("admin") || User.IsInRole("director") || User.IsInRole("accountant");
}
