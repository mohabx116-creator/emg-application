using ClosedXML.Excel;
using EMG.Api.Modules.Imports.Services;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class LegacyWorkbookCurrencyTests
{
    [Theory]
    [InlineData("column", "AED", false)]
    [InlineData("column", "ZZZ", false)]
    [InlineData("column", "", false)]
    [InlineData("heading", "المبلغ بالدولار", false)]
    [InlineData("format", "[$AED-3801]#,##0.00", false)]
    [InlineData("builtin", "", false)]
    [InlineData("conflict", "", false)]
    [InlineData("project", "عملة المشروع: USD", false)]
    [InlineData("metadata", "عملة: مجهولة", false)]
    [InlineData("text", "100 USD", false)]
    [InlineData("column", "جنيه مصري", true)]
    [InlineData("format", "[$ج.م.-C01]#,##0.00", true)]
    [InlineData("format", "[$-409]#,##0.00", true)]
    [InlineData("local-builtin", "", true)]
    [InlineData("notes", "كان عرض البديل بالدولار وسُدد المبلغ المتفق عليه بالجنيه", true)]
    public async Task PreviewAndPlanUseTheSameCurrencyEvidence(string source, string value, bool accepted)
    {
        using var workbook = Template();
        var sheet = workbook.Worksheet(1);
        switch (source)
        {
            case "column":
                sheet.Cell("L9").Value = "العملة";
                sheet.Cell("L10").Value = value;
                break;
            case "heading": sheet.Cell("J9").Value = value; break;
            case "format": sheet.Cell("J10").Style.NumberFormat.Format = value; break;
            case "builtin": sheet.Cell("J10").Style.NumberFormat.NumberFormatId = 5; break;
            case "project": workbook.AddWorksheet("مرفقات").Cell("A1").Value = value; break;
            case "metadata": sheet.Cell("A2").Value = value; break;
            case "text": sheet.Cell("J10").Value = value; break;
            case "notes": sheet.Cell("K10").Value = value; break;
            case "local-builtin":
                sheet.Cell("L9").Value = "Currency";
                sheet.Cell("L10").Value = "EGP";
                sheet.Cell("J10").Style.NumberFormat.NumberFormatId = 5;
                break;
            case "conflict":
                sheet.Cell("L9").Value = "Currency";
                sheet.Cell("L10").Value = "EGP";
                sheet.Cell("J10").Style.NumberFormat.Format = "[$USD-409]#,##0.00";
                break;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var file = new FormFile(stream, 0, stream.Length, "file", "test.xlsx");
        var service = new WorkbookImportService();
        var preview = await service.PreviewAsync(file, default);
        Assert.Equal(accepted, preview.IsValid);
        if (!accepted) Assert.Contains(preview.Warnings, w => w.Contains("الجنيه المصري") && w.Contains('!'));
        Assert.Equal(accepted, await service.BuildNewProjectPlanAsync(file, default) is not null);
    }

    [Fact]
    public async Task AllLegacyWritersStopBeforeSavingAnyAmount()
    {
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new EmgDbContext(options);
        var project = new Project { Code = "QA-CURRENCY", Name = "QA" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        var host = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = Path.GetTempPath() }).Environment;
        var service = new WorkbookImportService(db, host);
        using var workbook = Template();
        workbook.Worksheet(1).Cell("J10").Style.NumberFormat.Format = "[$USD-409]#,##0.00";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var file = new FormFile(stream, 0, stream.Length, "file", "foreign.xlsx");

        Assert.Null(await service.CommitNewProjectAsync(file, null, null, null, null, null, 1, default));
        Assert.Null(await service.CommitExistingProjectAsync(file, project.Id, 1, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitApprovedHistoryAsync(
            file, project.Id, true, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 1, true, default));
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(1, await db.Projects.CountAsync());
        Assert.False(await db.SiteMovements.AnyAsync());
        Assert.False(await db.Attachments.AnyAsync());
    }

    private static XLWorkbook Template()
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("اعمال الموقع");
        sheet.Cell("A9").Value = "رقم البند";
        sheet.Cell("B9").Value = "التاريخ";
        sheet.Cell("C9").Value = "نوع الاعمال";
        sheet.Cell("F9").Value = "السعر";
        sheet.Cell("J9").Value = "المبلغ";
        sheet.Cell("K9").Value = "ملاحظات";
        sheet.Cell("A10").Value = 1;
        sheet.Cell("B10").Value = new DateTime(2025, 1, 1);
        sheet.Cell("C10").Value = "عمل تجريبي";
        sheet.Cell("J10").Value = 100;
        return workbook;
    }
}
