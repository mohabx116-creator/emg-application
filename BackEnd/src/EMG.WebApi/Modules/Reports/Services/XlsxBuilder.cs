using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using EMG.Api.Modules.FinalAccounts.Dtos;
using EMG.Api.Modules.Reports.Dtos;

namespace EMG.Api.Modules.Reports.Services;

internal static class XlsxBuilder
{
    private const int SiteTemplateRows = 42;

    public static byte[] Build(ProjectReportSummary report, OperationalAuditSummary? audit)
    {
        EnsureCapacity(report);
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Modules", "Reports", "Templates", "ProjectReportTemplate.xlsx");
        if (File.Exists(templatePath)) return BuildFromTemplate(templatePath, report, audit);

        const int sheetCount = 6;
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(archive, "[Content_Types].xml", ContentTypes(sheetCount));
            Add(archive, "_rels/.rels", Xml("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>"));
            Add(archive, "xl/workbook.xml", Workbook());
            Add(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships(sheetCount));
            Add(archive, "xl/styles.xml", Styles());
            Add(archive, "xl/worksheets/sheet1.xml", SummarySheet(report, audit));
            Add(archive, "xl/worksheets/sheet2.xml", MovementsSheet(report.Movements));
            Add(archive, "xl/worksheets/sheet3.xml", AdvancesSheet(report.Advances));
            Add(archive, "xl/worksheets/sheet4.xml", SuppliesSheet(report.SupplierSupplies));
            Add(archive, "xl/worksheets/sheet5.xml", PaymentsSheet(report.SupplierPayments));
            Add(archive, "xl/worksheets/sheet6.xml", AuditSheet(audit));
        }
        return CompactSharedStrings(output.ToArray());
    }

    private static byte[] CompactSharedStrings(byte[] workbookBytes)
    {
        using var stream = new MemoryStream(workbookBytes);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedEntry is null) return workbookBytes;

            XDocument shared;
            using (var input = sharedEntry.Open()) shared = XDocument.Load(input, LoadOptions.PreserveWhitespace);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var items = shared.Root?.Elements(ns + "si").ToArray() ?? [];
            var sheets = new List<(ZipArchiveEntry Entry, XDocument Xml)>();
            var references = new List<XElement>();
            var active = new HashSet<int>();
            foreach (var entry in archive.Entries.Where(item =>
                         item.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                         item.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                XDocument xml;
                using (var input = entry.Open()) xml = XDocument.Load(input, LoadOptions.PreserveWhitespace);
                foreach (var cell in xml.Descendants(ns + "c").Where(cell => (string?)cell.Attribute("t") == "s"))
                {
                    var value = cell.Element(ns + "v") ?? throw new InvalidDataException("Missing shared-string index.");
                    if (!int.TryParse(value.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= items.Length)
                        throw new InvalidDataException("Invalid shared-string index.");
                    references.Add(value);
                    active.Add(index);
                }
                sheets.Add((entry, xml));
            }

            var ordered = active.Order().ToArray();
            var remap = ordered.Select((oldIndex, newIndex) => (oldIndex, newIndex)).ToDictionary(item => item.oldIndex, item => item.newIndex);
            foreach (var value in references) value.Value = remap[int.Parse(value.Value, CultureInfo.InvariantCulture)].ToString(CultureInfo.InvariantCulture);
            shared.Root!.RemoveNodes();
            shared.Root.SetAttributeValue("count", references.Count);
            shared.Root.SetAttributeValue("uniqueCount", ordered.Length);
            shared.Root.Add(ordered.Select(index => new XElement(items[index])));

            foreach (var (entry, xml) in sheets) ReplaceXml(entry, xml);
            ReplaceXml(sharedEntry, shared);
        }
        return stream.ToArray();
    }

    private static void ReplaceXml(ZipArchiveEntry entry, XDocument xml)
    {
        using var target = entry.Open();
        target.SetLength(0);
        using var writer = new StreamWriter(target, new System.Text.UTF8Encoding(false), leaveOpen: true);
        xml.Save(writer, SaveOptions.DisableFormatting);
    }

    private static void EnsureCapacity(ProjectReportSummary report)
    {
        Ensure(report.Excavations.Count, 37, "أعمال الحفر");
        // The steel template reserves row 52 for the merged summary and row 54
        // for the net amount; detail rows safely occupy rows 7-51 only.
        Ensure(report.Steels.Count, 45, "أعمال الحديد");
        // The concrete template reserves rows 30-31 for merged summary cells;
        // detail rows safely occupy 10-29 only.
        Ensure(report.Concretes.Count, 20, "أعمال الخرسانات");
        Ensure(report.Finishings.Count(IsBuilding) + report.Finishings.Count(IsUnclassifiedFinishing), 36, "أعمال المباني والمصنّعيات غير المصنفة");
        Ensure(report.Finishings.Count(IsPlaster), 36, "أعمال المحارة");
        Ensure(report.Finishings.Count(IsInsulation), 36, "أعمال العزل");
        Ensure(report.Finishings.Count(IsElectrical), 36, "أعمال الكهرباء");
        Ensure(report.Finishings.Count(IsPlumbing), 36, "أعمال السباكة");
    }

    private static void Ensure(int actual, int capacity, string sheet)
    {
        if (actual > capacity) throw new XlsxExportCapacityException(sheet, actual, capacity);
    }

    private static byte[] BuildFromTemplate(string templatePath, ProjectReportSummary report, OperationalAuditSummary? audit)
    {
        var appendFullMovements = report.Movements.Count > SiteTemplateRows;
        using var source = ZipFile.OpenRead(templatePath);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in source.Entries)
            {
                // The template contains a calculation chain for its original
                // formulas. We replace those formulas with final exported values,
                // so retaining the old chain makes Excel open the workbook as
                // [Repaired]. Remove the chain and its package references.
                if (entry.FullName.Equals("xl/calcChain.xml", StringComparison.OrdinalIgnoreCase)) continue;
                var target = archive.CreateEntry(entry.FullName, CompressionLevel.Fastest);
                using var input = entry.Open();
                using var stream = target.Open();
                if (entry.FullName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                {
                    var contentTypes = XDocument.Load(input, LoadOptions.PreserveWhitespace);
                    XNamespace contentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";
                    contentTypes.Root?.Elements(contentTypesNs + "Override")
                        .Where(item => ((string?)item.Attribute("PartName"))?.Equals("/xl/calcChain.xml", StringComparison.OrdinalIgnoreCase) == true)
                        .Remove();
                    contentTypes.Root?.Add(
                        new XElement(contentTypesNs + "Override", new XAttribute("PartName", "/xl/worksheets/sheet12.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                        new XElement(contentTypesNs + "Override", new XAttribute("PartName", "/xl/worksheets/sheet13.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
                    if (appendFullMovements)
                        contentTypes.Root?.Add(new XElement(contentTypesNs + "Override", new XAttribute("PartName", "/xl/worksheets/sheet14.xml"),
                            new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
                    using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
                    contentTypes.Save(writer, SaveOptions.DisableFormatting);
                }
                else if (entry.FullName.Equals("xl/_rels/workbook.xml.rels", StringComparison.OrdinalIgnoreCase))
                {
                    var relationships = XDocument.Load(input, LoadOptions.PreserveWhitespace);
                    XNamespace relationshipsNs = "http://schemas.openxmlformats.org/package/2006/relationships";
                    relationships.Root?.Elements(relationshipsNs + "Relationship")
                        .Where(item => ((string?)item.Attribute("Type"))?.EndsWith("/calcChain", StringComparison.OrdinalIgnoreCase) == true)
                        .Remove();
                    relationships.Root?.Add(
                        new XElement(relationshipsNs + "Relationship", new XAttribute("Id", "rId17"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new XAttribute("Target", "worksheets/sheet12.xml")),
                        new XElement(relationshipsNs + "Relationship", new XAttribute("Id", "rId18"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new XAttribute("Target", "worksheets/sheet13.xml")));
                    if (appendFullMovements)
                        relationships.Root?.Add(new XElement(relationshipsNs + "Relationship", new XAttribute("Id", "rId19"),
                            new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                            new XAttribute("Target", "worksheets/sheet14.xml")));
                    using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
                    relationships.Save(writer, SaveOptions.DisableFormatting);
                }
                else if (entry.FullName.Equals("xl/workbook.xml", StringComparison.OrdinalIgnoreCase))
                {
                    var workbook = XDocument.Load(input, LoadOptions.PreserveWhitespace);
                    XNamespace workbookNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                    workbook.Root?.Element(workbookNs + "sheets")?.Add(
                        new XElement(workbookNs + "sheet", new XAttribute("name", "توريدات الموردين"), new XAttribute("sheetId", "17"), new XAttribute(relNs + "id", "rId17")),
                        new XElement(workbookNs + "sheet", new XAttribute("name", "مدفوعات الموردين"), new XAttribute("sheetId", "18"), new XAttribute(relNs + "id", "rId18")));
                    if (appendFullMovements)
                        workbook.Root?.Element(workbookNs + "sheets")?.Add(new XElement(workbookNs + "sheet",
                            new XAttribute("name", "كل حركات الموقع"), new XAttribute("sheetId", "19"), new XAttribute(relNs + "id", "rId19")));
                    var calculation = workbook.Root?.Element(workbookNs + "calcPr");
                    if (calculation is null)
                    {
                        calculation = new XElement(workbookNs + "calcPr");
                        workbook.Root?.Add(calculation);
                    }
                    calculation.SetAttributeValue("calcMode", "auto");
                    calculation.SetAttributeValue("fullCalcOnLoad", "1");
                    calculation.SetAttributeValue("forceFullCalc", "1");
                    using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
                    workbook.Save(writer, SaveOptions.DisableFormatting);
                }
                else if (entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(input, System.Text.Encoding.UTF8);
                    var sheet = XDocument.Parse(reader.ReadToEnd(), LoadOptions.PreserveWhitespace);
                    var sheetName = Path.GetFileNameWithoutExtension(entry.FullName);
                    var sheetNumber = int.Parse(sheetName["sheet".Length..], CultureInfo.InvariantCulture);
                    PopulateTemplateSheet(sheet, sheetNumber, report);
                    using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
                    sheet.Save(writer, SaveOptions.DisableFormatting);
                }
                else
                {
                    input.CopyTo(stream);
                }
            }
            Add(archive, "xl/worksheets/sheet12.xml", SuppliesSheet(report.SupplierSupplies));
            Add(archive, "xl/worksheets/sheet13.xml", PaymentsSheet(report.SupplierPayments));
            if (appendFullMovements)
                Add(archive, "xl/worksheets/sheet14.xml", MovementsSheet(report.Movements));
        }
        return CompactSharedStrings(output.ToArray());
    }

    private static void PopulateTemplateSheet(XDocument sheet, int number, ProjectReportSummary report)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var worksheet = sheet.Root!;
        var preserveTableFormulas = number == 1 || number == 4 || number is >= 7 and <= 11;
        foreach (var cell in worksheet.Descendants(ns + "c"))
        {
            if (!preserveTableFormulas)
                cell.Element(ns + "f")?.Remove();
            // Source templates store fixed labels as shared strings in <v> too.
            // Remove only cached numeric values; keeping text values preserves the
            // original headings, captions and sheet labels from the real workbook.
            var type = (string?)cell.Attribute("t");
            if (type is null || type == "n") cell.Element(ns + "v")?.Remove();
        }

        if (number == 5) PopulateAdvancesTemplate(worksheet, report, ns);
        else if (number == 6) PopulateFinalAccountTemplate(worksheet, report.FinalAccount, ns);
        else if (number == 4) PopulateSiteTemplate(worksheet, report, ns);
        else PopulateWorkTemplate(worksheet, number, report, ns);

        // SetCell may create a missing address after the cells already present
        // in a template row. Excel requires cells in each row to be ordered by
        // column; normalize that order before writing the package.
        NormalizeCellOrder(worksheet, ns);
    }

    private static void PopulateAdvancesTemplate(XElement worksheet, ProjectReportSummary report, XNamespace ns)
    {
        var advances = report.Advances;
        var account = report.FinalAccount;
        SetText(worksheet, "A1", $"كشف حساب العميل بالدفعات الموزعه / النقديه / شيكات / تحويل -- بتاريخ : {DateTime.Now:yyyy/MM/dd}", ns);
        SetText(worksheet, "N1", $"الدفعات الرئسية : {account.PayerName}", ns);
        SetText(worksheet, "A2", $"اسم العميل : {account.ClientName} — رقم القطعة: {account.ProjectCode}", ns);
        var headers = new[] { "التاريخ", "دفعات تحت الحساب للموقع", "دفعات تحت الحساب للعرب", "دفعات تحت حساب المقاول", "دفعات تحت حساب المحطه", "دفعات حساب الحديد", "دفعات تحت حساب المباني", "دفعات تحت حساب المحاره", "دفعات تحت حساب العزل", "دفعات تحت حساب الكهرباء", "دفعات تحت حساب السباكه", "دفعات تحت حساب المكتب", "ملاحظات", "الدفعه", "تاريخ" };
        for (var i = 0; i < headers.Length; i++) SetText(worksheet, $"{Column(i)}3", headers[i], ns);
        var categories = new[] { "الموقع", "العرب", "المقاول", "المحطة", "الحديد", "المباني", "المحارة", "العزل", "الكهرباء", "السباكة", "المكتب" };
        for (var row = 4; row <= 17; row++) ClearRow(worksheet, row, ns);
        for (var i = 0; i < advances.Count && i < 14; i++)
        {
            var item = advances[i];
            var row = i + 4;
            SetNumber(worksheet, $"A{row}", ExcelDate(item.Date), ns);
            for (var c = 0; c < categories.Length; c++) SetNumber(worksheet, $"{Column(c + 1)}{row}", Value(item.AllocationsByCategory, categories[c]), ns);
            SetText(worksheet, $"M{row}", item.PayerName, ns);
            SetNumber(worksheet, $"N{row}", item.Amount, ns);
            SetText(worksheet, $"O{row}", item.Reference, ns);
        }
        var totalRow = 19;
        SetText(worksheet, $"A{totalRow}", "اجمالي دفعات", ns);
        for (var c = 0; c < categories.Length; c++) SetNumber(worksheet, $"{Column(c + 1)}{totalRow}", advances.Sum(item => Value(item.AllocationsByCategory, categories[c])), ns);
        SetNumber(worksheet, $"M{totalRow}", advances.Sum(item => item.Allocated), ns);
        ClearCell(worksheet, $"N{totalRow}", ns); ClearCell(worksheet, $"O{totalRow}", ns);

        var workCategories = new[] { "الموقع", "العرب", "المقاول", "المحطة", "الحديد", "المباني", "المحارة", "العزل", "الكهرباء", "السباكة" };
        for (var c = 0; c < workCategories.Length; c++)
        {
            var category = workCategories[c];
            var work = Value(account.OutgoingByCategory, category);
            var paid = advances.Sum(item => Value(item.AllocationsByCategory, category));
            SetText(worksheet, $"{Column(c + 1)}20", $"ح/ {category}", ns);
            SetNumber(worksheet, $"{Column(c + 1)}21", work, ns);
            SetNumber(worksheet, $"{Column(c + 1)}22", work - paid, ns);
        }
        var workTotal = workCategories.Sum(category => Value(account.OutgoingByCategory, category));
        SetText(worksheet, "L20", "اجمالي الاعمال", ns);
        SetNumber(worksheet, "L21", workTotal, ns);
        ClearCell(worksheet, "L22", ns);
        ClearCell(worksheet, "M20", ns);
        ClearCell(worksheet, "M21", ns);
        SetNumber(worksheet, "N22", account.TotalIncoming, ns);
        SetNumber(worksheet, "O22", advances.Sum(item => item.Remaining), ns);
        var officeAllocated = advances.Sum(item => Value(item.AllocationsByCategory, "المكتب"));
        SetNumber(worksheet, "G24", workTotal - (advances.Sum(item => item.Allocated) - officeAllocated), ns);
        SetNumber(worksheet, "L24", account.TotalIncoming - workTotal, ns);
    }

    private static void PopulateFinalAccountTemplate(XElement worksheet, FinalAccountSummary account, XNamespace ns)
    {
        SetText(worksheet, "D5", account.HasUnresolvedImportRecords ? "كشف حساب تشخيصي: استيراد غير مكتمل" : "كشف حساب", ns);
        SetText(worksheet, "D6", $"اسم العميل: {account.ClientName}", ns);
        SetText(worksheet, "D7", $"رقم القطعة: {account.ProjectCode}", ns);
        SetText(worksheet, "E8", "ح / الموقع", ns);
        var categories = new[] { "الموقع", "العرب", "المقاول", "المحطة", "الحديد", "باقي المصنعيات" };
        for (var i = 0; i < categories.Length; i++)
        {
            var value = categories[i] == "باقي المصنعيات" ? OtherCategories(account.OutgoingByCategory) : Value(account.OutgoingByCategory, categories[i]);
            SetNumber(worksheet, $"{Column(i + 4)}9", value, ns);
            var incoming = categories[i] == "باقي المصنعيات" ? OtherCategories(account.IncomingByCategory) : Value(account.IncomingByCategory, categories[i]);
            SetNumber(worksheet, $"{Column(i + 4)}10", incoming, ns);
        }
        SetNumber(worksheet, "K9", account.TotalOutgoing, ns);
        SetNumber(worksheet, "K10", account.TotalIncoming, ns);
        SetText(worksheet, "E12", "دفعات تحت حساب المكتب", ns);
        SetNumber(worksheet, "F12", account.OfficePaid, ns);
        SetText(worksheet, "G13", "نسبة المكتب", ns);
        SetNumber(worksheet, "H13", account.OfficeSharePercentage / 100m, ns);
        if (account.HasUnresolvedImportRecords) ClearCell(worksheet, "I13", ns);
        else SetNumber(worksheet, "I13", account.OfficeNetDue, ns);
    }

    private static void PopulateSiteTemplate(XElement worksheet, ProjectReportSummary report, XNamespace ns)
    {
        var movements = report.Movements;
        var account = report.FinalAccount;
        SetText(worksheet, "C2", "خاص باعمال الموقع", ns);
        SetText(worksheet, "A5", $"اسم العميل: {account.ClientName}", ns);
        SetText(worksheet, "E6", $"رقم قطعه :{account.ProjectCode}", ns);
        // Keep the template's shared-string headers. Inline text without
        // xml:space="preserve" can lose the trailing space in a table heading.
        for (var row = 10; row <= 51; row++) ClearRow(worksheet, row, ns, preserveFormulaColumns: true);
        for (var i = 0; i < SiteTemplateRows; i++)
        {
            var row = i + 10;
            SetNumber(worksheet, $"G{row}", 0m, ns);
        }
        for (var i = 0; i < movements.Count && i < SiteTemplateRows; i++)
        {
            var row = i + 10;
            var item = movements[i];
            SetNumber(worksheet, $"B{row}", ExcelDate(item.Date), ns);
            SetText(worksheet, $"C{row}", item.Description, ns);
            SetText(worksheet, $"D{row}", item.SupplierName, ns);
            SetText(worksheet, $"E{row}", item.Unit, ns);
            SetNumber(worksheet, $"F{row}", item.UnitPrice ?? 0m, ns);
            SetNumber(worksheet, $"G{row}", 0m, ns);
            SetNumber(worksheet, $"H{row}", item.Quantity ?? 0m, ns);
            SetText(worksheet, $"K{row}", item.Reference, ns);
        }
        for (var i = 0; i < SiteTemplateRows; i++)
        {
            var row = i + 10;
            var quantity = i < movements.Count ? movements[i].Quantity ?? 0m : 0m;
            var price = i < movements.Count ? movements[i].UnitPrice ?? 0m : 0m;
            SetFormulaCache(worksheet, $"A{row}", i + 1, ns);
            SetFormulaCache(worksheet, $"I{row}", quantity, ns);
            // Amount-only expenses and historically documented overrides
            // have an authoritative posted amount that Excel must retain.
            if (i < movements.Count && decimal.Round(quantity * price, 2) != movements[i].Amount)
                SetNumber(worksheet, $"J{row}", movements[i].Amount, ns);
            else
                SetFormulaCache(worksheet, $"J{row}", quantity * price, ns);
        }
        SetNumber(worksheet, "K5", Value(account.OutgoingByCategory, "المقاول"), ns);
        SetNumber(worksheet, "K6", Value(account.OutgoingByCategory, "المحطة"), ns);
        SetNumber(worksheet, "K7", Value(account.OutgoingByCategory, "الموقع"), ns);
        SetNumber(worksheet, "K8", Value(account.OutgoingByCategory, "المقاول") + Value(account.OutgoingByCategory, "المحطة") + Value(account.OutgoingByCategory, "الموقع"), ns);
    }

    private static void PopulateWorkTemplate(XElement worksheet, int number, ProjectReportSummary report, XNamespace ns)
    {
        var title = number switch { 1 => "حساب العرب (الحفر)", 2 => "اعمال توريد الحديد", 3 => "اعمال الخرسانات", 7 => "كشف حساب مقاول المباني / باقي المصنعيات", 8 => "كشف حساب مقاول المحاره", 9 => "كشف حساب مقاول العزل", 10 => "كشف حساب مصنعية الكهرباء", 11 => "كشف حساب مصنعية السباكه", _ => "أعمال المشروع" };
        // The concrete-work template intentionally has no title in C2; its
        // original workbook starts with the owner/client metadata block.
        if (number == 2) SetText(worksheet, "A2", title, ns);
        else if (number != 3) SetText(worksheet, "C2", title, ns);
        if (number == 1)
        {
            SetText(worksheet, "E3", $"اسم العميل : {report.FinalAccount.ClientName}", ns);
        }
        else if (number == 2)
        {
            SetText(worksheet, "A4", $"رقم القطعه :{report.FinalAccount.ProjectCode}", ns);
            SetText(worksheet, "A5", $"اسم العميل :{report.FinalAccount.ClientName}", ns);
        }
        else if (number == 3)
        {
            SetText(worksheet, "E2", $"اسم المالك : {report.FinalAccount.OwnerName}", ns);
            SetText(worksheet, "E4", $"رقم القطعه: {report.FinalAccount.ProjectCode}", ns);
            SetText(worksheet, "E5", $"اسم العميل: {report.FinalAccount.ClientName}", ns);
            SetText(worksheet, "Q6", report.FinalAccount.PayerName, ns);
        }
        if (number >= 7)
        {
            SetText(worksheet, "A5", $"اسم العميل: {report.FinalAccount.ClientName}", ns);
            SetText(worksheet, "E6", $"رقم قطعه :{report.FinalAccount.ProjectCode}", ns);
        }
        if (number == 1) PopulateExcavationTemplate(worksheet, report.Excavations, report, ns);
        else if (number == 2) PopulateSteelTemplate(worksheet, report.Steels, report, ns);
        else if (number == 3) PopulateConcreteTemplate(worksheet, report.Concretes, report, ns);
        else
        {
            var finishings = report.Finishings.Where(item => number switch
            {
                7 => IsBuilding(item) || IsUnclassifiedFinishing(item),
                8 => IsPlaster(item),
                9 => IsInsulation(item),
                10 => IsElectrical(item),
                11 => IsPlumbing(item),
                _ => true
            }).ToList();
            PopulateFinishingTemplate(worksheet, finishings, report, number, ns);
        }
    }

    private static void PopulateExcavationTemplate(XElement worksheet, IReadOnlyList<ProjectWorkRow> items, ProjectReportSummary report, XNamespace ns)
    {
        var headers = new[] { "م", "التاريخ", "البيان", "الوحد", "الكميه", "السعر", "الاجمالي", "الدفعات" };
        for (var i = 0; i < headers.Length; i++) SetText(worksheet, $"{Column(i + 3)}8", headers[i], ns);
        for (var row = 9; row <= 45; row++) ClearRow(worksheet, row, ns, preserveFormulaColumns: true);
        for (var i = 0; i < items.Count && i < 37; i++)
        {
            var item = items[i]; var row = i + 9;
            SetNumber(worksheet, $"E{row}", ExcelDate(item.Date), ns); SetText(worksheet, $"F{row}", item.Description, ns);
            SetText(worksheet, $"G{row}", item.Unit, ns); SetNumber(worksheet, $"H{row}", item.Quantity ?? 0m, ns); SetNumber(worksheet, $"I{row}", item.UnitPrice ?? 0m, ns); SetText(worksheet, $"K{row}", item.Extra1 + " " + item.Extra2, ns);
        }
        for (var i = 0; i < 37; i++)
        {
            var row = i + 9;
            SetFormulaCache(worksheet, $"D{row}", i + 1, ns);
            SetFormulaCache(worksheet, $"J{row}", i < items.Count ? items[i].TotalAmount : 0m, ns);
        }
        var total = items.Sum(item => item.TotalAmount);
        var paid = report.Advances.Sum(item => Value(item.AllocationsByCategory, "العرب"));
        SetNumber(worksheet, "J5", total - paid, ns);
        SetNumber(worksheet, "J7", total, ns);
        SetNumber(worksheet, "K7", paid, ns);
    }

    private static void PopulateSteelTemplate(XElement worksheet, IReadOnlyList<ProjectWorkRow> items, ProjectReportSummary report, XNamespace ns)
    {
        var headers = new[] { "م", "التاريخ", "الوزن بالطن", "المقاس(مم)", "السعر", "التكلفه", "ملاحظات", "الدفعات" };
        for (var i = 0; i < headers.Length; i++) SetText(worksheet, $"{Column(i)}6", headers[i], ns);
        // Do not clear or write through the reserved summary rows.
        for (var row = 7; row <= 51; row++) ClearRow(worksheet, row, ns);
        for (var i = 0; i < 45; i++)
        {
            var row = i + 7;
            SetNumber(worksheet, $"A{row}", i + 1, ns);
            SetNumber(worksheet, $"F{row}", 0m, ns);
        }
        for (var i = 0; i < items.Count && i < 45; i++)
        {
            var item = items[i]; var row = i + 7;
            SetNumber(worksheet, $"A{row}", i + 1, ns); SetNumber(worksheet, $"B{row}", ExcelDate(item.Date), ns); SetNumber(worksheet, $"C{row}", item.Quantity ?? 0m, ns);
            SetText(worksheet, $"D{row}", item.Extra1, ns); SetNumber(worksheet, $"E{row}", item.UnitPrice ?? 0m, ns); SetNumber(worksheet, $"F{row}", item.TotalAmount, ns); SetText(worksheet, $"G{row}", string.IsNullOrWhiteSpace(item.SupplierName) ? item.Item : $"{item.SupplierName} — {item.Item}", ns); SetText(worksheet, $"H{row}", item.Extra2 + " " + item.Extra3, ns);
        }
        var total = items.Sum(item => item.TotalAmount);
        var paid = report.Advances.Sum(item => Value(item.AllocationsByCategory, "الحديد"));
        ClearCell(worksheet, "F5", ns);
        SetNumber(worksheet, "C52", items.Sum(item => item.Quantity ?? 0m), ns);
        SetNumber(worksheet, "F52", total, ns);
        SetNumber(worksheet, "H52", paid, ns);
        SetNumber(worksheet, "E54", total - paid, ns);
    }

    private static void PopulateConcreteTemplate(XElement worksheet, IReadOnlyList<ProjectWorkRow> items, ProjectReportSummary report, XNamespace ns)
    {
        // The concrete header is a merged layout. Write only to each merge's
        // top-left cell; writing to A8/B8/C8/J8 corrupts merged-cell content.
        SetText(worksheet, "A7", "م", ns);
        SetText(worksheet, "B7", "التاريخ", ns);
        SetText(worksheet, "C7", "النوع", ns);
        SetText(worksheet, "D8", "خرساانه مورده", ns);
        SetText(worksheet, "E8", "الدفعات", ns);
        SetText(worksheet, "G8", "السعر بالمتر مكعب", ns);
        SetText(worksheet, "H8", "صافي قيمة الاعمال بالجنيه", ns);
        SetText(worksheet, "I8", "بيان", ns);
        SetText(worksheet, "J7", "اسم المحطه", ns);
        SetText(worksheet, "M7", "م", ns); SetText(worksheet, "N7", "بيان بالاعمال", ns); SetText(worksheet, "O7", "الوحده", ns); SetText(worksheet, "P7", "الكميه", ns); SetText(worksheet, "S7", "الفئه", ns); SetText(worksheet, "T7", "النسبه", ns); SetText(worksheet, "U7", "الاجمالي", ns); SetText(worksheet, "V7", "دفعات", ns); SetText(worksheet, "W7", "البيان", ns);
        // Rows 7-9 are merged headers and rows 30-31 are merged summaries.
        // Never clear or write detail values into either reserved region.
        for (var row = 10; row <= 29; row++) ClearRow(worksheet, row, ns);
        for (var i = 0; i < 20; i++)
        {
            var row = i + 10;
            SetNumber(worksheet, $"A{row}", i + 1, ns);
            SetNumber(worksheet, $"H{row}", 0m, ns);
            SetNumber(worksheet, $"U{row}", 0m, ns);
        }
        for (var i = 0; i < items.Count && i < 20; i++)
        {
            var item = items[i]; var row = i + 10;
            SetNumber(worksheet, $"A{row}", i + 1, ns); SetNumber(worksheet, $"B{row}", ExcelDate(item.Date), ns); SetText(worksheet, $"C{row}", item.Item, ns); SetText(worksheet, $"D{row}", item.Extra1, ns); SetNumber(worksheet, $"E{row}", 0m, ns); SetNumber(worksheet, $"G{row}", item.UnitPrice ?? 0m, ns); SetNumber(worksheet, $"H{row}", item.TotalAmount, ns); SetText(worksheet, $"I{row}", item.Description, ns); SetText(worksheet, $"J{row}", item.SupplierName, ns);
            SetNumber(worksheet, $"M{row}", i + 1, ns); SetText(worksheet, $"N{row}", item.Item, ns); SetText(worksheet, $"O{row}", item.Unit, ns); SetNumber(worksheet, $"P{row}", 0m, ns); SetNumber(worksheet, $"Q{row}", item.Quantity ?? 0m, ns); SetNumber(worksheet, $"R{row}", item.Quantity ?? 0m, ns); SetText(worksheet, $"S{row}", item.Extra2, ns); SetText(worksheet, $"T{row}", "100%", ns); SetNumber(worksheet, $"U{row}", item.TotalAmount, ns); SetNumber(worksheet, $"V{row}", 0m, ns); SetText(worksheet, $"W{row}", item.Reference, ns);
        }
        var total = items.Sum(item => item.TotalAmount);
        var paid = report.Advances.Sum(item => Value(item.AllocationsByCategory, "المقاول"));
        SetNumber(worksheet, "G31", Value(report.FinalAccount.OutgoingByCategory, "المحطة"), ns);
        SetNumber(worksheet, "U30", total, ns);
        SetNumber(worksheet, "V31", paid, ns);
        SetNumber(worksheet, "W31", total - paid, ns);
    }

    private static void PopulateFinishingTemplate(XElement worksheet, IReadOnlyList<ProjectWorkRow> items, ProjectReportSummary report, int sheetNumber, XNamespace ns)
    {
        // Preserve the template's shared-string headers and their exact
        // accountant-approved labels, including trailing spaces.
        for (var row = 10; row <= 45; row++) ClearRow(worksheet, row, ns, preserveFormulaColumns: true);
        for (var i = 0; i < 36; i++)
        {
            var row = i + 10;
            SetNumber(worksheet, $"G{row}", 0m, ns);
        }
        for (var i = 0; i < items.Count && i < 36; i++)
        {
            var item = items[i]; var row = i + 10;
            SetNumber(worksheet, $"B{row}", ExcelDate(item.Date), ns); SetText(worksheet, $"C{row}", item.Item, ns); SetText(worksheet, $"D{row}", item.SupplierName, ns); SetText(worksheet, $"E{row}", item.Unit, ns); SetNumber(worksheet, $"F{row}", item.UnitPrice ?? 0m, ns); SetNumber(worksheet, $"G{row}", 0m, ns); SetNumber(worksheet, $"H{row}", item.Quantity ?? 0m, ns);
        }
        for (var i = 0; i < 36; i++)
        {
            var row = i + 10;
            var quantity = i < items.Count ? items[i].Quantity ?? 0m : 0m;
            var price = i < items.Count ? items[i].UnitPrice ?? 0m : 0m;
            SetFormulaCache(worksheet, $"A{row}", i + 1, ns);
            SetFormulaCache(worksheet, $"I{row}", quantity, ns);
            SetFormulaCache(worksheet, $"J{row}", quantity * price, ns);
        }
        var category = sheetNumber switch { 7 => "المباني", 8 => "المحارة", 9 => "العزل", 10 => "الكهرباء", 11 => "السباكة", _ => string.Empty };
        var paid = report.Advances.Sum(item => Value(item.AllocationsByCategory, category));
        if (sheetNumber == 7)
            paid += report.Advances.Sum(item => Value(item.AllocationsByCategory, "باقي المصنعيات") + Value(item.AllocationsByCategory, "غير محدد"));
        SetNumber(worksheet, "K6", paid, ns);
        SetNumber(worksheet, "K7", items.Sum(item => item.TotalAmount), ns);
        SetNumber(worksheet, "K8", items.Sum(item => item.TotalAmount) - paid, ns);
    }

    private static bool IsBuilding(ProjectWorkRow item) => HasAny(item, "مباني", "مبنى", "بنا", "بناء");
    private static bool IsPlaster(ProjectWorkRow item) => HasAny(item, "محارة", "محار", "لياسة");
    private static bool IsInsulation(ProjectWorkRow item) => HasAny(item, "عزل", "عوازل");
    private static bool IsElectrical(ProjectWorkRow item) => HasAny(item, "كهرب", "كهرباء");
    private static bool IsPlumbing(ProjectWorkRow item) => HasAny(item, "سباك", "سباكة", "صرف", "تغذية");
    private static bool IsUnclassifiedFinishing(ProjectWorkRow item) => !IsBuilding(item) && !IsPlaster(item) && !IsInsulation(item) && !IsElectrical(item) && !IsPlumbing(item);

    private static bool HasAny(ProjectWorkRow item, params string[] terms)
    {
        var category = Normalize(item.Category);
        var itemName = Normalize(item.Item);
        return terms.Any(term => category.Contains(term, StringComparison.OrdinalIgnoreCase) || itemName.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string? value)
        => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void ClearRow(XElement worksheet, int row, XNamespace ns, bool preserveFormulaColumns = false)
    {
        foreach (var cell in worksheet.Descendants(ns + "c").Where(cell => RegexRow(cell.Attribute("r")?.Value) == row))
        {
            var column = new string((cell.Attribute("r")?.Value ?? string.Empty).TakeWhile(char.IsLetter).ToArray());
            if (preserveFormulaColumns && (column is "A" or "D" or "I" or "J") && cell.Element(ns + "f") is not null)
            {
                // Keep the formula and its cache type. Remove stale text results
                // so Excel recalculates them from the preserved formula.
                if ((string?)cell.Attribute("t") == "str") cell.Element(ns + "v")?.Remove();
                continue;
            }
            cell.Attribute("t")?.Remove(); cell.Element(ns + "f")?.Remove(); cell.Element(ns + "v")?.Remove(); cell.Element(ns + "is")?.Remove();
        }
    }

    private static void ClearCell(XElement worksheet, string address, XNamespace ns)
    {
        var cell = worksheet.Descendants(ns + "c").FirstOrDefault(item => (string?)item.Attribute("r") == address);
        if (cell is null) return;
        cell.Attribute("t")?.Remove(); cell.Element(ns + "f")?.Remove(); cell.Element(ns + "v")?.Remove(); cell.Element(ns + "is")?.Remove();
    }

    private static void SetFormulaCache(XElement worksheet, string address, decimal value, XNamespace ns)
    {
        var cell = worksheet.Descendants(ns + "c").FirstOrDefault(item => (string?)item.Attribute("r") == address);
        if (cell?.Element(ns + "f") is null) return;
        cell.Element(ns + "v")?.Remove();
        cell.Add(new XElement(ns + "v", value.ToString("0.########", CultureInfo.InvariantCulture)));
    }

    private static int RegexRow(string? address) => int.TryParse(new string((address ?? string.Empty).Where(char.IsDigit).ToArray()), out var row) ? row : 0;
    private static void NormalizeCellOrder(XElement worksheet, XNamespace ns)
    {
        foreach (var row in worksheet.Descendants(ns + "row"))
        {
            var cells = row.Elements(ns + "c").ToList();
            var ordered = cells.OrderBy(cell => ColumnIndex((string?)cell.Attribute("r"))).ToList();
            if (!cells.SequenceEqual(ordered))
            {
                foreach (var cell in cells) cell.Remove();
                row.Add(ordered);
            }
        }
    }

    private static int ColumnIndex(string? address)
    {
        var letters = new string((address ?? string.Empty).TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        var result = 0;
        foreach (var letter in letters) result = result * 26 + letter - 'A' + 1;
        return result;
    }

    private static void SetText(XElement worksheet, string address, string? value, XNamespace ns) => SetCell(worksheet, address, value ?? string.Empty, false, ns);
    private static void SetNumber(XElement worksheet, string address, decimal value, XNamespace ns) => SetCell(worksheet, address, value.ToString("0.########", CultureInfo.InvariantCulture), true, ns);
    private static void SetCell(XElement worksheet, string address, string value, bool number, XNamespace ns)
    {
        var cell = worksheet.Descendants(ns + "c").FirstOrDefault(item => (string?)item.Attribute("r") == address);
        if (cell is null)
        {
            var rowNumber = RegexRow(address);
            var row = worksheet.Descendants(ns + "row").FirstOrDefault(item => (string?)item.Attribute("r") == rowNumber.ToString(CultureInfo.InvariantCulture));
            if (row is null) { row = new XElement(ns + "row", new XAttribute("r", rowNumber)); worksheet.Element(ns + "sheetData")!.Add(row); }
            cell = new XElement(ns + "c", new XAttribute("r", address)); row.Add(cell);
        }
        cell.Element(ns + "f")?.Remove(); cell.Element(ns + "v")?.Remove(); cell.Element(ns + "is")?.Remove(); cell.Attribute("t")?.Remove();
        if (number) cell.Add(new XElement(ns + "v", value));
        else { cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(ns + "is", new XElement(ns + "t", value))); }
    }

    private static decimal ExcelDate(DateTime date) => (decimal)(date.Date - new DateTime(1899, 12, 30)).TotalDays;

    private static void Add(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false));
        document.Save(writer, SaveOptions.DisableFormatting);
    }

    private static XDocument SummarySheet(ProjectReportSummary report, OperationalAuditSummary? audit)
    {
        var categories = new[] { "الموقع", "العرب", "المقاول", "المحطة", "الحديد", "باقي المصنعيات" };
        var outgoing = categories.Select(category => category == "باقي المصنعيات" ? OtherCategories(report.FinalAccount.OutgoingByCategory) : Value(report.FinalAccount.OutgoingByCategory, category)).ToList();
        var incoming = categories.Select(category => category == "باقي المصنعيات" ? OtherCategories(report.FinalAccount.IncomingByCategory) : Value(report.FinalAccount.IncomingByCategory, category)).ToList();
        var rows = new List<string[]> {
            new[] { report.FinalAccount.HasUnresolvedImportRecords ? "كشف حساب تشخيصي: استيراد غير مكتمل" : "كشف حساب", report.FinalAccount.ProjectName },
            new[] { "اسم العميل", report.FinalAccount.ClientName },
            new[] { "الدافع", report.FinalAccount.PayerName },
            Array.Empty<string>(),
            new[] { "البند" }.Concat(categories).Concat(new[] { "إجمالي" }).ToArray(),
            new[] { "صادر" }.Concat(outgoing.Select(N)).Concat(new[] { N(report.FinalAccount.TotalOutgoing) }).ToArray(),
            new[] { "وارد" }.Concat(incoming.Select(N)).Concat(new[] { N(report.FinalAccount.TotalIncoming) }).ToArray(),
            new[] { "ح/ نسبة المكتب", "", "", "", "", "", report.FinalAccount.HasUnresolvedImportRecords ? "" : N(report.FinalAccount.OfficeShareAmount) },
            new[] { "إجمالي الدفعات تحت ح/ المكتب", "", "", "", "", "", N(report.FinalAccount.OfficePaid) },
            new[] { "صافي المستحق للمكتب", "", "", "", "", "", report.FinalAccount.HasUnresolvedImportRecords ? "" : N(report.FinalAccount.OfficeNetDue) },
            Array.Empty<string>(),
            new[] { "إجمالي المصروفات النقدية", N(report.FinalAccount.TotalExpenses) },
            new[] { "صافي المشروع بعد نصيب المكتب", report.FinalAccount.HasUnresolvedImportRecords ? "" : N(report.FinalAccount.NetAfterOfficeShare) },
            new[] { "رصيد الدافع", report.FinalAccount.HasUnresolvedImportRecords ? "" : N(report.FinalAccount.PayerBalance) },
            new[] { "حالة الرصيد", report.FinalAccount.BalanceStatus }
        };
        if (report.FinalAccount.OutgoingByCategory.Count > 0)
        {
            rows.Add(new[] { "", "" });
            rows.Add(new[] { "الصادر حسب البند", "قيمة" });
            rows.AddRange(report.FinalAccount.OutgoingByCategory.OrderBy(item => item.Key).Select(item => new[] { item.Key, N(item.Value) }));
        }
        if (report.FinalAccount.IncomingByCategory.Count > 0)
        {
            rows.Add(new[] { "", "" });
            rows.Add(new[] { "الوارد حسب البند", "قيمة" });
            rows.AddRange(report.FinalAccount.IncomingByCategory.OrderBy(item => item.Key).Select(item => new[] { item.Key, N(item.Value) }));
        }
        if (audit is not null) { rows.Add(new[] { "", "" }); rows.Add(new[] { "تدقيق الأعمال", "قيمة" }); rows.Add(new[] { "الحفر", N(audit.ExcavationTotal) }); rows.Add(new[] { "الخرسانات", N(audit.ConcreteTotal) }); rows.Add(new[] { "الحديد", N(audit.SteelTotal) }); rows.Add(new[] { "التشطيبات", N(audit.FinishingTotal) }); rows.Add(new[] { "توريدات الموردين", N(audit.SupplierSuppliesTotal) }); rows.Add(new[] { "مدفوعات الموردين", N(audit.SupplierPaymentsTotal) }); }
        return Sheet(rows, true);
    }

    private static decimal Value(IReadOnlyDictionary<string, decimal> values, string category) => values.TryGetValue(category, out var value) ? value : 0m;
    private static decimal OtherCategories(IReadOnlyDictionary<string, decimal> values) => values.Where(item => item.Key is not ("الموقع" or "العرب" or "المقاول" or "المحطة" or "الحديد")).Sum(item => item.Value);

    private static XDocument MovementsSheet(IReadOnlyList<ProjectMovementRow> movements)
    {
        var rows = new List<string[]> { new[] { "رقم البند", "التاريخ", "نوع الأعمال", "اسم المورد", "الوحدة", "السعر بالجنيه", "الكمية", "إجمالي تكلفة الأعمال", "تصنيف الحساب", "الحالة", "المرجع", "الوصف" } };
        rows.AddRange(movements.Select((item, index) => new[]
        {
            (index + 1).ToString(CultureInfo.InvariantCulture), item.Date.ToString("yyyy-MM-dd"),
            string.IsNullOrWhiteSpace(item.Type) ? item.Description : item.Type, item.SupplierName, item.Unit,
            item.UnitPrice?.ToString(CultureInfo.InvariantCulture) ?? "",
            item.Quantity?.ToString(CultureInfo.InvariantCulture) ?? "",
            N(item.Amount), item.ExpenseCategory, item.Status, item.Reference, item.Description
        }));
        return Sheet(rows, numericColumns: new HashSet<int> { 0, 5, 6, 7 });
    }

    private static XDocument AdvancesSheet(IReadOnlyList<ProjectAdvanceRow> advances)
    {
        var categories = new[] { "الموقع", "العرب", "المقاول", "المحطة", "الحديد", "المباني", "المحارة", "العزل", "الكهرباء", "السباكة", "المكتب", "غير محدد", "باقي المصنعيات" };
        var rows = new List<string[]> { new[] { "التاريخ", "العميل" }.Concat(categories).Concat(new[] { "إجمالي الدفعة", "الموزع", "المتبقي", "الدافع", "الحالة", "المرجع" }).ToArray() };
        rows.AddRange(advances.Select(item => new[] { item.Date.ToString("yyyy-MM-dd"), item.ClientName }.Concat(categories.Select(category => category == "باقي المصنعيات" ? N(OtherAdvanceCategories(item.AllocationsByCategory)) : N(item.AllocationsByCategory.TryGetValue(category, out var value) ? value : 0m))).Concat(new[] { N(item.Amount), N(item.Allocated), N(item.Remaining), item.PayerName, item.Status, item.Reference }).ToArray()));
        return Sheet(rows);
    }

    private static decimal OtherAdvanceCategories(IReadOnlyDictionary<string, decimal> values) => values.Where(item => item.Key is not ("الموقع" or "العرب" or "المقاول" or "المحطة" or "الحديد" or "المباني" or "المحارة" or "العزل" or "الكهرباء" or "السباكة" or "المكتب" or "غير محدد")).Sum(item => item.Value);

    private static XDocument SuppliesSheet(IReadOnlyList<ProjectSupplierSupplyRow> supplies)
    {
        var rows = new List<string[]> { new[] { "التاريخ", "المورد", "نوع التوريد", "الصنف", "الوحدة", "الكمية", "سعر الوحدة", "الإجمالي", "رقم الفاتورة" } };
        rows.AddRange(supplies.Select(item => new[] { item.Date.ToString("yyyy-MM-dd"), item.SupplierName, item.SupplyType, item.ItemName, item.Unit, item.Quantity.ToString(CultureInfo.InvariantCulture), N(item.UnitPrice), N(item.TotalAmount), item.InvoiceNumber }));
        return Sheet(rows, numericColumns: new HashSet<int> { 5, 6, 7 });
    }

    private static XDocument PaymentsSheet(IReadOnlyList<ProjectSupplierPaymentRow> payments)
    {
        var rows = new List<string[]> { new[] { "التاريخ", "المورد", "المبلغ", "طريقة الدفع", "المرجع" } };
        rows.AddRange(payments.Select(item => new[] { item.Date.ToString("yyyy-MM-dd"), item.SupplierName, N(item.Amount), item.Method, item.Reference }));
        return Sheet(rows, numericColumns: new HashSet<int> { 2 });
    }

    private static XDocument AuditSheet(OperationalAuditSummary? audit)
    {
        var rows = new List<string[]> { new[] { "بند التدقيق", "النتيجة", "الفارق", "الحالة" } };
        if (audit is not null)
            rows.AddRange(audit.Checks.Select(item => new[] { item.Name, item.Message, N(item.Difference), item.IsValid ? "سليم" : "يحتاج مراجعة" }));
        return Sheet(rows);
    }

    private static XDocument ContentTypes(int count)
    {
        var xml = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>";
        for (var i = 1; i <= count; i++) xml += $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>";
        return Xml(xml + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
    }

    private static XDocument Workbook() => Xml("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"ملخص\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"حركة الموقع\" sheetId=\"2\" r:id=\"rId2\"/><sheet name=\"دفعات تحت الحساب\" sheetId=\"3\" r:id=\"rId3\"/><sheet name=\"توريدات الموردين\" sheetId=\"4\" r:id=\"rId4\"/><sheet name=\"مدفوعات الموردين\" sheetId=\"5\" r:id=\"rId5\"/><sheet name=\"التدقيق\" sheetId=\"6\" r:id=\"rId6\"/></sheets></workbook>");

    private static XDocument WorkbookRelationships(int count)
    {
        var xml = "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">";
        for (var i = 1; i <= count; i++) xml += $"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>";
        return Xml(xml + $"<Relationship Id=\"rId{count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
    }

    private static XDocument Sheet(IReadOnlyList<string[]> rows, bool summary = false, IReadOnlySet<int>? numericColumns = null)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheetData = new XElement(ns + "sheetData");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = new XElement(ns + "row", new XAttribute("r", rowIndex + 1));
            for (var col = 0; col < rows[rowIndex].Length; col++)
            {
                var address = $"{Column(col)}{rowIndex + 1}";
                var value = rows[rowIndex][col] ?? string.Empty;
                var cell = rowIndex > 0 && numericColumns?.Contains(col) == true
                    ? new XElement(ns + "c", new XAttribute("r", address), value.Length == 0 ? null : new XElement(ns + "v", value))
                    : new XElement(ns + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"),
                        new XElement(ns + "is", new XElement(ns + "t", value)));
                var style = rowIndex == 0 ? 1 : 0;
                if (summary && rows[rowIndex].Length > 0)
                {
                    var label = rows[rowIndex][0] ?? string.Empty;
                    style = label is "صادر" ? 2 : label is "وارد" ? 3 : label.Contains("مكتب") || label.Contains("المكتب") ? 4 : label is "البند" ? 1 : style;
                }
                if (style > 0) cell.SetAttributeValue("s", style.ToString(CultureInfo.InvariantCulture));
                row.Add(cell);
            }
            sheetData.Add(row);
        }
        var maxColumns = rows.Count == 0 ? 1 : Math.Max(1, rows.Max(row => row.Length));
        var cols = new XElement(ns + "cols", Enumerable.Range(1, maxColumns).Select(index => new XElement(ns + "col", new XAttribute("min", index), new XAttribute("max", index), new XAttribute("width", index == 1 ? 28 : 16), new XAttribute("customWidth", 1))));
        var views = new XElement(ns + "sheetViews", new XElement(ns + "sheetView", new XAttribute("workbookViewId", 0), new XAttribute("rightToLeft", 1)));
        return new XDocument(new XElement(ns + "worksheet", new XAttribute(XNamespace.Xmlns + "r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"), views, cols, sheetData));
    }

    private static XDocument Styles() => Xml("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"3\"><font><sz val=\"11\"/><name val=\"Arial\"/></font><font><b/><sz val=\"11\"/><name val=\"Arial\"/></font><font><b/><sz val=\"12\"/><color rgb=\"FFFFFFFF\"/><name val=\"Arial\"/></font></fonts><fills count=\"5\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"D9EAF7\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"1F4E78\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"E2F0D9\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF2CC\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style=\"thin\"/><right style=\"thin\"/><top style=\"thin\"/><bottom style=\"thin\"/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"5\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"1\" borderId=\"1\"/><xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\"/><xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"1\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"4\" borderId=\"1\"/></cellXfs></styleSheet>");

    private static string Column(int index) { var result = string.Empty; for (var n = index + 1; n > 0; n = (n - 1) / 26) result = (char)('A' + (n - 1) % 26) + result; return result; }
    private static string N(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static XDocument Xml(string xml) => XDocument.Parse(xml);
}

public sealed class XlsxExportCapacityException(string sheet, int actual, int capacity) : Exception($"تجاوز تصدير Excel سعة صفحة «{sheet}»: تم العثور على {actual} صفوف بينما السعة الآمنة {capacity}.")
{
    public string Sheet { get; } = sheet;
    public int Actual { get; } = actual;
    public int Capacity { get; } = capacity;
}
