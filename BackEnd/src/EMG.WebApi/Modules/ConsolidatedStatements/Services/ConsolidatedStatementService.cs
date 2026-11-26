using EMG.Api.Modules.AccountingClassification.Dtos;
using EMG.Api.Modules.AccountingClassification.Services;
using EMG.Api.Modules.ConsolidatedStatements.Dtos;
using EMG.Api.Modules.FinalAccounts.Services;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.ConsolidatedStatements.Services;

public sealed class ConsolidatedStatementService(EmgDbContext db, IAccountingClassificationService classificationService) : IConsolidatedStatementService
{
    private static readonly IReadOnlyDictionary<string, (string Heading, AccountingClassificationCode? Code)> ColumnMap =
        new Dictionary<string, (string, AccountingClassificationCode?)>
        {
            ["G"] = ("إجمالي أعمال الموقع", AccountingClassificationCode.SiteExpense),
            ["H"] = ("إجمالي الخرسانة", null),
            ["I"] = ("إجمالي حساب العرب", AccountingClassificationCode.Excavation),
            ["J"] = ("إجمالي حساب المقاول", AccountingClassificationCode.ContractorWorkmanship),
            ["K"] = ("إجمالي حساب المحطة", AccountingClassificationCode.ConcreteStation),
            ["L"] = ("إجمالي حساب الحديد", AccountingClassificationCode.Steel),
            ["M"] = ("إجمالي أعمال المباني", AccountingClassificationCode.Buildings),
            ["N"] = ("إجمالي حساب المحارة", AccountingClassificationCode.Plaster),
            ["O"] = ("إجمالي حساب العزل", AccountingClassificationCode.Insulation),
            ["P"] = ("إجمالي حساب الكهرباء", AccountingClassificationCode.Electrical),
            ["Q"] = ("إجمالي حساب السباكة", AccountingClassificationCode.Plumbing),
            ["R"] = ("إجمالي حساب المكتب", AccountingClassificationCode.OfficePayment),
            ["S"] = ("إجمالي الدفعات", AccountingClassificationCode.OwnerFunding),
            ["T"] = ("صافي المعاملة", null),
            ["U"] = ("ملاحظات", null)
        };

    public async Task<ConsolidatedStatementPreview> PreviewAsync(int? userId, bool isGlobalAccess, CancellationToken cancellationToken)
    {
        List<ProjectRef> projects;
        if (isGlobalAccess)
        {
            projects = await db.Projects.AsNoTracking().OrderBy(item => item.Code).ThenBy(item => item.Id)
                .Select(item => new ProjectRef(item.Id, item.Code, item.Name)).ToListAsync(cancellationToken);
        }
        else if (userId is null)
        {
            projects = [];
        }
        else
        {
            var authorized = await db.UserProjectAssignments.AsNoTracking().Where(item => item.UserId == userId && item.IsActive)
                .Join(db.Projects.AsNoTracking(), assignment => assignment.ProjectId, project => project.Id, (_, project) => new { project.Id, project.Code, project.Name })
                .Distinct().OrderBy(item => item.Code).ThenBy(item => item.Id).ToListAsync(cancellationToken);
            projects = authorized.Select(item => new ProjectRef(item.Id, item.Code, item.Name)).ToList();
        }

        var rows = new List<ConsolidatedStatementRow>(projects.Count);
        foreach (var project in projects)
        {
            var classification = await classificationService.PreviewAsync(project.Id, userId, isGlobalAccess, cancellationToken);
            if (classification is not null) rows.Add(BuildRow(classification));
        }

        return new ConsolidatedStatementPreview(DateTime.UtcNow,
            "Read-only projection. ImportedAmount is the imported subset of approved/posted rows; CurrentAmount includes all approved/posted rows. T and H are diagnostic only.", rows);
    }

    private static ConsolidatedStatementRow BuildRow(AccountingClassificationPreview source)
    {
        var visible = source.Lines.Where(item => item.IncludedInCurrentTotals).ToList();
        var unresolvedLines = source.UnresolvedRecords;
        var unresolved = unresolvedLines.Select(item => new UnresolvedRecordDiagnostic(item.EntityType, item.EntityId, item.Amount, item.Status,
            item.Provenance.SourceText, item.Provenance.SourceSheet, item.Provenance.SourceRow, item.Diagnostic)).ToList();
        var concrete = visible.Where(item => item.EntityType == "concrete").Sum(item => item.Amount);
        var resolvedOperational = visible.Where(IsOperational).Sum(item => item.Amount);
        var unresolvedOperational = visible.Where(item => item.Resolution == ClassificationResolution.Unresolved && IsOperationalSource(item.EntityType)).Sum(item => item.Amount);
        var dimensions = new ConsolidatedFinancialDimensions(
            Dimension(visible, IsOperational, "Direct operational costs; each classified work row is counted once."),
            Dimension(visible, item => item.Classification == AccountingClassificationCode.SupplierSupply, "Supplier liability/supply schedule; not another operational cost."),
            Dimension(visible, item => item.Classification == AccountingClassificationCode.SupplierPayment, "Supplier cash payment schedule; not another supply or work cost."),
            Dimension(visible, item => item.Classification == AccountingClassificationCode.OwnerFunding, "Actual approved/posted owner funding receipts only."),
            Dimension(visible, item => item.Classification == AccountingClassificationCode.FundingAllocation, "Advance allocations; not new owner receipts."),
            Dimension(visible, item => item.Classification == AccountingClassificationCode.OfficePayment, "Office allocations/payments; separate from office-fee accrual."),
            Dimension(visible, item => item.Resolution == ClassificationResolution.Unresolved, "Financial rows requiring review; excluded from resolved category totals."));

        var officePercentage = source.OfficeSharePercentage;
        var hasUnresolvedBasis = visible.Any(item => item.Resolution == ClassificationResolution.Unresolved && IsOperationalSource(item.EntityType));
        var officeBasis = hasUnresolvedBasis ? resolvedOperational : visible.Where(item => IsOperationalSource(item.EntityType)).Sum(item => item.Amount);
        var office = OfficeFeeCalculation.Calculate(officeBasis, officePercentage, dimensions.OfficePayments.CurrentAmount);
        var officeAccrual = new OfficeAccrualPreview(office.Fee, officePercentage, officeBasis, !hasUnresolvedBasis,
            hasUnresolvedBasis ? "INCOMPLETE_UNVERIFIED" : "VERIFIED_PROJECTION",
            hasUnresolvedBasis
                ? "Unresolved operational rows remain outside the diagnostic basis."
                : "Project outgoing multiplied by its office percentage. Office payments reduce the outstanding fee.")
        {
            OfficeFee = hasUnresolvedBasis ? null : office.Fee,
            OfficeOutstanding = hasUnresolvedBasis ? null : office.Outstanding
        };
        var netDiagnostics = new List<NetTransactionDiagnostic>
        {
            new("funding-minus-resolved-operational-costs", dimensions.OwnerFunding.CurrentAmount - resolvedOperational,
                "OwnerFunding.CurrentAmount - OperationalCosts.CurrentAmount", "DIAGNOSTIC_ONLY"),
            new("funding-minus-operational-costs-including-unresolved", dimensions.OwnerFunding.CurrentAmount - resolvedOperational - unresolvedOperational,
                "OwnerFunding.CurrentAmount - resolved operational costs - unresolved operational rows", "INCOMPLETE_UNVERIFIED")
        };

        var columns = ColumnMap.Select(pair => BuildColumn(pair.Key, pair.Value.Heading, pair.Value.Code, source, concrete, unresolvedLines)).ToList();
        var complete = unresolved.Count == 0;
        return new ConsolidatedStatementRow(source.ProjectId, source.ProjectCode, source.ProjectName, columns, dimensions, officeAccrual,
            netDiagnostics, unresolved, complete, complete ? "COMPLETE_FOR_RESOLVED_CATEGORIES" : "INCOMPLETE_UNVERIFIED");
    }

    private static MasterStatementColumn BuildColumn(string column, string heading, AccountingClassificationCode? code, AccountingClassificationPreview source,
        decimal concreteAggregate, IReadOnlyList<AccountingClassificationLine> unresolved)
    {
        if (column == "H") return new(column, heading, null, null, concreteAggregate, 0, unresolved.Count(item => item.EntityType == "concrete"), false,
            "UNAPPROVED_OVERLAP", "Concrete aggregate is diagnostic only and may overlap J/K; it is not an official monetary total.");
        if (column == "T") return new(column, heading, null, null, null, 0, 0, false, "UNAPPROVED", "Net Transaction remains null until its sign and component basis are approved.");
        if (column == "U") return new(column, heading, null, null, null, unresolved.Sum(item => item.Amount), unresolved.Count, false, "DIAGNOSTIC", "Source references and unresolved diagnostics are returned at row level.");
        var total = source.Totals.Single(item => item.Classification == code);
        var affected = unresolved.Where(item => code == AccountingClassificationCode.ContractorWorkmanship || code == AccountingClassificationCode.ConcreteStation
            ? item.EntityType == "concrete" : false).ToList();
        var status = total.UnresolvedRecordCount > 0 || affected.Count > 0 ? "INCOMPLETE_UNVERIFIED" : "VERIFIED_PROJECTION";
        return new(column, heading, total.ImportedApprovedPostedAmount, total.CurrentApprovedPostedAmount, null,
            affected.Sum(item => item.Amount), affected.Count, true, status, status == "VERIFIED_PROJECTION" ? "Resolved classification total." : "Unresolved source rows affect this category.");
    }

    private static StatementDimension Dimension(IEnumerable<AccountingClassificationLine> lines, Func<AccountingClassificationLine, bool> predicate, string meaning)
    {
        var matching = lines.Where(predicate).ToList();
        return new(matching.Where(item => item.Provenance.IsImported).Sum(item => item.Amount), matching.Sum(item => item.Amount),
            matching.Count(item => item.Provenance.IsImported), matching.Count, meaning);
    }

    private static bool IsOperational(AccountingClassificationLine item) => item.Resolution == ClassificationResolution.Resolved && item.Classification is
        AccountingClassificationCode.SiteExpense or AccountingClassificationCode.Excavation or AccountingClassificationCode.ContractorWorkmanship or AccountingClassificationCode.ConcreteStation or
        AccountingClassificationCode.Steel or AccountingClassificationCode.Buildings or AccountingClassificationCode.Plaster or AccountingClassificationCode.Insulation or AccountingClassificationCode.Electrical or AccountingClassificationCode.Plumbing;

    private static bool IsOperationalSource(string entityType) => entityType is "site-movement" or "excavation" or "concrete" or "steel" or "finishing";

    private sealed record ProjectRef(int Id, string Code = "", string Name = "");
}
