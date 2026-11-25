using System.Security.Claims;
using EMG.Api.Modules.Contractors.Dtos;
using EMG.Api.Modules.Contractors.Services;
using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMG.Api.Modules.Contractors.Controllers;

[ApiController, Authorize, Route("api/contractors")]
public sealed class ContractorsController(IContractorStatementService service) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("suppliers.view")]
    public async Task<ActionResult<IReadOnlyList<ContractorSummary>>> Get(CancellationToken cancellationToken) => Ok(await service.GetContractorsAsync(CurrentUserId(), GlobalAccess(), cancellationToken));

    [HttpPost]
    [PermissionAuthorize("suppliers.create")]
    public async Task<ActionResult<ContractorSummary>> Create(CreateContractorRequest request, CancellationToken cancellationToken) => (await service.CreateContractorAsync(request, cancellationToken)) is { } result ? Ok(result) : BadRequest(new { message = "اسم المقاول مطلوب." });

    [HttpGet("contracts")]
    [PermissionAuthorize("suppliers.view")]
    public async Task<ActionResult<IReadOnlyList<ContractorContractSummary>>> Contracts([FromQuery] int? projectId, [FromQuery] int? contractorId, CancellationToken cancellationToken) => Ok(await service.GetContractsAsync(projectId, contractorId, CurrentUserId(), GlobalAccess(), cancellationToken));

    [HttpPost("{contractorId:int}/contracts")]
    [PermissionAuthorize("suppliers.manage")]
    public async Task<ActionResult<ContractorContractSummary>> CreateContract(int contractorId, CreateContractRequest request, CancellationToken cancellationToken)
    {
        try { return (await service.CreateContractAsync(contractorId, request, CurrentUserId(), GlobalAccess(), cancellationToken)) is { } result ? Ok(result) : BadRequest(new { message = "بيانات العقد أو نطاق المشروع غير صحيحة." }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpGet("contracts/{contractId:int}/items")]
    [PermissionAuthorize("suppliers.view")]
    public async Task<ActionResult<IReadOnlyList<ContractorContractItemSummary>>> Items(int contractId, CancellationToken cancellationToken) => Ok(await service.GetItemsAsync(contractId, CurrentUserId(), GlobalAccess(), cancellationToken));

    [HttpGet("contracts/{contractId:int}/eligible-work")]
    [PermissionAuthorize("reports.view")]
    public async Task<ActionResult<IReadOnlyList<EligibleContractorWorkSummary>>> EligibleWork(int contractId, CancellationToken cancellationToken) => Ok(await service.GetEligibleWorkAsync(contractId, CurrentUserId(), GlobalAccess(), cancellationToken));

    [HttpPost("contracts/{contractId:int}/items")]
    [PermissionAuthorize("suppliers.manage")]
    public async Task<ActionResult<ContractorContractItemSummary>> AddItem(int contractId, CreateContractItemRequest request, CancellationToken cancellationToken) => (await service.AddItemAsync(contractId, request, CurrentUserId(), GlobalAccess(), cancellationToken)) is { } result ? Ok(result) : BadRequest(new { message = "بيانات بند العقد غير صحيحة." });

    [HttpGet("statements")]
    [PermissionAuthorize("reports.view")]
    public async Task<ActionResult<IReadOnlyList<ContractorStatementSummary>>> Statements([FromQuery] int? projectId, CancellationToken cancellationToken) => Ok(await service.GetStatementsAsync(projectId, CurrentUserId(), GlobalAccess(), cancellationToken));

    [HttpPost("statements/draft")]
    [PermissionAuthorize("reports.view")]
    public async Task<ActionResult<ContractorStatementSummary>> Draft(CreateStatementRequest request, CancellationToken cancellationToken)
    {
        try { return (await service.CreateDraftAsync(request, CurrentUserId(), GlobalAccess(), cancellationToken)) is { } result ? Ok(result) : BadRequest(new { message = "بيانات المسودة أو ربط الأعمال المعتمد غير صحيح." }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpGet("statements/{statementId:int}.xlsx")]
    [PermissionAuthorize("reports.export")]
    public async Task<IActionResult> Export(int statementId, CancellationToken cancellationToken) => (await service.ExportDraftAsync(statementId, CurrentUserId(), GlobalAccess(), cancellationToken)) is { } bytes ? File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"contractor-statement-{statementId}-draft.xlsx") : NotFound();

    private int? CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private bool GlobalAccess() => User.IsInRole("admin") || User.IsInRole("director") || User.IsInRole("accountant");
}
