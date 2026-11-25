using ClosedXML.Excel;
using EMG.Api.Modules.FinalAccounts.Services;
using EMG.Api.Modules.Imports.Services;
using EMG.Api.Modules.Projects.Repositories;
using EMG.Api.Modules.Suppliers.Services;
using EMG.Api.Modules.Workflow.Controllers;
using EMG.Api.Modules.Workflow.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed partial class WorkflowAuthorizationTests
{
    private const string SupplyType = "supplier-supply";
    private const string TechnicalAction = "قبول فني";
    private const string FinancialAction = "اعتماد مالي";
    private const string TechnicalStatus = "مقبول فنياً";

    [Fact]
    public async Task SupplierSupplyRecognizesOnlyAfterSeparateFinancialApprovalAndPostingDoesNotCountItAgain()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("supplier-accounting");
        var engineer = await qa.AddUserAsync("engineer");
        var manager = await qa.AddUserAsync("project_manager");
        var accountant = await qa.AddUserAsync("accountant");
        await qa.AssignAsync(engineer, project);
        await qa.AssignAsync(manager, project);
        var supply = await AddSupplierSupplyAsync(qa.Db, project, "مسودة", 300m);
        qa.Db.SupplierSupplies.Add(new SupplierSupply
        {
            ProjectId = project, SupplierId = supply.SupplierId, ItemName = "legacy posted", Unit = "unit",
            Quantity = 1m, UnitPrice = 120m, TotalAmount = 120m, Status = "مرحّل"
        });
        await qa.Db.SaveChangesAsync();
        var historic = await qa.Db.SupplierSupplies.Where(item => item.Status == "مرحّل").SingleAsync();
        qa.Db.SupplierPayments.Add(new SupplierPayment
        {
            ProjectId = project, SupplierId = supply.SupplierId, SupplyId = historic.Id, Amount = 20m, Status = "معتمد"
        });
        await qa.Db.SaveChangesAsync();
        var creator = qa.Controller(engineer, "engineer");
        var technical = qa.Controller(manager, "project_manager");
        var finance = qa.Controller(accountant, "accountant");
        await AssertOutstandingAsync(qa.Db, project, supply.SupplierId, 100m);
        Assert.True(Value<WorkflowAuditResult>((await creator.Audit(SupplyType, supply.Id, Cancellation)).Result).CanSubmit);
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(creator, supply.Id, "إرسال"));
        var review = Assert.Single(Value<IReadOnlyList<WorkflowReviewItem>>((await technical.ReviewQueue(Cancellation)).Result), item => item.EntityType == SupplyType);
        Assert.True(review.CanAcceptTechnically);
        Assert.False(review.CanApproveFinancially);
        Assert.False(review.CanApprove);
        AssertForbidden(await SupplyActionAsync(finance, supply.Id, TechnicalAction));
        Assert.IsType<BadRequestObjectResult>(await SupplyActionAsync(finance, supply.Id, "اعتماد"));
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(technical, supply.Id, TechnicalAction));
        await AssertOutstandingAsync(qa.Db, project, supply.SupplierId, 100m);

        review = Assert.Single(Value<IReadOnlyList<WorkflowReviewItem>>((await finance.ReviewQueue(Cancellation)).Result), item => item.EntityType == SupplyType);
        Assert.Equal(TechnicalStatus, review.Status);
        Assert.True(review.CanApproveFinancially);
        Assert.False(review.CanAcceptTechnically);
        AssertForbidden(await SupplyActionAsync(technical, supply.Id, FinancialAction));
        Assert.IsType<BadRequestObjectResult>(await SupplyActionAsync(finance, supply.Id, "ترحيل"));
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(finance, supply.Id, FinancialAction));
        await AssertOutstandingAsync(qa.Db, project, supply.SupplierId, 400m);
        Assert.IsType<BadRequestObjectResult>(await SupplyActionAsync(finance, supply.Id, FinancialAction));
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(finance, supply.Id, "ترحيل"));
        await AssertOutstandingAsync(qa.Db, project, supply.SupplierId, 400m);
        Assert.Equal(4, await qa.Db.WorkflowActions.CountAsync(row => row.EntityType == SupplyType && row.EntityId == supply.Id));
        Assert.Equal((120m, "مرحّل"), await qa.Db.SupplierSupplies.Where(row => row.Id == historic.Id)
            .Select(row => new ValueTuple<decimal, string>(row.TotalAmount, row.Status)).SingleAsync());
        Assert.Equal(historic.Id, await qa.Db.SupplierPayments.Select(row => row.SupplyId).SingleAsync());
    }

    [Theory]
    [InlineData("director")]
    [InlineData("admin")]
    public async Task SameUserCannotApproveBothStagesEvenWithBothGrantsOrCanonicalAdminAuthority(string role)
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("supplier-separation");
        var actor = await qa.AddUserAsync(role);
        var peer = await qa.AddUserAsync(role);
        var supply = await AddSupplierSupplyAsync(qa.Db, project);
        var first = qa.Controller(actor, role);
        var second = qa.Controller(peer, role);

        Assert.IsType<OkObjectResult>(await SupplyActionAsync(first, supply.Id, TechnicalAction));
        Assert.False(Value<WorkflowAuditResult>((await first.Audit(SupplyType, supply.Id, Cancellation)).Result).CanApproveFinancially);
        AssertForbidden(await SupplyActionAsync(first, supply.Id, FinancialAction));
        Assert.Equal(1, await qa.Db.WorkflowActions.CountAsync());
        Assert.True(Value<WorkflowAuditResult>((await second.Audit(SupplyType, supply.Id, Cancellation)).Result).CanApproveFinancially);
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(second, supply.Id, FinancialAction));
        Assert.Equal(new[] { actor, peer }, await qa.Db.WorkflowActions.OrderBy(row => row.Id).Select(row => row.UserId!.Value).ToArrayAsync());
    }

    [Fact]
    public async Task TechnicallyAcceptedStateWithoutActualAcceptanceHistoryFailsClosed()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("supplier-missing-history");
        var actor = await qa.AddUserAsync("admin");
        var supply = await AddSupplierSupplyAsync(qa.Db, project, TechnicalStatus);
        var admin = qa.Controller(actor, "admin");
        Assert.IsType<UnprocessableEntityObjectResult>(await SupplyActionAsync(admin, supply.Id, FinancialAction));
        Assert.Equal(TechnicalStatus, await qa.Db.SupplierSupplies.Select(row => row.Status).SingleAsync());
        await qa.AssertNoSideEffectsAsync();
    }

    [Theory]
    [InlineData("engineer")]
    [InlineData("user")]
    public async Task NonReviewerCannotUseSupplierStagePermissions(string role)
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("supplier-non-reviewer");
        var actor = await qa.AddUserAsync(role);
        await qa.AssignAsync(actor, project);
        await qa.GrantAsync(role, "suppliers.view", "assigned");
        await qa.GrantAsync(role, "suppliers.supplies-accept-technical", "assigned");
        await qa.GrantAsync(role, "suppliers.supplies-approve-financial", "assigned");
        var review = await AddSupplierSupplyAsync(qa.Db, project);
        var pending = await AddSupplierSupplyAsync(qa.Db, project, TechnicalStatus);
        var controller = qa.Controller(actor, role);
        AssertForbidden(await SupplyActionAsync(controller, review.Id, TechnicalAction));
        AssertForbidden(await SupplyActionAsync(controller, pending.Id, FinancialAction));
        await qa.AssertNoSideEffectsAsync();
    }

    [Fact]
    public async Task SupplierStagesEnforceSingleGrantScopeAndCapAcrossMixedRoles()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("supplier-limits");
        var actor = await qa.AddUserAsync("director", "project_manager");
        var accountant = await qa.AddUserAsync("accountant");
        await qa.GrantAsync("director", "suppliers.supplies-accept-technical", "all", 99m);
        await qa.GrantAsync("project_manager", "suppliers.supplies-accept-technical", "all", 100m);
        await qa.GrantAsync("accountant", "suppliers.supplies-approve-financial", "all", 99m);
        var supply = await AddSupplierSupplyAsync(qa.Db, project);
        var mixed = qa.Controller(actor, "director", "project_manager");
        var finance = qa.Controller(accountant, "accountant");

        AssertForbidden(await SupplyActionAsync(mixed, supply.Id, TechnicalAction));
        await qa.AssertNoSideEffectsAsync();
        await qa.AssignAsync(actor, project);
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(mixed, supply.Id, TechnicalAction));
        AssertForbidden(await SupplyActionAsync(finance, supply.Id, FinancialAction));
        await qa.GrantAsync("accountant", "suppliers.supplies-approve-financial", "assigned", 100m);
        AssertForbidden(await SupplyActionAsync(finance, supply.Id, FinancialAction));
        await qa.AssignAsync(accountant, project);
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(finance, supply.Id, FinancialAction));
    }

    [Theory]
    [InlineData("director", "suppliers.supplies-accept-technical")]
    [InlineData("director", "suppliers.supplies-approve-financial")]
    [InlineData("project_manager", "suppliers.supplies-accept-technical")]
    [InlineData("accountant", "suppliers.supplies-approve-financial")]
    public async Task RevokedStageGrantsRemainRevokedAndCustomizedGrantsSurviveStartup(string role, string permission)
    {
        await using var qa = await WorkflowQa.CreateAsync();
        await qa.GrantAsync(role, "suppliers.approve", "assigned", 75m);
        await qa.RemoveGrantAsync(role, permission);
        await qa.RunStartupSeedingAsync();
        Assert.False(await qa.Db.RolePermissions.AnyAsync(grant => grant.Role.Name == role && grant.Permission.Code == permission));
        await qa.GrantAsync(role, permission, "own", 55m);
        await qa.RunStartupSeedingAsync();
        var grant = await qa.GetGrantAsync(role, permission);
        Assert.Equal("own", grant.Scope);
        Assert.Equal(55m, grant.AmountLimit);
    }

    [Fact]
    public async Task SupplyRejectionRequiresStageAuthorityAndReasonAndResubmissionStartsNewTechnicalReview()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("supplier-correction");
        var engineer = await qa.AddUserAsync("engineer");
        var manager = await qa.AddUserAsync("project_manager");
        var accountant = await qa.AddUserAsync("accountant");
        await qa.AssignAsync(engineer, project);
        await qa.AssignAsync(manager, project);
        var supply = await AddSupplierSupplyAsync(qa.Db, project, notes: "استيراد: مصدر تجريبي");
        var technical = qa.Controller(manager, "project_manager");
        var finance = qa.Controller(accountant, "accountant");
        var creator = qa.Controller(engineer, "engineer");
        AssertForbidden(await SupplyActionAsync(finance, supply.Id, "رفض", "قيمة خاطئة"));
        Assert.IsType<UnprocessableEntityObjectResult>(await SupplyActionAsync(technical, supply.Id, "رفض", " "));
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(technical, supply.Id, TechnicalAction));
        AssertForbidden(await SupplyActionAsync(technical, supply.Id, "رفض", "قيمة خاطئة"));
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(finance, supply.Id, "رفض", "قيمة الفاتورة تحتاج تصحيحاً"));
        var audit = Value<WorkflowAuditResult>((await creator.Audit(SupplyType, supply.Id, Cancellation)).Result);
        Assert.True(audit.CanCorrectSupply);
        Assert.Equal("قيمة الفاتورة تحتاج تصحيحاً", audit.RejectionReason);
        Assert.IsType<NoContentResult>(await creator.CorrectSupply(supply.Id, new("خامات", "توريدة مصححة", "وحدة", 2m, 60m, "QA-120"), Cancellation));
        var corrected = await qa.Db.SupplierSupplies.AsNoTracking().SingleAsync();
        Assert.Equal((120m, "مرفوض", "استيراد: مصدر تجريبي"), (corrected.TotalAmount, corrected.Status, corrected.Notes));
        await AssertOutstandingAsync(qa.Db, project, supply.SupplierId, 0m);
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(creator, supply.Id, "إرسال"));
        Assert.IsType<BadRequestObjectResult>(await SupplyActionAsync(finance, supply.Id, FinancialAction));
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(technical, supply.Id, TechnicalAction));
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(finance, supply.Id, FinancialAction));
        Assert.IsType<BadRequestObjectResult>(await creator.CorrectSupply(supply.Id, new("خامات", "محاولة متأخرة", "وحدة", 1m, 30m, null), Cancellation));
        await AssertOutstandingAsync(qa.Db, project, supply.SupplierId, 120m);
    }

    [Fact]
    public async Task SupplierPaymentApprovalRemainsFinancialAndItsRouteHasNoTechnicalStage()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("payment-approval");
        var manager = await qa.AddUserAsync("project_manager");
        var accountant = await qa.AddUserAsync("accountant");
        await qa.AssignAsync(manager, project);
        var payment = await qa.AddReviewRecordAsync("supplier-payment", project);
        var projectManager = qa.Controller(manager, "project_manager");
        var finance = qa.Controller(accountant, "accountant");
        Assert.IsType<BadRequestObjectResult>((await projectManager.Execute("supplier-payment", payment, TechnicalAction, new(null), Cancellation)).Result);
        AssertForbidden((await projectManager.Execute("supplier-payment", payment, "اعتماد", new(null), Cancellation)).Result);
        Assert.IsType<OkObjectResult>((await finance.Execute("supplier-payment", payment, "اعتماد", new(null), Cancellation)).Result);
        Assert.IsType<OkObjectResult>((await finance.Execute("supplier-payment", payment, "ترحيل", new(null), Cancellation)).Result);
    }

    [Theory]
    [InlineData("مسودة")]
    [InlineData("للمراجعة")]
    [InlineData(TechnicalStatus)]
    [InlineData("مرفوض")]
    public async Task BatchApprovalProtectsEveryRowUntilImportedSuppliesAreFinanciallyApproved(string pendingStatus)
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var approver = await qa.AddUserAsync("admin");
        var accountant = await qa.AddUserAsync("accountant");
        var project = new Project { Name = "batch gate", Code = "QA-BATCH", Status = "تحت المراجعة" };
        qa.Db.Projects.Add(project);
        await qa.Db.SaveChangesAsync();
        qa.Db.Attachments.Add(new Attachment { EntityType = "project-import", EntityId = project.Id, OriginalFileName = "synthetic.xlsx" });
        qa.Db.SiteMovements.Add(new SiteMovement { ProjectId = project.Id, Status = "مسودة", Reference = "استيراد: صفحة تجريبية", TotalAmount = 999m });
        await qa.Db.SaveChangesAsync();
        var supply = await AddSupplierSupplyAsync(qa.Db, project.Id, pendingStatus, notes: "استيراد: توريدات تجريبية");
        if (pendingStatus == TechnicalStatus)
        {
            qa.Db.WorkflowActions.Add(new WorkflowAction
            {
                EntityType = SupplyType, EntityId = supply.Id, Action = TechnicalAction,
                FromStatus = "للمراجعة", ToStatus = TechnicalStatus, UserId = approver
            });
            await qa.Db.SaveChangesAsync();
        }
        var repository = new EfProjectsRepository(qa.Db);

        var denied = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DecideImportAsync(project.Id, true, null, approver, Cancellation));
        Assert.Contains("القبول الفني والاعتماد المالي", denied.Message);
        Assert.Equal("تحت المراجعة", await qa.Db.Projects.Where(row => row.Id == project.Id).Select(row => row.Status).SingleAsync());
        Assert.Equal("مسودة", await qa.Db.SiteMovements.Select(row => row.Status).SingleAsync());
        Assert.Equal(pendingStatus, await qa.Db.SupplierSupplies.Select(row => row.Status).SingleAsync());
        Assert.Empty(await qa.Db.SecurityAuditLogs.ToListAsync());

        var admin = qa.Controller(approver, "admin");
        if (pendingStatus is "مسودة" or "مرفوض")
            Assert.IsType<OkObjectResult>(await SupplyActionAsync(admin, supply.Id, "إرسال"));
        if (pendingStatus != TechnicalStatus)
            Assert.IsType<OkObjectResult>(await SupplyActionAsync(admin, supply.Id, TechnicalAction));
        Assert.IsType<OkObjectResult>(await SupplyActionAsync(qa.Controller(accountant, "accountant"), supply.Id, FinancialAction));
        Assert.NotNull(await repository.DecideImportAsync(project.Id, true, null, approver, Cancellation));
        Assert.Equal("معتمد", await qa.Db.SupplierSupplies.Select(row => row.Status).SingleAsync());
        Assert.Equal("مسودة", await qa.Db.SiteMovements.Select(row => row.Status).SingleAsync());
    }

    [Theory]
    [InlineData("للمراجعة", false)]
    [InlineData(TechnicalStatus, false)]
    [InlineData("مرفوض", false)]
    [InlineData("معتمد", false)]
    [InlineData("مرحّل", true)]
    [InlineData("مسودة", true)]
    public async Task ReimportProtectsReviewedSuppliesAndLinkedPaymentsBeforeDeletingAnyRows(string status, bool linked)
    {
        await using var qa = await WorkflowQa.CreateAsync();
        await AssertReimportBlockedAsync(qa.Db, status, linked);
    }

    private static async Task AssertReimportBlockedAsync(EmgDbContext db, string status, bool linked)
    {
        var project = new Project { Name = "QA retained project", Code = "KEEP-" + Guid.NewGuid().ToString("N") };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        var supply = await AddSupplierSupplyAsync(db, project.Id, status, notes: "استيراد: توريدات تجريبية");
        var unrelated = new SiteMovement { ProjectId = project.Id, TotalAmount = 50m, Reference = "استيراد: موقع تجريبي" };
        db.SiteMovements.Add(unrelated);
        if (linked) db.SupplierPayments.Add(new SupplierPayment { ProjectId = project.Id, SupplierId = supply.SupplierId, SupplyId = supply.Id, Amount = 15m });
        await db.SaveChangesAsync();
        var environment = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = Path.GetTempPath() }).Environment;
        var importer = new WorkbookImportService(db, environment);
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("أعمال الموقع").Cell("A1").Value = "QA synthetic source";
        using var source = new MemoryStream();
        workbook.SaveAs(source);
        source.Position = 0;
        var file = new FormFile(source, 0, source.Length, "file", "synthetic.xlsx");

        var denied = await Assert.ThrowsAsync<InvalidOperationException>(() => importer.CommitExistingProjectAsync(file, project.Id, 1, Cancellation));
        Assert.Contains("تعذر إعادة الاستيراد", denied.Message);
        Assert.Equal(status, await db.SupplierSupplies.Where(row => row.Id == supply.Id).Select(row => row.Status).SingleAsync());
        Assert.True(await db.SiteMovements.AnyAsync(row => row.Id == unrelated.Id && row.TotalAmount == 50m));
        Assert.Equal(linked, await db.SupplierPayments.AnyAsync(row => row.SupplyId == supply.Id && row.Amount == 15m));
        Assert.False(await db.Attachments.AnyAsync(row => row.EntityType == "project-import" && row.EntityId == project.Id));
    }

    private static async Task<SupplierSupply> AddSupplierSupplyAsync(EmgDbContext db, int projectId, string status = "للمراجعة", decimal amount = 100m, string? notes = null)
    {
        var supply = new SupplierSupply
        {
            ProjectId = projectId, Supplier = new Supplier { Name = "QA supplier " + Guid.NewGuid().ToString("N") },
            Status = status, ItemName = "QA item", Unit = "وحدة", Quantity = 1m, UnitPrice = amount, TotalAmount = amount, Notes = notes
        };
        db.SupplierSupplies.Add(supply);
        await db.SaveChangesAsync();
        return supply;
    }

    private static async Task<IActionResult?> SupplyActionAsync(WorkflowController controller, int supplyId, string action, string? notes = null) =>
        (await controller.Execute(SupplyType, supplyId, action, new(notes), Cancellation)).Result;

    private static async Task AssertOutstandingAsync(EmgDbContext db, int projectId, int supplierId, decimal expected)
    {
        var accounts = await new FinalAccountsService(db).GetAsync(projectId, null, true, Cancellation);
        Assert.NotNull(accounts);
        Assert.Equal(expected, accounts.SupplierOutstanding);
        var supplier = await new SuppliersService(db).GetSupplierAsync(supplierId, null, true, Cancellation);
        Assert.NotNull(supplier);
        Assert.Equal(expected, supplier.Remaining);
    }
}
