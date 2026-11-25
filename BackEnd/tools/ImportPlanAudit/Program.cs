using EMG.Api.Modules.Imports.Services;
using Microsoft.AspNetCore.Http;

var requestedPath = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
var root = requestedPath ?? throw new ArgumentException("Supply a workbook directory.");
var files = Directory.EnumerateFiles(root, "*.xlsx")
    .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToList();
var service = new WorkbookImportService();
var totalSheets = 0;
var lowConfidence = 0;
var dataGaps = 0;
Console.WriteLine($"FILES|{files.Count}");
foreach (var path in files)
{
    try
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var form = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(path)) { Headers = new HeaderDictionary(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" };
        var plan = await service.BuildNewProjectPlanAsync(form, CancellationToken.None);
        if (plan is null) { Console.WriteLine($"PLAN_ERROR|{Path.GetFileName(path)}"); continue; }
        totalSheets += plan.Sheets.Count;
        var low = plan.Sheets.Where(sheet => sheet.WillCreateData && sheet.Confidence is "منخفضة" or "تحتاج مراجعة").ToList();
        var gaps = plan.Sheets.Where(IsBlockingZeroCandidateSheet).ToList();
        lowConfidence += low.Count;
        dataGaps += gaps.Count;
        Console.WriteLine($"PLAN|{Path.GetFileName(path)}|sheets={plan.Sheets.Count}|low={low.Count}|gaps={gaps.Count}|warnings={plan.Warnings.Count}");
        foreach (var sheet in low.Concat(gaps).DistinctBy(item => item.Order))
            Console.WriteLine($"ISSUE|{Path.GetFileName(path)}|{sheet.OriginalName}|{sheet.Classification}|{sheet.SourceRows}/{sheet.CandidateRows}|{sheet.Confidence}|{string.Join(";", sheet.Diagnostics ?? [])}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR|{Path.GetFileName(path)}|{ex.GetType().Name}|{ex.Message}");
    }
}
Console.WriteLine($"SUMMARY|files={files.Count}|sheets={totalSheets}|low={lowConfidence}|gaps={dataGaps}");

static bool IsBlockingZeroCandidateSheet(EMG.Api.Modules.Imports.Dtos.WorkbookSheetPlan sheet) =>
    sheet.Recognized &&
    sheet.WillCreateData &&
    sheet.SourceRows > 0 &&
    sheet.CandidateRows == 0 &&
    !string.Equals(sheet.Confidence, "عالية - قالب فارغ", StringComparison.Ordinal);
