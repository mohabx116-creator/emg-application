using EMG.Api.Modules.ConsolidatedStatements.Controllers;
using EMG.Api.Modules.ConsolidatedStatements.Dtos;
using EMG.Api.Modules.ConsolidatedStatements.Services;
using EMG.Api.Modules.AccountingClassification.Services;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace EMG.WebApi.Tests;

public sealed class ConsolidatedStatementTests
{
    [Fact]
    public async Task Preview_UsesProject191SourceClassification_AndKeepsDimensionsSeparate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Project 191", Code = "191", OfficeSharePercentage = 5m };
        var user = new AppUser { UserName = "manager", Email = "manager@example.test", DisplayName = "Manager", PasswordHash = "test" };
        var supplier = new Supplier { Name = "Supplier" };
        db.AddRange(project, user, supplier);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = user.Id, ProjectId = project.Id });
        db.SiteMovements.Add(new SiteMovement { ProjectId = project.Id, TotalAmount = 217175m, Status = "معتمد", Reference = "استيراد: خاص باعمال الموقع صف 7" });
        db.ExcavationRecords.Add(new ExcavationRecord { ProjectId = project.Id, TotalAmount = 442500m, Status = "معتمد", Notes = "استيراد: اعمال الحفر صف 7" });
        db.ConcreteRecords.AddRange(
            new ConcreteRecord { ProjectId = project.Id, TotalAmount = 1372798m, AccountCategory = "المقاول", Status = "معتمد", Notes = "استيراد: اعمال الخرسانات صف 30" },
            new ConcreteRecord { ProjectId = project.Id, TotalAmount = 1943350m, AccountCategory = "المحطة", Status = "معتمد", Notes = "استيراد: اعمال الخرسانات صف 31" });
        db.SteelRecords.Add(new SteelRecord { ProjectId = project.Id, TotalAmount = 2423575m, Status = "معتمد", Notes = "استيراد: اعمال توريد الحديد صف 52" });
        db.FinishingRecords.Add(new FinishingRecord { ProjectId = project.Id, TotalAmount = 64600m, Status = "معتمد", AccountCategory = "غير محدد", Notes = "استيراد: اعمال العزل صف 7" });
        var advance = new AdvancePayment { ProjectId = project.Id, Amount = 3649261m, Status = "معتمد", Notes = "استيراد: دفعات تحت الحساب صف 21" };
        db.Add(advance);
        await db.SaveChangesAsync();
        db.AdvanceAllocations.Add(new AdvanceAllocation { AdvancePaymentId = advance.Id, Amount = 227000m, AccountCategory = "المكتب", Notes = "استيراد: دفعات تحت الحساب صف 19" });
        db.SupplierSupplies.Add(new SupplierSupply { ProjectId = project.Id, SupplierId = supplier.Id, TotalAmount = 500m, Status = "معتمد", Notes = "استيراد: توريدات صف 9" });
        db.SupplierPayments.Add(new SupplierPayment { ProjectId = project.Id, SupplierId = supplier.Id, Amount = 100m, Status = "معتمد", Notes = "استيراد دفعة من توريدات صف 10" });
        await db.SaveChangesAsync();
        var before = await db.SiteMovements.CountAsync() + await db.ConcreteRecords.CountAsync() + await db.AdvancePayments.CountAsync();

        var preview = await CreateService(db).PreviewAsync(user.Id, false, CancellationToken.None);
        var row = Assert.Single(preview.Projects);

        Assert.Equal("191", row.ProjectCode);
        Assert.Equal(1372798m, Column(row, "J").CurrentAmount);
        Assert.Equal(1943350m, Column(row, "K").CurrentAmount);
        Assert.Equal(3316148m, Column(row, "H").DiagnosticAmount);
        Assert.Equal(64600m, Column(row, "O").CurrentAmount);
        Assert.Equal(3649261m, Column(row, "S").CurrentAmount);
        Assert.Equal(227000m, Column(row, "R").CurrentAmount);
        Assert.Equal(500m, row.Dimensions.SupplierSupplies.CurrentAmount);
        Assert.Equal(100m, row.Dimensions.SupplierPayments.CurrentAmount);
        Assert.Null(Column(row, "T").CurrentAmount);
        Assert.Empty(row.UnresolvedRecords);
        Assert.Equal(before, await db.SiteMovements.CountAsync() + await db.ConcreteRecords.CountAsync() + await db.AdvancePayments.CountAsync());
    }

    [Fact]
    public async Task Preview_ExcludesUnauthorizedProjects_AndMarksAmbiguousRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var visible = new Project { Name = "Visible", Code = "002" };
        var hidden = new Project { Name = "Hidden", Code = "001" };
        var user = new AppUser { UserName = "engineer", Email = "engineer@example.test", DisplayName = "Engineer", PasswordHash = "test" };
        db.AddRange(visible, hidden, user);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = user.Id, ProjectId = visible.Id });
        db.ConcreteRecords.Add(new ConcreteRecord { ProjectId = visible.Id, TotalAmount = 75m, AccountCategory = "باقي المصنعيات", Status = "معتمد", Notes = "استيراد: اعمال الخرسانات صف 21" });
        db.SiteMovements.Add(new SiteMovement { ProjectId = hidden.Id, TotalAmount = 900m, Status = "معتمد", Reference = "استيراد: Hidden صف 1" });
        await db.SaveChangesAsync();

        var preview = await CreateService(db).PreviewAsync(user.Id, false, CancellationToken.None);
        var row = Assert.Single(preview.Projects);

        Assert.Equal("002", row.ProjectCode);
        Assert.DoesNotContain(preview.Projects, item => item.ProjectCode == "001");
        Assert.Equal("INCOMPLETE_UNVERIFIED", row.CompletenessStatus);
        Assert.Equal(75m, row.Dimensions.UnresolvedFinancialRecords.CurrentAmount);
        Assert.Null(Column(row, "T").CurrentAmount);
    }

    [Fact]
    public void Controller_RequiresAuthenticationAndReportsPermission()
    {
        var type = typeof(ConsolidatedStatementsController);
        Assert.NotNull(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
        var method = type.GetMethod(nameof(ConsolidatedStatementsController.Preview));
        Assert.Contains(method!.GetCustomAttributes(true), attribute => attribute.GetType().Name == "PermissionAuthorizeAttribute");
    }

    private static MasterStatementColumn Column(ConsolidatedStatementRow row, string column) => row.Columns.Single(item => item.Column == column);

    private static IConsolidatedStatementService CreateService(EmgDbContext db) =>
        new ConsolidatedStatementService(db, new AccountingClassificationService(db));

    private static EmgDbContext CreateDb(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options);
}
