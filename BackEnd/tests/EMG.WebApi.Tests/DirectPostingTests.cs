using System.Text.Json;
using EMG.Api.Modules.SiteMovements.Dtos;
using EMG.Api.Modules.SiteMovements.Services;
using EMG.Api.Modules.Suppliers.Dtos;
using EMG.Api.Modules.Suppliers.Services;
using EMG.Api.Modules.Transactions.Dtos;
using EMG.Api.Modules.Transactions.Services;
using EMG.Domain.Entities;
using EMG.Domain.Security;
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
    public async Task CreatorCanCorrectAndPermanentlyDeleteWithoutExtraPermission()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new AppUser { Id = 7, UserName = "creator", Email = "creator@example.test", DisplayName = "Creator", PasswordHash = "test-hash" });
        await db.SaveChangesAsync();
        var project = new Project { Name = "Correction QA", Code = "CORRECTION-QA" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = 7, ProjectId = project.Id, IsActive = true });
        await db.SaveChangesAsync();
        var movement = await new SiteMovementsService(db).CreateAsync(
            new CreateSiteMovementRequest(project.Id, null, "مصروفات", "قابل للتصحيح", "مقطوعية", null, null, 100m,
                null, "COR-1", null, "موقع", null, "نقدي", false), 7, true, CancellationToken.None);
        var service = new TransactionCorrectionService(db);
        using var document = JsonDocument.Parse("{\"Description\":\"تم التصحيح\",\"TotalAmount\":125}");
        var corrected = await service.CorrectAsync("site-movement", movement!.Id,
            new TransactionCorrectionRequest(document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), ""), 7, false, false, CancellationToken.None);
        Assert.Equal("تصحيح", corrected!.Action);
        Assert.Equal(125m, await db.SiteMovements.Where(x => x.Id == movement.Id).Select(x => x.TotalAmount).SingleAsync());
        var deleted = await service.DeleteAsync("site-movement", movement.Id, 7, false, false, CancellationToken.None);
        Assert.Equal("حذف", deleted!.Action);
        Assert.Null(await db.SiteMovements.SingleOrDefaultAsync(x => x.Id == movement.Id));
        Assert.DoesNotContain(await db.WorkflowActions.ToListAsync(), x => x.EntityType == "site-movement" && x.EntityId == movement.Id);
        Assert.Contains(await db.SecurityAuditLogs.ToListAsync(), x => x.Action == "transaction.deleted" && x.EntityType == "site-movement" && x.EntityId == movement.Id);
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
            new TransactionCorrectionRequest(document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), ""), 99, false, false, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task CreatorDeleteIsBlockedByFinancialDependencies()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new AppUser { Id = 7, UserName = "creator", Email = "creator-dependency@example.test", DisplayName = "Creator", PasswordHash = "test-hash" });
        await db.SaveChangesAsync();
        var project = new Project { Name = "Dependency QA", Code = "DEPENDENCY-QA" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        var movement = await new SiteMovementsService(db).CreateAsync(
            new CreateSiteMovementRequest(project.Id, null, "مصروفات", "مرتبط بسلفة", "مقطوعية", null, null, 20m,
                null, "DEP-1", null, "موقع", null, "نقدي", false), 7, true, CancellationToken.None);
        db.AdvancePayments.Add(new AdvancePayment { ProjectId = project.Id, ClientName = "عميل", PayerName = "دافع", Amount = 10m, PaymentMethod = "نقدي", Status = "مرحّل" });
        await db.SaveChangesAsync();
        db.AdvanceAllocations.Add(new AdvanceAllocation { AdvancePaymentId = await db.AdvancePayments.Select(x => x.Id).SingleAsync(), SiteMovementId = movement!.Id, Amount = 5m });
        await db.SaveChangesAsync();
        var service = new TransactionCorrectionService(db);
        Assert.Null(await service.DeleteAsync("site-movement", movement.Id, 7, true, false, CancellationToken.None));
        Assert.NotNull(await db.SiteMovements.SingleOrDefaultAsync(x => x.Id == movement.Id));
    }

    [Fact]
    public async Task CorrectionCannotLowerAdvanceBelowExistingAllocations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new AppUser { Id = 7, UserName = "advance-owner", Email = "advance-owner@example.test", DisplayName = "Advance owner", PasswordHash = "test-hash" });
        var project = new Project { Name = "Advance correction QA", Code = "ADV-CORRECTION-QA" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = 7, ProjectId = project.Id, IsActive = true });
        var advance = new AdvancePayment { ProjectId = project.Id, ClientName = "Client", PayerName = "Payer", Amount = 100m, Status = "مرحّل", CreatedByUserId = 7 };
        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync();
        db.AdvanceAllocations.Add(new AdvanceAllocation { AdvancePaymentId = advance.Id, AccountCategory = "الموقع", Amount = 75m });
        await db.SaveChangesAsync();

        using var document = JsonDocument.Parse("{\"Amount\":50}");
        var service = new TransactionCorrectionService(db);
        await Assert.ThrowsAsync<TransactionValidationException>(() => service.CorrectAsync("advance-payment", advance.Id,
            new TransactionCorrectionRequest(document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), ""), 7, false, false, CancellationToken.None));
        Assert.Equal(100m, await db.AdvancePayments.Where(x => x.Id == advance.Id).Select(x => x.Amount).SingleAsync());
    }
}
