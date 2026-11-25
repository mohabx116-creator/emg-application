using EMG.Api.Modules.ReportBuilder.Controllers;
using EMG.Api.Modules.ReportBuilder.Dtos;
using EMG.Api.Modules.ReportBuilder.Services;
using EMG.Api.Modules.AccountingClassification.Services;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class ReportBuilderTests
{
    [Fact]
    public async Task Preview_ComposesProjectCategoryDateAndPartyFilters_ServerSide()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        var visible = new Project { Name = "Visible", Code = "001" };
        var hidden = new Project { Name = "Hidden", Code = "002" };
        var user = new AppUser { UserName = "reporter", Email = "reporter@example.test", DisplayName = "Reporter", PasswordHash = "test" };
        var supplier = new Supplier { Name = "Supplier A" };
        db.AddRange(visible, hidden, user, supplier);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { ProjectId = visible.Id, UserId = user.Id });
        db.SiteMovements.Add(new SiteMovement { ProjectId = visible.Id, TotalAmount = 100m, Status = "معتمد", MovementDate = new DateTime(2026, 1, 10), Reference = "استيراد: الموقع صف 1" });
        db.SupplierSupplies.Add(new SupplierSupply { ProjectId = visible.Id, SupplierId = supplier.Id, TotalAmount = 250m, Status = "معتمد", SupplyDate = new DateTime(2026, 1, 11), Notes = "استيراد: توريدات صف 2" });
        db.SiteMovements.Add(new SiteMovement { ProjectId = hidden.Id, TotalAmount = 999m, Status = "معتمد", Reference = "استيراد: مشروع مخفي" });
        await db.SaveChangesAsync();
        var before = await db.SiteMovements.CountAsync() + await db.SupplierSupplies.CountAsync();

        var result = await CreateService(db).PreviewAsync(new ReportBuilderQuery(), user.Id, false, CancellationToken.None);

        Assert.Equal(2, result.Transactions.Count);
        Assert.All(result.Transactions, row => Assert.Equal("001", row.ProjectCode));
        Assert.Contains(result.Transactions, row => row.Category == "SiteExpense" && row.CurrentAmount == 100m);
        Assert.Contains(result.Transactions, row => row.Category == "SupplierSupply" && row.CurrentAmount == 250m && row.Party == "Supplier A");
        Assert.DoesNotContain(result.Transactions, row => row.CurrentAmount == 999m);

        var filtered = await CreateService(db).PreviewAsync(new ReportBuilderQuery(Categories: ["SupplierSupply"], Parties: ["Supplier A"], FromDate: new DateTime(2026, 1, 11), ToDate: new DateTime(2026, 1, 11)), user.Id, false, CancellationToken.None);
        Assert.Single(filtered.Transactions);
        Assert.Equal(250m, filtered.Summary.Single().CurrentAmount);
        Assert.Equal(before, await db.SiteMovements.CountAsync() + await db.SupplierSupplies.CountAsync());
    }

    [Fact]
    public async Task Preview_SeparatesImportedCurrentAndUnresolvedDimensions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Snapshots", Code = "003" };
        db.Add(project);
        db.SiteMovements.AddRange(
            new SiteMovement { Project = project, TotalAmount = 100m, Status = "معتمد", MovementDate = new DateTime(2026, 2, 1), Reference = "استيراد: Sheet1 صف 2" },
            new SiteMovement { Project = project, TotalAmount = 50m, Status = "معتمد", MovementDate = new DateTime(2026, 2, 2), Reference = "تشغيل حالي" });
        db.ConcreteRecords.Add(new ConcreteRecord { Project = project, TotalAmount = 20m, Status = "معتمد", Notes = "استيراد: خرسانة غير مصنفة صف 4", AccountCategory = "باقي المصنعيات" });
        await db.SaveChangesAsync();

        var result = await CreateService(db).PreviewAsync(new ReportBuilderQuery(GroupBy: ["dimension"], Snapshot: "both", IncludeUnresolved: true), null, true, CancellationToken.None);

        Assert.Equal(3, result.Transactions.Count);
        Assert.Equal(150m, result.Transactions.Where(row => row.Category == "SiteExpense").Sum(row => row.CurrentAmount));
        Assert.Equal(100m, result.Transactions.Where(row => row.Category == "SiteExpense").Sum(row => row.ImportedAmount));
        var unresolved = Assert.Single(result.Transactions.Where(row => row.IsUnresolved));
        Assert.Equal(20m, unresolved.UnresolvedAmount);
        Assert.Contains(result.Summary, row => row.UnresolvedAmount == 20m && !row.IsComplete);
        Assert.Contains(result.Warnings, warning => warning.Contains("H", StringComparison.Ordinal));
    }

    [Fact]
    public void Controller_RequiresAuthenticationAndReportsPermission()
    {
        var type = typeof(ReportBuilderController);
        Assert.NotNull(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
        var method = type.GetMethod(nameof(ReportBuilderController.Preview));
        Assert.Contains(method!.GetCustomAttributes(true), attribute => attribute.GetType().Name == "PermissionAuthorizeAttribute");
    }

    private static IReportBuilderService CreateService(EmgDbContext db) => new ReportBuilderService(db, new AccountingClassificationService(db));

    private static EmgDbContext CreateDb(SqliteConnection connection) => new(new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options);
}
