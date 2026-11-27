using System.IO.Compression;
using System.Globalization;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Text;
using ClosedXML.Excel;
using EMG.Api.Modules.Imports.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Imports.Services;

public sealed partial class WorkbookImportService : IWorkbookImportService
{
    private readonly EmgDbContext? db;
    private readonly IWebHostEnvironment? environment;

    public WorkbookImportService() { }
    public WorkbookImportService(EmgDbContext db, IWebHostEnvironment environment)
    {
        this.db = db;
        this.environment = environment;
    }
    private const long MaxWorkbookBytes = 20 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> KnownSheets = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["اعمال الحفر"] = "الحفر",
        ["اعمال الخرسانات"] = "الخرسانات",
        ["حساب الخرسانات"] = "الخرسانات",
        ["اعمال توريد الحديد"] = "الحديد",
        ["اعمال توريد الحديد اولاد شرف"] = "الحديد",
        ["اعمال توريد الحديد الغانم"] = "الحديد",
        ["توريد الحديد اولاد شرف"] = "الحديد",
        ["اعمال الموقع"] = "أعمال الموقع",
        ["خاص باعمال الموقع"] = "أعمال الموقع",
        ["دفعات تحت الحساب"] = "دفعات تحت الحساب",
        ["كشف حساب نهائي"] = "كشف الحساب النهائي",
        ["اعمال المباني"] = "المباني",
        ["حساب المباني"] = "المباني",
        ["حساب البنا"] = "المباني",
        ["حساب الطوب"] = "المباني",
        ["اعمال المحاره"] = "المحارة",
        ["المحاره"] = "المحارة",
        ["حساب المحاره"] = "المحارة",
        ["اعمال العزل"] = "العزل",
        ["اعمال الكهرباء"] = "الكهرباء",
        ["اعمال السباكه"] = "السباكة",
        ["تقارير"] = "مرفقات/تقارير",
        ["مرفقات"] = "مرفقات/تقارير",
        ["النثريات"] = "أعمال أخرى",
        ["اعمال الشيخ امين"] = "أعمال أخرى",
        ["اعمال الشيخ عمر"] = "أعمال أخرى",
        ["اعمال الدبش"] = "أعمال أخرى",
        ["اعمال الهليكوبتر"] = "أعمال أخرى",
        ["اعمال الاسمنت"] = "أعمال أخرى"
    };

    public async Task<WorkbookPreview> PreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        var warnings = new List<string>();
        var fileName = Path.GetFileName(file.FileName);
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Invalid(fileName, file.Length, "الملف يجب أن يكون بصيغة XLSX فقط.");
        if (file.Length <= 0 || file.Length > MaxWorkbookBytes)
            return Invalid(fileName, file.Length, "حجم الملف يجب أن يكون أكبر من صفر وأقل من 20 ميجابايت.");

        await using var input = file.OpenReadStream();
        await using var memory = new MemoryStream();
        await input.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        try
        {
            using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            if (workbookEntry is null) return Invalid(fileName, file.Length, "الملف لا يحتوي على Workbook صالح.");
            using var reader = new StreamReader(workbookEntry.Open());
            var workbook = XDocument.Parse(await reader.ReadToEndAsync(cancellationToken));
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheets = workbook.Root?.Element(main + "sheets")?.Elements(main + "sheet")
                .Select((sheet, index) =>
                {
                    var original = sheet.Attribute("name")?.Value?.Trim() ?? string.Empty;
                    var normalized = Normalize(original);
                    var isKnown = KnownSheets.TryGetValue(normalized, out var classification);
                    classification ??= ClassifyUnknown(normalized);
                    return new WorkbookSheetPreview(index + 1, original, classification, isKnown, isKnown && classification != "مرفقات/تقارير");
                }).ToList() ?? [];

            // Reclassify from the actual worksheet contents.  Workbook names
            // such as Sheet1 are not business metadata and are frequently
            // used for real concrete, excavation, or summary pages in the
            // live archive.
            XLWorkbook? contentWorkbook = null;
            try
            {
                memory.Position = 0;
                contentWorkbook = new XLWorkbook(memory);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Minimal XLSX files may expose metadata only. Building a plan still requires a readable workbook.
            }
            using (contentWorkbook)
            {
                if (contentWorkbook is not null)
                {
                    sheets = sheets.Select(sheet =>
                    {
                        var worksheet = contentWorkbook.Worksheets.FirstOrDefault(item => item.Name == sheet.OriginalName)
                            ?? contentWorkbook.Worksheets.FirstOrDefault(item => Normalize(item.Name) == Normalize(sheet.OriginalName));
                        if (worksheet is null) return sheet;
                        var classification = ClassifySheet(worksheet);
                        var recognized = classification != "صفحة غير معنونة";
                        var isSourceData = recognized && classification is not ("كشف الحساب النهائي" or "مرفقات/تقارير" or "ملخص مشروع" or "مستخلص");
                        return sheet with { Classification = classification, Recognized = recognized, IsSourceData = isSourceData };
                    }).ToList();
                    var currencyIssue = LegacyCurrencyIssue(contentWorkbook, sheets);
                    if (currencyIssue is not null) return Invalid(fileName, file.Length, currencyIssue);
                }
            }

            if (sheets.Count == 0) warnings.Add("لم يتم العثور على صفحات داخل الملف.");
            var unknown = sheets.Where(item => !item.Recognized).Select(item => item.OriginalName).Distinct(StringComparer.Ordinal).ToList();
            if (unknown.Count > 0) warnings.Add($"صفحات تحتاج مراجعة يدوية: {string.Join("، ", unknown)}.");
            var required = new[] { "أعمال الموقع", "دفعات تحت الحساب", "كشف الحساب النهائي" };
            var missing = required.Where(section => sheets.All(item => item.Classification != section)).ToList();
            if (missing.Count > 0) warnings.Add($"أقسام غير موجودة في الملف: {string.Join("، ", missing)}. لن يتم إنشاء بيانات افتراضية لها.");

            return new WorkbookPreview(fileName, file.Length, sheets.Count > 0, unknown.Count > 0 || missing.Count > 0, sheets, warnings);
        }
        catch (InvalidDataException)
        {
            return Invalid(fileName, file.Length, "الملف ليس أرشيف XLSX صالحًا أو تم تلفه.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Invalid(fileName, file.Length, "تعذر قراءة بنية ملف Excel بأمان.");
        }
    }

    public async Task<WorkbookMigrationPlan?> BuildNewProjectPlanAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var preview = await PreviewAsync(file, cancellationToken);
        if (!preview.IsValid) return null;

        var safeName = Path.GetFileNameWithoutExtension(preview.FileName).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "مشروع مستورد";
        var codeSuffix = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..8];
        await using var input = file.OpenReadStream();
        using var workbook = new XLWorkbook(input);
        var sheets = preview.Sheets.Select(sheet =>
        {
            var worksheet = workbook.Worksheets.FirstOrDefault(item => item.Name == sheet.OriginalName)
                ?? workbook.Worksheets.FirstOrDefault(item => Normalize(item.Name) == Normalize(sheet.OriginalName));
            var classification = worksheet is null ? sheet.Classification : ClassifySheet(worksheet);
            var recognized = sheet.Recognized || classification != "صفحة غير معنونة";
            var analysis = worksheet is null ? SheetAnalysis.Empty("الصفحة غير موجودة داخل الملف") : AnalyzeSheet(worksheet, classification);
            var referenceOnly = classification is "كشف الحساب النهائي" or "مرفقات/تقارير" or "ملخص مشروع" or "مستخلص";
            var willCreateData = !referenceOnly && (
                sheet.IsSourceData
                || classification == "دفعات تحت الحساب"
                || (recognized && classification is ("أعمال الموقع" or "الحفر" or "الخرسانات" or "الحديد" or "المباني" or "المحارة" or "العزل" or "الكهرباء" or "السباكة" or "أعمال أخرى")));
            return new WorkbookSheetPlan(
                sheet.Order, sheet.OriginalName, classification, recognized,
                willCreateData,
                analysis.SourceRows, analysis.CandidateRows, analysis.Profile, analysis.Confidence, analysis.Diagnostics);
        }).ToList();
        // Preview classifies by worksheet name only. The plan may reclassify
        // Sheet1/Sheet2 from their actual content, so rebuild the review warning
        // from the final content-based classifications instead of carrying a
        // stale "unknown sheet" warning into the UI.
        var warnings = preview.Warnings
            .Where(item => !item.StartsWith("صفحات تحتاج مراجعة يدوية:", StringComparison.Ordinal))
            .Where(item => !item.StartsWith("أقسام غير موجودة في الملف:", StringComparison.Ordinal))
            .Where(item => !item.StartsWith("يوجد أكثر من مصدر لنفس القسم:", StringComparison.Ordinal))
            .ToList();
        var unknownSheets = sheets.Where(item => !item.Recognized).Select(item => item.OriginalName).Distinct(StringComparer.Ordinal).ToList();
        if (unknownSheets.Count > 0)
            warnings.Add($"صفحات محفوظة كمصدر مرجعي وتحتاج تصنيفًا يدويًا قبل تحويلها إلى حركات: {string.Join("، ", unknownSheets)}.");
        var reviewSheets = sheets.Where(item => item.Recognized && item.WillCreateData && item.Confidence == "تحتاج مراجعة").Select(item => item.OriginalName).Distinct(StringComparer.Ordinal).ToList();
        if (reviewSheets.Count > 0)
            warnings.Add($"صفحات تحتاج مراجعة يدوية قبل الترحيل: {string.Join("، ", reviewSheets)}.");
        var required = new[] { "أعمال الموقع", "دفعات تحت الحساب", "كشف الحساب النهائي" };
        var missing = required.Where(section => sheets.All(item => item.Classification != section)).ToList();
        if (missing.Count > 0)
            warnings.Add($"أقسام غير موجودة في الملف: {string.Join("، ", missing)}. لن يتم إنشاء بيانات افتراضية لها.");
        // Never allow a data-bearing operational sheet to disappear silently.
        // A sheet may be empty by design, or may be a reference summary, but
        // once it contains meaningful source rows it must produce candidates
        // before the user can approve a new-project migration.
        var zeroCandidateDataSheets = sheets.Where(IsBlockingZeroCandidateSheet).ToList();
        if (zeroCandidateDataSheets.Count > 0)
            warnings.Add($"صفحات بها صفوف مستخدمة لكن لم يتم العثور على حركات مرشحة: {string.Join("، ", zeroCandidateDataSheets.Select(item => item.OriginalName))}. راجع تحليل الصفحة قبل الترحيل.");
        warnings.Add("هذه خطة مراجعة فقط. لم يتم إنشاء المشروع أو حفظ أي حركة.");
        return new WorkbookMigrationPlan(
            "create-new-project", safeName, $"IMP-{codeSuffix}", sheets, warnings,
            ExtractClient(workbook),
            ExtractLabeledValue(workbook, "اسم المالك"),
            ExtractLabeledValue(workbook, "الدافع") ?? ExtractLabeledValue(workbook, "الدفعات الرئيسية"),
            ExtractLabeledValue(workbook, "الموقع"));
    }

    public async Task<ImportedProjectResult?> CommitNewProjectAsync(IFormFile file, string? projectName, string? projectCode, string? clientName, string? location, string? engineerName, int? userId, CancellationToken cancellationToken)
    {
        var context = db ?? throw new InvalidOperationException("قاعدة البيانات غير متاحة لتنفيذ الترحيل.");
        var host = environment ?? throw new InvalidOperationException("بيئة التخزين غير متاحة لتنفيذ الترحيل.");
        var plan = await BuildNewProjectPlanAsync(file, cancellationToken);
        if (plan is null) return null;
        if (plan.Sheets.Any(item => item.Recognized && item.WillCreateData && item.Confidence == "تحتاج مراجعة")) return null;
        if (plan.Sheets.Any(IsBlockingZeroCandidateSheet)) return null;

        await using var input = file.OpenReadStream();
        await using var memory = new MemoryStream();
        await input.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        using var workbook = new XLWorkbook(memory);
        var warnings = new List<string>(plan.Warnings.Where(item => !item.Contains("هذه خطة مراجعة فقط", StringComparison.Ordinal)));
        var skippedSheets = plan.Sheets.Where(item => !item.Recognized).Select(item => item.OriginalName).ToList();
        if (skippedSheets.Count > 0 && !warnings.Any(item => item.StartsWith("صفحات محفوظة كمصدر مرجعي", StringComparison.Ordinal)))
            warnings.Add($"تم الاحتفاظ بالصفحات غير المعنونة داخل المرفق ولم تُنشأ منها حركات: {string.Join("، ", skippedSheets)}.");
        var sourceOnlySheets = plan.Sheets.Where(item => item.Recognized && !item.WillCreateData).Select(item => $"{item.OriginalName} ({item.Classification})").ToList();
        if (sourceOnlySheets.Count > 0)
            warnings.Add($"تم الاحتفاظ بصفحات الملخص/المستخلص كمصدر مرجعي داخل المرفق دون تحويلها إلى حركات تفصيلية: {string.Join("، ", sourceOnlySheets)}.");
        var sourceName = Path.GetFileName(file.FileName);
        var name = string.IsNullOrWhiteSpace(projectName) ? plan.SuggestedProjectName : projectName.Trim();
        var sourcePlotNumber = ExtractPlotNumber(workbook);
        // The generated IMP-* value is only a review-plan identifier.  If the
        // workbook contains an explicit plot number, it is the source of truth
        // for the project code and must win over the UI's suggested value.
        // This prevents the import identity from leaking into the accounting
        // report as the plot number.
        var requestedCode = NullIfEmpty(projectCode);
        var code = sourcePlotNumber
            ?? (IsGeneratedImportCode(requestedCode) ? null : requestedCode)
            ?? plan.SuggestedProjectCode;
        var existingProject = await context.Projects
            .AsNoTracking()
            .Where(item => item.Code == code)
            .Select(item => new { item.Id, item.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (existingProject is not null)
        {
            throw new InvalidOperationException(
                $"رقم القطعة «{code}» موجود بالفعل في المشروع «{existingProject.Name}» (رقم {existingProject.Id}). " +
                "لا يمكن إنشاء مشروع جديد بنفس رقم القطعة. استخدم «تحديث مشروع موجود» إذا كان الملف نسخة محدثة من نفس المشروع، أو غيّر رقم القطعة إذا كان مشروعًا مستقلًا.");
        }

        var project = new Project
        {
            Name = name,
            Code = code,
            ClientName = NullIfEmpty(clientName) ?? ExtractClient(workbook),
            OwnerName = ExtractLabeledValue(workbook, "اسم المالك"),
            PayerName = ExtractLabeledValue(workbook, "الدافع") ?? ExtractLabeledValue(workbook, "الدفعات الرئيسية"),
            Location = NullIfEmpty(location) ?? ExtractLabeledValue(workbook, "الموقع"),
            EngineerName = NullIfEmpty(engineerName),
            Status = "تحت المراجعة",
            CreatedAtUtc = DateTime.UtcNow
        };
        if (string.IsNullOrWhiteSpace(project.ClientName)) project.ClientName = ExtractClient(workbook);
        project.OfficeSharePercentage = Math.Clamp(ExtractOfficeShare(workbook), 0m, 100m);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            context.Projects.Add(project);
            await context.SaveChangesAsync(cancellationToken);

            var rows = ImportWorkbookRows(context, workbook, project, userId, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var uploadRoot = Path.Combine(host.ContentRootPath, "uploads", "project-imports");
            Directory.CreateDirectory(uploadRoot);
            var extension = Path.GetExtension(sourceName).ToLowerInvariant();
            var storedName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadRoot, storedName);
            await using (var stream = System.IO.File.Create(fullPath))
            {
                memory.Position = 0;
                await memory.CopyToAsync(stream, cancellationToken);
            }
            var attachment = new Attachment
            {
                EntityType = "project-import", EntityId = project.Id, OriginalFileName = sourceName,
                StoredFileName = storedName, RelativePath = Path.Combine("uploads", "project-imports", storedName),
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : file.ContentType,
                SizeInBytes = file.Length, UploadedByUserId = userId
            };
            context.Attachments.Add(attachment);
            context.SecurityAuditLogs.Add(new EMG.Domain.Security.SecurityAuditLog
            {
                UserId = userId, Action = "استيراد مشروع كامل", EntityType = "project-import", EntityId = project.Id,
                Details = $"تم ترحيل {sourceName}: {rows.Summary}.",
                CreatedAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return rows.Result(project, attachment, warnings);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<WorkbookMigrationPlan?> BuildExistingProjectPlanAsync(IFormFile file, int projectId, CancellationToken cancellationToken)
    {
        var context = db ?? throw new InvalidOperationException("قاعدة البيانات غير متاحة.");
        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null) return null;
        var plan = await BuildNewProjectPlanAsync(file, cancellationToken);
        return plan is null ? null : plan with
        {
            Mode = "update-existing-project",
            SuggestedProjectName = project.Name,
            SuggestedProjectCode = project.Code,
            Warnings = plan.Warnings.Append($"سيتم تحديث المشروع الحالي «{project.Name}» بالكود {project.Code}. لن يتم إنشاء مشروع مكرر.").ToList()
        };
    }

    public async Task<ImportedProjectResult?> CommitExistingProjectAsync(IFormFile file, int projectId, int? userId, CancellationToken cancellationToken)
    {
        var context = db ?? throw new InvalidOperationException("قاعدة البيانات غير متاحة لتنفيذ التحديث.");
        var host = environment ?? throw new InvalidOperationException("بيئة التخزين غير متاحة لتنفيذ التحديث.");
        var plan = await BuildExistingProjectPlanAsync(file, projectId, cancellationToken);
        // The plan is a review surface, not a second parser.  A workbook can
        // legitimately contain a known operational sheet whose visual layout
        // makes the preview confidence conservative while the section importer
        // can still read its rows safely.  Do not reject the whole atomic
        // update on that warning; unknown sheets remain reference-only and
        // imported sections are still committed in one transaction below.
        if (plan is null) return null;

        await using var input = file.OpenReadStream();
        await using var memory = new MemoryStream();
        await input.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        using var workbook = new XLWorkbook(memory);
        var sourceName = Path.GetFileName(file.FileName);
        // Updating an existing project must refresh the canonical identity
        // from the workbook as well. Otherwise a project imported before the
        // identity rules were fixed keeps the temporary IMP-* code or the
        // wrong client name forever, and every report remains wrong even when
        // its operational rows are re-imported correctly.
        var sourcePlotNumber = ExtractPlotNumber(workbook);
        var sourceClientName = ExtractClient(workbook);
        var sourceOwnerName = ExtractLabeledValue(workbook, "اسم المالك");
        var sourcePayerName = ExtractLabeledValue(workbook, "الدافع") ?? ExtractLabeledValue(workbook, "الدفعات الرئيسية");
        var sourceLocation = ExtractLabeledValue(workbook, "الموقع");
        var sourceOfficeShare = ExtractOfficeShare(workbook);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await ImportProjectLock.AcquireAsync(context, projectId, cancellationToken);
            var project = await context.Projects.FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
            if (project is null) return null;
            await RequireEmptyProjectLedgerAsync(context, projectId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(sourcePlotNumber))
            {
                var duplicateCode = await context.Projects.AnyAsync(item => item.Id != projectId && item.Code == sourcePlotNumber, cancellationToken);
                if (duplicateCode)
                    throw new InvalidOperationException($"رقم القطعة {sourcePlotNumber} مرتبط بمشروع آخر، وتم إيقاف التحديث لحماية ترابط البيانات.");
                project.Code = sourcePlotNumber;
            }
            if (!string.IsNullOrWhiteSpace(sourceClientName)) project.ClientName = sourceClientName;
            if (!string.IsNullOrWhiteSpace(sourceOwnerName)) project.OwnerName = sourceOwnerName;
            if (!string.IsNullOrWhiteSpace(sourcePayerName)) project.PayerName = sourcePayerName;
            if (!string.IsNullOrWhiteSpace(sourceLocation)) project.Location = sourceLocation;
            if (sourceOfficeShare > 0) project.OfficeSharePercentage = Math.Clamp(sourceOfficeShare, 0m, 100m);

            var rows = ImportWorkbookRows(context, workbook, project, userId, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var uploadRoot = Path.Combine(host.ContentRootPath, "uploads", "project-imports");
            Directory.CreateDirectory(uploadRoot);
            var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(sourceName).ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadRoot, storedName);
            await using (var stream = System.IO.File.Create(fullPath)) { memory.Position = 0; await memory.CopyToAsync(stream, cancellationToken); }
            var attachment = new Attachment { EntityType = "project-import", EntityId = project.Id, OriginalFileName = sourceName, StoredFileName = storedName, RelativePath = Path.Combine("uploads", "project-imports", storedName), ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : file.ContentType, SizeInBytes = file.Length, UploadedByUserId = userId };
            context.Attachments.Add(attachment);
            project.Status = "تحت المراجعة";
            context.SecurityAuditLogs.Add(new EMG.Domain.Security.SecurityAuditLog { UserId = userId, Action = "استيراد إلى مشروع موجود", EntityType = "project-import", EntityId = project.Id, Details = $"تم استيراد {sourceName}: {rows.Summary}.", CreatedAtUtc = DateTime.UtcNow });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return rows.Result(project, attachment, ["تم استيراد الحركات كمسودات إلى مشروع خالٍ من الحركات المالية."]);
        }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }
    }

    internal static async Task RequireEmptyProjectLedgerAsync(EmgDbContext context, int projectId, CancellationToken cancellationToken)
    {
        // Legacy workbook references are editable text. They cannot identify rows safe to replace.
        if (await context.SiteMovements.AnyAsync(row => row.ProjectId == projectId, cancellationToken) ||
            await context.ExcavationRecords.AnyAsync(row => row.ProjectId == projectId, cancellationToken) ||
            await context.ConcreteRecords.AnyAsync(row => row.ProjectId == projectId, cancellationToken) ||
            await context.SteelRecords.AnyAsync(row => row.ProjectId == projectId, cancellationToken) ||
            await context.FinishingRecords.AnyAsync(row => row.ProjectId == projectId, cancellationToken) ||
            await context.AdvancePayments.AnyAsync(row => row.ProjectId == projectId, cancellationToken) ||
            await context.SupplierSupplies.AnyAsync(row => row.ProjectId == projectId, cancellationToken) ||
            await context.SupplierPayments.AnyAsync(row => row.ProjectId == projectId, cancellationToken))
            throw new InvalidOperationException("تعذر إعادة الاستيراد لأن المشروع به حركات مالية. استخدم مشروعاً خالياً من الحركات، أو راجع السجلات القائمة قبل أي تسوية.");
    }

    private ImportedRows ImportWorkbookRows(EmgDbContext context, XLWorkbook workbook, Project project, int? userId,
        CancellationToken cancellationToken, bool historical = false)
    {
        var supplierCache = new Dictionary<string, Supplier>(StringComparer.OrdinalIgnoreCase);
        var payments = new List<(Supplier Supplier, SupplierSupply? Supply, decimal Amount, DateTime Date, string Notes)>();
        var site = 0; var excavation = 0; var concrete = 0; var steel = 0; var finishing = 0;
        var supplies = 0; var advances = 0; var allocations = 0;
        foreach (var sheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var classification = ClassifySheet(sheet);
            if (historical) ValidateHistoricalSourceSheet(sheet, classification);
            switch (classification)
            {
                case "أعمال الموقع": site += ImportSiteMovements(sheet, project, supplierCache, cancellationToken, historical); break;
                case "الحفر": excavation += ImportExcavation(sheet, project); break;
                case "الخرسانات": concrete += ImportConcrete(sheet, project, supplierCache, historical); break;
                case "الحديد": steel += ImportSteel(sheet, project, supplierCache, historical); break;
                case "المباني":
                case "المحارة":
                case "العزل":
                case "الكهرباء":
                case "السباكة":
                case "أعمال أخرى":
                    var finish = ImportFinishing(sheet, project, classification, supplierCache, payments, historical);
                    finishing += finish.Records;
                    supplies += finish.Supplies;
                    break;
                case "دفعات تحت الحساب":
                    var funding = ImportAdvances(sheet, project, userId, historical);
                    advances += funding.Advances;
                    allocations += funding.Allocations;
                    break;
            }
        }
        ValidatePendingAdvanceAllocations(context);
        foreach (var payment in payments)
            context.SupplierPayments.Add(new SupplierPayment
            {
                Supplier = payment.Supplier, Project = project, Supply = payment.Supply,
                Amount = payment.Amount, PaymentDate = payment.Date, PaymentMethod = "غير محدد", Notes = payment.Notes
            });
        return new ImportedRows(site, excavation, concrete, steel, finishing, supplies, payments.Count, advances, allocations);
    }

    private sealed record ImportedRows(int Site, int Excavation, int Concrete, int Steel, int Finishing, int Supplies, int Payments, int Advances, int Allocations)
    {
        public string Summary => $"أعمال موقع {Site}، حفر {Excavation}، خرسانات {Concrete}، حديد {Steel}، تشطيبات {Finishing}، توريدات {Supplies}، دفعات موردين {Payments}، دفعات تحت الحساب {Advances}، توزيعات {Allocations}";

        public ImportedProjectResult Result(Project project, Attachment attachment, IReadOnlyList<string> warnings) =>
            new(project.Id, project.Name, project.Code, attachment.OriginalFileName, attachment.Id, project.Status,
                Site, Excavation, Concrete, Steel, Finishing, Supplies, Payments, Advances, Allocations, warnings);
    }

    private int ImportSiteMovements(IXLWorksheet sheet, Project project, Dictionary<string, Supplier> suppliers, CancellationToken cancellationToken, bool historical)
    {
        var count = 0;
        var layout = DetectLayout(sheet, "أعمال الموقع");
        foreach (var row in RowsAfter(sheet, layout.HeaderRow))
        {
            var description = Text(row, layout.Column("description", 3));
            var total = Decimal(row, layout.Column("total", 10));
            if (string.IsNullOrWhiteSpace(description) || total == 0) continue;
            var supplier = GetSupplier(Text(row, layout.Column("supplier", 4)), suppliers, historical) ?? GetSupplier("غير محدد", suppliers, historical)!;
            db!.SiteMovements.Add(new SiteMovement
            {
                Project = project, Supplier = supplier, MovementType = "أعمال الموقع", Description = description,
                Unit = NormalizeImportedUnit(Text(row, layout.Column("unit", 5)), "وحدة", description), UnitPrice = Decimal(row, layout.Column("unitPrice", 6)), Quantity = Decimal(row, layout.Column("quantity", 8)), TotalAmount = total,
                MovementDate = Date(row, layout.Column("date", 2)), ExpenseCategory = sheet.Name,
                IsPaid = Decimal(row, layout.Column("paid", 11)) > 0,
                Reference = $"استيراد: {sheet.Name} صف {row.RowNumber()}", Details = Text(row, layout.Column("notes", 11))
            });
            count++;
        }
        return count;
    }

    private int ImportExcavation(IXLWorksheet sheet, Project project)
    {
        var count = 0;
        var layout = DetectLayout(sheet, "الحفر");
        foreach (var row in RowsAfter(sheet, layout.HeaderRow))
        {
            var total = Decimal(row, layout.Column("total", 10));
            if (total == 0) continue;
            var directQuantity = Decimal(row, layout.Column("quantity", 8));
            var length = Decimal(row, layout.Column("length", 0));
            var width = Decimal(row, layout.Column("width", 0));
            var depth = Decimal(row, layout.Column("depth", 0));
            var loads = Decimal(row, layout.Column("loads", 0));
            var quantity = directQuantity;
            var method = "كمية مباشرة";
            var unit = Text(row, layout.Column("unit", 7));
            if (quantity == 0 && length > 0 && width > 0 && depth > 0)
            {
                quantity = Math.Round(length * width * depth, 3);
                method = "أبعاد (طول × عرض × عمق)";
                unit = string.IsNullOrWhiteSpace(unit) ? "متر مكعب" : unit;
            }
            else if (quantity == 0 && loads > 0)
            {
                quantity = loads;
                method = "نقلات/عربات";
                unit = string.IsNullOrWhiteSpace(unit) ? "نقلة" : unit;
            }
            unit = NormalizeImportedUnit(unit, method.Contains("نقل") ? "نقلة" : "متر مكعب", Text(row, layout.Column("description", 6)));
            db!.ExcavationRecords.Add(new ExcavationRecord
            {
                // Excavation is the live workbook's "حساب العرب" section.
                // A supplier/person name in the sheet is not an accounting
                // category and must never replace that section identity.
                Project = project, AccountCategory = "العرب", CalculationMethod = method, CalculatedQuantity = quantity,
                Unit = unit, UnitPrice = Decimal(row, layout.Column("unitPrice", 9)), TotalAmount = total,
                Description = Text(row, layout.Column("description", 6)) is { Length: > 0 } description ? description : "أعمال حفر",
                ExcavationDate = Date(row, layout.Column("date", 5)), Status = "مسودة", Notes = $"استيراد: {sheet.Name} صف {row.RowNumber()} | {layout.Profile}"
            });
            count++;
        }

        // A number of the live workbooks keep the excavation account as a
        // summary amount in the upper metadata block instead of repeating it
        // in the detail grid.  When no detail row was found, recover that
        // explicit source amount as one auditable summary record.  This is
        // deliberately gated by the absence of detail rows so it cannot
        // double count a workbook that has both forms.
        if (count == 0)
        {
            var summary = FindExcavationSummary(sheet);
            if (summary is not null && summary.Value.Amount != 0)
            {
                db!.ExcavationRecords.Add(new ExcavationRecord
                {
                    Project = project,
                    AccountCategory = "العرب",
                    CalculationMethod = "ملخص المصدر",
                    CalculatedQuantity = summary.Value.Quantity,
                    Unit = NormalizeImportedUnit(summary.Value.Unit, "متر مكعب", "إجمالي أعمال الحفر"),
                    UnitPrice = summary.Value.UnitPrice,
                    TotalAmount = summary.Value.Amount,
                    Description = "إجمالي أعمال الحفر",
                    ExcavationDate = summary.Value.Date,
                    Status = "مسودة",
                    Notes = $"استيراد: {sheet.Name} ملخص المصدر صف {summary.Value.RowNumber}"
                });
                count++;
            }
        }
        return count;
    }

    private static (decimal Amount, decimal Quantity, decimal UnitPrice, string Unit, DateTime Date, int RowNumber)? FindExcavationSummary(IXLWorksheet sheet)
    {
        foreach (var row in sheet.RowsUsed().Take(20))
        {
            var rowText = string.Join(" ", row.CellsUsed().Select(cell => NormalizeHeader(SafeCellText(cell))));
            if (!rowText.Contains("المبلغ") && !rowText.Contains("اجمالي") && !rowText.Contains("اجمال")) continue;

            var moneyCells = row.CellsUsed()
                .Select(cell => (Column: cell.Address.ColumnNumber, Value: Decimal(row, cell.Address.ColumnNumber)))
                .Where(item => item.Value != 0)
                .ToList();
            if (moneyCells.Count == 0) continue;

            var amount = moneyCells.OrderByDescending(item => item.Value).First().Value;
            var quantity = moneyCells.FirstOrDefault(item => item.Column is 8 or 12).Value;
            var unitPrice = moneyCells.FirstOrDefault(item => item.Column is 9 or 13).Value;
            return (amount, quantity, unitPrice, quantity != 0 ? "متر مكعب" : string.Empty, DateTime.UtcNow.Date, row.RowNumber());
        }
        return null;
    }

    private int ImportConcrete(IXLWorksheet sheet, Project project, Dictionary<string, Supplier> suppliers, bool historical)
    {
        var count = 0;
        var layout = DetectLayout(sheet, "الخرسانات");
        var isSplitConcreteLayout = sheet.CellsUsed()
            .Select(cell => NormalizeHeader(SafeCellText(cell)))
            .Any(value => value.Contains("صافيقيمهالاعمالبالجنيه") || value.Contains("بيانبالاعمال"));
        DateTime? lastKnownDate = null;
        foreach (var row in RowsAfter(sheet, layout.HeaderRow))
        {
            // Live workbooks contain three concrete shapes:
            // 1) the full block in N:V (date, supplier, current quantity, price, total),
            // 2) a compact block in C:H (description, quantity, price, total), and
            // 3) a second compact block in L:O on the same row. The latter is a real
            // second transaction, not a duplicate column group, so each block is read.
            var date = TryDate(row, layout.Column("date", 2)) ?? lastKnownDate ?? DateTime.UtcNow.Date;
            lastKnownDate = date;
            var rowCount = 0;
            void Add(decimal quantity, decimal unitPrice, decimal total, string description, string unit, string supplierName, string grade, string profile, DateTime entryDate)
            {
                if (quantity == 0 && total == 0 && (!historical || unitPrice == 0)) return;
                if (historical && (quantity == 0 || total == 0 || string.IsNullOrWhiteSpace(description)))
                    throw HistoricalSourceError(row, "بيانات بند الخرسانة غير مكتملة.");
                if (quantity == 0 && string.IsNullOrWhiteSpace(description)) return;
                if (string.IsNullOrWhiteSpace(description)) description = "أعمال خرسانات";
                db!.ConcreteRecords.Add(new ConcreteRecord
                {
                    // Concrete is reported in the historical final account as
                    // "باقي المصنعيات".  Keep the operational sheet/category
                    // in the record itself, but use the final-account bucket
                    // for financial aggregation.
                    Project = project, Supplier = GetSupplier(supplierName, suppliers, historical), AccountCategory = "باقي المصنعيات", ConcreteType = "خرسانة", ElementType = description,
                    Quantity = quantity, Unit = NormalizeImportedUnit(unit, "متر مكعب", description), UnitPrice = unitPrice, TotalAmount = total,
                    PourDate = entryDate, Description = description, Status = total == 0 ? "تحت المراجعة" : "مسودة", MixGrade = grade,
                    Notes = $"استيراد: {sheet.Name} صف {row.RowNumber()} | {profile}{(total == 0 ? " | قيمة/سعر ناقص في المصدر" : string.Empty)}"
                });
                rowCount++;
            }

            if (isSplitConcreteLayout)
            {
                if (historical) ValidateHistoricalMoneyCells(row, [4, 7, 8, 18, 19, 21]);
                // Some live sheets have two independent blocks on the same row:
                // D/G/H (description/quantity/rate/total) and N/R/S/U
                // (description/current quantity/rate/total). They must both be
                // imported, while the template/footer rows must be ignored.
                Add(Decimal(row, 4), Decimal(row, 7), Decimal(row, 8), Text(row, 3), Text(row, 15), string.Empty, Text(row, 9), "خرسانات مزدوجة D:G:H", date);
                Add(Decimal(row, 18), Decimal(row, 19), Decimal(row, 21), Text(row, 14), Text(row, 15), string.Empty, string.Empty, "خرسانات مزدوجة N:R:S:U", date);
            }
            else
            {
                if (historical) ValidateHistoricalMoneyCells(row, [5, 8, 9, 13, 14, 15, 18, 20, 22]);
                var standardDate = TryDate(row, 24) ?? date;
                Add(Decimal(row, 18), Decimal(row, 20), Decimal(row, 22), Text(row, 15), Text(row, 16), string.Empty, Text(row, 10), "خرسانات قياسي N:V", standardDate);
                Add(Decimal(row, 5), Decimal(row, 8), Decimal(row, 9), string.IsNullOrWhiteSpace(Text(row, 4)) ? Text(row, 10) : Text(row, 4), string.Empty, string.Empty, Text(row, 10), "خرسانات مختصر C:I", date);
                Add(Decimal(row, 13), Decimal(row, 14), Decimal(row, 15), Text(row, 12), string.Empty, string.Empty, string.Empty, "خرسانات مختصر L:O", date);
            }
            count += rowCount;
        }
        return count;
    }

    private int ImportSteel(IXLWorksheet sheet, Project project, Dictionary<string, Supplier> suppliers, bool historical)
    {
        var count = 0;
        var layout = DetectLayout(sheet, "الحديد");
        var supplier = GetSupplier(SteelSupplierName(sheet.Name), suppliers, historical);
        foreach (var row in RowsAfter(sheet, layout.HeaderRow))
        {
            if (IsSteelSummaryRow(row)) continue;
            var quantity = Decimal(row, layout.Column("quantity", 3));
            var total = Decimal(row, layout.Column("total", 6));
            if (quantity == 0 || total == 0) continue;
            var isAdjustment = quantity < 0 || total < 0;
            db!.SteelRecords.Add(new SteelRecord
            {
                Project = project, Supplier = supplier, Quantity = quantity, Unit = "طن",
                UnitPrice = Decimal(row, layout.Column("unitPrice", 5)), TotalAmount = total, OperationDate = Date(row, layout.Column("date", 2)),
                Description = isAdjustment ? "تسوية/خصم توريد حديد" : "توريد حديد",
                OperationType = isAdjustment ? "تسوية حديد" : "توريد حديد",
                BarDiameter = Text(row, layout.Column("diameter", 4)), Grade = Text(row, layout.Column("grade", 4)),
                Notes = $"استيراد: {sheet.Name} صف {row.RowNumber()} | {Text(row, layout.Column("notes", 7))}{(isAdjustment ? " | حركة سالبة من المصدر" : string.Empty)}", Status = "مسودة"
            });
            count++;
        }
        return count;
    }

    private (int Records, int Supplies, int Payments) ImportFinishing(IXLWorksheet sheet, Project project, string category, Dictionary<string, Supplier> suppliers, List<(Supplier Supplier, SupplierSupply? Supply, decimal Amount, DateTime Date, string Notes)> payments, bool historical)
    {
        var records = 0; var supplies = 0; var paymentCount = 0;
        var layout = DetectLayout(sheet, category);
        foreach (var row in RowsAfter(sheet, layout.HeaderRow))
        {
            var description = Text(row, layout.Column("description", 3));
            var quantity = Decimal(row, layout.Column("quantity", 9));
            var total = Decimal(row, layout.Column("total", 10));
            if (string.IsNullOrWhiteSpace(description) && quantity == 0 && total == 0) continue;
            if (string.IsNullOrWhiteSpace(description)) description = "بند غير موصوف - يحتاج مراجعة";
            if (quantity == 0 && total == 0) continue;
            var supplierName = Text(row, layout.Column("supplier", 4));
            if (historical && string.IsNullOrWhiteSpace(supplierName)) throw HistoricalSourceError(row, "اسم المورد مطلوب.");
            var supplier = GetSupplier(supplierName, suppliers, historical) ?? GetSupplier("غير محدد", suppliers, historical)!;
            var supply = new SupplierSupply
            {
                Project = project, Supplier = supplier, SupplyType = category, ItemName = description, Unit = NormalizeImportedUnit(Text(row, layout.Column("unit", 5)), "وحدة", description),
                Quantity = quantity, UnitPrice = Decimal(row, layout.Column("unitPrice", 6)), TotalAmount = total, SupplyDate = Date(row, layout.Column("date", 2)), Notes = $"استيراد: {sheet.Name} صف {row.RowNumber()} | {layout.Profile}"
            };
            db!.SupplierSupplies.Add(supply);
            supplies++;
            db!.FinishingRecords.Add(new FinishingRecord
            {
                Project = project, Supplier = supplier, AccountCategory = FinalAccountCategory(category), WorkType = description, Quantity = quantity,
                Unit = NormalizeImportedUnit(Text(row, layout.Column("unit", 5)), "وحدة", description), UnitPrice = Decimal(row, layout.Column("unitPrice", 6)), TotalAmount = total, WorkDate = Date(row, layout.Column("date", 2)), Description = description,
                Status = total == 0 ? "تحت المراجعة" : "مسودة", Notes = $"استيراد: {sheet.Name} صف {row.RowNumber()}{(total == 0 ? " | قيمة/سعر ناقص في المصدر" : string.Empty)}"
            });
            records++;
            var paid = Decimal(row, layout.Column("paid", 11));
            if (paid > 0) { payments.Add((supplier, supply, paid, Date(row, layout.Column("date", 2)), $"استيراد دفعة من {sheet.Name} صف {row.RowNumber()}")); paymentCount++; }
        }
        return (records, supplies, paymentCount);
    }

    private (int Advances, int Allocations) ImportAdvances(IXLWorksheet sheet, Project project, int? userId, bool historical)
    {
        var advances = 0; var allocations = 0;
        var layout = DetectLayout(sheet, "دفعات تحت الحساب");
        var header = sheet.Row(layout.HeaderRow).CellsUsed().ToList();
        var paymentColumn = header
            .Where(cell => IsAdvanceReceiptHeader(SafeCellText(cell)))
            .Select(cell => cell.Address.ColumnNumber).LastOrDefault();
        if (paymentColumn <= 0) paymentColumn = 14;
        var dateColumn = layout.Column("date", 1);
        var allocationColumns = header
            .Where(cell => cell.Address.ColumnNumber != paymentColumn && IsAdvanceAllocationHeader(SafeCellText(cell)))
            .Select(cell => (Column: cell.Address.ColumnNumber, Category: AdvanceCategory(NormalizeHeader(SafeCellText(cell)), SafeCellText(cell))))
            .Where(item => item.Column > 0)
            .ToList();
        if (allocationColumns.Count == 0)
        {
            allocationColumns = Enumerable.Range(2, 11)
                .Select((column, index) => (Column: column, Category: new[] { "الموقع", "العرب", "المقاول", "المحطة", "الحديد", "المباني", "المحارة", "العزل", "الكهرباء", "السباكة", "المكتب" }[index]))
                .ToList();
        }
        AdvancePayment? lastAdvance = null;
        var moneyColumns = allocationColumns.Select(item => item.Column).Append(paymentColumn).ToArray();
        foreach (var row in AdvanceRows(sheet, layout.HeaderRow))
        {
            if (historical) ValidateHistoricalMoneyCells(row, moneyColumns);
            var amount = Decimal(row, paymentColumn);
            var date = Date(row, dateColumn);
            if (historical && (amount < 0 || allocationColumns.Any(item => Decimal(row, item.Column) < 0)))
                throw HistoricalSourceError(row, "قيمة دفعة تحت الحساب أو توزيعها سالبة.");
            var categoryTotal = allocationColumns.Sum(item => Decimal(row, item.Column));
            if (amount <= 0 && categoryTotal == 0) continue;
            // Some live sheets continue the allocation of the previous payment
            // on rows that have no payment amount/date. They are not new cash
            // receipts; keep the allocation attached to the last real payment.
            if (amount <= 0)
            {
                if (lastAdvance is null)
                {
                    if (historical) throw HistoricalSourceError(row, "توزيع لا يرتبط بدفعة تحت الحساب.");
                    continue;
                }
                if (historical && row.Cell(dateColumn).IsEmpty()) date = lastAdvance.PaymentDate ?? throw HistoricalSourceError(row, "تاريخ الدفعة غير مدون.");
                foreach (var item in allocationColumns)
                {
                    var allocated = Decimal(row, item.Column);
                    if (allocated == 0) continue;
                    lastAdvance.Allocations.Add(new AdvanceAllocation { AccountCategory = item.Category, Amount = allocated, AllocationDate = date == default ? lastAdvance.PaymentDate ?? throw HistoricalSourceError(row, "تاريخ الدفعة غير مدون.") : date, Notes = $"استيراد توزيع تكميلي: {sheet.Name} صف {row.RowNumber()}" });
                    allocations++;
                }
                continue;
            }
            var advance = new AdvancePayment
            {
                Project = project, ClientName = project.ClientName ?? ExtractValue(sheet, "A2") ?? string.Empty, PayerName = Text(row, 15),
                Amount = amount, PaymentDate = date,
                PaymentMethod = "غير محدد", Reference = Text(row, 15), Notes = $"استيراد: {sheet.Name} صف {row.RowNumber()} | {Text(row, 13)}", CreatedByUserId = userId, Status = "مسودة"
            };
            db!.AdvancePayments.Add(advance); advances++;
            lastAdvance = advance;
            foreach (var item in allocationColumns)
            {
                var allocated = Decimal(row, item.Column);
                if (allocated == 0) continue;
                advance.Allocations.Add(new AdvanceAllocation { AccountCategory = item.Category, Amount = allocated, AllocationDate = date, Notes = $"استيراد: {sheet.Name} صف {row.RowNumber()} | المصدر: {item.Category}" });
                allocations++;
            }
        }
        return (advances, allocations);
    }

    private static IEnumerable<IXLRow> AdvanceRows(IXLWorksheet sheet, int headerRow = 3)
    {
        // The live workbooks place totals and balance calculations below the
        // transaction area. They look like money rows, but must never become
        // new payments or allocations during import.
        foreach (var row in RowsAfter(sheet, headerRow))
        {
            var firstCell = SafeCellText(row.Cell(1));
            if (firstCell.StartsWith("اجمالي", StringComparison.OrdinalIgnoreCase) ||
                firstCell.StartsWith("المتبقي", StringComparison.OrdinalIgnoreCase) ||
                firstCell.Contains("المستحق للاعمال", StringComparison.OrdinalIgnoreCase))
                yield break;
            yield return row;
        }
    }

    private static string AdvanceCategory(string normalizedHeader, string originalHeader)
    {
        if (normalizedHeader.Contains("الموقع")) return "الموقع";
        if (normalizedHeader.Contains("العرب")) return "العرب";
        if (normalizedHeader.Contains("المقاول")) return "المقاول";
        if (normalizedHeader.Contains("المحطه") || normalizedHeader.Contains("المحطة")) return "المحطة";
        if (normalizedHeader.Contains("الحديد")) return "الحديد";
        if (normalizedHeader.Contains("المباني")) return "المباني";
        if (normalizedHeader.Contains("المحاره") || normalizedHeader.Contains("المحارة")) return "المحارة";
        if (normalizedHeader.Contains("العزل")) return "العزل";
        if (normalizedHeader.Contains("الكهرباء")) return "الكهرباء";
        if (normalizedHeader.Contains("السباكه") || normalizedHeader.Contains("السباكة")) return "السباكة";
        if (normalizedHeader.Contains("المكتب")) return "المكتب";
        var value = originalHeader.Trim();
        value = value.Replace("دفعات تحت حساب", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("دفعات تحت", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim(' ', ':', '-', 'ـ');
        return string.IsNullOrWhiteSpace(value) ? "أعمال أخرى" : $"أعمال أخرى: {value}";
    }

    private static bool IsAdvanceAllocationHeader(string value)
    {
        var normalized = NormalizeHeader(value);
        return normalized.Contains("دفعاتتحت") || normalized.Contains("دفعاتحساب") ||
               normalized.Contains("حسابالموقع") || normalized.Contains("حسابالعرب") ||
               normalized.Contains("حسابالمقاول") || normalized.Contains("حسابالمحطه") ||
               normalized.Contains("حسابالحديد") || normalized.Contains("حسابالمباني") ||
               normalized.Contains("حسابالمحاره") || normalized.Contains("حسابالعزل") ||
               normalized.Contains("حسابالكهرباء") || normalized.Contains("حسابالسباكه") ||
               normalized.Contains("حسابالمكتب");
    }

    private static bool IsAdvanceReceiptHeader(string value)
    {
        var normalized = NormalizeHeader(value);
        if (IsAdvanceAllocationHeader(value)) return false;
        return normalized is "الدفعه" or "الدفعة" or "مبلغ" or "المبلغ" ||
               normalized.Contains("اجماليدفعات") || normalized.Contains("اجماليدفع");
    }

    private static string FinalAccountCategory(string section) => section switch
    {
        "أعمال الموقع" => "الموقع",
        "الحفر" => "العرب",
        "الحديد" => "الحديد",
        // The source final-account template combines concrete and finishing
        // work under this exact bucket.  The detailed source section remains
        // available through SupplyType/WorkType.
        "الخرسانات" or "المباني" or "المحارة" or "العزل" or "الكهرباء" or "السباكة" or "أعمال أخرى" => "باقي المصنعيات",
        _ => section
    };

    private static void ValidatePendingAdvanceAllocations(EmgDbContext context)
    {
        var invalid = context.ChangeTracker.Entries<AdvancePayment>()
            .Select(entry => entry.Entity)
            .Where(item => item.Allocations.Sum(allocation => allocation.Amount) > item.Amount)
            .Select(item => item.Amount)
            .FirstOrDefault();
        if (invalid > 0)
            throw new InvalidOperationException("لا يمكن استيراد توزيع يتجاوز قيمة دفعة تحت الحساب.");
    }

    private static IEnumerable<IXLRow> RowsAfter(IXLWorksheet sheet, int headerRow) =>
        sheet.RowsUsed().Where(row => row.RowNumber() > headerRow);

    private sealed record SheetAnalysis(int SourceRows, int CandidateRows, string Profile, string Confidence, IReadOnlyList<string> Diagnostics)
    {
        public static SheetAnalysis Empty(string diagnostic) => new(0, 0, "غير محدد", "منخفضة", [diagnostic]);
    }

    // SourceRows can include deliberately preformatted/template rows.  Those
    // rows are reported as "عالية - قالب فارغ" and must not block a complete
    // migration.  A real data-bearing sheet with no candidates remains a hard
    // stop so that import never silently drops operational data.
    private static bool IsBlockingZeroCandidateSheet(WorkbookSheetPlan item) =>
        item.Recognized &&
        item.WillCreateData &&
        item.SourceRows > 0 &&
        item.CandidateRows == 0 &&
        !string.Equals(item.Confidence, "عالية - قالب فارغ", StringComparison.Ordinal);

    private static SheetAnalysis AnalyzeSheet(IXLWorksheet sheet, string classification)
    {
        if (classification is "ملخص مشروع" or "مستخلص")
        {
            var usedRows = sheet.RangeUsed()?.RowsUsed().Count() ?? 0;
            return new SheetAnalysis(usedRows, 0, classification == "مستخلص" ? "مستخلص مرجعي" : "ملخص مشروع مرجعي", "عالية", ["هذه الصفحة ملخص/مستخلص تجميعي وليست سجل حركات تفصيلية؛ تم حفظها كمصدر مرجعي دون تخمين توزيع القيم."]);
        }
        var layout = DetectLayout(sheet, classification);
        var header = layout.HeaderRow;
        if (header == 0) return new SheetAnalysis(0, 0, "بيانات وصفية/مرفقات", "عالية", []);
        var rows = classification == "دفعات تحت الحساب"
            ? AdvanceRows(sheet, header).Where(row => MeaningfulDataRow(row, classification, layout)).ToList()
            : RowsAfter(sheet, header).Where(row => MeaningfulDataRow(row, classification, layout)).ToList();
        bool HasText(IXLRow row, int column) => !string.IsNullOrWhiteSpace(Text(row, column));
        bool HasMoney(IXLRow row, int column) => Decimal(row, column) != 0;
        var candidates = classification switch
        {
            "أعمال الموقع" => rows.Where(row => HasText(row, layout.Column("description", 3)) && HasMoney(row, layout.Column("total", 10))).ToList(),
            "الحفر" => rows.Where(row => HasMoney(row, layout.Column("total", 10))).ToList(),
            "الخرسانات" => rows.Where(row => HasMoney(row, 18) && (HasMoney(row, 22) || HasText(row, 15))
                || HasMoney(row, 5) && (HasMoney(row, 9) || HasText(row, 4))
                || HasMoney(row, 4) && HasMoney(row, 8)
                || HasMoney(row, 18) && HasMoney(row, 21)).ToList(),
            "الحديد" => rows.Where(row => HasMoney(row, layout.Column("quantity", 3)) && HasMoney(row, layout.Column("total", 6))).ToList(),
            "المباني" or "المحارة" or "العزل" or "الكهرباء" or "السباكة" or "أعمال أخرى" => rows.Where(row => (HasText(row, layout.Column("description", 3)) || HasMoney(row, layout.Column("quantity", 9)) || HasMoney(row, layout.Column("total", 10))) && (HasMoney(row, layout.Column("quantity", 9)) || HasMoney(row, layout.Column("total", 10)))).ToList(),
            "دفعات تحت الحساب" => rows.Where(row => HasMoney(row, 14) || Enumerable.Range(0, 11).Sum(index => Decimal(row, index + 2)) != 0).ToList(),
            _ => []
        };
        var relevantData = classification switch
        {
            "أعمال الموقع" => rows.Any(row => HasText(row, layout.Column("description", 3)) || HasMoney(row, layout.Column("total", 10))),
            "الحفر" => rows.Any(row => HasMoney(row, layout.Column("total", 10))),
            "الخرسانات" => rows.Any(row => HasMoney(row, 22) || HasMoney(row, 21) || HasMoney(row, 18) || HasMoney(row, 9) || HasMoney(row, 8) || HasMoney(row, 5)),
            "الحديد" => rows.Any(row => HasMoney(row, layout.Column("quantity", 3)) || HasMoney(row, layout.Column("total", 6))),
            "المباني" or "المحارة" or "العزل" or "الكهرباء" or "السباكة" or "أعمال أخرى" => rows.Any(row => HasText(row, layout.Column("description", 3)) || HasMoney(row, layout.Column("quantity", 9)) || HasMoney(row, layout.Column("total", 10)) || HasMoney(row, layout.Column("paid", 11))),
            "دفعات تحت الحساب" => rows.Any(row => HasMoney(row, 14) || Enumerable.Range(0, 11).Sum(index => Decimal(row, index + 2)) != 0),
            _ => false
        };
        var candidateCount = classification == "الخرسانات" ? rows.Sum(ConcreteCandidateCount) : candidates.Count;
        var profile = classification == "الخرسانات" && candidateCount > 0 ? "خرسانات متعددة الأنماط: قياسي/مختصر/بلوكين" : layout.Profile;
        var confidence = candidateCount > 0 ? layout.Confidence : !relevantData ? "عالية - قالب فارغ" : "تحتاج مراجعة";
        var diagnostics = new List<string>(layout.Diagnostics);
        var isSplitConcreteLayout = classification == "الخرسانات" && sheet.CellsUsed()
            .Select(cell => NormalizeHeader(SafeCellText(cell)))
            .Any(value => value.Contains("صافيقيمهالاعمالبالجنيه") || value.Contains("بيانبالاعمال"));
        if (relevantData && candidateCount == 0) diagnostics.Add("تم العثور على بيانات فعلية، لكن لا توجد صفوف مرشحة حسب خريطة القراءة الحالية.");
        if (rows.Count > candidateCount && (candidateCount > 0 || relevantData)) diagnostics.Add($"تم استبعاد {rows.Count - candidateCount} صفًا/بلوكًا؛ غالبًا صفوف قالب أو صفوف بلا إجمالي.");
        if (classification == "الخرسانات" && !isSplitConcreteLayout && rows.Any(row => HasMoney(row, 5) && !HasMoney(row, 9) || HasMoney(row, 18) && !HasMoney(row, 22)))
            diagnostics.Add("توجد خرسانات بكمية/بيان فعلي وإجمالي ناقص؛ سيتم حفظها تحت المراجعة دون تأثير مالي.");
        if (classification is "المباني" or "المحارة" or "العزل" or "الكهرباء" or "السباكة" or "أعمال أخرى" && rows.Any(row => HasMoney(row, 9) && !HasMoney(row, 10)))
            diagnostics.Add("توجد حركة بكمية فعلية وإجمالي ناقص؛ سيتم حفظها تحت المراجعة دون تأثير مالي.");
        return new SheetAnalysis(rows.Count, candidateCount, profile, confidence, diagnostics);
    }

    private static int ConcreteCandidateCount(IXLRow row) =>
        (Decimal(row, 18) != 0 && (Decimal(row, 22) != 0 || !string.IsNullOrWhiteSpace(Text(row, 15))) ? 1 : 0) +
        (Decimal(row, 5) != 0 && (Decimal(row, 9) != 0 || !string.IsNullOrWhiteSpace(Text(row, 4))) ? 1 : 0) +
        (Decimal(row, 4) != 0 && Decimal(row, 8) != 0 ? 1 : 0) +
        (Decimal(row, 18) != 0 && Decimal(row, 21) != 0 ? 1 : 0) +
        (Decimal(row, 13) != 0 && Decimal(row, 15) != 0 ? 1 : 0);

    private static bool MeaningfulDataRow(IXLRow row, string classification, SheetLayout layout)
    {
        var description = NormalizeHeader(Text(row, layout.Column("description", 3)));
        if (description is "نوعالاعمال" or "البيان" or "النوع" or "ملاحظات" or "بيانبالاعمال") return false;
        var total = Decimal(row, layout.Column("total", classification == "الحفر" ? 14 : 10));
        var quantity = Decimal(row, layout.Column("quantity", classification == "الحفر" ? 12 : 9));
        var unitPrice = Decimal(row, layout.Column("unitPrice", classification == "الحفر" ? 13 : 6));
        var paid = Decimal(row, layout.Column("paid", 11));
        var hasNumericData = total != 0 || quantity != 0 || unitPrice != 0 || paid != 0 ||
            Decimal(row, layout.Column("length", 0)) != 0 || Decimal(row, layout.Column("width", 0)) != 0 ||
            Decimal(row, layout.Column("depth", 0)) != 0 || Decimal(row, layout.Column("loads", 0)) != 0;
        return classification switch
        {
        "أعمال الموقع" => hasNumericData && (!string.IsNullOrWhiteSpace(Text(row, layout.Column("description", 3)) ) || total != 0),
        "الحفر" => hasNumericData && (!string.IsNullOrWhiteSpace(Text(row, layout.Column("description", 6))) || total != 0),
        "الخرسانات" => Decimal(row, 5) != 0 || Decimal(row, 9) != 0 || Decimal(row, 18) != 0 || Decimal(row, 22) != 0 ||
                        (Decimal(row, 4) != 0 && Decimal(row, 8) != 0) || (Decimal(row, 13) != 0 && Decimal(row, 15) != 0),
        "الحديد" => !IsSteelSummaryRow(row) && (Decimal(row, 3) != 0 || Decimal(row, 6) != 0),
        "المباني" or "المحارة" or "العزل" or "الكهرباء" or "السباكة" or "أعمال أخرى" => hasNumericData,
        "دفعات تحت الحساب" => Decimal(row, 14) != 0 || Enumerable.Range(2, 11).Sum(column => Decimal(row, column)) != 0,
        _ => row.CellsUsed().Any(cell => !string.IsNullOrWhiteSpace(SafeCellText(cell)))
        };
    }

    private static bool IsSteelSummaryRow(IXLRow row)
    {
        var text = string.Join(' ', row.CellsUsed().Select(cell => NormalizeHeader(SafeCellText(cell))));
        return text.Contains("اجماليالتكلفه", StringComparison.Ordinal) ||
               text.Contains("اجماليالتكلفة", StringComparison.Ordinal) ||
               text.Contains("اجماليالحديد", StringComparison.Ordinal) ||
               text.Contains("تكلفةالحديد", StringComparison.Ordinal);
    }

    private sealed record SheetLayout(int HeaderRow, IReadOnlyDictionary<string, int> Columns, string Profile, string Confidence, IReadOnlyList<string> Diagnostics)
    {
        public int Column(string name, int fallback) => Columns.TryGetValue(name, out var column) && column > 0 ? column : fallback;
    }

    private static SheetLayout DetectLayout(IXLWorksheet sheet, string classification)
    {
        var fallbackHeader = classification switch
        {
            "دفعات تحت الحساب" => 3,
            "أعمال الموقع" => 9,
            "الحفر" or "الخرسانات" => 8,
            "الحديد" => 6,
            "المباني" or "المحارة" or "العزل" or "الكهرباء" or "السباكة" or "أعمال أخرى" => 9,
            _ => 0
        };
        var used = sheet.RangeUsed();
        if (used is null) return new SheetLayout(fallbackHeader, new Dictionary<string, int>(), "قالب فارغ", "عالية", []);
        var header = used.Rows().Take(20).Select(row => (Row: row, Score: HeaderScore(row, classification)))
            .OrderByDescending(item => item.Score).ThenBy(item => item.Row.RowNumber()).FirstOrDefault();
        var headerRow = header.Score >= 2 ? header.Row.RowNumber() : fallbackHeader;
        if (headerRow <= 0) return new SheetLayout(0, new Dictionary<string, int>(), "بيانات وصفية/مرفقات", "عالية", []);
        var headerCells = sheet.Row(headerRow).CellsUsed().ToList();
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        void Put(string key, Func<string, bool> predicate)
        {
            var cell = headerCells.FirstOrDefault(item => predicate(NormalizeHeader(SafeCellText(item))));
            if (cell is not null) columns[key] = cell.Address.ColumnNumber;
        }
        Put("date", value => value.Contains("التاريخ"));
        Put("description", value => value.Contains("نوعالاعمال") || value.Contains("البيان") || value.Contains("بيانبالاعمال") || value.Contains("الوصف"));
        Put("supplier", value => value.Contains("اسم المورد"));
        Put("unit", value => value == "الوحده" || value == "الوحدة" || value.Contains("الوحد"));
        Put("quantity", value => value.Contains("الكميه") || value.Contains("الكمية") || value.Contains("مقدارالاعمالالمنفذه") || value.Contains("مقدارالاعمالالمنفذة"));
        Put("unitPrice", value => value.Contains("السعر") || value.Contains("الفئه") || value.Contains("الفئة"));
        Put("total", value => value.Contains("اجمال") || value.Contains("اجماليالتكلفه") || value.Contains("صافيقيمهالاعمالبالجنيه") || value.Contains("المبلغ"));
        Put("previous", value => value.Contains("مقدارالاعمالالسابق"));
        Put("paid", value => value.Contains("الدفعات") || value.Contains("دفعة") || value.Contains("الدفعه") || value.Contains("الدفعة"));
        Put("notes", value => value.Contains("ملاحظات"));
        Put("length", value => value.Contains("الطول"));
        Put("width", value => value.Contains("العرض"));
        Put("depth", value => value.Contains("العمق") || value.Contains("السمك"));
        Put("loads", value => value.Contains("العربات") || value.Contains("النقلات") || value.Contains("عدد العربات") || value.Contains("عدد النقلات"));
        Put("diameter", value => value.Contains("المقاس") || value.Contains("النوع"));
        Put("grade", value => value.Contains("الرتبه") || value.Contains("الرتبة") || value.Contains("الفئه") || value.Contains("الفئة"));
        var diagnostics = new List<string>();
        var profile = classification == "الخرسانات" && (columns.ContainsKey("total") && columns["total"] <= 10 || headerCells.Any(item => NormalizeHeader(SafeCellText(item)).Contains("مصنعية المقاول")))
            ? "خرسانات مختصر: D/G/H/I" : $"النمط المكتشف: {classification} / صف العناوين {headerRow}";
        var confidence = header.Score >= 4 ? "عالية" : header.Score >= 2 ? "متوسطة" : "منخفضة";
        if (header.Score < 2) diagnostics.Add($"تعذر اكتشاف صف عناوين واضح؛ تم استخدام الصف الافتراضي {fallbackHeader}.");
        if (classification == "دفعات تحت الحساب")
        {
            var advanceHeaders = headerCells.Where(cell => NormalizeHeader(SafeCellText(cell)).Contains("دفعات") || NormalizeHeader(SafeCellText(cell)).Contains("الدفعه") || NormalizeHeader(SafeCellText(cell)).Contains("الدفعة")).ToList();
            if (advanceHeaders.Count == 0) diagnostics.Add("لم يتم العثور على أعمدة دفعات/توزيعات واضحة.");
        }
        return new SheetLayout(headerRow, columns, profile, confidence, diagnostics);
    }

    private static int HeaderScore(IXLRangeRow row, string classification)
    {
        var values = row.CellsUsed().Select(cell => NormalizeHeader(SafeCellText(cell))).Where(value => value.Length > 0).ToList();
        if (values.Count == 0) return 0;
        var score = 0;
        if (values.Any(value => value.Contains("التاريخ"))) score += 2;
        if (values.Any(value => value.Contains("اجمال") || value.Contains("التكلفه") || value.Contains("التكلفة"))) score++;
        if (values.Any(value => value.Contains("الكميه") || value.Contains("دفعات") || value.Contains("نوعالاعمال") || value.Contains("البيان") || value.Contains("ملاحظات"))) score++;
        if (classification == "الحفر")
        {
            if (values.Any(value => value.Contains("اجمال") || value.Contains("المبلغ"))) score += 2;
            if (values.Any(value => value.Contains("الكميه") || value.Contains("الطول") || value.Contains("البيان"))) score += 2;
        }
        if (classification == "دفعات تحت الحساب" && values.Count(value => value.Contains("دفعات")) >= 2) score += 3;
        return score;
    }

    private static string NormalizeHeader(string value)
    {
        var decomposed = Normalize(value).Normalize(NormalizationForm.FormD);
        var withoutMarks = new string(decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray());
        return withoutMarks
        .Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ة", "ه")
        .Replace("ى", "ي").Replace("ؤ", "و").Replace("ئ", "ي").Replace("ـ", string.Empty)
        .Replace(" ", string.Empty).Replace("/", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty);
    }

    private static string Text(IXLRow row, int column) => column <= 0 ? string.Empty : SafeCellText(row.Cell(column));

    private static string NormalizeImportedUnit(string? raw, string fallback, string? context = null)
    {
        var value = (raw ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            var normalized = NormalizeHeader(value);
            if (normalized is "م3" or "مترمكعب" or "مترمكعب") return "متر مكعب";
            if (normalized is "م2" or "مترمربع") return "متر مربع";
            if (normalized.Contains("طن")) return "طن";
            if (normalized.Contains("ساعه")) return "ساعة";
            if (normalized.Contains("يوم")) return "يوم";
            if (normalized.Contains("نقله") || normalized.Contains("عربه")) return "نقلة";
            return value;
        }

        var source = NormalizeHeader(context ?? string.Empty);
        if (source.Contains("مترمربع") || source.Contains("مساحه") || source.Contains("تشطيب") || source.Contains("محاره") || source.Contains("عزل")) return "متر مربع";
        if (source.Contains("مترمكعب") || source.Contains("خرسان") || source.Contains("حفر") || source.Contains("ردم")) return "متر مكعب";
        if (source.Contains("حديد")) return "طن";
        if (source.Contains("ساعه")) return "ساعة";
        if (source.Contains("يوم")) return "يوم";
        if (source.Contains("نقل") || source.Contains("عرب")) return "نقلة";
        return fallback;
    }

    private static string SafeCellText(IXLCell cell)
    {
        try
        {
            if (cell.HasFormula)
            {
                try
                {
                    return cell.CachedValue.GetText().Trim();
                }
                catch
                {
                    // Excel error results (for example #VALUE!) are not
                    // readable through XLCellValue.GetText(). Treat them as
                    // unavailable input and keep analyzing the workbook.
                }
            }

            return cell.GetString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static decimal Decimal(IXLRow row, int column) => column <= 0 ? 0m : ReadDecimal(row.Cell(column)) ?? 0m;

    private static decimal? ReadDecimal(IXLCell cell)
    {
        try
        {
            if (cell.TryGetValue<decimal>(out var value)) return Math.Round(value, 2);
        }
        catch
        {
            // Some legacy workbooks contain formula error cells. They are
            // handled below as unavailable numeric input.
        }
        // ClosedXML does not always expose the cached result of a formula
        // through GetString/TryGetValue when the workbook was last calculated
        // by Excel.  The live EMG sheets use formulas for totals and running
        // quantities, so read the cached value before treating the cell as
        // empty.  This keeps preview and import aligned with what Excel shows.
        var text = SafeCellText(cell).Replace(",", string.Empty).Replace("٬", string.Empty).Replace("٫", ".");
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? Math.Round(parsed, 2) : null;
    }

    private static DateTime Date(IXLRow row, int column)
    {
        return TryDate(row, column) ?? DateTime.UtcNow.Date;
    }

    private static DateTime? TryDate(IXLRow row, int column)
    {
        if (column <= 0) return null;
        var cell = row.Cell(column);
        if (cell.IsEmpty()) return null;
        try
        {
            if (cell.TryGetValue<DateTime>(out var value)) return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
            if (cell.TryGetValue<double>(out var serial) && serial >= 1 && serial <= 100000)
                return DateTime.SpecifyKind(DateTime.FromOADate(serial).Date, DateTimeKind.Utc);
        }
        catch
        {
            // Fall through to the safe text path for malformed/error cells.
        }

        var text = SafeCellText(cell);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedDate)
            ? DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc) : null;
    }

    private Supplier? GetSupplier(string name, Dictionary<string, Supplier> cache, bool historical)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = historical ? name : Normalize(name);
        if (cache.TryGetValue(key, out var cached)) return cached;
        var supplier = db!.Suppliers.Local.FirstOrDefault(item => historical
            ? item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            : Normalize(item.Name) == key);
        if (supplier is null)
        {
            var sameName = db.Suppliers.Where(item => item.Name.ToLower() == name.ToLower());
            if (historical)
            {
                var matches = sameName.Take(2).ToList();
                if (matches.Count > 1) throw new InvalidOperationException($"اسم المورد «{name}» يطابق أكثر من مورد.");
                supplier = matches.SingleOrDefault();
            }
            else supplier = sameName.FirstOrDefault();
        }
        supplier ??= new Supplier { Name = name, SupplierType = "مورد", IsActive = true };
        if (supplier.Id == 0) db.Suppliers.Add(supplier);
        cache[key] = supplier;
        return supplier;
    }

    private static string SteelSupplierName(string sheetName) =>
        sheetName.Contains("اولاد شرف", StringComparison.OrdinalIgnoreCase) ? "أولاد شرف" :
        sheetName.Contains("الغانم", StringComparison.OrdinalIgnoreCase) ? "الغانم" : sheetName;

    private static string ClassifySheet(string sheetName)
    {
        var normalized = Normalize(sheetName);
        return KnownSheets.TryGetValue(normalized, out var classification) ? classification : ClassifyUnknown(normalized);
    }

    private static string ClassifySheet(IXLWorksheet sheet)
    {
        var name = NormalizeHeader(sheet.Name);
        var text = string.Join(' ', sheet.CellsUsed().Select(cell => NormalizeHeader(SafeCellText(cell))));
        var rawText = string.Join(' ', sheet.CellsUsed().Select(SafeCellText));
        // Unnamed sheets are common in the live workbooks.  Do not treat the
        // Excel-generated name as the business meaning of the sheet: infer it
        // from the content before falling back to manual review.
        if (string.IsNullOrWhiteSpace(text))
            return "مرفقات/تقارير";
        if (text.Contains("اجماليالمصاريف", StringComparison.Ordinal) &&
            (text.Contains("اجماليالدفعات", StringComparison.Ordinal) || text.Contains("المستحق", StringComparison.Ordinal)))
            return "كشف الحساب النهائي";
        if ((rawText.Contains("مستخ", StringComparison.Ordinal) || rawText.Contains("مستخلص", StringComparison.Ordinal)) && rawText.Contains("كميات", StringComparison.Ordinal) && rawText.Contains("فئ", StringComparison.Ordinal))
            return "مستخلص";
        var scores = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["دفعات تحت الحساب"] = 0,
            ["كشف الحساب النهائي"] = 0,
            ["الخرسانات"] = 0,
            ["الحديد"] = 0,
            ["الحفر"] = 0,
            ["أعمال الموقع"] = 0,
            ["المباني"] = 0,
            ["المحارة"] = 0,
            ["العزل"] = 0,
            ["الكهرباء"] = 0,
            ["السباكة"] = 0,
            ["أعمال أخرى"] = 0,
            ["مرفقات/تقارير"] = 0,
            ["ملخص مشروع"] = 0,
            ["مستخلص"] = 0
        };
        var signals = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["دفعات تحت الحساب"] = ["دفعاتتحتالحساب", "الدفعه", "دفعات"],
            ["كشف الحساب النهائي"] = ["كشفالحسابالنهائي", "نسبهالمكتب", "صافياح"],
            ["الخرسانات"] = ["خرسان", "صبالقواعد", "مكعب", "مترمكعب"],
            ["الحديد"] = ["حديد", "وزنبالطن", "قطر", "طن"],
            ["الحفر"] = ["حفر", "حسابالعرب", "نقلات", "عربات"],
            ["أعمال الموقع"] = ["نوعالاعمال", "اجماليالتكلفه", "اسمالمورد"],
            ["المباني"] = ["مباني", "طوب", "بلوك"],
            ["المحارة"] = ["محاره", "لياسه"],
            ["العزل"] = ["عزل", "لفائف", "بيتومين"],
            ["الكهرباء"] = ["كهرباء", "كابلات", "لوحات"],
            ["السباكة"] = ["سباكه", "مواسير", "خلاطات"],
            ["ملخص مشروع"] = ["اسمالقطعه", "اجمالياعمالالموقع", "اجماليالخرسانه", "اجماليحساب"],
            ["مستخلص"] = ["مستخلص", "الكمياتالمنفذه", "فئه", "بيانالاعمال"]
        };
        var namedClassification = ClassifySheet(sheet.Name);
        if (namedClassification != "صفحة غير معنونة")
            scores[namedClassification] += 8;
        foreach (var pair in signals)
        {
            if (pair.Value.Any(signal => name.Contains(signal, StringComparison.Ordinal))) scores[pair.Key] += 4;
            scores[pair.Key] += pair.Value.Count(signal => text.Contains(signal, StringComparison.Ordinal));
        }
        if ((text.Contains("مستخ", StringComparison.Ordinal) || text.Contains("مستخلص", StringComparison.Ordinal)) && text.Contains("الكميات", StringComparison.Ordinal) && text.Contains("فئ", StringComparison.Ordinal) && text.Contains("قيم", StringComparison.Ordinal))
            return "مستخلص";
        if (scores["ملخص مشروع"] >= 5 && text.Contains("اجمالي", StringComparison.Ordinal)) scores["ملخص مشروع"] += 4;
        if (scores["مستخلص"] >= 4 && text.Contains("اجمالي", StringComparison.Ordinal)) scores["مستخلص"] += 3;
        if ((text.Contains("مستخلص", StringComparison.Ordinal) || text.Contains("مستخ", StringComparison.Ordinal)) && text.Contains("الكميات", StringComparison.Ordinal) && text.Contains("فئ", StringComparison.Ordinal)) scores["مستخلص"] += 10;
        if (text.Contains("اسمالقطعه", StringComparison.Ordinal) && text.Contains("اجمالي", StringComparison.Ordinal)) scores["ملخص مشروع"] += 10;
        if (scores["دفعات تحت الحساب"] >= 3 && text.Count(value => value == 'د') >= 2) scores["دفعات تحت الحساب"] += 3;
        var winner = scores.OrderByDescending(pair => pair.Value).First();
        if (winner.Value >= 2) return winner.Key;

        var hasWorkTableHeaders = text.Contains("بيانبالاعمال", StringComparison.Ordinal) &&
            text.Contains("الوحد", StringComparison.Ordinal) &&
            text.Contains("كمي", StringComparison.Ordinal) &&
            (text.Contains("اجمالي", StringComparison.Ordinal) || text.Contains("فئ", StringComparison.Ordinal));
        if (hasWorkTableHeaders)
        {
            if (text.Contains("خرسان", StringComparison.Ordinal) || text.Contains("مسلح", StringComparison.Ordinal) ||
                text.Contains("صب", StringComparison.Ordinal) || text.Contains("قواعد", StringComparison.Ordinal) ||
                text.Contains("م3", StringComparison.Ordinal))
                return "الخرسانات";
            if (text.Contains("حفر", StringComparison.Ordinal) || text.Contains("ردم", StringComparison.Ordinal) ||
                text.Contains("نقلات", StringComparison.Ordinal) || text.Contains("عرب", StringComparison.Ordinal))
                return "الحفر";
            if (text.Contains("حديد", StringComparison.Ordinal) || text.Contains("طن", StringComparison.Ordinal))
                return "الحديد";
            return "أعمال أخرى";
        }

        // Some live workbooks contain a compact movement ledger without a
        // header row (date, description, supplier, unit, rate, quantity,
        // total).  Equipment/labour terms are strong enough to identify it
        // as a site movement without asking the user to classify SheetN.
        if (text.Contains("لودر", StringComparison.Ordinal) || text.Contains("حفار", StringComparison.Ordinal) ||
            text.Contains("باليوميه", StringComparison.Ordinal) || text.Contains("بالساعه", StringComparison.Ordinal) ||
            text.Contains("مورد", StringComparison.Ordinal) || text.Contains("توريد", StringComparison.Ordinal))
            return "أعمال الموقع";

        // A populated but non-operational page is still retained as a source
        // attachment.  It should not block a migration merely because its
        // author left the default SheetN name.
        if (text.Contains("اجمالي", StringComparison.Ordinal) || text.Contains("ملاحظات", StringComparison.Ordinal) ||
            text.Contains("تقرير", StringComparison.Ordinal) || text.Contains("مرفق", StringComparison.Ordinal))
            return "مرفقات/تقارير";
        return "صفحة غير معنونة";
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsGeneratedImportCode(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value.Trim(), "^IMP-[A-F0-9]{8}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static string? ExtractClient(XLWorkbook workbook)
    {
        var labeled = ExtractLabeledValue(workbook, "اسم العميل", LooksLikeClientName);
        if (LooksLikeClientName(labeled)) return labeled;

        // A few historical files do not place the label and value together.
        // Keep the legacy fallback only when A5 is semantically a client name;
        // never turn a work title such as "أعمال ..." into the client.
        var siteSheet = workbook.Worksheets.FirstOrDefault(item => ClassifySheet(item.Name) == "أعمال الموقع");
        var legacy = ExtractValue(siteSheet, "A5");
        return LooksLikeClientName(legacy) ? legacy : null;
    }

    private static bool LooksLikeClientName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || LooksLikeStandaloneLabel(value)) return false;
        var normalized = NormalizeHeader(value);
        if (normalized.Length < 2) return false;

        // These are operational labels commonly found beside the customer
        // label in the live workbooks. They are not customer identities.
        var operationalPrefixes = new[]
        {
            "اعمال", "حساب", "كشف", "بيان", "ملاحظات", "دفعات", "اجمالي",
            "توريد", "مصروف"
        };
        if (operationalPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal))) return false;

        var exactOperationalNames = new[]
        {
            "خرسانات", "حديد", "الحفر", "الردم", "المحاره", "العزل", "الكهرباء",
            "السباكه", "المباني", "التشطيبات", "المخلقات", "النقل"
        };
        return !exactOperationalNames.Contains(normalized, StringComparer.Ordinal);
    }

    private static bool LooksLikeStandaloneLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = NormalizeHeader(value);
        return new[]
        {
            "اسم العميل", "اسم المالك", "الدافع", "الدفعات الرئيسية", "الموقع",
            "رقم القطعة", "رقم قطعه", "التاريخ", "البيان", "الملاحظات", "المبلغ",
            "الاجمالي", "اسم المورد", "المورد", "العميل", "المالك"
        }.Select(NormalizeHeader).Contains(normalized, StringComparer.Ordinal);
    }

    private static string? ExtractPlotNumber(XLWorkbook workbook)
    {
        var value = ExtractLabeledValue(workbook, "رقم القطعة") ?? ExtractLabeledValue(workbook, "رقم قطعه");
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = Regex.Match(value, @"[0-9٠-٩]+(?:\s*[-/]\s*[0-9٠-٩]+)?");
        // A missing plot number is valid. Never turn a neighboring label such
        // as "اسم العميل:" into a project identity; the caller will generate
        // a unique IMP-* code when no real numeric plot number is available.
        return match.Success ? match.Value.Replace(" ", string.Empty) : null;
    }

    internal static string? ExtractLabeledValue(XLWorkbook workbook, string label, Func<string, bool>? validator = null, bool exactLabel = false)
    {
        validator ??= static value => !string.IsNullOrWhiteSpace(value);
        var normalizedLabel = NormalizeHeader(label);
        var candidates = new List<(string Value, int Score)>();
        foreach (var sheet in workbook.Worksheets)
        {
            var used = sheet.RangeUsed();
            if (used is null) continue;
            foreach (var row in used.Rows())
            {
                var cells = row.CellsUsed().ToList();
                for (var index = 0; index < cells.Count; index++)
                {
                    var text = SafeCellText(cells[index]);
                    var separator = text.IndexOf(':');
                    var candidateLabel = exactLabel && separator >= 0 ? text[..separator] : text;
                    var normalized = NormalizeHeader(candidateLabel);
                    if (exactLabel ? normalized != normalizedLabel : !normalized.Contains(normalizedLabel, StringComparison.Ordinal)) continue;
                    if (separator >= 0 && separator < text.Length - 1)
                    {
                        var inline = NullIfEmpty(text[(separator + 1)..]);
                        if (!LooksLikeStandaloneLabel(inline) && validator(inline!)) candidates.Add((inline!, 100));
                    }
                    // Do not use the next non-empty cell in the row: Excel
                    // templates often have unrelated merged sections on the
                    // same row (for example a label at F2 and another title
                    // at O2). That old approach could turn "حساب الحفر"
                    // into a plot number. Only accept an inline value, the
                    // immediately adjacent physical cell, or the cell below.
                    var anchor = cells[index].Address;
                    if (anchor.ColumnNumber < 16_384)
                    {
                        var adjacent = SafeCellText(sheet.Cell(anchor.RowNumber, anchor.ColumnNumber + 1));
                        if (!LooksLikeStandaloneLabel(adjacent) && validator(adjacent)) candidates.Add((adjacent.Trim(), 50));
                    }
                    if (anchor.RowNumber < 1_048_576)
                    {
                        var below = SafeCellText(sheet.Cell(anchor.RowNumber + 1, anchor.ColumnNumber));
                        if (!LooksLikeStandaloneLabel(below) && validator(below)) candidates.Add((below.Trim(), 40));
                    }
                }
            }
        }
        return candidates
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Value.Length)
            .Select(item => item.Value)
            .FirstOrDefault();
    }

    private static string? ExtractValue(IXLWorksheet? sheet, string address)
    {
        if (sheet is null) return null;
        var value = SafeCellText(sheet.Cell(address));
        var separator = value.IndexOf(':');
        return separator >= 0 ? NullIfEmpty(value[(separator + 1)..]) : NullIfEmpty(value);
    }

    private static decimal ExtractOfficeShare(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.FirstOrDefault(item => ClassifySheet(item.Name) == "كشف الحساب النهائي");
        if (sheet is null) return 0;
        // The live files place this value in different columns/rows. Prefer a
        // percentage that is explicitly adjacent to the office-share label and
        // only then fall back to the historical H13 position.
        foreach (var row in sheet.RowsUsed())
        {
            foreach (var cell in row.CellsUsed())
            {
                if (!NormalizeHeader(SafeCellText(cell)).Contains("نسبهالمكتب", StringComparison.Ordinal)) continue;
                foreach (var candidate in row.CellsUsed().Concat(sheet.Row(cell.Address.RowNumber + 1).CellsUsed()))
                {
                    var parsed = ParsePercentage(SafeCellText(candidate));
                    if (parsed > 0) return parsed;
                    if (candidate.TryGetValue<decimal>(out var numeric) && numeric > 0 && numeric <= 100)
                        return numeric <= 1 ? numeric * 100 : numeric;
                }
            }
        }
        var value = sheet.Cell("H13").TryGetValue<decimal>(out var percentage) ? percentage : 0m;
        return value <= 1 ? value * 100 : value;
    }

    private static decimal ParsePercentage(string value)
    {
        var normalized = value.Trim().Replace("٪", "%").Replace(",", string.Empty).Replace("٫", ".");
        if (!normalized.EndsWith('%')) return 0m;
        var percent = normalized[..^1].Trim();
        return decimal.TryParse(percent, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0m && parsed <= 100m
            ? parsed : 0m;
    }

    private static string Normalize(string value)
    {
        var cleaned = value.Trim()
            .Replace('：', ':')
            .Replace(':', ' ')
            .Replace('ـ', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ');
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ClassifyUnknown(string name) => name.StartsWith("sheet", StringComparison.OrdinalIgnoreCase) ? "صفحة غير معنونة" : "أعمال أخرى";

    private static WorkbookPreview Invalid(string fileName, long size, string warning) =>
        new(fileName, size, false, true, [], [warning]);
}
