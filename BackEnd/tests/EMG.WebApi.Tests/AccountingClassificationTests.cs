using EMG.Api.Modules.AccountingClassification.Dtos;
using EMG.Api.Modules.AccountingClassification.Services;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class AccountingClassificationTests
{
    [Fact]
    public async Task Preview_ClassifiesProvenanceAndKeepsFinancialSourcesSeparate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        var project = new Project { Name = "Project 191 fixture", Code = "191" };
        var otherProject = new Project { Name = "Other", Code = "OTHER" };
        var user = new AppUser { UserName = "engineer", Email = "engineer@example.test", DisplayName = "Engineer", PasswordHash = "test" };
        var supplier = new Supplier { Name = "Synthetic supplier" };
        db.AddRange(project, otherProject, user, supplier);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { ProjectId = project.Id, UserId = user.Id });
        db.SiteMovements.Add(new SiteMovement { Project = project, TotalAmount = 100m, Status = "معتمد", Reference = "استيراد: Sheet1 صف 8" });
        db.ConcreteRecords.AddRange(
            new ConcreteRecord { Project = project, TotalAmount = 200m, Status = "معتمد", AccountCategory = "غير محدد", Notes = "استيراد: SheetConcrete صف 21" },
            new ConcreteRecord { Project = project, TotalAmount = 300m, Status = "معتمد", AccountCategory = "المحطة", Notes = "استيراد: SheetConcrete صف 22" });
        db.FinishingRecords.Add(new FinishingRecord { Project = project, TotalAmount = 400m, Status = "معتمد", AccountCategory = "غير محدد", Notes = "استيراد: اعمال العزل صف 7" });
        var advance = new AdvancePayment { Project = project, Amount = 1000m, Status = "معتمد", Notes = "استيراد: دفعات صف 3" };
        db.Add(advance);
        db.AdvanceAllocations.Add(new AdvanceAllocation { AdvancePayment = advance, Amount = 250m, AccountCategory = "المكتب", Notes = "استيراد: دفعات صف 4" });
        db.SupplierSupplies.Add(new SupplierSupply { Project = project, Supplier = supplier, TotalAmount = 500m, Status = "معتمد", Notes = "استيراد: توريدات صف 9" });
        db.SupplierPayments.Add(new SupplierPayment { Project = project, Supplier = supplier, Amount = 125m, Status = "معتمد", Notes = "استيراد دفعة من توريدات صف 10" });
        db.SiteMovements.Add(new SiteMovement { Project = otherProject, TotalAmount = 9999m, Status = "معتمد", Reference = "استيراد: Other صف 1" });
        await db.SaveChangesAsync();

        var preview = await new AccountingClassificationService(db).PreviewAsync(project.Id, user.Id, false, CancellationToken.None);

        Assert.NotNull(preview);
        Assert.Equal(8, preview!.Lines.Count);
        Assert.Equal(100m, Total(preview, AccountingClassificationCode.SiteExpense));
        Assert.Equal(300m, Total(preview, AccountingClassificationCode.ConcreteStation));
        Assert.Equal(400m, Total(preview, AccountingClassificationCode.Insulation));
        Assert.Equal(1000m, Total(preview, AccountingClassificationCode.OwnerFunding));
        Assert.Equal(250m, Total(preview, AccountingClassificationCode.OfficePayment));
        Assert.Equal(500m, Total(preview, AccountingClassificationCode.SupplierSupply));
        Assert.Equal(125m, Total(preview, AccountingClassificationCode.SupplierPayment));
        Assert.Equal(200m, Total(preview, AccountingClassificationCode.Unresolved));
        Assert.Contains(preview.UnresolvedRecords, item => item.EntityType == "concrete" && item.Diagnostic.Contains("لم يتم التخمين", StringComparison.Ordinal));
        Assert.Contains(preview.Lines, item => item.EntityType == "supplier-payment" && item.Provenance.SourceSheet == "توريدات");
        Assert.DoesNotContain(preview.Lines, item => item.Amount == 9999m);
    }

    [Fact]
    public async Task Preview_DoesNotExposeProjectWithoutAssignment()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Restricted", Code = "RESTRICTED" };
        var user = new AppUser { UserName = "engineer", Email = "engineer2@example.test", DisplayName = "Engineer", PasswordHash = "test" };
        db.AddRange(project, user);
        await db.SaveChangesAsync();

        var preview = await new AccountingClassificationService(db).PreviewAsync(project.Id, user.Id, false, CancellationToken.None);

        Assert.Null(preview);
    }

    [Fact]
    public async Task Preview_IncludesOnlyApprovedOrPostedInCurrentTotals_ButPreservesDiagnostics()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Status fixture", Code = "STATUS" };
        db.Add(project);
        db.SiteMovements.AddRange(
            new SiteMovement { Project = project, TotalAmount = 75m, Status = "مسودة", Reference = "استيراد: Sheet1 صف 2" },
            new SiteMovement { Project = project, TotalAmount = 25m, Status = "مرحّل", Reference = "استيراد: Sheet1 صف 3" });
        await db.SaveChangesAsync();

        var preview = await new AccountingClassificationService(db).PreviewAsync(project.Id, null, true, CancellationToken.None);

        Assert.NotNull(preview);
        var total = preview!.Totals.Single(item => item.Classification == AccountingClassificationCode.SiteExpense);
        Assert.Equal(25m, total.CurrentApprovedPostedAmount);
        Assert.Equal(25m, total.ImportedApprovedPostedAmount);
        Assert.Equal(2, preview.Lines.Count);
        Assert.All(preview.Lines, item => Assert.Equal(AccountingClassificationCode.SiteExpense, item.Classification));
    }

    [Fact]
    public async Task Preview_UsesTrustedConcreteImportProfilesWithoutRewritingRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Profile fixture", Code = "PROFILE" };
        db.Add(project);
        db.ConcreteRecords.AddRange(
            new ConcreteRecord { Project = project, TotalAmount = 1943350m, AccountCategory = "باقي المصنعيات", Status = "معتمد", Notes = "استيراد: اعمال الخرسانات صف 31 | خرسانات مزدوجة D:G:H" },
            new ConcreteRecord { Project = project, TotalAmount = 1372798m, AccountCategory = "باقي المصنعيات", Status = "معتمد", Notes = "استيراد: اعمال الخرسانات صف 30 | خرسانات مزدوجة N:R:S:U" });
        await db.SaveChangesAsync();

        var preview = await new AccountingClassificationService(db).PreviewAsync(project.Id, null, true, CancellationToken.None);

        Assert.NotNull(preview);
        Assert.Equal(1943350m, preview!.Totals.Single(item => item.Classification == AccountingClassificationCode.ConcreteStation).CurrentApprovedPostedAmount);
        Assert.Equal(1372798m, preview.Totals.Single(item => item.Classification == AccountingClassificationCode.ContractorWorkmanship).CurrentApprovedPostedAmount);
        Assert.Empty(preview.UnresolvedRecords);
        Assert.All(await db.ConcreteRecords.Select(item => item.AccountCategory).ToListAsync(), category => Assert.Equal("باقي المصنعيات", category));
    }

    private static decimal Total(AccountingClassificationPreview preview, AccountingClassificationCode code) =>
        preview.Totals.Single(item => item.Classification == code).CurrentApprovedPostedAmount;

    private static EmgDbContext CreateDb(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options);
}
