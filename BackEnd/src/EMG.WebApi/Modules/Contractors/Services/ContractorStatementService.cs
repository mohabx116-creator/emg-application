using ClosedXML.Excel;
using System.Data;
using EMG.Api.Modules.Contractors.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Contractors.Services;

public sealed class ContractorStatementService(EmgDbContext db) : IContractorStatementService
{
    private static readonly string[] EligibleTypes = ["site-movement", "excavation", "concrete", "steel", "finishing"];

    public async Task<IReadOnlyList<ContractorSummary>> GetContractorsAsync(int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        var query = db.Contractors.AsNoTracking().Where(item => item.IsActive);
        var assigned = db.UserProjectAssignments.Where(a => a.IsActive && a.UserId == userId).Select(a => a.ProjectId);
        var works = db.HistoricalWorkLines.Where(w => w.ContractorId != null && w.Currency == "EGP" && (globalAccess || assigned.Contains(w.Batch.ProjectId)));
        var settlements = db.HistoricalSettlements.Where(s => s.ContractorId != null && s.Currency == "EGP" && (globalAccess || assigned.Contains(s.Batch.ProjectId)));
        if (!globalAccess)
            query = query.Where(item => db.ContractorContracts.Any(c => c.ContractorId == item.Id && assigned.Contains(c.ProjectId))
                || works.Any(w => w.ContractorId == item.Id) || settlements.Any(s => s.ContractorId == item.Id));
        var lines = await works.Select(w => new { Id = w.ContractorId!.Value, w.Batch.ProjectId, ProjectName = w.Batch.Project.Name, w.Currency, Works = w.Amount, Settled = 0m })
            .Concat(settlements.Select(s => new { Id = s.ContractorId!.Value, s.Batch.ProjectId, ProjectName = s.Batch.Project.Name, s.Currency, Works = 0m, Settled = s.Amount }))
            .GroupBy(v => new { v.Id,v.ProjectId,v.ProjectName,v.Currency })
            .Select(g => new { g.Key.Id, Balance = new ContractorHistoricalBalance(g.Key.ProjectId,g.Key.ProjectName,g.Key.Currency,g.Sum(v => v.Works),g.Sum(v => v.Settled)) })
            .ToListAsync(cancellationToken);
        var balances = lines.ToLookup(l => l.Id,l => l.Balance);
        var contractors = await query.OrderBy(c => c.Name).Select(ToContractorSummary()).ToListAsync(cancellationToken);
        return contractors.Select(c => c with { HistoricalBalances = balances[c.Id].ToList() }).ToList();
    }

    public async Task<ContractorSummary?> CreateContractorAsync(CreateContractorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return null;
        var contractor = new Contractor { Name = request.Name.Trim(), Phone = request.Phone?.Trim(), TaxNumber = request.TaxNumber?.Trim(), Notes = request.Notes?.Trim() };
        db.Contractors.Add(contractor);
        await db.SaveChangesAsync(cancellationToken);
        return new ContractorSummary(contractor.Id, contractor.Name, contractor.Phone ?? string.Empty, contractor.TaxNumber ?? string.Empty, contractor.Notes ?? string.Empty, contractor.IsActive);
    }

    public async Task<ContractorContractSummary?> CreateContractAsync(int contractorId, CreateContractRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        if (!await CanAccessProjectAsync(request.ProjectId, userId, globalAccess, cancellationToken) ||
            !await db.Contractors.AnyAsync(item => item.Id == contractorId && item.IsActive, cancellationToken) ||
            string.IsNullOrWhiteSpace(request.ContractNumber) || !IsCurrency(request.Currency) ||
            request.StartsOn is not null && request.EndsOn is not null && request.StartsOn > request.EndsOn) return null;
        var duplicate = await db.ContractorContracts.AnyAsync(item => item.ProjectId == request.ProjectId && item.ContractNumber == request.ContractNumber.Trim(), cancellationToken);
        if (duplicate) throw new InvalidOperationException("رقم العقد مستخدم بالفعل داخل المشروع.");
        var contract = new ContractorContract
        {
            ProjectId = request.ProjectId, ContractorId = contractorId, ContractNumber = request.ContractNumber.Trim(), Currency = request.Currency.Trim().ToUpperInvariant(),
            StartsOn = DateOnly(request.StartsOn), EndsOn = DateOnly(request.EndsOn), Terms = request.Terms?.Trim(), CreatedByUserId = userId
        };
        db.ContractorContracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);
        return await db.ContractorContracts.AsNoTracking().Where(item => item.Id == contract.Id).Select(ToContractSummary()).SingleAsync(cancellationToken);
    }

    public async Task<ContractorContractItemSummary?> AddItemAsync(int contractId, CreateContractItemRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        var contract = await db.ContractorContracts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == contractId, cancellationToken);
        if (contract is null || !await CanAccessProjectAsync(contract.ProjectId, userId, globalAccess, cancellationToken) || string.IsNullOrWhiteSpace(request.Description) || string.IsNullOrWhiteSpace(request.Unit) || request.ApprovedQuantity <= 0 || request.UnitRate < 0) return null;
        var item = new ContractorContractItem { ContractId = contractId, Description = request.Description.Trim(), Unit = request.Unit.Trim(), ApprovedQuantity = request.ApprovedQuantity, UnitRate = request.UnitRate, SourceReference = request.SourceReference?.Trim() };
        db.ContractorContractItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return new ContractorContractItemSummary(item.Id, item.ContractId, item.Description, item.Unit, item.ApprovedQuantity, item.UnitRate, item.SourceReference ?? string.Empty, 0, item.ApprovedQuantity);
    }

    public async Task<IReadOnlyList<ContractorContractSummary>> GetContractsAsync(int? projectId, int? contractorId, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        var query = db.ContractorContracts.AsNoTracking().AsQueryable();
        if (projectId is not null) query = query.Where(item => item.ProjectId == projectId);
        if (contractorId is not null) query = query.Where(item => item.ContractorId == contractorId);
        if (!globalAccess) query = query.Where(item => userId.HasValue && db.UserProjectAssignments.Any(assignment => assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive));
        return await query.OrderByDescending(item => item.Id).Select(ToContractSummary()).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContractorContractItemSummary>> GetItemsAsync(int contractId, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        var contract = await db.ContractorContracts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == contractId, cancellationToken);
        if (contract is null || !await CanAccessProjectAsync(contract.ProjectId, userId, globalAccess, cancellationToken)) return [];
        var items = await db.ContractorContractItems.AsNoTracking().Where(item => item.ContractId == contractId).OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var used = await db.ContractorStatementLines.AsNoTracking().Where(line => line.ContractItem.ContractId == contractId && line.Statement.Status != "مرفوض")
            .GroupBy(line => line.ContractItemId).Select(group => new { group.Key, Quantity = group.Sum(line => line.Quantity) }).ToDictionaryAsync(item => item.Key, item => item.Quantity, cancellationToken);
        return items.Select(item => new ContractorContractItemSummary(item.Id, item.ContractId, item.Description, item.Unit, item.ApprovedQuantity, item.UnitRate, item.SourceReference ?? string.Empty, used.GetValueOrDefault(item.Id), item.ApprovedQuantity - used.GetValueOrDefault(item.Id))).ToList();
    }

    public async Task<IReadOnlyList<EligibleContractorWorkSummary>> GetEligibleWorkAsync(int contractId, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        var contract = await db.ContractorContracts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == contractId, cancellationToken);
        if (contract is null || !await CanAccessProjectAsync(contract.ProjectId, userId, globalAccess, cancellationToken)) return [];

        var used = (await db.ContractorStatementLines.AsNoTracking()
            .Where(line => line.Statement.Status != "مرفوض")
            .Select(line => new { line.SourceEntityType, line.SourceEntityId })
            .ToListAsync(cancellationToken))
            .Select(item => $"{item.SourceEntityType}:{item.SourceEntityId}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<EligibleContractorWorkSummary>();
        result.AddRange(await db.SiteMovements.AsNoTracking().Where(item => item.ProjectId == contract.ProjectId && (item.Status == "معتمد" || item.Status == "مرحّل") && item.Quantity > 0 && item.UnitPrice >= 0)
            .Select(item => new EligibleContractorWorkSummary("site-movement", item.Id, item.Description, item.Unit ?? string.Empty, item.Quantity!.Value, item.UnitPrice!.Value, item.Status, item.Reference ?? $"site-movement:{item.Id}", false)).ToListAsync(cancellationToken));
        result.AddRange(await db.ExcavationRecords.AsNoTracking().Where(item => item.ProjectId == contract.ProjectId && (item.Status == "معتمد" || item.Status == "مرحّل") && item.CalculatedQuantity > 0)
            .Select(item => new EligibleContractorWorkSummary("excavation", item.Id, item.Description, item.Unit, item.CalculatedQuantity, item.UnitPrice, item.Status, $"excavation:{item.Id}", false)).ToListAsync(cancellationToken));
        result.AddRange(await db.ConcreteRecords.AsNoTracking().Where(item => item.ProjectId == contract.ProjectId && (item.Status == "معتمد" || item.Status == "مرحّل") && item.Quantity > 0)
            .Select(item => new EligibleContractorWorkSummary("concrete", item.Id, item.Description, item.Unit, item.Quantity, item.UnitPrice, item.Status, $"concrete:{item.Id}", false)).ToListAsync(cancellationToken));
        result.AddRange(await db.SteelRecords.AsNoTracking().Where(item => item.ProjectId == contract.ProjectId && (item.Status == "معتمد" || item.Status == "مرحّل") && item.Quantity > 0)
            .Select(item => new EligibleContractorWorkSummary("steel", item.Id, item.Description, item.Unit, item.Quantity, item.UnitPrice, item.Status, $"steel:{item.Id}", false)).ToListAsync(cancellationToken));
        result.AddRange(await db.FinishingRecords.AsNoTracking().Where(item => item.ProjectId == contract.ProjectId && (item.Status == "معتمد" || item.Status == "مرحّل") && item.Quantity > 0)
            .Select(item => new EligibleContractorWorkSummary("finishing", item.Id, item.Description, item.Unit, item.Quantity, item.UnitPrice, item.Status, $"finishing:{item.Id}", false)).ToListAsync(cancellationToken));
        return result.OrderBy(item => item.SourceEntityType).ThenBy(item => item.SourceEntityId)
            .Select(item => item with { AlreadyUsed = used.Contains($"{item.SourceEntityType}:{item.SourceEntityId}") }).ToList();
    }

    public async Task<IReadOnlyList<ContractorStatementSummary>> GetStatementsAsync(int? projectId, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        var query = db.ContractorStatements.AsNoTracking().Include(item => item.Project).Include(item => item.Contractor).Include(item => item.Contract).Include(item => item.Lines).ThenInclude(line => line.ContractItem).AsQueryable();
        if (projectId is not null) query = query.Where(item => item.ProjectId == projectId);
        if (!globalAccess) query = query.Where(item => userId.HasValue && db.UserProjectAssignments.Any(assignment => assignment.ProjectId == item.ProjectId && assignment.UserId == userId && assignment.IsActive));
        var statements = await query.OrderByDescending(item => item.Id).ToListAsync(cancellationToken);
        return statements.Select(ToSummary).ToList();
    }

    public async Task<ContractorStatementSummary?> CreateDraftAsync(CreateStatementRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        if (request.PeriodFrom.Date > request.PeriodTo.Date || request.Lines is null || request.Lines.Count == 0 || !IsCurrency(request.Currency) ||
            !await CanAccessProjectAsync(request.ProjectId, userId, globalAccess, cancellationToken)) return null;
        var contract = await db.ContractorContracts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.ContractId && item.ProjectId == request.ProjectId && item.ContractorId == request.ContractorId, cancellationToken);
        if (contract is null || !string.Equals(contract.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
        var items = await db.ContractorContractItems.AsNoTracking().Where(item => item.ContractId == contract.Id && item.IsActive).ToDictionaryAsync(item => item.Id, cancellationToken);
        var usedQuantities = await db.ContractorStatementLines.AsNoTracking().Where(line => line.ContractItem.ContractId == contract.Id && line.Statement.Status != "مرفوض")
            .GroupBy(line => line.ContractItemId).Select(group => new { group.Key, Quantity = group.Sum(line => line.Quantity) }).ToDictionaryAsync(item => item.Key, item => item.Quantity, cancellationToken);
        var existingSources = await db.ContractorStatementLines.AsNoTracking().Where(line => line.Statement.ContractId == contract.Id && line.Statement.Status != "مرفوض")
            .Select(line => new { line.SourceEntityType, line.SourceEntityId }).ToListAsync(cancellationToken);
        var sourceKeys = existingSources.Select(item => $"{item.SourceEntityType}:{item.SourceEntityId}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lines = new List<ContractorStatementLine>();
        foreach (var requestLine in request.Lines)
        {
            if (!EligibleTypes.Contains(requestLine.SourceEntityType, StringComparer.OrdinalIgnoreCase) || requestLine.Quantity <= 0 || string.IsNullOrWhiteSpace(requestLine.SourceReference) || !items.TryGetValue(requestLine.ContractItemId, out var item)) return null;
            var source = await ReadSourceAsync(requestLine.SourceEntityType, requestLine.SourceEntityId, request.ProjectId, cancellationToken);
            if (source is null || source.Quantity is null || source.Quantity <= 0 || source.Status is not ("معتمد" or "مرحّل") || requestLine.Quantity > source.Quantity || requestLine.UnitRate != item.UnitRate || requestLine.UnitRate < 0) return null;
            if (sourceKeys.Contains($"{requestLine.SourceEntityType}:{requestLine.SourceEntityId}")) throw new InvalidOperationException("تم استخدام حركة العمل المعتمدة في كشف سابق ولا يمكن اعتمادها مرة أخرى.");
            if (usedQuantities.GetValueOrDefault(item.Id) + lines.Where(line => line.ContractItemId == item.Id).Sum(line => line.Quantity) + requestLine.Quantity > item.ApprovedQuantity) return null;
            lines.Add(new ContractorStatementLine { ContractItemId = item.Id, SourceEntityType = requestLine.SourceEntityType.Trim().ToLowerInvariant(), SourceEntityId = requestLine.SourceEntityId, Quantity = requestLine.Quantity, UnitRate = requestLine.UnitRate, GrossAmount = decimal.Round(requestLine.Quantity * requestLine.UnitRate, 2), SourceReference = requestLine.SourceReference.Trim() });
        }
        var gross = lines.Sum(item => item.GrossAmount);
        var statement = new ContractorStatement
        {
            ProjectId = request.ProjectId, ContractorId = request.ContractorId, ContractId = contract.Id,
            PeriodFrom = DateTime.SpecifyKind(request.PeriodFrom.Date, DateTimeKind.Utc), PeriodTo = DateTime.SpecifyKind(request.PeriodTo.Date, DateTimeKind.Utc),
            Currency = contract.Currency, Status = "مسودة", GrossAmount = gross, PreviousPaymentsAmount = 0m, DeductionsAmount = 0m, NetAmount = gross,
            SourceReference = request.SourceReference?.Trim(), CreatedByUserId = userId, Lines = lines
        };
        db.ContractorStatements.Add(statement);
        await db.SaveChangesAsync(cancellationToken);
        db.WorkflowActions.Add(new WorkflowAction { EntityType = "contractor-statement", EntityId = statement.Id, Action = "إنشاء مسودة", FromStatus = "", ToStatus = "مسودة", UserId = userId, Notes = "مسودة للمعاينة فقط؛ لا أثر مالي ولا اعتماد متاح." });
        await db.SaveChangesAsync(cancellationToken);
        return await LoadSummaryAsync(statement.Id, cancellationToken);
    }

    public async Task<ContractorPaymentSummary?> AddPaymentAsync(int contractorId, CreateContractorPaymentRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0 || !IsCurrency(request.Currency ?? "EGP") || !await CanAccessProjectAsync(request.ProjectId, userId, globalAccess, cancellationToken)) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var contractor = await db.Contractors.SingleOrDefaultAsync(item => item.Id == contractorId && item.IsActive, cancellationToken);
        var source = request.PaymentSource?.Trim() == "مالك مباشر" ? "مالك مباشر" : "مكتب";
        var statement = request.ContractorStatementId is int statementId
            ? await db.ContractorStatements.SingleOrDefaultAsync(item => item.Id == statementId && item.ProjectId == request.ProjectId && item.ContractorId == contractorId && item.Status != "مرفوض", cancellationToken)
            : null;
        if (contractor is null || request.ContractorStatementId.HasValue && statement is null || source == "مالك مباشر" && statement is null)
            throw new InvalidOperationException("يجب ربط الدفع المباشر من المالك بكشف مقاول مثبت.");
        var currency = (request.Currency ?? statement?.Currency ?? "EGP").Trim().ToUpperInvariant();
        if (statement is not null && statement.Currency != currency) return null;
        if (!string.IsNullOrWhiteSpace(request.Reference) && await db.ContractorPayments.AnyAsync(item => item.ProjectId == request.ProjectId && item.Reference == request.Reference.Trim(), cancellationToken))
            throw new InvalidOperationException("مرجع دفعة المقاول مستخدم بالفعل داخل المشروع.");
        if (statement is not null && request.Amount > statement.NetAmount)
            throw new InvalidOperationException("قيمة الدفعة تتجاوز صافي التزام المقاول المتبقي.");
        var payment = new ContractorPayment
        {
            ProjectId = request.ProjectId, ContractorId = contractorId, ContractorStatementId = request.ContractorStatementId,
            Amount = request.Amount, Currency = currency, PaymentDate = request.PaymentDate is null ? DateTime.UtcNow.Date : DateTime.SpecifyKind(request.PaymentDate.Value.Date, DateTimeKind.Utc),
            PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "نقدي" : request.PaymentMethod.Trim(), PaymentSource = source,
            Reference = request.Reference?.Trim(), Notes = string.IsNullOrWhiteSpace(request.Notes) && source == "مالك مباشر" ? "تخليص حساب" : request.Notes?.Trim(), CreatedByUserId = userId
        };
        db.ContractorPayments.Add(payment);
        if (statement is not null)
        {
            statement.PreviousPaymentsAmount += payment.Amount;
            statement.NetAmount = statement.GrossAmount - statement.PreviousPaymentsAmount - statement.DeductionsAmount;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ContractorPaymentSummary(payment.Id, payment.ProjectId, payment.ContractorId, payment.ContractorStatementId, payment.Amount, payment.Currency, payment.PaymentDate, payment.PaymentMethod, payment.PaymentSource, payment.Reference ?? string.Empty, payment.Notes ?? string.Empty);
    }

    public async Task<byte[]?> ExportDraftAsync(int statementId, int? userId, bool globalAccess, CancellationToken cancellationToken)
    {
        var statement = await db.ContractorStatements.AsNoTracking().Include(item => item.Project).Include(item => item.Contractor).Include(item => item.Contract).Include(item => item.Lines).ThenInclude(line => line.ContractItem).SingleOrDefaultAsync(item => item.Id == statementId, cancellationToken);
        if (statement is null || !await CanAccessProjectAsync(statement.ProjectId, userId, globalAccess, cancellationToken)) return null;
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("كشف المقاول - مسودة");
        sheet.Cell("A1").Value = "كشف مقاول - مسودة للمعاينة فقط";
        sheet.Cell("A2").Value = "المشروع"; sheet.Cell("B2").Value = statement.Project.Name;
        sheet.Cell("A3").Value = "المقاول"; sheet.Cell("B3").Value = statement.Contractor.Name;
        sheet.Cell("A4").Value = "العقد"; sheet.Cell("B4").Value = statement.Contract.ContractNumber;
        sheet.Cell("A5").Value = "الفترة"; sheet.Cell("B5").Value = $"{statement.PeriodFrom:yyyy-MM-dd} - {statement.PeriodTo:yyyy-MM-dd}";
        sheet.Cell("A6").Value = "الحالة"; sheet.Cell("B6").Value = statement.Status;
        var row = 8;
        foreach (var header in new[] { "البيان", "مصدر العمل", "معرف المصدر", "الكمية", "الوحدة", "السعر", "الإجمالي", "مرجع المصدر" }) sheet.Cell(row, Array.IndexOf(new[] { "البيان", "مصدر العمل", "معرف المصدر", "الكمية", "الوحدة", "السعر", "الإجمالي", "مرجع المصدر" }, header) + 1).Value = header;
        foreach (var line in statement.Lines)
        {
            row++; sheet.Cell(row, 1).Value = line.ContractItem.Description; sheet.Cell(row, 2).Value = line.SourceEntityType; sheet.Cell(row, 3).Value = line.SourceEntityId; sheet.Cell(row, 4).Value = line.Quantity; sheet.Cell(row, 5).Value = line.ContractItem.Unit; sheet.Cell(row, 6).Value = line.UnitRate; sheet.Cell(row, 7).Value = line.GrossAmount; sheet.Cell(row, 8).Value = line.SourceReference;
        }
        row += 2; sheet.Cell(row, 1).Value = "الإجمالي الإجمالي"; sheet.Cell(row, 2).Value = statement.GrossAmount; sheet.Cell(row + 1, 1).Value = "الصافي الحالي للمسودة"; sheet.Cell(row + 1, 2).Value = statement.NetAmount;
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    private async Task<SourceMeasurement?> ReadSourceAsync(string type, int id, int projectId, CancellationToken cancellationToken) => type.ToLowerInvariant() switch
    {
        "site-movement" => await db.SiteMovements.Where(item => item.Id == id && item.ProjectId == projectId).Select(item => new SourceMeasurement(item.Quantity, item.Unit ?? string.Empty, item.UnitPrice, item.Status)).SingleOrDefaultAsync(cancellationToken),
        "excavation" => await db.ExcavationRecords.Where(item => item.Id == id && item.ProjectId == projectId).Select(item => new SourceMeasurement(item.CalculatedQuantity, item.Unit, item.UnitPrice, item.Status)).SingleOrDefaultAsync(cancellationToken),
        "concrete" => await db.ConcreteRecords.Where(item => item.Id == id && item.ProjectId == projectId).Select(item => new SourceMeasurement(item.Quantity, item.Unit, item.UnitPrice, item.Status)).SingleOrDefaultAsync(cancellationToken),
        "steel" => await db.SteelRecords.Where(item => item.Id == id && item.ProjectId == projectId).Select(item => new SourceMeasurement(item.Quantity, item.Unit, item.UnitPrice, item.Status)).SingleOrDefaultAsync(cancellationToken),
        "finishing" => await db.FinishingRecords.Where(item => item.Id == id && item.ProjectId == projectId).Select(item => new SourceMeasurement(item.Quantity, item.Unit, item.UnitPrice, item.Status)).SingleOrDefaultAsync(cancellationToken),
        _ => null
    };

    private async Task<ContractorStatementSummary?> LoadSummaryAsync(int id, CancellationToken cancellationToken) => (await db.ContractorStatements.AsNoTracking().Include(item => item.Project).Include(item => item.Contractor).Include(item => item.Contract).Include(item => item.Lines).ThenInclude(line => line.ContractItem).SingleOrDefaultAsync(item => item.Id == id, cancellationToken)) is { } statement ? ToSummary(statement) : null;
    private Task<bool> CanAccessProjectAsync(int projectId, int? userId, bool globalAccess, CancellationToken cancellationToken) => Task.FromResult(globalAccess || userId is not null && HasProjectAccess(projectId, userId));
    private bool HasProjectAccess(int projectId, int? userId) => userId is not null && db.UserProjectAssignments.Any(item => item.ProjectId == projectId && item.UserId == userId && item.IsActive);
    private static DateTime? DateOnly(DateTime? value) => value is null ? null : DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);
    private static bool IsCurrency(string value) => value.Trim().ToUpperInvariant() is "EGP" or "USD" or "EUR";
    private static System.Linq.Expressions.Expression<Func<Contractor, ContractorSummary>> ToContractorSummary() => item => new ContractorSummary(item.Id, item.Name, item.Phone ?? string.Empty, item.TaxNumber ?? string.Empty, item.Notes ?? string.Empty, item.IsActive);
    private static System.Linq.Expressions.Expression<Func<ContractorContract, ContractorContractSummary>> ToContractSummary() => item => new ContractorContractSummary(item.Id, item.ProjectId, item.Project.Name, item.ContractorId, item.Contractor.Name, item.ContractNumber, item.Currency, item.Status, item.Items.Count);
    private static ContractorStatementSummary ToSummary(ContractorStatement item) => new(item.Id, item.ProjectId, item.Project.Name, item.ContractorId, item.Contractor.Name, item.ContractId, item.Contract.ContractNumber, item.PeriodFrom, item.PeriodTo, item.Currency, item.Status, item.GrossAmount, item.PreviousPaymentsAmount, item.DeductionsAmount, item.NetAmount, item.SourceReference ?? string.Empty, item.Lines.OrderBy(line => line.Id).Select(line => new ContractorStatementLineSummary(line.Id, line.ContractItemId, line.ContractItem.Description, line.SourceEntityType, line.SourceEntityId, line.Quantity, line.ContractItem.Unit, line.UnitRate, line.GrossAmount, line.SourceReference)).ToList());
    private sealed record SourceMeasurement(decimal? Quantity, string Unit, decimal? UnitRate, string Status);
}
