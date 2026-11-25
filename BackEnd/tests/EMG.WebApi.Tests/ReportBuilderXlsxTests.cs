using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using EMG.Api.Modules.AccountingClassification.Services;
using EMG.Api.Modules.ReportBuilder.Controllers;
using EMG.Api.Modules.ReportBuilder.Dtos;
using EMG.Api.Modules.ReportBuilder.Services;
using EMG.Api.Modules.Security.Authorization;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EMG.WebApi.Tests;

public sealed class ReportBuilderXlsxTests
{
    private static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly DateTime January = new(2026, 1, 12);
    private static readonly string[] Columns = ["sourceText", "currentAmount", "project", "importedAmount", "category", "party", "date", "source"];

    [Fact]
    public void Detailed_LegacyGeometryHeadersAndFinancialCells()
    {
        using var output = Export(Example(), "detailed", "dynamic-detailed.xlsx");
        Assert.Equal(16, output.Names.Length);
        Assert.Equal(new[] { "اعمال الحفر", "اعمال توريد الحديد", "اعمال الخرسانات", "خاص باعمال الموقع", "دفعات تحت الحساب",
            "كشف حساب نهائي", "اعمال المباني", "اعمال المحاره", "اعمال العزل", "اعمال الكهرباء", "اعمال السباكه" }, output.Names.Take(11));
        Assert.Contains("توريدات الموردين", output.Names);
        Assert.Contains("مدفوعات الموردين", output.Names);
        Assert.Equal(200m, output.Number("اعمال الخرسانات", "G31"));
        Assert.Equal(120m, output.Number("اعمال الخرسانات", "U30"));
        Assert.Equal(66m, output.Number("اعمال العزل", "K7"));
        Assert.Equal(1000m, output.Number("دفعات تحت الحساب", "N22"));
        Assert.Equal(180m, output.Number("توريدات الموردين", "B2"));
        Assert.Equal(110m, output.Number("مدفوعات الموردين", "B2"));
        Assert.Equal("ملاحظات ", output.Value("خاص باعمال الموقع", "K9"));
        Assert.Equal("s", (string?)output.Cell("خاص باعمال الموقع", "K9")?.Attribute("t"));
        Assert.Contains("A9:K51", output.Tables.Select(table => (string?)table.Attribute("ref")));
        AssertTemplateStyle(output, "ProjectReportTemplate.xlsx", "Reports", "خاص باعمال الموقع", 4);
    }

    [Fact]
    public void Consolidated_SeparateSnapshotsAndNoApprovedHOrT()
    {
        using var output = Export(Example(twoProjects: true), "consolidated", "dynamic-consolidated.xlsx");
        Assert.Equal("مسلسل", output.Value("المجمع الحالي", "E6"));
        Assert.Equal("1", output.Value("المجمع الحالي", "E7"));
        Assert.Equal("تشخيصي", output.Value("المجمع الحالي", "H7"));
        Assert.Equal("غير معتمد", output.Value("المجمع الحالي", "T7"));
        Assert.Equal(120m, output.Number("المجمع الحالي", "J7"));
        Assert.Equal(200m, output.Number("المجمع الحالي", "K7"));
        Assert.Equal(30m, output.Number("المجمع الحالي", "R7"));
        Assert.Equal(1000m, output.Number("المجمع الحالي", "S7"));
        Assert.Equal(60m, output.Number("المجمع المستورد", "G7"));
        Assert.Equal(100m, output.Number("المجمع الحالي", "G7"));
        Assert.Equal("QA-002 · المشروع الثاني", output.Value("المجمع الحالي", "F8"));
        Assert.Equal(25m, output.Number("المجمع الحالي", "G8"));
        Assert.Contains("F6:U30", output.Tables.Select(table => (string?)table.Attribute("ref")));
        AssertTemplateStyle(output, "ConsolidatedReportLayout.zip", "ReportBuilder", "المجمع الحالي", 1);
    }

    [Fact]
    public void Custom_ColumnOrderTypedValuesAndUnresolved()
    {
        using var output = Export(Example(), "custom", "dynamic-custom.xlsx");
        Assert.Equal(new[] { "إجماليات المعاينة", "الحركات", "ملاحظات التصدير" }, output.Names);
        Assert.Equal("مرجع المصدر", output.Value("الحركات", "A1"));
        Assert.Equal("الحالي", output.Value("الحركات", "B1"));
        Assert.Equal("المصدر", output.Value("الحركات", "H1"));
        Assert.Null(output.Cell("الحركات", "I1"));
        Assert.Equal("=1+1 ", output.Value("الحركات", "A2"));
        Assert.Equal("inlineStr", (string?)output.Cell("الحركات", "A2")?.Attribute("t"));
        Assert.Equal("الموقع / 10", output.Value("الحركات", "H2"));
        Assert.Equal((decimal)January.ToOADate(), output.Number("الحركات", "G2"));
        var unresolved = Array.FindIndex(Example().Summary.ToArray(), row => row.GroupKey == "Unresolved") + 2;
        Assert.Equal(35m, output.Number("إجماليات المعاينة", $"D{unresolved}"));
        Assert.Equal("مراجعة", output.Value("إجماليات المعاينة", $"G{unresolved}"));
    }

    [Fact]
    public void Detailed_AtCapacityPreservesLastDetailsAndSubtotals()
    {
        using var output = Export(Example(atCapacity: true), "detailed", "dynamic-near-capacity.xlsx");
        Assert.Equal(7m, output.Number("اعمال الحفر", "J29"));
        Assert.Equal(147m, output.Number("اعمال الحفر", "J7"));
        Assert.Equal(19m, output.Number("اعمال توريد الحديد", "F51"));
        Assert.Equal(855m, output.Number("اعمال توريد الحديد", "F52"));
        Assert.Equal(13m, output.Number("اعمال الخرسانات", "H29"));
        Assert.Equal(17m, output.Number("اعمال الخرسانات", "U29"));
        Assert.Equal(260m, output.Number("اعمال الخرسانات", "G31"));
        Assert.Equal(340m, output.Number("اعمال الخرسانات", "U30"));
        Assert.Equal(9m, output.Number("خاص باعمال الموقع", "J51"));
        Assert.Equal(378m, output.Number("خاص باعمال الموقع", "K7"));
        Assert.Equal(7m, output.Number("اعمال العزل", "J45"));
        Assert.Equal(252m, output.Number("اعمال العزل", "K7"));
        Assert.Equal(3m, output.Number("دفعات تحت الحساب", "N17"));
        Assert.Equal(42m, output.Number("دفعات تحت الحساب", "N22"));
        Assert.Equal(342, output.SheetXml("الحركات").Descendants(Sheet + "row").Count() - 1);
    }

    [Fact]
    public void EmptyAndSelectedDetailLevelAreRepresented()
    {
        var empty = new ReportBuilderPreview(January, new(DetailLevel: "transactions"), ["project"], [], [], [], true);
        using var output = Export(empty, "custom");
        Assert.Equal(new[] { "الحركات", "ملاحظات التصدير" }, output.Names);
        Assert.Empty(output.SheetXml("الحركات").Elements(Sheet + "tableParts"));
        Assert.Equal("لا توجد سجلات مطابقة", output.Value("الحركات", "A2"));
        var report = Example();
        using var totals = Export(report with { Query = new(DetailLevel: "totals", Snapshot: "imported"), Transactions = [] }, "consolidated");
        Assert.DoesNotContain("الحركات", totals.Names);
        Assert.DoesNotContain("المجمع الحالي", totals.Names);
        Assert.Contains("المجمع المستورد", totals.Names);
    }

    [Fact]
    public void MultipleDetailedProjectsKeepIndependentTables()
    {
        using var output = Export(Example(twoProjects: true), "detailed");
        Assert.Equal(14, output.Tables.Count(table => ((string?)table.Attribute("ref")) is "A9:K51" or "D8:K29" or "A9:K45"));
        Assert.Equal(100m, output.Number("1 خاص باعمال الموقع", "K7"));
        Assert.Equal(25m, output.Number("2 خاص باعمال الموقع", "K7"));
    }

    [Fact]
    public async Task Export_ReturnsStructuredCapacityErrorWithNoWorkbook()
    {
        var fixture = Example(atCapacity: true);
        var excess = fixture.Transactions.First(row => row.Category == "Steel") with { EntityId = 9000 };
        var controller = Controller(new FixtureService(fixture with { Transactions = [.. fixture.Transactions, excess] }), Principal(1));
        var outcome = Assert.IsType<UnprocessableEntityObjectResult>(
            await controller.ExportExcel(new(fixture.Query, "detailed"), CancellationToken.None));
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(outcome.Value));
        Assert.Equal("XLSX_EXPORT_CAPACITY_EXCEEDED", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(46, body.RootElement.GetProperty("actual").GetInt32());
        Assert.Equal(45, body.RootElement.GetProperty("capacity").GetInt32());
    }

    [Fact]
    public async Task Export_MatchesRealFilteredPreviewAndExcludesUnassignedProject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var visible = new Project { Name = "Authorized QA", Code = "QA-001" };
        var hidden = new Project { Name = "Forbidden QA", Code = "QA-002" };
        var supplier = new Supplier { Name = "Supplier A" };
        var other = new Supplier { Name = "Supplier B" };
        var user = new AppUser { UserName = "qa", Email = "qa@example.test" };
        db.AddRange(visible, hidden, supplier, other, user);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = user.Id, ProjectId = visible.Id });
        db.SupplierSupplies.AddRange(
            Supply(visible, supplier, 250, January, "استيراد: توريدات صف 22"),
            Supply(visible, supplier, 125, January.AddDays(1), "current-QA"),
            Supply(visible, supplier, 800, January.AddDays(-1), "outside-date-QA"),
            Supply(visible, other, 900, January, "other-party-QA"),
            Supply(hidden, supplier, 999, January, "secret-source-QA"));
        await db.SaveChangesAsync();
        var service = new ReportBuilderService(db, new AccountingClassificationService(db));
        var controller = Controller(service, Principal(user.Id));
        var query = new ReportBuilderQuery(ProjectIds: [visible.Id, hidden.Id], FromDate: January, ToDate: January.AddDays(1),
            Categories: ["SupplierSupply"], Parties: ["Supplier A"], Dimensions: ["supplier-supply"],
            GroupBy: ["project", "party", "dimension"], Columns: ["source", "currentAmount", "importedAmount", "project"], SortBy: "date");
        var previewResult = await controller.Preview(query, CancellationToken.None);
        var preview = Assert.IsType<ReportBuilderPreview>(Assert.IsType<OkObjectResult>(previewResult.Result).Value);
        var file = Assert.IsType<FileContentResult>(await controller.ExportExcel(new(query), CancellationToken.None));
        using var output = new Workbook(file.FileContents);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.Equal(375m, output.Number("إجماليات المعاينة", "C2"));
        Assert.Equal(preview.Summary.Single().CurrentAmount, output.Number("إجماليات المعاينة", "C2"));
        Assert.Equal(250m, output.Number("إجماليات المعاينة", "B2"));
        Assert.Equal(250m, output.Number("الحركات", "B2"));
        Assert.Equal(125m, output.Number("الحركات", "B3"));
        Assert.Equal("توريدات / 22", output.Value("الحركات", "A2"));
        Assert.Null(output.Value("الحركات", "B4"));
        Assert.DoesNotContain(output.AllText(), text => text.Contains("Forbidden QA") || text.Contains("secret-source-QA"));
        Assert.Equal(5, await db.SupplierSupplies.CountAsync());

        var historical = Assert.IsType<FileContentResult>(await controller.ExportExcel(new(query with { Snapshot = "imported" }), CancellationToken.None));
        using var imported = new Workbook(historical.FileContents);
        Assert.Equal(250m, imported.Number("إجماليات المعاينة", "C2"));
        Assert.Null(imported.Value("الحركات", "B3"));
    }

    [Fact]
    public async Task Export_RequiresExistingReportsExportPolicy()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var user = new AppUser { UserName = "qa", Email = "qa@example.test" };
        var role = new Role { Name = "qa-reporter" };
        db.Add(new UserRole { User = user, Role = role });
        db.Add(new RolePermission { Role = role, Permission = new Permission { Code = "reports.view" } });
        await db.SaveChangesAsync();
        var method = typeof(ReportBuilderController).GetMethod(nameof(ReportBuilderController.ExportExcel))!;
        var data = typeof(ReportBuilderController).GetCustomAttributes(true).Concat(method.GetCustomAttributes(true)).OfType<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(new PermissionPolicyProvider(Options.Create(new AuthorizationOptions())), data);
        Assert.NotNull(policy);
        Assert.Equal("reports.export", Assert.Single(policy.Requirements.OfType<PermissionRequirement>()).PermissionCode);
        var handler = new PermissionAuthorizationHandler(db);
        var withoutExport = new AuthorizationHandlerContext(policy.Requirements, Principal(user.Id), null);
        await handler.HandleAsync(withoutExport);
        Assert.False(withoutExport.HasSucceeded);
        Assert.Single(withoutExport.PendingRequirements.OfType<PermissionRequirement>());
        db.Add(new RolePermission { Role = role, Permission = new Permission { Code = "reports.export" } });
        await db.SaveChangesAsync();
        var granted = new AuthorizationHandlerContext(policy.Requirements.OfType<PermissionRequirement>(), Principal(user.Id), null);
        await handler.HandleAsync(granted);
        Assert.True(granted.HasSucceeded);
    }

    [Fact]
    public void UnsafeExcelValuesFailClosed()
    {
        var report = Example();
        var rows = report.Transactions.ToArray();
        rows[0] = rows[0] with { CurrentAmount = 123456789012345.67m };
        Assert.Equal("XLSX_EXPORT_UNSUPPORTED_VALUE",
            Assert.Throws<ReportWorkbookException>(() => ReportBuilderXlsxWriter.Build(report with { Transactions = rows }, "custom")).Code);
        rows[0] = rows[0] with { CurrentAmount = 60, SourceText = new string('a', 32768) };
        Assert.Equal("XLSX_EXPORT_UNSUPPORTED_VALUE",
            Assert.Throws<ReportWorkbookException>(() => ReportBuilderXlsxWriter.Build(report with { Transactions = rows }, "custom")).Code);
    }

    private static SupplierSupply Supply(Project project, Supplier supplier, decimal amount, DateTime date, string notes) => new()
    {
        ProjectId = project.Id, SupplierId = supplier.Id, TotalAmount = amount, SupplyDate = date, Notes = notes, Status = "معتمد"
    };

    private static EmgDbContext CreateDb(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options);

    private static ClaimsPrincipal Principal(int userId) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "qa"));

    private static ReportBuilderController Controller(IReportBuilderService service, ClaimsPrincipal principal) =>
        new(service, NullLogger<ReportBuilderController>.Instance) { ControllerContext = new ControllerContext
            { HttpContext = new DefaultHttpContext { User = principal } } };

    private static ReportBuilderPreview Example(bool atCapacity = false, bool twoProjects = false)
    {
        var rows = new List<ReportBuilderTransactionRow>();
        void Add(string category, decimal amount, decimal imported = 0, int project = 1, string dimension = "operational-cost")
        {
            var id = rows.Count + 1;
            rows.Add(new ReportBuilderTransactionRow(project, $"QA-00{project}", project == 1 ? "مشروع التحقق" : "المشروع الثاني",
                "QA", id, category, dimension, "طرف تجريبي", January, imported, amount, category == "Unresolved" ? amount : 0,
                "معتمد", id == 1 ? "=1+1 " : "مرجع تجريبي", "الموقع", id + 9, category == "Unresolved", "ملاحظة اختبار")
            { IncludedInFinancialTotals = true });
        }
        void Repeat(string category, int count, int amount)
        {
            for (var i = 0; i < count; i++) Add(category, amount);
        }
        if (atCapacity)
        {
            Repeat("SiteExpense", 42, 9);
            Repeat("Excavation", 21, 7);
            Repeat("Steel", 45, 19);
            Repeat("ConcreteStation", 20, 13);
            Repeat("ContractorWorkmanship", 20, 17);
            Repeat("OwnerFunding", 14, 3);
            foreach (var category in new[] { "Buildings", "Plaster", "Insulation", "Electrical", "Plumbing" })
                Repeat(category, 36, 7);
        }
        else
        {
            Add("SiteExpense", 60, 60);
            Add("SiteExpense", 40);
            Add("Excavation", 50, 50);
            Add("ContractorWorkmanship", 120, 120);
            Add("ConcreteStation", 200, 200);
            Add("Steel", 75, 75);
            Add("Buildings", 22, 22);
            Add("Insulation", 66, 66);
            Add("OwnerFunding", 1000, 900, dimension: "owner-funding");
            Add("FundingAllocation", 400, 300, dimension: "advance-allocation");
            Add("OfficePayment", 30, 20, dimension: "office-payment");
            Add("SupplierSupply", 180, 150, dimension: "supplier-supply");
            Add("SupplierPayment", 110, 80, dimension: "supplier-payment");
            Add("Unresolved", 35, 35, dimension: "unresolved");
            if (twoProjects) Add("SiteExpense", 25, project: 2);
        }
        var summary = rows.GroupBy(row => row.Category).Select(group => new ReportBuilderSummaryRow(group.Key, group.Key,
            group.Sum(row => row.ImportedAmount), group.Sum(row => row.CurrentAmount), group.Sum(row => row.UnresolvedAmount),
            group.Count(), group.Count(row => row.IsUnresolved), !group.Any(row => row.IsUnresolved))).ToArray();
        var projects = rows.GroupBy(row => (row.ProjectId, row.ProjectCode, row.ProjectName)).Select(group =>
            new ReportBuilderProjectAmounts(group.Key.ProjectId, group.Key.ProjectCode, group.Key.ProjectName,
                group.GroupBy(row => row.Category).ToDictionary(category => category.Key,
                    category => new ReportBuilderAmounts(category.Sum(row => row.ImportedAmount), category.Sum(row => row.CurrentAmount))))).ToArray();
        return new ReportBuilderPreview(January, new(Columns: Columns, GroupBy: ["category"]), Columns, summary, rows,
            ["H تشخيصي", "T غير معتمد"], atCapacity) { Projects = projects };
    }

    private static Workbook Export(ReportBuilderPreview report, string presentation, string? name = null)
    {
        var bytes = ReportBuilderXlsxWriter.Build(report, presentation);
        var directory = Environment.GetEnvironmentVariable("EMG_REPORT_BUILDER_QA");
        if (name is not null && Directory.Exists(directory)) File.WriteAllBytes(Path.Combine(directory!, name), bytes);
        return new Workbook(bytes);
    }

    private static void AssertTemplateStyle(Workbook book, string file, string module, string sheetName, int number)
    {
        using var template = ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, "Modules", module, "Templates", file));
        using var styles = template.GetEntry("xl/styles.xml")!.Open();
        using var theme = template.GetEntry("xl/theme/theme1.xml")!.Open();
        using var source = template.GetEntry($"xl/worksheets/sheet{number}.xml")!.Open();
        Assert.True(XNode.DeepEquals(XDocument.Load(styles), Workbook.Xml(book.Part.WorkbookStylesPart!)));
        Assert.True(XNode.DeepEquals(XDocument.Load(theme), Workbook.Xml(book.Part.ThemePart!)));
        var layout = XDocument.Load(source).Root!;
        var destination = book.SheetXml(sheetName);
        foreach (var name in new[] { "cols", "mergeCells" })
            Assert.True(XNode.DeepEquals(layout.Element(Sheet + name), destination.Element(Sheet + name)), name);
        Assert.True(XNode.DeepEquals(layout.Element(Sheet + "sheetPr")?.Element(Sheet + "tabColor"),
            destination.Element(Sheet + "sheetPr")?.Element(Sheet + "tabColor")));
    }

    private sealed class FixtureService(ReportBuilderPreview report) : IReportBuilderService
    {
        public Task<ReportBuilderPreview> PreviewAsync(ReportBuilderQuery query, int? userId, bool isGlobalAccess,
            CancellationToken cancellationToken, bool includeProjectAmounts = false) => Task.FromResult(report);
    }

    private sealed class Workbook : IDisposable
    {
        private readonly SpreadsheetDocument document;
        private readonly Dictionary<string, XElement> sheets;
        private readonly string[] shared;
        public Workbook(byte[] bytes)
        {
            document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
            Part = document.WorkbookPart!;
            sheets = Part.Workbook.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().ToDictionary(
                item => item.Name!.Value!, item => Xml(Part.GetPartById(item.Id!.Value!)).Root!);
            shared = Xml(Part.SharedStringTablePart!).Root!.Elements().Select(ReadText).ToArray();
            var tables = new List<XElement>();
            foreach (var entry in Part.Workbook.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>())
            {
                var worksheet = (WorksheetPart)Part.GetPartById(entry.Id!.Value!);
                foreach (var tablePart in worksheet.TableDefinitionParts)
                {
                    var table = Xml(tablePart).Root!;
                    var reference = (string)table.Attribute("ref")!;
                    var first = reference.Split(':')[0];
                    var columnLetters = first.TakeWhile(char.IsLetter).ToArray();
                    var column = columnLetters.Aggregate(0, (index, letter) => index * 26 + letter - 'A' + 1);
                    var row = first[columnLetters.Length..];
                    foreach (var heading in table.Element(Sheet + "tableColumns")!.Elements())
                        Assert.Equal((string?)heading.Attribute("name"), Value(entry.Name!.Value!, $"{ReportWorksheet.Column(column++)}{row}"));
                    tables.Add(table);
                }
            }
            Tables = tables.ToArray();
            using var zip = new ZipArchive(new MemoryStream(bytes));
            Assert.DoesNotContain(zip.Entries, entry => entry.FullName.Contains("externalLink") || entry.FullName.Contains("calcChain"));
            Assert.All(sheets.Values, worksheet =>
            {
                Assert.Empty(worksheet.Descendants(Sheet + "f"));
                Assert.Equal("1", (string?)worksheet.Descendants(Sheet + "sheetView").Single().Attribute("rightToLeft"));
                foreach (var cell in worksheet.Descendants(Sheet + "c").Where(cell => cell.Attribute("t") is null && cell.Element(Sheet + "v") is not null))
                    Assert.True(decimal.TryParse(cell.Element(Sheet + "v")!.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
            });
            Assert.Equal(Tables.Length, Tables.Select(table => (string?)table.Attribute("name")).Distinct().Count());
            Assert.All(Tables, table => Assert.Equal((string?)table.Attribute("ref"), (string?)table.Element(Sheet + "autoFilter")?.Attribute("ref")));
            var errors = new OpenXmlValidator(FileFormatVersions.Office2013).Validate(document)
                .Select(error => $"{error.Part?.Uri} {error.Path?.XPath}: {error.Description}").Take(10).ToArray();
            Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors));
        }
        public WorkbookPart Part { get; }
        public XElement[] Tables { get; }
        public string[] Names => sheets.Keys.ToArray();
        public XElement SheetXml(string name) => sheets[name];
        public static XDocument Xml(OpenXmlPart part)
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            return XDocument.Load(stream);
        }
        public IEnumerable<string> AllText() => shared.Concat(sheets.Values.SelectMany(sheet => sheet.Descendants(Sheet + "is")).Select(ReadText));
        public XElement? Cell(string sheet, string address) => sheets[sheet].Descendants(Sheet + "c").SingleOrDefault(cell => (string?)cell.Attribute("r") == address);
        public decimal Number(string sheet, string address) => decimal.Parse(Value(sheet, address)!, NumberStyles.Float, CultureInfo.InvariantCulture);
        public string? Value(string sheet, string address)
        {
            var cell = Cell(sheet, address);
            var value = (string?)cell?.Element(Sheet + "v");
            return (string?)cell?.Attribute("t") switch
            {
                "s" => shared[int.Parse(value!, CultureInfo.InvariantCulture)],
                "inlineStr" => ReadText(cell!),
                _ => value
            };
        }
        private static string ReadText(XElement node) => string.Concat(node.Descendants(Sheet + "t").Select(text => text.Value));
        public void Dispose() => document.Dispose();
    }
}
