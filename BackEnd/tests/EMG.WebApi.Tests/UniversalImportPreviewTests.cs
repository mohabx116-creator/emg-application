using EMG.Api.Modules.UniversalImports;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using ClosedXML.Excel;

namespace EMG.WebApi.Tests;

public sealed class UniversalImportPreviewTests
{
    private static string SourceDirectory => Environment.GetEnvironmentVariable("EMG_UNIVERSAL_IMPORT_SOURCE_DIR")
        ?? throw new InvalidOperationException("Set EMG_UNIVERSAL_IMPORT_SOURCE_DIR to the approved read-only workbook directory.");

    [Fact]
    public void RegistryContainsAllEightAuditedFamiliesAndRequiresMappingForUnknown()
    {
        var registry = new UniversalImportMappingRegistry();
        Assert.Equal(8, registry.Definitions.Count);
        Assert.Equal(8, registry.Definitions.Select(item => item.FamilyKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Null(registry.Resolve("unclassified", "no known business evidence"));
    }

    [Fact]
    public async Task ExpenseColumnsInAnUnclassifiedSheetRequireManualConfirmation()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Daily records");
        sheet.Cell("A1").Value = "Date";
        sheet.Cell("B1").Value = "Description";
        sheet.Cell("C1").Value = "Expense amount";
        sheet.Cell("D1").Value = "Currency";
        sheet.Cell("A2").Value = "2025-01-15";
        sheet.Cell("B2").Value = "QA expense";
        sheet.Cell("C2").Value = 123.45;
        sheet.Cell("D2").Value = "EGP";
        sheet.Cell("A3").Value = "2025-01-15";
        sheet.Cell("B3").Value = "QA original currency";
        sheet.Cell("C3").Value = 70;
        sheet.Cell("D3").Value = "USD";
        using var input = new MemoryStream();
        workbook.SaveAs(input);
        var upload = new FormFile(input, 0, input.Length, "file", "qa-expense.xlsx");
        var service = new UniversalImportPreviewService(new UniversalImportMappingRegistry());

        var preview = await service.PreviewAsync(upload, CancellationToken.None);

        Assert.True(preview.RequiresMapping);
        var local = Assert.Single(preview.Records, row => row.SourceCoordinate == "Daily records!C2");
        Assert.Equal(123.45m, local.Amount);
        Assert.Equal("SiteMovement", local.OperationalEvidence?.Target);
        Assert.True(local.OperationalEvidence?.RequiresManualMapping);
        Assert.False(local.EligibleForPosting);
        var foreign = Assert.Single(preview.Records, row => row.SourceCoordinate == "Daily records!C3");
        Assert.Equal("USD", foreign.Currency);
        Assert.False(foreign.EligibleForPosting);
    }

    [Fact]
    public async Task FormulaBackedOperationalRowsMapWhenCachedFormulaValuesAreAbsent()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("خاص باعمال الموقع");
        sheet.Cell("A1").Value = "رقم القطعة: 315";
        sheet.Cell("A9").Value = "رقم البند";
        sheet.Cell("B9").Value = "التاريخ";
        sheet.Cell("C9").Value = "نوع الاعمال";
        sheet.Cell("D9").Value = "اسم المورد";
        sheet.Cell("E9").Value = "الوحدة";
        sheet.Cell("F9").Value = "السعر بالجنية";
        sheet.Cell("G9").Value = "مقدار الاعمال السابق";
        sheet.Cell("H9").Value = "خلال الفترة";
        sheet.Cell("I9").Value = "مجموع مقدار لاعمال";
        sheet.Cell("J9").Value = "اجمالي تكلفة العمل الاعمال";
        sheet.Cell("K9").Value = "ملاحظات";
        sheet.Cell("A10").Value = 1;
        sheet.Cell("B10").Value = new DateTime(2026, 6, 18);
        sheet.Cell("C10").Value = "لودر QA";
        sheet.Cell("E10").Value = "بالساعه";
        sheet.Cell("F10").Value = 450;
        sheet.Cell("H10").Value = 9;
        sheet.Cell("I10").FormulaA1 = "=H10+G10";
        sheet.Cell("J10").FormulaA1 = "=I10*F10";
        sheet.Cell("A11").Value = 2;
        sheet.Cell("B11").Value = new DateTime(2026, 6, 19);
        sheet.Cell("C11").Value = "خصم اختبار";
        sheet.Cell("E11").Value = "بالجنيه";
        sheet.Cell("F11").Value = 100;
        sheet.Cell("G11").Value = 0;
        sheet.Cell("H11").Value = -1;
        sheet.Cell("I11").FormulaA1 = "=H11+G11";
        sheet.Cell("J11").FormulaA1 = "=I11*F11";
        sheet.Cell("K7").Value = 3950m;
        using var input = new MemoryStream();
        workbook.SaveAs(input);
        input.Position = 0;
        var upload = new FormFile(input, 0, input.Length, "file", "formula-cache-absent.xlsx");
        var service = new UniversalImportPreviewService(new UniversalImportMappingRegistry());

        var preview = await service.PreviewAsync(upload, CancellationToken.None);

        var rows = preview.Records.Where(item => item.SheetName == "خاص باعمال الموقع" && item.OperationalEvidence is not null).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, item => item.SourceCoordinate == "خاص باعمال الموقع!J10" && item.Amount == 4050m && item.EligibleForPosting);
        Assert.Contains(rows, item => item.SourceCoordinate == "خاص باعمال الموقع!J11" && item.Amount == -100m && item.RecordType == UniversalRecordType.Adjustment);
    }

    [Fact]
    public async Task Q315SeparatesTheThreeParallelFinancialStreams()
    {
        var path = Path.Combine(SourceDirectory, "للصناعات ق315 منطقه الصناعيه ال710 اسلام عدوي.xlsx");
        Assert.True(File.Exists(path), $"Missing approved Q315 workbook: {path}");
        var result = await Preview(path);

        Assert.Equal("7FEF67459E1DF1910EAF9657EFDE376C59CCD2BF6BF13DBBE0A41B6611A353D4", result.SourceSha256);
        var sections = result.Sections.Where(item => item.SheetName == "دفعات تحت الحساب").ToList();
        Assert.Equal(4, sections.Count(item => item.StreamKey is "allocation_ledger" or "main_payment_ledger" or "unallocated_multicurrency_ledger"));
        Assert.Contains(sections, item => item.StreamKey == "allocation_ledger");
        Assert.Contains(sections, item => item.StreamKey == "main_payment_ledger");
        Assert.Contains(sections, item => item.StreamKey == "unallocated_multicurrency_ledger");
        Assert.Contains(sections, item => item.StreamKey == "unallocated_multicurrency_ledger" && item.SourceRange == "N5:N149");
        Assert.Contains(sections, item => item.StreamKey == "unallocated_multicurrency_ledger" && item.SourceRange == "O5:O149");
        Assert.Contains(result.Totals, item => item.StreamKey == "allocation_ledger" && item.Currency == "EGP" && item.Amount == 9936400m);
        Assert.Contains(result.Totals, item => item.StreamKey == "main_payment_ledger" && item.Currency == "EGP" && item.Amount == 10300020m);
        Assert.Contains(result.Reconciliations, item => item.StreamKey == "unallocated_multicurrency_ledger" && item.Currency == "EGP" && item.SourceSummaryAmount == 7985420m && item.Status == "MATCHED");
        Assert.Contains(result.Reconciliations, item => item.StreamKey == "unallocated_multicurrency_ledger" && item.Currency == "USD" && item.SourceSummaryAmount == 18056m && item.Status == "MATCHED");
        Assert.Contains(result.Totals, item => item.Currency == "EGP");
        Assert.Contains(result.Totals, item => item.Currency == "USD");
        Assert.Contains(result.Records, item => item.SourceCoordinate == "دفعات تحت الحساب!K17" &&
            item.RecordType == UniversalRecordType.Adjustment && item.Amount == -2610m && !item.EligibleForPosting);
        Assert.DoesNotContain(result.Records, item => item.IsSummary && item.EligibleForPosting);
        Assert.DoesNotContain(result.Exceptions, item => item.Contains("allocation", StringComparison.OrdinalIgnoreCase) && item.Contains("payment", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Q315OperationalRowsAndSignedAdjustmentsReconcileTheirSourceControls()
    {
        var path = Path.Combine(SourceDirectory, "للصناعات ق315 منطقه الصناعيه ال710 اسلام عدوي.xlsx");
        var result = await Preview(path);

        Assert.Equal("7FEF67459E1DF1910EAF9657EFDE376C59CCD2BF6BF13DBBE0A41B6611A353D4", result.SourceSha256);

        Assert.Contains(result.Records, item => item.SourceCoordinate == "خاص باعمال الموقع!J123" &&
            item.RecordType == UniversalRecordType.Expense && item.Amount == 4050m &&
            item.OperationalEvidence?.Target == "SiteMovement" && item.EligibleForPosting);
        Assert.Contains(result.Records, item => item.SourceCoordinate == "خاص باعمال الموقع!J267" &&
            item.RecordType == UniversalRecordType.Adjustment && item.Amount == -400m && item.EligibleForPosting);
        Assert.Contains(result.Records, item => item.SourceCoordinate == "خاص باعمال الموقع!J300" &&
            item.RecordType == UniversalRecordType.Adjustment && item.Amount == -200m && item.EligibleForPosting);
        Assert.Contains(result.Records, item => item.SourceCoordinate == "خاص باعمال الموقع!J303" &&
            item.RecordType == UniversalRecordType.Adjustment && item.Amount == -1350m && item.EligibleForPosting);

        var lateSiteRows = result.Records.Where(item => item.SourceCoordinate is
            "خاص باعمال الموقع!J414" or "خاص باعمال الموقع!J415" or "خاص باعمال الموقع!J416" or
            "خاص باعمال الموقع!J417" or "خاص باعمال الموقع!J418").ToList();
        Assert.Equal(5, lateSiteRows.Count);
        Assert.Equal(3400m, lateSiteRows.Sum(item => item.Amount ?? 0m));

        var buildingAdjustments = result.Records.Where(item => item.SourceCoordinate is
            "حساب المباني!J14" or "حساب المباني!J15" or "حساب المباني!J16").ToList();
        Assert.Equal(3, buildingAdjustments.Count);
        Assert.All(buildingAdjustments, item => Assert.Equal(UniversalRecordType.Adjustment, item.RecordType));
        Assert.Equal(-565m, buildingAdjustments.Sum(item => item.Amount ?? 0m));

        var site = Assert.Single(result.Reconciliations, item => item.EvidenceCoordinate == "خاص باعمال الموقع!K7");
        Assert.Equal(2949413m, site.PreviewAmount);
        Assert.Equal(2949413m, site.SourceSummaryAmount);
        Assert.Equal(0m, site.Difference);
        Assert.Equal("MATCHED", site.Status);

        var building = Assert.Single(result.Reconciliations, item => item.EvidenceCoordinate == "حساب المباني!K7");
        Assert.Equal(155385m, building.PreviewAmount);
        Assert.Equal(155385m, building.SourceSummaryAmount);
        Assert.Equal(0m, building.Difference);
        Assert.Equal("MATCHED", building.Status);
    }

    [Fact]
    public void Q315FinalAccountFormulaUsesTheDeclaredComponentRange()
    {
        var path = Path.Combine(SourceDirectory, "للصناعات ق315 منطقه الصناعيه ال710 اسلام عدوي.xlsx");
        using var source = OpenSource(path);
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        copy.Position = 0;
        using var workbook = new XLWorkbook(copy);
        var sheet = workbook.Worksheet("كشف حساب نهائي");

        Assert.Equal("SUM(E9:I9)", sheet.Cell("J9").FormulaA1.Replace("$", "", StringComparison.Ordinal));
        Assert.Equal(10951183.45, sheet.Cell("J9").CachedValue.GetNumber(), 2);
    }

    [Fact]
    public async Task FormulaBackedOperationalLabelsAndAllocationsRetainEvidenceInProject191()
    {
        var result = await Preview(Path.Combine(SourceDirectory, "191.xlsx"));

        Assert.Contains(result.Records, item => item.SourceCoordinate is
            "خاص باعمال الموقع!J45" or "خاص باعمال الموقع!J46" or
            "خاص باعمال الموقع!J47" or "خاص باعمال الموقع!J49");
        var site = result.Reconciliations.Single(item => item.EvidenceCoordinate == "خاص باعمال الموقع!K7");
        Assert.Equal(217175m, site.PreviewAmount);
        Assert.Equal(217175m, site.SourceSummaryAmount);
        Assert.Equal("MATCHED", site.Status);

        var allocation = Assert.Single(result.Records, item => item.SourceCoordinate == "دفعات تحت الحساب!I11");
        Assert.Equal(UniversalRecordType.Allocation, allocation.RecordType);
        Assert.Equal(26800m, allocation.Amount);
        Assert.True(allocation.IsFormulaDerived);
        Assert.False(allocation.EligibleForPosting);
        var allocations = result.Reconciliations.Single(item => item.StreamKey == "allocation_ledger" && item.Currency == "EGP");
        Assert.Equal(3492302m, allocations.PreviewAmount);
        Assert.Equal(3492302m, allocations.SourceSummaryAmount);
        Assert.Equal("MATCHED", allocations.Status);
    }

    [Fact]
    public async Task Project191InsulationUsesFinishingStreamAndKeepsPriorPaymentAsEvidence()
    {
        var result = await Preview(Path.Combine(SourceDirectory, "191.xlsx"));
        var rows = result.Records.Where(item => item.SheetName == "اعمال العزل" && item.OperationalEvidence is not null).ToList();

        Assert.Contains(result.Sections, item => item.SheetName == "اعمال العزل" && item.SourceRange == "A5:K45" && item.MappingFamily == "F05_FINISHING");
        Assert.Equal(2, rows.Count);
        Assert.Equal(64600m, rows.Sum(item => item.Amount ?? 0m));
        Assert.Contains(rows, item => item.SourceCoordinate == "اعمال العزل!J10" && item.Amount == 37800m && item.OperationalEvidence!.Target == "FinishingRecord");
        Assert.Contains(rows, item => item.SourceCoordinate == "اعمال العزل!J11" && item.Amount == 26800m && item.OperationalEvidence!.Target == "FinishingRecord");
        Assert.DoesNotContain(result.Records, item => item.SheetName == "اعمال العزل" && item.SourceCoordinate.Contains("K10", StringComparison.Ordinal));
        var reconciliation = Assert.Single(result.Reconciliations, item => item.EvidenceCoordinate == "اعمال العزل!K7");
        Assert.Equal(64600m, reconciliation.PreviewAmount);
        Assert.Equal(64600m, reconciliation.SourceSummaryAmount);
        Assert.Equal("MATCHED", reconciliation.Status);
    }

    [Fact]
    public async Task AllFortyApprovedWorkbooksProduceDeterministicPreviews()
    {
        var files = CorpusFiles().OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(40, files.Count);
        var service = new UniversalImportPreviewService(new UniversalImportMappingRegistry());
        var resultDirectory = Environment.GetEnvironmentVariable("EMG_UNIVERSAL_IMPORT_RESULTS_DIR");
        if (!string.IsNullOrWhiteSpace(resultDirectory)) Directory.CreateDirectory(resultDirectory);
        var report = new List<string> { "file,sha256,sections,records,unresolved,streams,duplicates,operational_candidates,coverage" };
        var exceptions = new List<string> { "file,sha256,sheet,source_coordinate,family,record_type,currency,requirement" };
        foreach (var path in files)
        {
            await using var stream = OpenSource(path);
            var file = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(path));
            var result = await service.PreviewAsync(file, CancellationToken.None);
            Assert.False(string.IsNullOrWhiteSpace(result.SourceSha256));
            Assert.Equal(UniversalImportMappingRegistry.Version, result.EngineVersion);
            Assert.Equal(result.Records.Count, result.Records.Select(item => item.RecordId).Distinct(StringComparer.Ordinal).Count());
            var duplicateIds = result.DuplicateCandidates.SelectMany(item => item.RecordIds).ToHashSet();
            var candidates = result.Records.Count(item => item.EligibleForPosting && !duplicateIds.Contains(item.RecordId));
            var pending = result.Records.Count(item => !item.IsSummary && (!item.EligibleForPosting || duplicateIds.Contains(item.RecordId)));
            var coverage = pending == 0 && candidates > 0 ? "FULLY_SUPPORTED_PREVIEW" :
                candidates > 0 ? "PARTIALLY_SUPPORTED_PREVIEW" : "REQUIRES_SOURCE_CLARIFICATION";
            report.Add(string.Join(',', Csv(Path.GetFileName(path)), result.SourceSha256, result.Sections.Count,
                result.Records.Count, result.Records.Count(item => item.RecordType == UniversalRecordType.Unresolved),
                result.Totals.Count, result.DuplicateCandidates.Count, candidates, coverage));
            var sections = result.Sections.ToDictionary(item => item.SectionId);
            exceptions.AddRange(result.Records.Where(item => !item.IsSummary && (!item.EligibleForPosting || duplicateIds.Contains(item.RecordId)))
                .Select(item => string.Join(',', Csv(Path.GetFileName(path)), result.SourceSha256, Csv(item.SheetName),
                    Csv(item.SourceCoordinate), sections[item.SectionId].MappingFamily, item.RecordType, item.Currency,
                    duplicateIds.Contains(item.RecordId) ? "DISTINGUISH_DUPLICATE" : item.Currency != "EGP" ? "SOURCE_CURRENCY_ONLY" :
                    item.StreamKey == "allocation_ledger" ? "VERIFIED_SETTLEMENT_LINK_REQUIRED" : "EXPLICIT_OPERATIONAL_FIELDS_OR_ACCOUNTING_LINK_REQUIRED")));
        }
        if (!string.IsNullOrWhiteSpace(resultDirectory))
        {
            await File.WriteAllLinesAsync(Path.Combine(resultDirectory, "universal-preview-golden-results.csv"), report);
            await File.WriteAllLinesAsync(Path.Combine(resultDirectory, "universal-preview-source-exceptions.csv"), exceptions);
        }
    }

    [Fact]
    public async Task RepresentativeWorkbookPerformanceIsRecordedWithoutDatabaseAccess()
    {
        var files = CorpusFiles().OrderBy(item => new FileInfo(item).Length).ToList();
        Assert.Equal(40, files.Count);
        var selected = new[] { files[0], files[files.Count / 2], files[^1] };
        var service = new UniversalImportPreviewService(new UniversalImportMappingRegistry());
        var lines = new List<string> { "size_class,file,size_bytes,elapsed_ms,managed_memory_delta,sections,records" };
        foreach (var path in selected.Select((item, index) => (item, index)))
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var before = GC.GetTotalMemory(true);
            var clock = Stopwatch.StartNew();
            await using var stream = OpenSource(path.item);
            var formFile = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(path.item));
            var result = await service.PreviewAsync(formFile, CancellationToken.None);
            clock.Stop();
            var after = GC.GetTotalMemory(false);
            lines.Add(string.Join(',', path.index == 0 ? "small" : path.index == 1 ? "medium" : "large",
                Csv(Path.GetFileName(path.item)), stream.Length, clock.ElapsedMilliseconds, after - before, result.Sections.Count, result.Records.Count));
        }
        var output = Environment.GetEnvironmentVariable("EMG_UNIVERSAL_IMPORT_RESULTS_DIR");
        if (!string.IsNullOrWhiteSpace(output)) await File.WriteAllLinesAsync(Path.Combine(output, "universal-import-performance.csv"), lines);
    }

    private static async Task<UniversalPreviewResponse> Preview(string path)
    {
        var service = new UniversalImportPreviewService(new UniversalImportMappingRegistry());
        await using var stream = OpenSource(path);
        var file = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(path));
        return await service.PreviewAsync(file, CancellationToken.None);
    }

    private static IEnumerable<string> CorpusFiles() => Directory.EnumerateFiles(SourceDirectory, "*.xlsx", SearchOption.TopDirectoryOnly)
        .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal));

    private static FileStream OpenSource(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
