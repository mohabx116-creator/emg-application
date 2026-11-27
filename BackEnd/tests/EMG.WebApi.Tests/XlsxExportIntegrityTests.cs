using EMG.Api.Modules.FinalAccounts.Dtos;
using EMG.Api.Modules.Reports.Dtos;
using EMG.Api.Modules.Reports.Controllers;
using EMG.Api.Modules.Reports.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace EMG.WebApi.Tests;

public sealed class XlsxExportIntegrityTests
{
    [Fact]
    public void GIT02E1B_SyntheticTemplateRetention()
    {
        var date = new DateTime(2031, 4, 17);
        var empty = CreateReport();
        var populated = CreateRepresentativeReport() with
        {
            Movements = [new(91, date, "أعمال الموقع", "SYNTHETIC MOVEMENT", "SYNTHETIC SUPPLIER", "م", 7, 101, 707, "الموقع", "معتمد", "SYNTHETIC-M")],
            Advances = [new(92, date, "SYNTHETIC CLIENT", "SYNTHETIC PAYER", 808, 0, 808, "معتمد", "SYNTHETIC-A", new Dictionary<string, decimal>())],
            SupplierSupplies = [new(93, date, "SYNTHETIC SUPPLIER", "SYNTHETIC TYPE", "SYNTHETIC ITEM", "م", 9, 101, 909, "SYNTHETIC-S")],
            SupplierPayments = [new(94, date, "SYNTHETIC SUPPLIER", 1009, "SYNTHETIC METHOD", "SYNTHETIC-P")]
        };
        var emptyBytes = XlsxBuilder.Build(empty, null);
        var populatedBytes = XlsxBuilder.Build(populated, null);
        TemplateRetentionProbe.AssertNoOrphanSharedStrings(emptyBytes, populatedBytes);
        TemplateRetentionProbe.AssertReviewedFormulaCachesCleared(emptyBytes, populatedBytes);
        AssertOpenXmlValid(emptyBytes);
        AssertOpenXmlValid(populatedBytes);
    }

    [Fact(Skip = "Private original workbook parity is an optional owner-run check.")]
    public void PrivateOriginalTemplateLegacyParity()
    {
        var original = Environment.GetEnvironmentVariable("EMG_TEMPLATE_PROBE_ORIGINAL")
            ?? throw new InvalidOperationException("Supply the approved private fixture path.");
        TemplateRetentionProbe.AssertReviewedLegacyPayloadsAbsent(
            original, XlsxBuilder.Build(CreateReport(), null), XlsxBuilder.Build(CreateRepresentativeReport(), null));
    }

    [Theory]
    [InlineData("excavation", 38)]
    [InlineData("concrete", 21)]
    [InlineData("steel", 46)]
    [InlineData("finishing", 40)]
    public void OverflowKeepsEveryWorkRowInAFullDetailSheet(string family, int count)
    {
        var rows = Enumerable.Range(1, count).Select(i => Item(i)).ToArray();
        var report = family switch
        {
            "excavation" => CreateReport(excavations: rows),
            "concrete" => CreateReport(concretes: rows),
            "steel" => CreateReport(steels: rows),
            _ => CreateReport(finishings: rows)
        };
        var bytes = XlsxBuilder.Build(report, null);
        AssertOpenXmlValid(bytes);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = XDocument.Load(archive.GetEntry("xl/worksheets/sheet14.xml")!.Open());
        Assert.Equal(count + 1, sheet.Descendants(ns + "row").Count());
        Assert.Contains($"item {count}", sheet.Descendants(ns + "t").Select(cell => cell.Value));
    }

    [Fact]
    public void ConcreteAtCapacityPreservesMergedHeadersAndSummaryRows()
    {
        var report = CreateReport(concretes: Enumerable.Range(1, 20).Select(index => Item(index, "خرسانة مقاول")).ToList());
        using var archive = new ZipArchive(new MemoryStream(XlsxBuilder.Build(report, null)), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = XDocument.Load(archive.GetEntry("xl/worksheets/sheet3.xml")!.Open());
        var merged = sheet.Root!.Element(ns + "mergeCells")!.Elements(ns + "mergeCell").Select(item => (string?)item.Attribute("ref")).ToHashSet();
        Assert.Contains("A7:A9", merged);
        Assert.Contains("M30:T30", merged);
        Assert.Contains("M31:U31", merged);
        Assert.Contains(sheet.Descendants(ns + "c"), cell => (string?)cell.Attribute("r") == "A29");
        Assert.Contains(sheet.Descendants(ns + "c"), cell => (string?)cell.Attribute("r") == "U30");
        Assert.DoesNotContain(sheet.Descendants(ns + "c"), cell => (string?)cell.Attribute("r") is "A8" or "B8" or "C8" or "J8" && (string?)cell.Attribute("t") == "inlineStr");
    }

    [Fact]
    public void SteelAtCapacityPreservesMergedSummaryRow()
    {
        var report = CreateReport(steels: Enumerable.Range(1, 45).Select(index => Item(index, "حديد")).ToList());
        using var archive = new ZipArchive(new MemoryStream(XlsxBuilder.Build(report, null)), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = XDocument.Load(archive.GetEntry("xl/worksheets/sheet2.xml")!.Open());
        var merged = sheet.Root!.Element(ns + "mergeCells")!.Elements(ns + "mergeCell").Select(item => (string?)item.Attribute("ref")).ToHashSet();
        Assert.Contains("A52:B52", merged);
        Assert.Contains(sheet.Descendants(ns + "c"), cell => (string?)cell.Attribute("r") == "A51");
        Assert.Contains(sheet.Descendants(ns + "c"), cell => (string?)cell.Attribute("r") == "F52");
    }



    [Fact]
    public async Task ControllerReturnsStructured422ForCapacityFailure()
    {
        var controller = new ReportsController(new CapacityFailingReportsService());
        var result = await controller.ProjectWorkbook(1, CancellationToken.None);
        var response = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(422, response.StatusCode);
        var body = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal(5, body.EnumerateObject().Count());
        Assert.Equal("XLSX_EXPORT_CAPACITY_EXCEEDED", body.GetProperty("code").GetString());
        Assert.Equal("تجاوز تصدير Excel سعة صفحة «اختبار»: تم العثور على 2 صفوف بينما السعة الآمنة 1.", body.GetProperty("message").GetString());
        Assert.Equal("اختبار", body.GetProperty("sheet").GetString());
        Assert.Equal(2, body.GetProperty("actual").GetInt32());
        Assert.Equal(1, body.GetProperty("capacity").GetInt32());
    }

    [Fact]
    public void ExportedTemplatePackageHasConsistentTablesAndCalculatedColumns()
    {
        using var stream = new MemoryStream(XlsxBuilder.Build(CreateRepresentativeReport(), null));
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.Null(archive.GetEntry("xl/calcChain.xml"));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase));

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var entry in archive.Entries.Where(item => item.FullName.StartsWith("xl/tables/table", StringComparison.OrdinalIgnoreCase) && item.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            var table = XDocument.Load(entry.Open());
            var root = table.Root!;
            var reference = (string?)root.Attribute("ref");
            Assert.False(string.IsNullOrWhiteSpace(reference));
            Assert.Equal(Width(reference!), root.Element(ns + "tableColumns")!.Elements(ns + "tableColumn").Count());
            Assert.Equal(reference, (string?)root.Element(ns + "autoFilter")?.Attribute("ref"));
            Assert.NotEmpty(root.Descendants(ns + "calculatedColumnFormula"));
            Assert.All(root.Element(ns + "tableColumns")!.Elements(ns + "tableColumn"), column =>
            {
                var name = (string?)column.Attribute("name");
                Assert.False(string.IsNullOrWhiteSpace(name));
                Assert.DoesNotContain("Column", name!, StringComparison.OrdinalIgnoreCase);
            });
        }

        var workbook = XDocument.Load(archive.GetEntry("xl/workbook.xml")!.Open());
        Assert.Equal(13, workbook.Root!.Element(ns + "sheets")!.Elements(ns + "sheet").Count());
    }

    [Fact]
    public void TableCalculatedColumnsRemainWorksheetFormulas()
    {
        using var archive = new ZipArchive(new MemoryStream(XlsxBuilder.Build(CreateRepresentativeReport(), null)), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        foreach (var sheetName in new[] { "sheet4.xml", "sheet7.xml" })
        {
            var sheet = XDocument.Load(archive.GetEntry($"xl/worksheets/{sheetName}")!.Open());
            Assert.NotNull(sheet.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "A10").Element(ns + "f"));
            Assert.NotNull(sheet.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "I10").Element(ns + "f"));
            Assert.NotNull(sheet.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "J10").Element(ns + "f"));
        }
    }

    [Fact]
    public void AmountOnlySiteExpenseRetainsItsPostedValueInTemplate()
    {
        var row = Movement(1, "QA") with { Amount = 125.50m, Quantity = null, UnitPrice = null };
        var bytes = XlsxBuilder.Build(CreateReport(movements: [row]), null);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = XDocument.Load(archive.GetEntry("xl/worksheets/sheet4.xml")!.Open());
        var amount = sheet.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "J10");
        Assert.Null(amount.Element(ns + "f"));
        Assert.Equal(125.50m, (decimal)amount.Element(ns + "v")!);
        AssertOpenXmlValid(bytes);
    }

    [Fact]
    public void SiteMovementsBeyondTemplateCapacityAreIncludedInFullDetailSheet()
    {
        var rows = Enumerable.Range(1, 43).Select(index =>
            Movement(index, "QA") with { Amount = index * 11m, Reference = $"QA-{index}" }).ToArray();
        var bytes = XlsxBuilder.Build(CreateReport(movements: rows), null);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var detail = XDocument.Load(archive.GetEntry("xl/worksheets/sheet14.xml")!.Open());
        Assert.Equal(44, detail.Descendants(ns + "row").Count());
        var lastAmount = detail.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "H44");
        Assert.Equal(473m, (decimal)lastAmount.Element(ns + "v")!);
        AssertOpenXmlValid(bytes);
    }

    [Fact]
    public void ExtendedPrimaryTablesKeepSignedRowsFormulasStylesAndFooter()
    {
        var movements = Enumerable.Range(1, 49).Select(i => Movement(i, "QA") with { Reference = $"Site!J{i}" }).ToArray();
        var steel = Enumerable.Range(1, 48).Select(i => Item(i, "الحديد") with { TotalAmount = -i }).ToArray();
        var report = CreateReport(movements: movements, steels: steel);
        var bytes = XlsxBuilder.Build(report, null);
        AssertOpenXmlValid(bytes);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument Read(string path) => XDocument.Load(archive.GetEntry(path)!.Open());
        var site = Read("xl/worksheets/sheet4.xml").Descendants(ns + "c").ToDictionary(c => (string)c.Attribute("r")!);
        Assert.Equal("Site!J49", site["K58"].Value);
        Assert.Equal("I58*F58", site["J58"].Element(ns + "f")!.Value);
        Assert.Equal((string?)site["J51"].Attribute("s"), (string?)site["J58"].Attribute("s"));
        Assert.Equal("A9:K58", (string?)Read("xl/tables/table2.xml").Root!.Attribute("ref"));
        var metal = Read("xl/worksheets/sheet2.xml");
        Assert.Contains(metal.Descendants(ns + "mergeCell"), m => (string?)m.Attribute("ref") == "A55:B55");
        Assert.Equal(-48m, (decimal)metal.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == "F54").Element(ns + "v")!);
    }

    [Fact]
    public void AllocationFundedSchedulePrintsIndependentReceiptAndAllocationWithSourceDate()
    {
        var date = new DateTime(2026, 7, 30);
        var source = new ProjectScheduleRow(date, "الموقع", 100000m, "EGP", "بيان", "Test!B4");
        var data = CreateReport() with { RecognizedFunding = [source], Allocations = [source] };
        var bytes = XlsxBuilder.Build(data, null);
        AssertOpenXmlValid(bytes);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cells = XDocument.Load(archive.GetEntry("xl/worksheets/sheet5.xml")!.Open())
            .Descendants(ns + "c").ToDictionary(c => (string)c.Attribute("r")!);
        Assert.Equal(100000m, (decimal)cells["B4"].Element(ns + "v")!);
        Assert.Equal(100000m, (decimal)cells["N4"].Element(ns + "v")!);
        Assert.Equal(date.ToOADate(), (double)cells["O4"].Element(ns + "v")!);
        Assert.Equal(date.ToOADate(), (double)cells["A4"].Element(ns + "v")!);
        Assert.Contains("Test!B4", cells["P4"].Value);
        Assert.Contains("Test!B4", cells["M4"].Value);
    }

    [Fact]
    public void SupplierWorkbookAmountsAreNumericAndInvoiceIdentifiersRemainText()
    {
        var date = new DateTime(2025, 1, 15);
        var report = CreateReport() with
        {
            SupplierSupplies = [new(1, date, "QA", "مواد", "QA", "طن", 2.125m, 125m, 265.63m, "0001")],
            SupplierPayments = [new(1, date, "QA", 100m, "تحويل", "0002")]
        };
        var bytes = XlsxBuilder.Build(report, null);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XElement Cell(int sheet, string address) => XDocument.Load(archive.GetEntry($"xl/worksheets/sheet{sheet}.xml")!.Open())
            .Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == address);
        Assert.Equal(2.125m, (decimal)Cell(12, "F2").Element(ns + "v")!);
        Assert.Equal(265.63m, (decimal)Cell(12, "H2").Element(ns + "v")!);
        Assert.Equal("0001", Cell(12, "I2").Value);
        Assert.Equal(100m, (decimal)Cell(13, "C2").Element(ns + "v")!);
        AssertOpenXmlValid(bytes);
    }

    [Fact]
    public void TextFormulaCachesAreClearedWhileFormulasAndStringTypesRemain()
    {
        var movements = Enumerable.Range(1, 36).Select(index => Movement(index, $"supplier {index}")).ToList();
        var finishings = new[] { Item(1, "أعمال عزل", supplier: "supplier 1") };
        using var archive = new ZipArchive(new MemoryStream(XlsxBuilder.Build(CreateReport(movements: movements, finishings: finishings), null)), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var site = XDocument.Load(archive.GetEntry("xl/worksheets/sheet4.xml")!.Open());
        var insulation = XDocument.Load(archive.GetEntry("xl/worksheets/sheet9.xml")!.Open());
        var siteCell = site.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "D46");
        var insulationCell = insulation.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "D11");

        Assert.Equal("str", (string?)siteCell.Attribute("t"));
        Assert.Equal("str", (string?)insulationCell.Attribute("t"));
        Assert.NotNull(siteCell.Element(ns + "f"));
        Assert.NotNull(insulationCell.Element(ns + "f"));
        Assert.Null(siteCell.Element(ns + "v"));
        Assert.Null(insulationCell.Element(ns + "v"));
    }

    [Fact]
    public void FormulaCellsWithNonNumericCachesNeverUseImplicitNumericType()
    {
        using var archive = new ZipArchive(new MemoryStream(XlsxBuilder.Build(CreateRepresentativeReport(), null)), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var entry in archive.Entries.Where(item => item.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)))
        {
            var sheet = XDocument.Load(entry.Open());
            foreach (var cell in sheet.Descendants(ns + "c").Where(cell => cell.Element(ns + "f") is not null && cell.Element(ns + "v") is not null))
            {
                var cached = (string?)cell.Element(ns + "v");
                if (!decimal.TryParse(cached, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    Assert.Equal("str", (string?)cell.Attribute("t"));
            }
        }
    }

    [Fact]
    public void TableHeadersPreserveTemplateSharedStringCells()
    {
        using var archive = new ZipArchive(new MemoryStream(XlsxBuilder.Build(CreateRepresentativeReport(), null)), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sharedStrings = XDocument.Load(archive.GetEntry("xl/sharedStrings.xml")!.Open()).Root!.Elements(ns + "si").ToArray();
        foreach (var (sheetNumber, tableNumber) in new[] { (4, 2), (7, 3), (8, 4), (9, 5), (10, 6), (11, 7) })
        {
            var sheet = XDocument.Load(archive.GetEntry($"xl/worksheets/sheet{sheetNumber}.xml")!.Open());
            var table = XDocument.Load(archive.GetEntry($"xl/tables/table{tableNumber}.xml")!.Open());
            var headerCells = sheet.Descendants(ns + "row")
                .Single(row => (string?)row.Attribute("r") == "9")
                .Elements(ns + "c")
                .ToList();
            Assert.Equal(11, headerCells.Count);
            Assert.All(headerCells, cell => Assert.Equal("s", (string?)cell.Attribute("t")));
            var headerNames = headerCells.Select(cell =>
                string.Concat(sharedStrings[(int)cell.Element(ns + "v")!].Descendants(ns + "t").Select(text => text.Value)));
            var tableNames = table.Descendants(ns + "tableColumn").Select(column => (string?)column.Attribute("name"));
            Assert.Equal(tableNames, headerNames);
        }
    }

    [Fact]
    public void ExportedWorkbookUsesUniqueWorksheetIdsForAppendedSheets()
    {
        using var archive = new ZipArchive(new MemoryStream(XlsxBuilder.Build(CreateRepresentativeReport(), null)), ZipArchiveMode.Read);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var sheets = XDocument.Load(archive.GetEntry("xl/workbook.xml")!.Open()).Root!.Element(ns + "sheets")!.Elements(ns + "sheet").ToList();
        var ids = sheets.Select(sheet => (string?)sheet.Attribute("sheetId")).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(sheets, sheet => (string?)sheet.Attribute("name") == "توريدات الموردين" && (string?)sheet.Attribute("sheetId") == "17" && (string?)sheet.Attribute(relNs + "id") == "rId17");
        Assert.Contains(sheets, sheet => (string?)sheet.Attribute("name") == "مدفوعات الموردين" && (string?)sheet.Attribute("sheetId") == "18" && (string?)sheet.Attribute(relNs + "id") == "rId18");
    }

    [Fact]
    public void RepresentativeExportPreservesArabicHeadersAndDetailedSections()
    {
        var bytes = XlsxBuilder.Build(CreateRepresentativeReport(), null);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var sheet1 = XDocument.Load(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ToString();
        var sheet3 = XDocument.Load(archive.GetEntry("xl/worksheets/sheet3.xml")!.Open()).ToString();
        var sheet5 = XDocument.Load(archive.GetEntry("xl/worksheets/sheet5.xml")!.Open()).ToString();
        var sharedStrings = XDocument.Load(archive.GetEntry("xl/sharedStrings.xml")!.Open()).ToString();
        Assert.NotEmpty(sheet1);
        Assert.NotEmpty(sheet3);
        Assert.NotEmpty(sheet5);
        Assert.Contains("رقم البند", sharedStrings);
        Assert.Contains("كشف حساب مجمع بكامل الاعمال", sharedStrings);
        Assert.Contains("دفعات تحت الحساب", sharedStrings);
        Assert.Contains("اجمالي تكلفة العمل الاعمال", sharedStrings);
    }

    private static ProjectWorkRow Item(int index, string category = "حفر", string supplier = "supplier") =>
        new(index, DateTime.UtcNow.Date, supplier, category, $"item {index}", "description", "م", 1, 1, 1, "", "", "", "معتمد", "");

    private static ProjectMovementRow Movement(int index, string supplier) =>
        new(index, DateTime.UtcNow.Date, "أعمال الموقع", $"item {index}", supplier, "م", 1, 1, 1, "الموقع", "معتمد", "");

    private static ProjectReportSummary CreateReport(IReadOnlyList<ProjectWorkRow>? excavations = null, IReadOnlyList<ProjectWorkRow>? finishings = null, IReadOnlyList<ProjectWorkRow>? concretes = null, IReadOnlyList<ProjectWorkRow>? steels = null, IReadOnlyList<ProjectMovementRow>? movements = null) =>
        new(new FinalAccountSummary(1, "test", "client", "payer", 5, 0, 0, 0, 0, 0, 0, 0, "متزن", "مسودة"), movements ?? [], [], [], [], excavations ?? [], concretes ?? [], steels ?? [], finishings ?? []);

    private static ProjectReportSummary CreateRepresentativeReport() =>
        CreateReport(
            excavations: [Item(1, "حفر")],
            concretes: [Item(2, "خرسانة مقاول"), Item(3, "خرسانة محطة")],
            steels: [Item(4, "حديد")],
            finishings: [Item(5, "أعمال مباني"), Item(6, "أعمال محارة"), Item(7, "أعمال عزل"), Item(8, "أعمال كهرباء"), Item(9, "أعمال سباكة")]);

    private static int Width(string reference)
    {
        var start = reference.Split(':')[0]; var end = reference.Split(':')[1];
        static int Column(string address) => address.TakeWhile(char.IsLetter).Aggregate(0, (total, letter) => total * 26 + letter - 'A' + 1);
        return Column(end) - Column(start) + 1;
    }

    private static void AssertOpenXmlValid(byte[] workbook)
    {
        using var stream = new MemoryStream(workbook);
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2013).Validate(document).Take(10).ToArray();
        Assert.Empty(errors);
    }

    private sealed class CapacityFailingReportsService : IReportsService
    {
        public Task<ProjectReportSummary?> GetProjectReportAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken) => Task.FromResult<ProjectReportSummary?>(null);
        public Task<string?> ExportProjectMovementsCsvAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<byte[]?> ExportProjectWorkbookAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken) =>
            throw new XlsxExportCapacityException(sheet: "اختبار", actual: 2, capacity: 1);
        public Task<OperationalAuditSummary?> GetOperationalAuditAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken) => Task.FromResult<OperationalAuditSummary?>(null);
        public Task<HistoricalProjectReport?> GetHistoricalProjectReportAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken) => Task.FromResult<HistoricalProjectReport?>(null);
    }
}
