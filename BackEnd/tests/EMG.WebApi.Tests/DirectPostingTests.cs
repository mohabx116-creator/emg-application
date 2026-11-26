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
    public async Task InlineSupplierCreationGeneratesAnInvoiceAndPostsTheLineAtomically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Inline supplier QA", Code = "INLINE-SUPPLIER-QA" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var result = await new SuppliersService(db).AddInlineSupplyAsync(new CreateInlineSupplierSupplyRequest(
            "مورد جديد", "مورد", "01000000000", null, null, project.Id, "خامات", "أسمنت", "طن", 2m, 300m,
            null, null, "إنشاء مباشر"), 17, true, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(result!.Supplier.Id, result.Supply.SupplierId);
        Assert.Equal("مرحّل", result.Supply.Status);
        Assert.Equal(600m, result.Supply.TotalAmount);
        Assert.StartsWith($"AUTO-{project.Id}-{result.Supplier.Id}-", result.Supply.InvoiceNumber);
        Assert.Equal(1, await db.Suppliers.CountAsync());
        Assert.Equal(1, await db.SupplierSupplies.CountAsync());
        Assert.Contains(await db.WorkflowActions.ToListAsync(), item => item.EntityType == "supplier-supply" && item.EntityId == result.Supply.Id);
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

    [Fact]
    public async Task ExcavationCorrectionRecalculatesQuantityAndAmountFromDimensions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new AppUser { Id = 7, UserName = "excavation-owner", Email = "exc-owner@example.test", DisplayName = "Excavation owner", PasswordHash = "test-hash" });
        var project = new Project { Name = "Excavation correction QA", Code = "EXC-CORRECTION-QA" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = 7, ProjectId = project.Id, IsActive = true });
        await db.SaveChangesAsync();
        var record = new ExcavationRecord { ProjectId = project.Id, CalculationMethod = "حجم", Length = 2m, Width = 3m, Depth = 4m, CalculatedQuantity = 24m, UnitPrice = 10m, TotalAmount = 240m, Description = "حفر", CreatedByUserId = 7, Status = "مرحّل" };
        db.ExcavationRecords.Add(record);
        await db.SaveChangesAsync();
        using var document = JsonDocument.Parse("{\"Length\":3,\"Width\":3,\"Depth\":4}");
        var corrected = await new TransactionCorrectionService(db).CorrectAsync("excavation", record.Id,
            new TransactionCorrectionRequest(document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), ""), 7, false, false, CancellationToken.None);
        Assert.Equal("تصحيح", corrected!.Action);
        var saved = await db.ExcavationRecords.SingleAsync(x => x.Id == record.Id);
        Assert.Equal(36m, saved.CalculatedQuantity);
        Assert.Equal(360m, saved.TotalAmount);
    }

    [Fact]
    public async Task CorrectionCannotReduceSiteMovementOrSupplierPaymentBelowAllocations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new AppUser { Id = 7, UserName = "allocation-owner", Email = "alloc-owner@example.test", DisplayName = "Allocation owner", PasswordHash = "test-hash" });
        var project = new Project { Name = "Allocation correction QA", Code = "ALLOC-CORRECTION-QA" };
        var supplier = new Supplier { Name = "Allocation supplier" };
        db.AddRange(project, supplier);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = 7, ProjectId = project.Id, IsActive = true });
        var movement = new SiteMovement { ProjectId = project.Id, Description = "حركة", MovementType = "مصروفات", TotalAmount = 100m, CreatedByUserId = 7, Status = "مرحّل" };
        var payment = new SupplierPayment { SupplierId = supplier.Id, ProjectId = project.Id, Amount = 100m, CreatedByUserId = 7, Status = "مرحّل" };
        db.AddRange(movement, payment);
        await db.SaveChangesAsync();
        var advance = new AdvancePayment { ProjectId = project.Id, ClientName = "عميل", PayerName = "دافع", Amount = 200m, Status = "مرحّل" };
        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync();
        db.AdvanceAllocations.AddRange(new AdvanceAllocation { AdvancePaymentId = advance.Id, SiteMovementId = movement.Id, Amount = 75m }, new AdvanceAllocation { AdvancePaymentId = advance.Id, SupplierPaymentId = payment.Id, Amount = 80m });
        await db.SaveChangesAsync();
        var service = new TransactionCorrectionService(db);
        using var movementJson = JsonDocument.Parse("{\"TotalAmount\":50}");
        using var paymentJson = JsonDocument.Parse("{\"Amount\":70}");
        await Assert.ThrowsAsync<TransactionValidationException>(() => service.CorrectAsync("site-movement", movement.Id, new TransactionCorrectionRequest(movementJson.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), ""), 7, false, false, CancellationToken.None));
        await Assert.ThrowsAsync<TransactionValidationException>(() => service.CorrectAsync("supplier-payment", payment.Id, new TransactionCorrectionRequest(paymentJson.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), ""), 7, false, false, CancellationToken.None));
    }

    [Fact]
    public async Task OtherUserCorrectionHonorsProjectScopeAndAmountLimit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var owner = new AppUser { Id = 7, UserName = "scope-owner", Email = "scope-owner@example.test", DisplayName = "Owner", PasswordHash = "test-hash" };
        var editor = new AppUser { Id = 8, UserName = "scope-editor", Email = "scope-editor@example.test", DisplayName = "Editor", PasswordHash = "test-hash" };
        var role = new Role { Name = "accountant", DisplayName = "Accountant" };
        var permission = new Permission { Code = "transactions.correct", DisplayName = "Correct transactions" };
        db.AddRange(owner, editor, role, permission);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = editor.Id, RoleId = role.Id });
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, Scope = "assigned", AmountLimit = 100m });
        var project = new Project { Name = "Scoped correction QA", Code = "SCOPED-CORRECTION-QA" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = editor.Id, ProjectId = project.Id, IsActive = true });
        var movement = new SiteMovement { ProjectId = project.Id, Description = "مملوك لمستخدم آخر", MovementType = "مصروفات", TotalAmount = 80m, CreatedByUserId = owner.Id, Status = "مرحّل" };
        db.SiteMovements.Add(movement);
        await db.SaveChangesAsync();
        var service = new TransactionCorrectionService(db);
        using var withinLimit = JsonDocument.Parse("{\"TotalAmount\":90}");
        var corrected = await service.CorrectAsync("site-movement", movement.Id, new TransactionCorrectionRequest(withinLimit.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), ""), editor.Id, false, true, CancellationToken.None);
        Assert.NotNull(corrected);
        using var aboveLimit = JsonDocument.Parse("{\"TotalAmount\":150}");
        Assert.Null(await service.CorrectAsync("site-movement", movement.Id, new TransactionCorrectionRequest(aboveLimit.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), ""), editor.Id, false, true, CancellationToken.None));

        var excavation = new ExcavationRecord { ProjectId = project.Id, Description = "حفر", CalculationMethod = "مساحة",
            Length = 2m, Width = 2m, CalculatedQuantity = 4m, UnitPrice = 20m, TotalAmount = 80m, CreatedByUserId = owner.Id, Status = "مرحّل" };
        db.ExcavationRecords.Add(excavation);
        await db.SaveChangesAsync();
        using var derivedAboveLimit = JsonDocument.Parse("{\"Length\":3}");
        Assert.Null(await service.CorrectAsync("excavation", excavation.Id,
            new TransactionCorrectionRequest(derivedAboveLimit.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone()), ""),
            editor.Id, false, true, CancellationToken.None));
        Assert.Equal(80m, excavation.TotalAmount);
        Assert.Equal(80m, await db.ExcavationRecords.AsNoTracking().Where(x => x.Id == excavation.Id).Select(x => x.TotalAmount).SingleAsync());
    }
}
