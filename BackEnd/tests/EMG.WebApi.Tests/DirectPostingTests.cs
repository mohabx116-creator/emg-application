using System.Text.Json;
using EMG.Api.Modules.SiteMovements.Dtos;
using EMG.Api.Modules.SiteMovements.Services;
using EMG.Api.Modules.Suppliers.Dtos;
using EMG.Api.Modules.Suppliers.Services;
using EMG.Api.Modules.Transactions.Dtos;
using EMG.Api.Modules.Transactions.Services;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class DirectPostingTests
{
    [Fact]
    public async Task NewOperationalRecordsArePostedImmediatelyAndAudited()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var project = new Project { Name = "Direct posting QA", Code = "DIRECT-POST-QA" };
        var supplier = new Supplier { Name = "Direct posting supplier" };
        db.AddRange(project, supplier);
        await db.SaveChangesAsync();

        var movement = await new SiteMovementsService(db).CreateAsync(
            new CreateSiteMovementRequest(project.Id, supplier.Id, "مصروفات", "Direct expense", "مقطوعية", null, null, 125m,
                null, "DP-1", null, "موقع", null, "نقدي", true), null, true, CancellationToken.None);
        var supply = await new SuppliersService(db).AddSupplyAsync(
            supplier.Id, new CreateSupplyRequest(project.Id, "خامات", "أسمنت", "طن", 2m, 300m, null, "DP-2", null), null, true, CancellationToken.None);
        var payment = await new SuppliersService(db).AddPaymentAsync(
            supplier.Id, new CreateSupplierPaymentRequest(project.Id, supply!.Id, 200m, null, "نقدي", "DP-PAY", null), null, true, CancellationToken.None);

        Assert.Equal("مرحّل", movement!.Status);
        Assert.Equal("مرحّل", supply!.Status);
        Assert.Equal("مرحّل", payment!.Status);
        Assert.Contains(await db.WorkflowActions.ToListAsync(), action => action.EntityType == "site-movement" && action.EntityId == movement.Id && action.Action == "ترحيل مباشر");
        Assert.Contains(await db.WorkflowActions.ToListAsync(), action => action.EntityType == "supplier-supply" && action.EntityId == supply.Id && action.Action == "ترحيل مباشر");
        Assert.Contains(await db.WorkflowActions.ToListAsync(), action => action.EntityType == "supplier-payment" && action.EntityId == payment.Id && action.Action == "ترحيل مباشر");
    }

    [Fact]
    public async Task AuthorizedCorrectionAndCancellationAreAtomicAuditedAndProjectScoped()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Correction QA", Code = "CORRECTION-QA" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        var movement = await new SiteMovementsService(db).CreateAsync(
            new CreateSiteMovementRequest(project.Id, null, "مصروفات", "قابل للتصحيح", "مقطوعية", null, null, 100m,
                null, "COR-1", null, "موقع", null, "نقدي", false), 7, true, CancellationToken.None);
        var service = new TransactionCorrectionService(db);
        using var document = JsonDocument.Parse("{\"Description\":\"تم التصحيح\",\"TotalAmount\":125}");
        var corrected = await service.CorrectAsync("site-movement", movement!.Id,
            new TransactionCorrectionRequest(document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), "تصحيح كمية موثق"), 7, true, CancellationToken.None);
        Assert.Equal("تصحيح", corrected!.Action);
        Assert.Equal(125m, await db.SiteMovements.Where(x => x.Id == movement.Id).Select(x => x.TotalAmount).SingleAsync());
        var cancelled = await service.CancelAsync("site-movement", movement.Id, new TransactionCancellationRequest("إلغاء مستند المصدر"), 7, true, CancellationToken.None);
        Assert.Equal("ملغى", cancelled!.Status);
        Assert.Null(await service.CancelAsync("site-movement", movement.Id, new TransactionCancellationRequest("محاولة مكررة"), 7, true, CancellationToken.None));
        var history = await db.WorkflowActions.Where(x => x.EntityType == "site-movement" && x.EntityId == movement.Id).ToListAsync();
        Assert.Contains(history, x => x.Action == "تصحيح" && x.Notes!.Contains("تصحيح كمية موثق"));
        Assert.Contains(history, x => x.Action == "إلغاء" && x.Notes!.Contains("إلغاء مستند المصدر"));
    }

    [Fact]
    public async Task CorrectionCannotCrossProjectScope()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Scope QA", Code = "SCOPE-QA" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        var movement = await new SiteMovementsService(db).CreateAsync(
            new CreateSiteMovementRequest(project.Id, null, "مصروفات", "نطاق", "مقطوعية", null, null, 20m,
                null, "SCOPE-1", null, "موقع", null, "نقدي", false), 7, true, CancellationToken.None);
        var service = new TransactionCorrectionService(db);
        using var document = JsonDocument.Parse("{\"Description\":\"غير مصرح\"}");
        var result = await service.CorrectAsync("site-movement", movement!.Id,
            new TransactionCorrectionRequest(document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), "اختبار نطاق"), 99, false, CancellationToken.None);
        Assert.Null(result);
    }
}
