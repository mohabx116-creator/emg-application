using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using EMG.Api.Modules.Security.Authorization;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EMG.Api.Modules.UniversalImports.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[PermissionAuthorize("data.migrate")]
[Route("api/projects/{projectId:int}/historical-settlements/{settlementId:long}/beneficiary")]
public sealed class HistoricalBeneficiaryCorrectionController(
    EmgDbContext db, IWebHostEnvironment environment, IConfiguration configuration) : ControllerBase
{
    private const long MaxWorkbookBytes = 20 * 1024 * 1024;
    public sealed record Proposal(int ProjectId, long SettlementId, int BatchId, string WorkbookSha256, string Sheet,
        string Cell, decimal Amount, string Currency, string Role, int AccountId, string Name,
        string TitleCell, string PaymentHeader, string PartyCell, string WorkCell);
    public sealed record Preview(Proposal Change, string ProposalSha256);
    public sealed class Form
    {
        public required IFormFile Workbook { get; init; }
        public string? ConfirmProposalSha256 { get; init; }
    }

    [HttpPost("preview")]
    [RequestSizeLimit(MaxWorkbookBytes + 65536)]
    public Task<ActionResult<Preview>> PreviewChange(int projectId, long settlementId, [FromForm] Form input,
        CancellationToken ct) => Review(projectId, settlementId, input, false, ct);

    [HttpPost("apply")]
    [RequestSizeLimit(MaxWorkbookBytes + 65536)]
    public Task<ActionResult<Preview>> Apply(int projectId, long settlementId, [FromForm] Form input,
        CancellationToken ct) => Review(projectId, settlementId, input, true, ct);

    private async Task<ActionResult<Preview>> Review(int projectId, long settlementId, Form input, bool apply, CancellationToken ct)
    {
        if (!environment.IsDevelopment() || !db.Database.IsNpgsql()) return Forbid();
        var target = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        if (target.Database != "emg_restore" || target.Port != 55464 || target.Host is not ("127.0.0.1" or "localhost"))
            return Forbid();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) ||
            apply && (actor <= 0 || actor != configuration.GetValue<int>("HistoricalCorrections:OwnerUserId")))
            return Forbid();
        if (input.Workbook is null || input.Workbook.Length is <= 0 or > MaxWorkbookBytes)
            return BadRequest(new { message = "أرفق المصنف الأصلي ضمن حد حجم الملف." });
        try
        {
            var archive = apply ? await VerifyPinnedBackupAsync(ct) : null;
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var line = await db.HistoricalSettlements.Include(s => s.Batch)
                .SingleOrDefaultAsync(s => s.Id == settlementId && s.Batch.ProjectId == projectId, ct);
            if (line is null) return NotFound();
            if (line.SupplierId.HasValue || line.ContractorId.HasValue)
                return Conflict(new { message = "توجد علاقة مستفيد بالفعل. أعد قراءة الحساب." });
            using var original = new MemoryStream();
            await input.Workbook.CopyToAsync(original, ct);
            var hash = Convert.ToHexString(SHA256.HashData(original.GetBuffer().AsSpan(0, checked((int)original.Length))));
            if (!string.Equals(hash, line.Batch.SourceSha256, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "بصمة الملف لا تطابق أصل دفعة الاستيراد." });
            original.Position = 0;
            using var book = new XLWorkbook(original);
            var proposal = await ProposeAsync(db, line, book, ct);
            var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(proposal)));
            var preview = new Preview(proposal, digest);
            if (!apply) return Ok(preview);
            if (!string.Equals(digest, input.ConfirmProposalSha256, StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "يلزم تأكيد بصمة مقترح المستفيد بعد مراجعة تفاصيله." });

            if (proposal.Role == "supplier") line.SupplierId = proposal.AccountId;
            else line.ContractorId = proposal.AccountId;
            db.HistoricalImportAuditEvents.Add(new HistoricalImportAuditEvent
            {
                BatchId = line.BatchId, ActorUserId = actor, Action = "HISTORICAL_SETTLEMENT_BENEFICIARY_LINKED",
                DetailsJson = JsonSerializer.Serialize(new { preview, BackupSha256 = archive, PreviousBeneficiary = (int?)null })
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Ok(preview);
        }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Conflict(new { message = "تعذر التحقق من المصنف أو نسخة الاستعادة المثبتة." });
        }
        catch (Exception e) when (e is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } ||
            e is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } })
        {
            return Conflict(new { message = "تغير الحساب بالتزامن. أعد معاينة التصحيح." });
        }
    }

    // One supported layout: a named whole-account title, a payment column inside the original
    // structured table, and an existing named work line on the very same row.
    internal static async Task<Proposal> ProposeAsync(EmgDbContext db, HistoricalSettlement line, XLWorkbook book, CancellationToken ct)
    {
        if (line.Currency != "EGP" || !book.TryGetWorksheet(line.Sheet, out var sheet))
            throw new InvalidOperationException("العملة أو ورقة التسوية غير مدعومة لهذا التصحيح.");
        var cell = sheet.Cell(line.Cell);
        if (cell.HasFormula || cell.DataType != XLDataType.Number || decimal.Round((decimal)cell.GetDouble(), 6) != line.Amount)
            throw new InvalidOperationException("مبلغ المصدر أو إشارته يختلف عن التسوية، أو أن المبلغ مشتق من صيغة.");
        var title = sheet.Cell("C2").GetString().Split([':', '：'], 2, StringSplitOptions.TrimEntries);
        if (title.Length != 2 || !title[0].StartsWith("حساب ", StringComparison.Ordinal) || title[0] != line.Sheet.Trim() ||
            string.IsNullOrWhiteSpace(title[1]))
            throw new InvalidOperationException("لا يوجد عنوان صريح باسم صاحب حساب الدفعات في هذا القالب.");
        var name = title[1];
        using var provenance = JsonDocument.Parse(line.SourceJson);
        if (!provenance.RootElement.TryGetProperty("header", out var headerRef) || headerRef.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("مرجع عنوان الدفعات مفقود.");
        var header = sheet.Cell(headerRef.GetString()!);
        var row = cell.Address.RowNumber;
        var table = sheet.Tables.SingleOrDefault(t => t.RangeAddress.FirstAddress.RowNumber == header.Address.RowNumber &&
            t.RangeAddress.LastAddress.RowNumber - (t.ShowTotalsRow ? 1 : 0) >= row && row > header.Address.RowNumber &&
            t.RangeAddress.FirstAddress.ColumnNumber <= cell.Address.ColumnNumber && t.RangeAddress.LastAddress.ColumnNumber >= cell.Address.ColumnNumber);
        if (table is null || header.Address.ColumnNumber != cell.Address.ColumnNumber || header.GetString().Trim() != "الدفعات")
            throw new InvalidOperationException("لم تثبت عضوية الخلية في جدول الدفعات الأصلي.");
        var partyHeaders = table.HeadersRow().Cells().Where(h => h.GetString().Trim() == "اسم المورد").ToList();
        if (partyHeaders.Count != 1) throw new InvalidOperationException("عنوان الطرف غير محدد داخل الجدول.");
        var partyCell = sheet.Cell(row, partyHeaders[0].Address.ColumnNumber);
        if (partyCell.GetString().Trim() != name) throw new InvalidOperationException("اسم الطرف في الصف يختلف عن عنوان الحساب.");
        var works = await db.HistoricalWorkLines.AsNoTracking().Where(w => w.BatchId == line.BatchId && w.Sheet == line.Sheet &&
            w.SourceRow == row && w.Currency == line.Currency && w.Party == name &&
            (w.SupplierId != null || w.ContractorId != null)).ToListAsync(ct);
        if (works.Count != 1) throw new InvalidOperationException("لا يوجد بند عمل وحيد مؤيد بنفس الطرف في صف المشروع.");
        var work = works[0];
        var workCell = sheet.Cell(work.Cell);
        if (workCell.Address.RowNumber != row || workCell.Address.ColumnNumber < table.RangeAddress.FirstAddress.ColumnNumber ||
            workCell.Address.ColumnNumber >= cell.Address.ColumnNumber || workCell.CachedValue.Type != XLDataType.Number ||
            decimal.Round((decimal)workCell.CachedValue.GetNumber(), 6) != work.Amount ||
            work.SupplierId.HasValue == work.ContractorId.HasValue)
            throw new InvalidOperationException("تعارض دليل المبلغ أو دور الطرف في بند العمل.");
        var role = work.SupplierId.HasValue ? "supplier" : "contractor";
        var id = work.SupplierId ?? work.ContractorId!.Value;
        var matchingAccounts = work.SupplierId.HasValue
            ? await db.Suppliers.Where(a => a.IsActive && a.Name == name).Select(a => a.Id).ToListAsync(ct)
            : await db.Contractors.Where(a => a.IsActive && a.Name == name).Select(a => a.Id).ToListAsync(ct);
        if (matchingAccounts.Count != 1 || matchingAccounts[0] != id)
            throw new InvalidOperationException("اسم الحساب أو دوره لا يطابق حسابًا نشطًا وحيدًا.");
        return new(line.Batch.ProjectId, line.Id, line.BatchId, line.Batch.SourceSha256, line.Sheet, line.Cell,
            line.Amount, line.Currency, role, id, name, "C2", headerRef.GetString()!, partyCell.Address.ColumnLetter + row, work.Cell);
    }

    private async Task<string> VerifyPinnedBackupAsync(CancellationToken ct)
    {
        var path = configuration["HistoricalCorrections:VerifiedBackupPath"];
        var expected = configuration["HistoricalCorrections:VerifiedBackupSha256"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || expected?.Length != 64 ||
            !Path.GetFullPath(path).StartsWith(@"E:\mohabx_116\EMG-backups\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("يلزم تثبيت نسخة محلية متحقق منها وبصمتها قبل تطبيق تصحيح.");
        var file = new FileInfo(Path.GetFullPath(path));
        for (FileSystemInfo? part = file; part != null; part = part is FileInfo f ? f.Directory : ((DirectoryInfo)part).Parent)
            if ((part.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("مسار النسخة المعتمدة يجب أن يكون مباشرًا.");
        await using var dump = file.OpenRead();
        var signature = new byte[5];
        await dump.ReadExactlyAsync(signature, ct);
        dump.Position = 0;
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(dump, ct));
        if (Encoding.ASCII.GetString(signature) != "PGDMP" || !actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("النسخة لا تطابق أرشيف PostgreSQL المتحقق منه.");
        return actual;
    }
}
