using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using EMG.Api.Modules.ReportBuilder.Dtos;
using static EMG.Api.Modules.ReportBuilder.Services.ReportWorkbookTemplate;
using static EMG.Api.Modules.ReportBuilder.Services.ReportWorksheet;

namespace EMG.Api.Modules.ReportBuilder.Services;

internal static class ReportBuilderXlsxWriter
{
    private const int ProjectionRowLimit = 30000;
    private const int DetailedProjectLimit = 40;
    private const int MasterRowsPerSheet = 24;
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly IReadOnlyDictionary<string, ExportColumn> Columns = new Dictionary<string, ExportColumn>(StringComparer.OrdinalIgnoreCase)
    {
        ["project"] = new("المشروع", 34, row => $"{row.ProjectCode} · {row.ProjectName}"),
        ["category"] = new("التصنيف", 26, row => row.Category),
        ["dimension"] = new("البعد المالي", 26, row => row.Dimension),
        ["party"] = new("الطرف", 30, row => row.Party),
        ["date"] = new("التاريخ", 17, row => row.TransactionDate),
        ["importedAmount"] = new("المستورد", 18, row => row.ImportedAmount),
        ["currentAmount"] = new("الحالي", 18, row => row.CurrentAmount),
        ["unresolvedAmount"] = new("غير محسوم", 18, row => row.UnresolvedAmount),
        ["source"] = new("المصدر", 38, row => row.SourceRow is { } n ? $"{row.SourceSheet} / {n}" : row.SourceSheet),
        ["sourceText"] = new("مرجع المصدر", 46, row => row.SourceText),
        ["diagnostic"] = new("ملاحظات المراجعة", 42, row => row.Diagnostic),
        ["status"] = new("حالة الحركة", 18, row => row.Status)
    };

    private static readonly (string Column, string Category)[] MasterCategories =
    [
        ("G", "SiteExpense"), ("I", "Excavation"), ("J", "ContractorWorkmanship"), ("K", "ConcreteStation"),
        ("L", "Steel"), ("M", "Buildings"), ("N", "Plaster"), ("O", "Insulation"), ("P", "Electrical"), ("Q", "Plumbing"),
        ("R", "OfficePayment"), ("S", "OwnerFunding")
    ];

    internal static bool Supports(string? presentation) => presentation is "custom" or "detailed" or "consolidated";

    internal static byte[] Build(ReportBuilderPreview report, string presentation, CancellationToken cancellationToken = default)
    {
        EnsureCapacity("صفوف الحركات", report.Transactions.Count, ProjectionRowLimit);
        EnsureCapacity("صفوف الإجماليات", report.Summary.Count, ProjectionRowLimit);

        var selectedColumns = report.VisibleColumns.Select(key =>
            key is not null && Columns.TryGetValue(key, out var column)
                ? column
                : throw ReportWorkbookException.Unsupported("أحد أعمدة التقرير غير مدعوم في Excel.")).ToArray();
        if (selectedColumns.Select(column => column.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() != selectedColumns.Length ||
            selectedColumns.Length == 0)
            throw ReportWorkbookException.Unsupported("اختر أعمدة تصدير غير مكررة.");

        using var template = new ReportWorkbookTemplate(presentation == "consolidated");
        var styles = template.IsMaster ? new GridStyles(9, 4, 15, 11) : new GridStyles(12, 56, 84, 55);
        var showSummary = !report.Query.DetailLevel.Equals("transactions", StringComparison.OrdinalIgnoreCase);
        var showTransactions = !report.Query.DetailLevel.Equals("totals", StringComparison.OrdinalIgnoreCase);
        var sheets = new List<ReportWorksheet>();
        if (presentation == "detailed")
        {
            EnsureCapacity("المشاريع في العرض التفصيلي", report.Projects.Count, DetailedProjectLimit);
            var byProject = report.Transactions.Where(row => row.IncludedInFinancialTotals).ToLookup(row => row.ProjectId);
            var projects = report.Projects.Count > 0 ? report.Projects : [new ReportBuilderProjectAmounts(0, "", "", new Dictionary<string, ReportBuilderAmounts>())];
            for (var i = 0; i < projects.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var prefix = projects.Count == 1 ? "" : $"{i + 1} ";
                var project = projects[i];
                sheets.AddRange(DetailedSheets(template, project, byProject[project.ProjectId], prefix, report, showSummary, styles, selectedColumns));
            }
        }
        else if (presentation == "consolidated" && showSummary)
        {
            sheets.AddRange(MasterSheets(template, report));
        }

        if (showSummary)
        {
            sheets.Add(Grid("إجماليات المعاينة",
                ["المجموعة", "المستورد", "الحالي", "غير محسوم", "عدد السجلات", "عدد غير المحسوم", "الحالة"],
                [52, 20, 20, 20, 19, 23, 16],
                report.Summary.Count,
                i => new object?[] { report.Summary[i].GroupLabel, report.Summary[i].ImportedAmount, report.Summary[i].CurrentAmount,
                    report.Summary[i].UnresolvedAmount, report.Summary[i].RecordCount, report.Summary[i].UnresolvedCount,
                    report.Summary[i].IsComplete ? "سليم" : "مراجعة" },
                styles));
        }
        if (showTransactions)
            sheets.Add(TransactionGrid("الحركات", report.Transactions, selectedColumns, styles));

        var warnings = new List<string>(report.Warnings)
        {
            "إجماليات المعاينة تتبع التجميع المختار. تجميع الأبعاد المختلفة لا يمثل رصيدًا.",
            "المستورد جزء من المعتمد والمرحّل الحالي؛ لا تُجمع اللقطتان معًا."
        };
        if (presentation != "custom")
            warnings.Add("أوراق القالب تعرض الحقول المتاحة في مواضعها الأصلية. ورقة الحركات تتبع ترتيب الأعمدة المختار.");
        if (presentation == "detailed")
            warnings.Add("عند اختيار اللقطتين، تعرض أوراق المشروع القيم الحالية. الكميات والأسعار والأرصدة غير المتاحة تظل فارغة. التوزيعات منفصلة عن تمويل المالك.");
        if (report.Summary.Count == 0 && report.Transactions.Count == 0)
            warnings.Add("لا توجد سجلات مطابقة للفلاتر وصلاحيات الوصول.");
        sheets.Add(Grid("ملاحظات التصدير", ["الملاحظة"], [102], warnings.Count, i => [warnings[i]], styles, height: 34));
        EnsureCapacity("أوراق المصنف", sheets.Count, 512);
        return Pack(template, sheets, cancellationToken);
    }

    private static IEnumerable<ReportWorksheet> MasterSheets(ReportWorkbookTemplate template, ReportBuilderPreview report)
    {
        var snapshots = report.Query.Snapshot.ToLowerInvariant() switch
        {
            "imported" => new[] { true },
            "current" => [false],
            _ => [false, true]
        };
        foreach (var imported in snapshots)
        {
            var pages = report.Projects.Chunk(MasterRowsPerSheet).ToArray();
            if (pages.Length == 0) pages = [[]];
            for (var page = 0; page < pages.Length; page++)
            {
                var label = imported ? "المجمع المستورد" : "المجمع الحالي";
                var sheet = template.Sheet(1, pages.Length > 1 ? $"{label} {page + 1}" : label);
                sheet.Set("A1", label);
                sheet.Set("F4", label);
                sheet.Set("E6", "مسلسل", shared: true);
                for (var i = 0; i < pages[page].Length; i++)
                {
                    var project = pages[page][i];
                    var row = 7 + i;
                    sheet.Set($"E{row}", page * MasterRowsPerSheet + i + 1);
                    sheet.Set($"F{row}", ProjectLabel(project));
                    foreach (var (column, category) in MasterCategories)
                        sheet.Set($"{column}{row}", Amount(project, category, imported));
                    sheet.Set($"H{row}", "تشخيصي");
                    sheet.Set($"T{row}", "غير معتمد");
                    sheet.Set($"U{row}", project.ByCategory.ContainsKey("Unresolved") ? "تصنيفات تحتاج مراجعة" : "H متداخل");
                }
                if (pages[page].Length == 0) sheet.Set("F7", "لا توجد سجلات مطابقة");
                yield return sheet;
            }
        }
    }

    private static IEnumerable<ReportWorksheet> DetailedSheets(
        ReportWorkbookTemplate template, ReportBuilderProjectAmounts project, IEnumerable<ReportBuilderTransactionRow> transactions,
        string prefix, ReportBuilderPreview report, bool showSummary, GridStyles styles, ExportColumn[] columns)
    {
        var imported = report.Query.Snapshot.Equals("imported", StringComparison.OrdinalIgnoreCase);
        var byCategory = transactions.ToLookup(row => row.Category, StringComparer.Ordinal);
        var label = ProjectLabel(project);
        var hasDate = report.VisibleColumns.Contains("date", StringComparer.OrdinalIgnoreCase);
        var hasParty = report.VisibleColumns.Contains("party", StringComparer.OrdinalIgnoreCase);
        var hasSource = report.VisibleColumns.Contains("source", StringComparer.OrdinalIgnoreCase);
        var hasSourceText = report.VisibleColumns.Contains("sourceText", StringComparer.OrdinalIgnoreCase);
        string? Description(ReportBuilderTransactionRow row)
        {
            if (hasSourceText && !string.IsNullOrWhiteSpace(row.SourceText)) return row.SourceText;
            if (hasSource && !string.IsNullOrWhiteSpace(row.SourceSheet))
                return row.SourceRow is int number ? $"{row.SourceSheet} / {number}" : row.SourceSheet;
            return null;
        }
        decimal Value(ReportBuilderTransactionRow row) => imported ? row.ImportedAmount : row.CurrentAmount;
        decimal? Total(string category) => showSummary ? Amount(project, category, imported) : null;
        ReportWorksheet Sheet(int index, string name, string titleCell, string? projectCell = null)
        {
            var fullName = prefix + name;
            var sheet = template.Sheet(index, fullName.Length > 31 ? fullName[..31] : fullName);
            sheet.Set(titleCell, name);
            if (projectCell is not null) sheet.Set(projectCell, label);
            return sheet;
        }
        void Labels(ReportWorksheet sheet, params string[] addresses)
        {
            foreach (var address in addresses) template.CopyLabel(sheet, address);
        }
        void Fill(ReportWorksheet sheet, string category, int first, int capacity, string? number, string? date, string? text, string? party, string amount)
        {
            var rows = byCategory[category].ToArray();
            EnsureCapacity(sheet.Name, rows.Length, capacity);
            for (var i = 0; i < rows.Length; i++)
            {
                var line = rows[i];
                var row = first + i;
                if (number is not null) sheet.Set($"{number}{row}", i + 1);
                if (date is not null && hasDate) sheet.Set($"{date}{row}", line.TransactionDate, styles.Date);
                if (text is not null) sheet.Set($"{text}{row}", Description(line));
                if (party is not null && hasParty) sheet.Set($"{party}{row}", line.Party);
                sheet.Set($"{amount}{row}", Value(line));
            }
        }

        var excavation = Sheet(1, "اعمال الحفر", "D6", "E3");
        excavation.Set("J4", "حساب الحفر");
        excavation.Set("J5", "إجمالي أعمال الحفر");
        Fill(excavation, "Excavation", 9, 21, "D", "E", "F", null, "J");
        excavation.Set("J7", Total("Excavation"));
        yield return excavation;

        var steel = Sheet(2, "اعمال توريد الحديد", "A2", "A4");
        Labels(steel, "A6", "B6", "C6", "D6", "E6", "F6", "G6", "H6", "A52");
        Fill(steel, "Steel", 7, 45, "A", "B", "G", null, "F");
        steel.Set("F52", Total("Steel"));
        yield return steel;

        var concrete = Sheet(3, "اعمال الخرسانات", "E2", "E4");
        Labels(concrete, "A7", "B7", "C7", "D7", "D8", "E8", "G8", "H8", "I8", "J7", "M7", "N7", "O7",
            "P7", "P8", "Q8", "R8", "S7", "T7", "U7", "V7", "W7", "C30", "E30", "G30", "M30", "M31");
        Fill(concrete, "ConcreteStation", 10, 20, "A", "B", "I", "J", "H");
        Fill(concrete, "ContractorWorkmanship", 10, 20, "M", null, "N", null, "U");
        concrete.Set("G31", Total("ConcreteStation"));
        concrete.Set("U30", Total("ContractorWorkmanship"));
        yield return concrete;

        var site = Sheet(4, "خاص باعمال الموقع", "A6", "A5");
        Labels(site, "J5", "J6", "J7", "J8");
        Fill(site, "SiteExpense", 10, 42, "A", "B", "C", "D", "J");
        site.Set("K5", Total("ContractorWorkmanship"));
        site.Set("K7", Total("SiteExpense"));
        yield return site;

        var advances = Sheet(5, "دفعات تحت الحساب", "A1", "A2");
        Labels(advances, "A3", "B3", "C3", "D3", "E3", "F3", "G3", "H3", "I3", "J3", "K3", "L3", "M3", "N3", "O3", "A19",
            "B20", "C20", "D20", "E20", "F20", "G20", "H20", "I20", "J20", "K20", "L20");
        Fill(advances, "OwnerFunding", 4, 14, null, "O", null, null, "N");
        advances.Set("N22", Total("OwnerFunding"));
        yield return advances;

        var account = Sheet(6, "كشف حساب نهائي", "D5", "D6");
        Labels(account, "D8", "E8", "F8", "G8", "H8", "I8", "J8", "K8", "D9", "D10", "D12", "G12", "H12", "I12");
        account.Set("E9", Total("SiteExpense"));
        account.Set("F9", Total("Excavation"));
        account.Set("G9", Total("ContractorWorkmanship"));
        account.Set("H9", Total("ConcreteStation"));
        account.Set("I9", Total("Steel"));
        account.Set("K10", Total("OwnerFunding"));
        account.Set("F12", Total("OfficePayment"));
        yield return account;

        var finishings = new[] { ("اعمال المباني", "Buildings"), ("اعمال المحاره", "Plaster"), ("اعمال العزل", "Insulation"),
            ("اعمال الكهرباء", "Electrical"), ("اعمال السباكه", "Plumbing") };
        for (var i = 0; i < finishings.Length; i++)
        {
            var (name, category) = finishings[i];
            var sheet = Sheet(i + 7, name, "C2", "A5");
            Labels(sheet, "A6", "J6", "J7", "J8");
            Fill(sheet, category, 10, 36, "A", "B", "C", "D", "J");
            sheet.Set("K7", Total(category));
            yield return sheet;
        }
        foreach (var (name, category) in new[] { ("توريدات الموردين", "SupplierSupply"), ("مدفوعات الموردين", "SupplierPayment") })
        {
            var rows = byCategory[category].ToArray();
            if (rows.Length > 0) yield return TransactionGrid(prefix + name, rows, columns, styles);
        }
    }

    private static string ProjectLabel(ReportBuilderProjectAmounts project) =>
        project.ProjectId == 0 ? "لا توجد سجلات مطابقة" : $"{project.ProjectCode} · {project.ProjectName}";

    private static decimal? Amount(ReportBuilderProjectAmounts project, string category, bool imported) =>
        project.ByCategory.TryGetValue(category, out var amount) ? imported ? amount.Imported : amount.Current : null;

    private static ReportWorksheet TransactionGrid(string name, IReadOnlyList<ReportBuilderTransactionRow> rows, ExportColumn[] columns, GridStyles styles) =>
        Grid(name, columns.Select(column => column.Label).ToArray(), columns.Select(column => column.Width).ToArray(),
            rows.Count, i => columns.Select(column => column.Value(rows[i])).ToArray(), styles);

    private static ReportWorksheet Grid(string name, string[] headings, double[] widths, int count, Func<int, object?[]> row, GridStyles styles, double? height = null)
    {
        var sheet = new ReportWorksheet(name, widths: widths);
        sheet.Row(1, 38);
        for (var i = 0; i < headings.Length; i++) sheet.Set($"{Column(i + 1)}1", headings[i], styles.Header, shared: true);
        if (count == 0) sheet.Set("A2", "لا توجد سجلات مطابقة", styles.Text);
        else
        {
            sheet.AddTable(new ReportExcelTable($"A1:{Column(headings.Length)}{count + 1}", headings,
                new XElement(SheetNamespace + "tableStyleInfo", new XAttribute("showRowStripes", 0))));
            for (var i = 0; i < count; i++)
            {
                sheet.Row(i + 2, height);
                var values = row(i);
                for (var j = 0; j < values.Length; j++)
                {
                    var style = values[j] switch { DateTime => styles.Date, decimal => styles.Amount, _ => styles.Text };
                    sheet.Set($"{Column(j + 1)}{i + 2}", values[j], style);
                }
            }
        }
        return sheet;
    }

    private static byte[] Pack(ReportWorkbookTemplate template, List<ReportWorksheet> sheets, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        using (var package = SpreadsheetDocument.Create(buffer, SpreadsheetDocumentType.Workbook))
        {
            var workbook = package.AddWorkbookPart();
            using (var styles = template.OpenStyles()) workbook.AddNewPart<WorkbookStylesPart>().FeedData(styles);
            using (var theme = template.OpenTheme()) workbook.AddNewPart<ThemePart>().FeedData(theme);
            var shared = workbook.AddNewPart<SharedStringTablePart>();
            var strings = new Dictionary<string, int>(StringComparer.Ordinal);
            int Index(string text)
            {
                if (!strings.TryGetValue(text, out var index)) strings.Add(text, index = strings.Count);
                return index;
            }
            var sheetItems = new XElement(SheetNamespace + "sheets");
            var id = 0;
            foreach (var sheet in sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var part = workbook.AddNewPart<WorksheetPart>();
                sheetItems.Add(new XElement(SheetNamespace + "sheet", new XAttribute("name", sheet.Name), new XAttribute("sheetId", ++id),
                    new XAttribute(Relationships + "id", workbook.GetIdOfPart(part))));
                string? tableId = null;
                if (sheet.Table is { } definition)
                {
                    var table = part.AddNewPart<TableDefinitionPart>();
                    Write(table, definition.Write(id));
                    tableId = part.GetIdOfPart(table);
                }
                Write(part, sheet.Write(Index, tableId));
            }
            Write(shared, new XElement(SheetNamespace + "sst",
                strings.OrderBy(pair => pair.Value).Select(pair => new XElement(SheetNamespace + "si", PreservedText(pair.Key)))));
            Write(workbook, new XElement(SheetNamespace + "workbook",
                new XElement(SheetNamespace + "bookViews", new XElement(SheetNamespace + "workbookView")),
                sheetItems));
        }
        return buffer.ToArray();
    }

    private static void Write(OpenXmlPart part, XElement xml)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        xml.Save(stream, SaveOptions.DisableFormatting);
    }

    private static void EnsureCapacity(string section, int actual, int capacity)
    {
        if (actual > capacity) throw new ReportWorkbookException("XLSX_EXPORT_CAPACITY_EXCEEDED",
            $"القسم «{section}» يحتوي على {actual} صفًا أو عنصرًا، والسعة المتاحة {capacity}. قلّل نطاق التقرير أو اختر العرض المخصص.",
            section, actual, capacity);
    }

    private sealed record ExportColumn(string Label, double Width, Func<ReportBuilderTransactionRow, object?> Value);
    private sealed record GridStyles(int Header, int Text, int Amount, int Date);
}

internal sealed class ReportWorkbookException(string code, string message, string? section = null, int? actual = null, int? capacity = null) : Exception(message)
{
    public string Code { get; } = code;
    public string? Section { get; } = section;
    public int? Actual { get; } = actual;
    public int? Capacity { get; } = capacity;

    public static ReportWorkbookException Unsupported(string message) => new("XLSX_EXPORT_UNSUPPORTED_VALUE", message);
}
