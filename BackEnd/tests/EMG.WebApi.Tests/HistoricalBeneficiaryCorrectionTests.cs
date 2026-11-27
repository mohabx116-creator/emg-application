using System.Security.Claims;
using System.Security.Cryptography;
using ClosedXML.Excel;
using EMG.Api.Modules.UniversalImports.Controllers;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace EMG.WebApi.Tests;

public sealed class HistoricalBeneficiaryCorrectionTests
{
    [Fact]
    public async Task NamedAccountProposalRequiresCorroboratingWorkWithinTheOriginalTable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Scoped test", Code = "PREVIEW" };
        var account = new Contractor { Name = "طرف الاختبار" };
        var batch = new HistoricalImportBatch { Project = project, Status = "POSTED", SourceSha256 = new string('A', 64) };
        db.AddRange(batch, account);
        await db.SaveChangesAsync();
        var line = new HistoricalSettlement { Batch = batch, Sheet = "حساب أعمال", Cell = "K10", Currency = "EGP", Amount = 30, SourceJson = """{"header":"K9"}""" };
        var work = new HistoricalWorkLine { Batch = batch, Sheet = line.Sheet, Cell = "J10", SourceRow = 10, Currency = "EGP", Amount = 100,
            Party = account.Name, ContractorId = account.Id };
        db.AddRange(work, line);
        await db.SaveChangesAsync();
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(line.Sheet);
        sheet.Cell("C2").Value = line.Sheet + " : " + account.Name;
        sheet.Cell("D9").Value = "اسم المورد";
        sheet.Cell("J9").Value = "العمل";
        sheet.Cell("K9").Value = "الدفعات";
        sheet.Cell("D10").Value = account.Name;
        sheet.Cell("J10").Value = 100;
        sheet.Cell("K10").Value = 30;
        sheet.Range("A9:K10").CreateTable();
        var proposed = await HistoricalBeneficiaryCorrectionController.ProposeAsync(db, line, book, default);
        Assert.Equal(account.Id, proposed.AccountId);
        Assert.Equal("contractor", proposed.Role);
        Assert.Null(line.ContractorId);
        Assert.Empty(await db.HistoricalImportAuditEvents.ToListAsync());

        sheet.Cell("K10").Value = -30;
        await Assert.ThrowsAsync<InvalidOperationException>(() => HistoricalBeneficiaryCorrectionController.ProposeAsync(db, line, book, default));
        sheet.Cell("K10").Value = 30;
        sheet.Cell("D10").Value = "طرف آخر";
        await Assert.ThrowsAsync<InvalidOperationException>(() => HistoricalBeneficiaryCorrectionController.ProposeAsync(db, line, book, default));
        sheet.Cell("D10").Value = account.Name;
        work.Sheet = "جدول مختلف";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => HistoricalBeneficiaryCorrectionController.ProposeAsync(db, line, book, default));
    }

    [Fact]
    public async Task ApplyRequiresExplicitOwnerAndLocalTargetBeforeOpeningAConnection()
    {
        foreach (var port in new[] { 55464, 5432 })
        {
            var target = new NpgsqlConnectionStringBuilder { Host = "127.0.0.1", Port = port, Database = "emg_restore" };
            await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(target.ConnectionString).Options);
            var controller = Controller(db);
            using var stream = new MemoryStream([1]);
            var result = await controller.Apply(1, 1, new() { Workbook = new FormFile(stream, 0, 1, "workbook", "fixture.xlsx") }, default);
            Assert.IsType<ForbidResult>(result.Result);
            Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
        }
    }

    // Optional read-only verification on the existing authorized local project, without running migrations.
    [OriginalStatementFact]
    public async Task OriginalNamedStatementPreviewsAllItsUnassignedSettlements()
    {
        var target = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__EmgDb"));
        Assert.Contains(target.Host, new[] { "127.0.0.1", "localhost" });
        Assert.Equal(55464, target.Port);
        Assert.Equal("emg_restore", target.Database);
        var path = Environment.GetEnvironmentVariable("EMG_CORRECTION_WORKBOOK")!;
        var source = await File.ReadAllBytesAsync(path);
        var hash = Convert.ToHexString(SHA256.HashData(source));
        var sheet = Environment.GetEnvironmentVariable("EMG_CORRECTION_SHEET")!;
        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(target.ConnectionString).Options);
        var matches = await db.HistoricalSettlements.AsNoTracking().Where(s => s.Batch.SourceSha256.ToUpper() == hash &&
                s.Sheet == sheet && s.SupplierId == null && s.ContractorId == null)
            .Select(s => new { s.Id, s.Batch.ProjectId }).ToListAsync();
        Assert.NotEmpty(matches);
        foreach (var line in matches)
        {
            using var stream = new MemoryStream(source);
            var result = await Controller(db).PreviewChange(line.ProjectId, line.Id, new() {
                Workbook = new FormFile(stream, 0, source.Length, "workbook", Path.GetFileName(path)) }, default);
            var preview = Assert.IsType<HistoricalBeneficiaryCorrectionController.Preview>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(line.Id, preview.Change.SettlementId);
            Assert.Equal(64, preview.ProposalSha256.Length);
        }
    }

    private static HistoricalBeneficiaryCorrectionController Controller(EmgDbContext db)
    {
        var host = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        return new(db, host.Environment, new ConfigurationBuilder().Build()) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "17"),
                    new Claim(ClaimTypes.Role, "admin")], "fixture")) } }
        };
    }

    public sealed class OriginalStatementFactAttribute : FactAttribute
    {
        public OriginalStatementFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EMG_CORRECTION_WORKBOOK")))
                Skip = "Requires an original workbook and an explicitly supplied local read-only verification target.";
        }
    }
}
