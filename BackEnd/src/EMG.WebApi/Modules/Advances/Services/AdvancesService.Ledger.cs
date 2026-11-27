using System.Data;
using EMG.Api.Modules.Advances.Dtos;
using EMG.Api.Modules.FinalAccounts.Services;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Advances.Services;

public sealed partial class AdvancesService
{
    public async Task<AdvanceLedger?> GetLedgerAsync(int? projectId, int? userId, bool isAdmin, CancellationToken ct)
    {
        await using var snapshot = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct) : null;
        var projects = db.Projects.AsNoTracking().Where(p => projectId == null || p.Id == projectId);
        if (!isAdmin) projects = projects.Where(p => db.UserProjectAssignments.Any(a => a.ProjectId == p.Id && a.UserId == userId && a.IsActive));
        var accessible = await projects.Select(p => new { p.Id,p.Name }).ToDictionaryAsync(p => p.Id,p => p.Name,ct);
        if (projectId != null && accessible.Count == 0) return null;
        var ids = accessible.Keys.ToArray();
        var original = await db.HistoricalFundingLines.AsNoTracking().Where(f => ids.Contains(f.Batch.ProjectId))
            .OrderBy(f => f.Batch.ProjectId).ThenBy(f => f.SourceRow).ThenBy(f => f.Id)
            .Select(f => new { Line = f, f.Batch.ProjectId, Workbook = f.Batch.SourceFileName }).ToListAsync(ct);
        var links = new Dictionary<long,int>();
        if (original.Any(f => f.Line.Stream == "main_schedule" || f.Line.Stream == "unallocated_schedule"))
        {
            var linked = await db.Database.SqlQuery<RecoveredReceipt>($"""
                SELECT r.line_id AS "LineId", r.advance_id AS "ReceiptId"
                FROM historical_receipt_recoveries r JOIN advance_payments a ON a."Id" = r.advance_id
                WHERE a."ProjectId" = ANY({ids})
                """).ToListAsync(ct);
            links = linked.ToDictionary(r => r.LineId,r => r.ReceiptId);
        }
        var receipts = await GetAsync(projectId,userId,isAdmin,ct);
        var linkedIds = links.Values.ToHashSet();
        var postedIds = receipts.Where(r => r.Status is "مرحّل" or "معتمد").Select(r => r.Id).ToHashSet();
        var components = new List<HistoricalAdvanceRow>();
        var basis = new HistoricalFundingBasis(original.Select(item => item.Line).ToList());
        foreach (var item in original.Where(f => (f.Line.Stream != "source_control" || f.Line.Basis == "verified_zero") && f.Line.Stream != "funding_qualification"))
        {
            var f = item.Line;
            var disposition = basis.Disputed(f) && f.Basis == "project_allocation" ? "conflicting_ownership" : f.Basis;
            var hasReceipt = links.TryGetValue(f.Id,out var receipt);
            components.Add(new(f.Id,item.ProjectId,accessible[item.ProjectId],item.Workbook,f.Sheet,f.Cell,f.SourceDate,
                f.Narrative,f.Category,f.Amount,f.Currency,disposition,f.Basis == "main_schedule_entry" && hasReceipt && postedIds.Contains(receipt) || basis.AdditionalFunding(f),
                basis.Allocates(f),hasReceipt ? receipt : null));
        }
        var directFunding = await db.SiteMovements.AsNoTracking().Where(p => ids.Contains(p.ProjectId) &&
                p.MovementType == "وارد" && (p.Status == "مرحّل" || p.Status == "معتمد"))
            .Select(p => new DirectFundingRow(p.Id,p.ProjectId,p.Project.Name,"وارد الموقع",p.PaidTo ?? "",p.MovementDate,
                p.TotalAmount,p.Currency,p.Reference ?? "")).ToListAsync(ct);
        directFunding.AddRange(await db.SupplierPayments.AsNoTracking().Where(p => p.ProjectId.HasValue && ids.Contains(p.ProjectId.Value) &&
                p.PaymentSource == "مالك مباشر" && (p.Status == "مرحّل" || p.Status == "معتمد"))
            .Select(p => new DirectFundingRow(p.Id,p.ProjectId!.Value,p.Project!.Name,"سداد المالك للمورد",p.Supplier.Name,p.PaymentDate,
                p.Amount,"EGP",p.Reference ?? "")).ToListAsync(ct));
        directFunding.AddRange(await db.ContractorPayments.AsNoTracking().Where(p => ids.Contains(p.ProjectId) && p.PaymentSource == "مالك مباشر")
            .Select(p => new DirectFundingRow(p.Id,p.ProjectId,p.Project.Name,"سداد المالك للمقاول",p.Contractor.Name,p.PaymentDate,
                p.Amount,p.Currency,p.Reference ?? "")).ToListAsync(ct));
        var accounts = new FinalAccountsService(db);
        var totals = new List<AdvanceCurrencyTotal>();
        var warnings = new List<string>();
        foreach (var (id,name) in accessible)
        {
            var account = await accounts.GetAsync(id,userId,isAdmin,ct);
            if (account == null) continue;
            warnings.AddRange(account.FinancialWarnings.Select(w => $"{name} — {w}"));
            foreach (var currency in account.Currencies)
            {
                var allocated = currency.Currency == "EGP" ? account.IncomingByCategory.Values.Sum()
                    : account.Sections.Where(s => s.Currency == currency.Currency).Sum(s => s.AllocatedFunding) + currency.OfficePayments;
                totals.Add(new(currency.Currency,currency.Works,currency.Payments,
                    currency.Currency == "EGP" && account.Sections.Any(s => s.FundingUnknown) ? null : allocated,currency.CashBalance));
            }
        }
        var portfolio = totals.GroupBy(t => t.Currency).Select(g => new AdvanceCurrencyTotal(g.Key,g.Sum(t => t.Works),
            g.All(t => t.Payments.HasValue) ? g.Sum(t => t.Payments) : null,
            g.All(t => t.Allocated.HasValue) ? g.Sum(t => t.Allocated) : null,
            g.All(t => t.CashBalance.HasValue) ? g.Sum(t => t.CashBalance) : null)).ToList();
        var ordinary = receipts.Where(r => !linkedIds.Contains(r.Id)).ToList();
        var fundingEntries = ordinary.Where(r => postedIds.Contains(r.Id))
            .Select(r => new ProjectFundingEntry("advance", r, null, null))
            .Concat(components.Where(r => r.FundsProject).Select(r => new ProjectFundingEntry("historical", null, r, null)))
            .Concat(directFunding.Select(r => new ProjectFundingEntry("direct", null, null, r)))
            .OrderByDescending(r => r.Advance?.PaymentDate ?? r.Historical?.Date ?? r.Direct?.Date)
            .ToList();
        var allocations = await db.AdvanceAllocations.AsNoTracking()
            .Where(a => ids.Contains(a.AdvancePayment.ProjectId) &&
                (a.AdvancePayment.Status == "مرحّل" || a.AdvancePayment.Status == "معتمد"))
            .OrderBy(a => a.Id)
            .Select(a => new ProjectAllocationEntry(a.AdvancePayment.ProjectId, a.AdvancePayment.Currency,
                new AllocationSummary(a.Id, a.AdvancePaymentId, a.SiteMovementId, a.SupplierPaymentId,
                    a.AccountCategory, a.Amount, a.AllocationDate, a.Notes ?? ""), null))
            .ToListAsync(ct);
        allocations.AddRange(components.Where(r => r.IsAllocation).Select(r =>
            new ProjectAllocationEntry(r.ProjectId, r.Currency, null, r)));
        if (snapshot != null) await snapshot.CommitAsync(ct);
        return new(ordinary, components, directFunding, portfolio, warnings)
        {
            FundingEntries = fundingEntries,
            AllocationEntries = allocations
        };
    }

    private sealed record RecoveredReceipt(long LineId,int ReceiptId);
}
