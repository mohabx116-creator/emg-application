using EMG.Api.Modules.Workflow.Dtos;
using EMG.Api.Modules.Projects.Services;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using EMG.Api.Modules.Notifications.Services;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Workflow.Services;

public sealed class WorkflowService(EmgDbContext db, INotificationService notificationService) : IWorkflowService
{
    private const string AcceptTechnically = "قبول فني";
    private const string ApproveFinancially = "اعتماد مالي";
    private const string TechnicallyAccepted = "مقبول فنياً";

    public async Task<WorkflowResult?> ExecuteAsync(string entityType, int entityId, string action, string? notes, int? userId, bool isCanonicalAdmin, CancellationToken cancellationToken)
    {
        if (userId is null)
            throw new WorkflowForbiddenException("لا تملك صلاحية الوصول إلى المشروع أو الحركة.");

        var normalizedType = Normalize(entityType);
        var normalizedAction = Normalize(action);
        var current = await GetRecordAsync(normalizedType, entityId, cancellationToken);
        if (current is null && isCanonicalAdmin) return null;
        var access = await LoadAccessAsync(userId.Value, isCanonicalAdmin, cancellationToken);
        if (current is null || !CanView(access, current, normalizedType))
            throw new WorkflowForbiddenException("لا تملك صلاحية الوصول إلى المشروع أو الحركة.");
        if (await HistoricalWorkReader.IsLinkedAsync(db, normalizedType, entityId, cancellationToken) ||
            !CanTransition(normalizedType, current.Status, normalizedAction)) return null;
        if (!CanPerformAction(access, current, normalizedType, normalizedAction))
            throw new WorkflowForbiddenException("لا تملك الصلاحية لتنفيذ هذا الإجراء.");
        if (normalizedType == "supplier-supply")
        {
            if (normalizedAction == "رفض" && string.IsNullOrWhiteSpace(notes))
                throw new WorkflowValidationException(["سبب رفض التوريدة مطلوب."]);
            if (notes?.Trim().Length > 2000)
                throw new WorkflowValidationException(["الحد الأقصى لملاحظات التوريدة ٢٠٠٠ حرف."]);
        }
        if (normalizedAction == ApproveFinancially)
        {
            var acceptance = await ReadSupplyActionAsync(current, cancellationToken);
            if (!IsTechnicalAcceptance(acceptance))
                throw new WorkflowValidationException(["سجل القبول الفني لهذه التوريدة غير مكتمل."]);
            if (acceptance!.UserId == userId)
                throw new WorkflowForbiddenException("يجب أن يعتمد التوريدة مالياً مستخدم مختلف عمّن قبلها فنياً.");
        }
        if (normalizedAction is "اعتماد" or "ترحيل" or AcceptTechnically or ApproveFinancially)
        {
            var checks = await ValidateForApprovalAsync(normalizedType, entityId, current, cancellationToken);
            if (checks.Count > 0) throw new WorkflowValidationException(checks);
        }
        var next = NextStatus(normalizedAction);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await SetStatusAsync(normalizedType, entityId, current, next, cancellationToken) != 1)
            throw new WorkflowConcurrencyException(normalizedType, entityId, current.Status);
        // The row update holds the supply lock. Recheck the review cycle before committing it.
        if (normalizedType == "supplier-supply" &&
            await SupplyHistory(entityId).Select(item => (int?)item.Id).FirstOrDefaultAsync(cancellationToken) != current.LatestSupplyActionId)
            throw new WorkflowConcurrencyException(normalizedType, entityId, current.Status);
        var history = new WorkflowAction
        {
            EntityType = normalizedType, EntityId = entityId, Action = normalizedAction,
            FromStatus = current.Status, ToStatus = next, UserId = userId, Notes = notes?.Trim()
        };
        db.WorkflowActions.Add(history);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await notificationService.NotifyAdminsAsync(
            normalizedAction == "رفض" ? "warning" : "info",
            $"تحديث حركة {normalizedType}",
            $"تم تنفيذ إجراء {normalizedAction} على الحركة رقم {entityId}.",
            normalizedType, entityId, cancellationToken);
        return new WorkflowResult(normalizedType, entityId, current.Status, next, normalizedAction, history.ActionDateUtc);
    }

    public async Task<IReadOnlyList<WorkflowResult>> GetHistoryAsync(string entityType, int entityId, CancellationToken cancellationToken) =>
        await db.WorkflowActions.AsNoTracking().Where(item => item.EntityType == Normalize(entityType) && item.EntityId == entityId)
            .OrderByDescending(item => item.ActionDateUtc)
            .Select(item => new WorkflowResult(item.EntityType, item.EntityId, item.FromStatus, item.ToStatus, item.Action, item.ActionDateUtc))
            .ToListAsync(cancellationToken);

    public async Task<bool> CorrectSupplyAsync(int entityId, CorrectSupplyRequest request, int? userId, bool isCanonicalAdmin, CancellationToken cancellationToken)
    {
        if (userId is null) throw new WorkflowForbiddenException("لا تملك صلاحية تصحيح هذه التوريدة.");
        var access = await LoadAccessAsync(userId.Value, isCanonicalAdmin, cancellationToken);
        var current = await GetRecordAsync("supplier-supply", entityId, cancellationToken);
        if (current is null || !CanView(access, current, "supplier-supply") || !CanCorrectSupply(access, current, current.Amount))
            throw new WorkflowForbiddenException("لا تملك صلاحية تصحيح هذه التوريدة.");
        if (current.Status != "مرفوض") return false;
        var quantity = decimal.Round(request.Quantity, 3);
        var unitPrice = decimal.Round(request.UnitPrice, 2);
        if (string.IsNullOrWhiteSpace(request.ItemName) || string.IsNullOrWhiteSpace(request.Unit) || quantity <= 0 || request.UnitPrice < 0)
            throw new WorkflowValidationException(["بيان التوريدة والوحدة والكمية والسعر مطلوبة بشكل صحيح."]);
        var amount = decimal.Round(quantity * unitPrice, 2);
        if (!CanCorrectSupply(access, current, Math.Max(Math.Abs(current.Amount), amount)))
            throw new WorkflowForbiddenException("قيمة التوريدة تتجاوز الحد المسموح به للتصحيح.");

        var invoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? null : request.InvoiceNumber.Trim();
        var supplyType = string.IsNullOrWhiteSpace(request.SupplyType) ? "خامات" : request.SupplyType.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.SupplierSupplies.Where(item => item.Id == entityId && item.Status == current.Status
                && item.ProjectId == current.ProjectId && item.TotalAmount == current.Amount && item.CreatedByUserId == current.OwnerUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.SupplyType, supplyType)
                .SetProperty(item => item.ItemName, request.ItemName.Trim()).SetProperty(item => item.Unit, request.Unit.Trim())
                .SetProperty(item => item.Quantity, quantity).SetProperty(item => item.UnitPrice, unitPrice)
                .SetProperty(item => item.TotalAmount, amount).SetProperty(item => item.InvoiceNumber, invoiceNumber), cancellationToken);
        if (updated != 1 || await SupplyHistory(entityId).Select(item => (int?)item.Id).FirstOrDefaultAsync(cancellationToken) != current.LatestSupplyActionId)
            return false;
        if (invoiceNumber is not null)
        {
            var supplierId = await db.SupplierSupplies.Where(item => item.Id == entityId).Select(item => item.SupplierId).SingleAsync(cancellationToken);
            var invoice = invoiceNumber.ToLowerInvariant();
            if (await db.SupplierSupplies.AnyAsync(item => item.Id != entityId && item.SupplierId == supplierId && item.ProjectId == current.ProjectId &&
                item.InvoiceNumber != null && item.InvoiceNumber.ToLower() == invoice, cancellationToken))
                throw new WorkflowValidationException(["رقم الفاتورة مستخدم بالفعل لهذا المورد داخل نفس المشروع."]);
        }
        db.WorkflowActions.Add(new WorkflowAction
        {
            EntityType = "supplier-supply", EntityId = entityId, Action = "تعديل",
            FromStatus = current.Status, ToStatus = current.Status, UserId = userId, Notes = "تصحيح التوريدة المرفوضة"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<WorkflowAuditResult?> AuditAsync(string entityType, int entityId, int userId, bool isCanonicalAdmin, CancellationToken cancellationToken) =>
        await AuditAsync(Normalize(entityType), entityId, await LoadAccessAsync(userId, isCanonicalAdmin, cancellationToken), cancellationToken);

    private async Task<WorkflowAuditResult?> AuditAsync(string type, int entityId, WorkflowAccess access, CancellationToken cancellationToken)
    {
        var record = await GetRecordAsync(type, entityId, cancellationToken);
        if (record is null || !CanView(access, record, type)) return null;
        var status = record.Status;
        var blockingChecks = await ValidateForApprovalAsync(type, entityId, record, cancellationToken);
        var attachmentCount = await db.Set<Attachment>().CountAsync(item => item.EntityType == type && item.EntityId == entityId, cancellationToken);
        var warnings = new List<string>();
        if (attachmentCount == 0) warnings.Add("لا يوجد مرفق مؤيد للحركة حتى الآن.");
        if (status == "مرفوض") warnings.Add("الحركة مرفوضة وتحتاج تعديلًا ثم إعادة إرسال للمراجعة.");
        // The audit result drives the action buttons in the review center.  It
        // must therefore include both data validation and the current user's
        // permission; otherwise an engineer could see a post/approve button
        // that the command endpoint would reject.
        var canApprove = CanTransition(type, status, "اعتماد") && blockingChecks.Count == 0 && CanPerformAction(access, record, type, "اعتماد");
        var canPost = CanTransition(type, status, "ترحيل") && blockingChecks.Count == 0 && CanPerformAction(access, record, type, "ترحيل");
        var canAcceptTechnically = CanTransition(type, status, AcceptTechnically) && blockingChecks.Count == 0 &&
            CanPerformAction(access, record, type, AcceptTechnically);
        var canApproveFinancially = false;
        if (CanTransition(type, status, ApproveFinancially) && blockingChecks.Count == 0 &&
            CanPerformAction(access, record, type, ApproveFinancially))
        {
            var acceptance = await ReadSupplyActionAsync(record, cancellationToken);
            canApproveFinancially = IsTechnicalAcceptance(acceptance) && acceptance!.UserId != access.UserId;
            if (!canApproveFinancially)
                warnings.Add("يتطلب الاعتماد المالي قبولاً فنياً مسجلاً بواسطة مستخدم آخر.");
        }
        return new WorkflowAuditResult(type, entityId, status, canApprove, canPost, blockingChecks, warnings)
        {
            CanSubmit = CanTransition(type, status, "إرسال") && CanPerformAction(access, record, type, "إرسال"),
            CanAcceptTechnically = canAcceptTechnically,
            CanApproveFinancially = canApproveFinancially,
            CanReject = type == "supplier-supply" && CanTransition(type, status, "رفض") && CanPerformAction(access, record, type, "رفض"),
            CanCorrectSupply = type == "supplier-supply" && status == "مرفوض" && CanCorrectSupply(access, record, record.Amount),
            RejectionReason = type == "supplier-supply" && status == "مرفوض"
                ? await SupplyHistory(entityId).Where(item => item.Action == "رفض").Select(item => item.Notes).FirstOrDefaultAsync(cancellationToken)
                : null
        };
    }

    public async Task<IReadOnlyList<WorkflowReviewItem>> GetReviewQueueAsync(int userId, bool isCanonicalAdmin, CancellationToken cancellationToken)
    {
        var rows = new List<(string Type, int Id, int ProjectId, string ProjectName, string Description, decimal Amount, string Status, DateTime? Date)>();
        var access = await LoadAccessAsync(userId, isCanonicalAdmin, cancellationToken);
        var assignedProjectIds = access.AssignedProjectIds;
        // This bounds the candidate query. Audit still checks the specific record's read grant.
        var includeUnassignedProjects = access.IsCanonicalAdmin || access.Grants.Any(grant =>
            grant.Scope == "all" && grant.Code.EndsWith(".view", StringComparison.Ordinal) && RoleReachesAllProjects(grant.Role));

        var projects = db.Projects.AsNoTracking();
        var site = await db.SiteMovements.AsNoTracking().Where(item => item.Status == "للمراجعة" || item.Status == "معتمد")
            .Join(projects, item => item.ProjectId, project => project.Id, (item, project) => new { item, project })
            .Where(row => includeUnassignedProjects || assignedProjectIds.Contains(row.item.ProjectId))
            .Select(row => new { Type = "site-movement", row.item.Id, row.item.ProjectId, ProjectName = row.project.Name, Description = row.item.Description, Amount = row.item.TotalAmount, row.item.Status, Date = row.item.MovementDate })
            .ToListAsync(cancellationToken);
        rows.AddRange(site.Select(item => (item.Type, item.Id, item.ProjectId, item.ProjectName, item.Description, item.Amount, item.Status, (DateTime?)item.Date)));

        var advances = await db.AdvancePayments.AsNoTracking().Where(item => item.Status == "للمراجعة" || item.Status == "معتمد")
            .Join(projects, item => item.ProjectId, project => project.Id, (item, project) => new { item, project })
            .Where(row => includeUnassignedProjects || assignedProjectIds.Contains(row.item.ProjectId))
            .Select(row => new { Type = "advance-payment", row.item.Id, row.item.ProjectId, ProjectName = row.project.Name, Description = $"دفعة تحت الحساب - {row.item.ClientName}", Amount = row.item.Amount, row.item.Status, Date = row.item.PaymentDate })
            .ToListAsync(cancellationToken);
        rows.AddRange(advances.Select(item => (item.Type, item.Id, item.ProjectId, item.ProjectName, item.Description, item.Amount, item.Status, item.Date)));

        var supplies = await db.SupplierSupplies.AsNoTracking().Where(item => item.Status == "للمراجعة" || item.Status == TechnicallyAccepted || item.Status == "معتمد")
            .Join(projects, item => item.ProjectId, project => project.Id, (item, project) => new { item, project })
            .Where(row => includeUnassignedProjects || assignedProjectIds.Contains(row.item.ProjectId))
            .Select(row => new { Type = "supplier-supply", row.item.Id, row.item.ProjectId, ProjectName = row.project.Name, Description = $"توريد {row.item.ItemName} - {row.item.Supplier.Name}", Amount = row.item.TotalAmount, row.item.Status, Date = row.item.SupplyDate })
            .ToListAsync(cancellationToken);
        rows.AddRange(supplies.Select(item => (item.Type, item.Id, item.ProjectId, item.ProjectName, item.Description, item.Amount, item.Status, (DateTime?)item.Date)));

        var payments = await db.SupplierPayments.AsNoTracking().Where(item => item.ProjectId != null && (item.Status == "للمراجعة" || item.Status == "معتمد"))
            .Join(projects, item => item.ProjectId!.Value, project => project.Id, (item, project) => new { item, project })
            .Where(row => includeUnassignedProjects || assignedProjectIds.Contains(row.item.ProjectId!.Value))
            .Select(row => new { Type = "supplier-payment", row.item.Id, ProjectId = row.item.ProjectId!.Value, ProjectName = row.project.Name, Description = $"دفعة للمورد {row.item.Supplier.Name}", Amount = row.item.Amount, row.item.Status, Date = row.item.PaymentDate })
            .ToListAsync(cancellationToken);
        rows.AddRange(payments.Select(item => (item.Type, item.Id, item.ProjectId, item.ProjectName, item.Description, item.Amount, item.Status, (DateTime?)item.Date)));

        await AddSpecializedRowsAsync(rows, assignedProjectIds, includeUnassignedProjects, cancellationToken);

        var result = new List<WorkflowReviewItem>();
        foreach (var row in rows.OrderByDescending(item => item.Date).ThenBy(item => item.ProjectName))
        {
            var audit = await AuditAsync(row.Type, row.Id, access, cancellationToken);
            if (audit is null) continue;
            var hasAttachment = await db.Attachments.AsNoTracking().AnyAsync(item => item.EntityType == row.Type && item.EntityId == row.Id, cancellationToken);
            result.Add(new WorkflowReviewItem(row.Type, row.Id, row.ProjectId, row.ProjectName, row.Description, row.Amount, audit.Status, row.Date, hasAttachment, audit.CanApprove, audit.CanPost, audit.BlockingChecks, audit.Warnings)
            {
                CanAcceptTechnically = audit.CanAcceptTechnically,
                CanApproveFinancially = audit.CanApproveFinancially,
                CanReject = audit.CanReject
            });
        }
        return result;
    }

    private async Task AddSpecializedRowsAsync(List<(string Type, int Id, int ProjectId, string ProjectName, string Description, decimal Amount, string Status, DateTime? Date)> rows, IReadOnlyList<int> assignedProjectIds, bool includeUnassignedProjects, CancellationToken cancellationToken)
    {
        var projects = db.Projects.AsNoTracking();
        var excavation = await db.ExcavationRecords.AsNoTracking().Where(item => item.Status == "للمراجعة" || item.Status == "معتمد").Join(projects, item => item.ProjectId, project => project.Id, (item, project) => new { item, project }).Where(row => includeUnassignedProjects || assignedProjectIds.Contains(row.item.ProjectId)).Select(row => new { Type = "excavation", row.item.Id, row.item.ProjectId, ProjectName = row.project.Name, row.item.Description, Amount = row.item.TotalAmount, row.item.Status, Date = row.item.ExcavationDate }).ToListAsync(cancellationToken);
        rows.AddRange(excavation.Select(item => (item.Type, item.Id, item.ProjectId, item.ProjectName, item.Description, item.Amount, item.Status, (DateTime?)item.Date)));
        var concrete = await db.ConcreteRecords.AsNoTracking().Where(item => item.Status == "للمراجعة" || item.Status == "معتمد").Join(projects, item => item.ProjectId, project => project.Id, (item, project) => new { item, project }).Where(row => includeUnassignedProjects || assignedProjectIds.Contains(row.item.ProjectId)).Select(row => new { Type = "concrete", row.item.Id, row.item.ProjectId, ProjectName = row.project.Name, row.item.Description, Amount = row.item.TotalAmount, row.item.Status, Date = row.item.PourDate }).ToListAsync(cancellationToken);
        rows.AddRange(concrete.Select(item => (item.Type, item.Id, item.ProjectId, item.ProjectName, item.Description, item.Amount, item.Status, (DateTime?)item.Date)));
        var steel = await db.SteelRecords.AsNoTracking().Where(item => item.Status == "للمراجعة" || item.Status == "معتمد").Join(projects, item => item.ProjectId, project => project.Id, (item, project) => new { item, project }).Where(row => includeUnassignedProjects || assignedProjectIds.Contains(row.item.ProjectId)).Select(row => new { Type = "steel", row.item.Id, row.item.ProjectId, ProjectName = row.project.Name, row.item.Description, Amount = row.item.TotalAmount, row.item.Status, Date = row.item.OperationDate }).ToListAsync(cancellationToken);
        rows.AddRange(steel.Select(item => (item.Type, item.Id, item.ProjectId, item.ProjectName, item.Description, item.Amount, item.Status, (DateTime?)item.Date)));
        var finishing = await db.FinishingRecords.AsNoTracking().Where(item => item.Status == "للمراجعة" || item.Status == "معتمد").Join(projects, item => item.ProjectId, project => project.Id, (item, project) => new { item, project }).Where(row => includeUnassignedProjects || assignedProjectIds.Contains(row.item.ProjectId)).Select(row => new { Type = "finishing", row.item.Id, row.item.ProjectId, ProjectName = row.project.Name, row.item.Description, Amount = row.item.TotalAmount, row.item.Status, Date = row.item.WorkDate }).ToListAsync(cancellationToken);
        rows.AddRange(finishing.Select(item => (item.Type, item.Id, item.ProjectId, item.ProjectName, item.Description, item.Amount, item.Status, (DateTime?)item.Date)));
    }

    public async Task<bool> CanViewHistoryAsync(string entityType, int entityId, int userId, bool isCanonicalAdmin, CancellationToken cancellationToken)
    {
        if (isCanonicalAdmin) return true;
        var type = Normalize(entityType);
        var record = await GetRecordAsync(type, entityId, cancellationToken);
        return record is not null && CanView(await LoadAccessAsync(userId, isCanonicalAdmin, cancellationToken), record, type);
    }

    private async Task<WorkflowAccess> LoadAccessAsync(int userId, bool isCanonicalAdmin, CancellationToken cancellationToken)
    {
        if (isCanonicalAdmin) return new WorkflowAccess(userId, true, [], []);
        var grants = await db.RolePermissions.AsNoTracking()
            .Where(grant => db.UserRoles.Any(role => role.RoleId == grant.RoleId && role.UserId == userId && role.User.IsActive))
            .Select(grant => new WorkflowGrant(grant.Role.Name, grant.Permission.Code, grant.Scope, grant.AmountLimit))
            .ToListAsync(cancellationToken);
        var projects = await db.UserProjectAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == userId && assignment.IsActive)
            .Select(assignment => assignment.ProjectId)
            .ToListAsync(cancellationToken);
        return new WorkflowAccess(userId, false, projects, grants);
    }

    private static bool CanView(WorkflowAccess access, WorkflowRecord record, string type) =>
        access.IsCanonicalAdmin || HasScopedGrant(access, record, GetPermissionCode(type, "view"));

    private static bool CanCorrectSupply(WorkflowAccess access, WorkflowRecord record, decimal amount) =>
        access.IsCanonicalAdmin || HasScopedGrant(access, record, "transactions.correct", amount: Math.Abs(amount));

    private static bool CanPerformAction(WorkflowAccess access, WorkflowRecord record, string type, string action)
    {
        if (access.IsCanonicalAdmin) return true;
        var operation = action switch
        {
            "إرسال" => "submit",
            "اعتماد" => "approve",
            AcceptTechnically => "supplies-accept-technical",
            ApproveFinancially => "supplies-approve-financial",
            "رفض" => "reject",
            "ترحيل" => "post",
            _ => null
        };
        if (operation is null) return false;

        var reviewerRole = action switch
        {
            AcceptTechnically => "project_manager",
            ApproveFinancially => "accountant",
            "رفض" when type == "supplier-supply" => record.Status == TechnicallyAccepted ? "accountant" : "project_manager",
            "اعتماد" or "رفض" => type is "advance-payment" or "supplier-payment" ? "accountant" : "project_manager",
            _ => null
        };
        return HasScopedGrant(access, record, GetPermissionCode(type, operation), reviewerRole, Math.Abs(record.Amount));
    }

    private static bool HasScopedGrant(WorkflowAccess access, WorkflowRecord record, string code, string? reviewerRole = null, decimal? amount = null)
    {
        if (record.ProjectId is not int projectId) return false;
        var assigned = access.AssignedProjectIds.Contains(projectId);
        return access.Grants.Any(grant =>
            grant.Code == code &&
            (reviewerRole is null || grant.Role == "director" || grant.Role == reviewerRole) &&
            (amount is null || grant.AmountLimit is null || grant.AmountLimit >= amount) &&
            (grant.Scope switch
            {
                "all" => assigned || RoleReachesAllProjects(grant.Role),
                "assigned" or "project" => assigned,
                "own" => assigned && record.OwnerUserId == access.UserId,
                _ => false
            }));
    }

    private static bool RoleReachesAllProjects(string role) => role is "director" or "accountant";

    private sealed record WorkflowRecord(int? ProjectId, int? OwnerUserId, string Status, decimal Amount, int? LatestSupplyActionId = null);
    private sealed record WorkflowGrant(string Role, string Code, string Scope, decimal? AmountLimit);
    private sealed record WorkflowAccess(int UserId, bool IsCanonicalAdmin, IReadOnlyList<int> AssignedProjectIds, IReadOnlyList<WorkflowGrant> Grants);

    private async Task<IReadOnlyList<string>> ValidateForApprovalAsync(string type, int id, WorkflowRecord current, CancellationToken cancellationToken)
    {
        var checks = new List<string>();
        var isSignedAdjustment = false;
        var amount = current.Amount;
        if (current.ProjectId is null) checks.Add("الحركة غير مرتبطة بمشروع صحيح.");

        if (type == "site-movement")
        {
            var movement = await db.SiteMovements.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new { item.Description, item.MovementType, item.ExpenseCategory }).FirstOrDefaultAsync(cancellationToken);
            if (movement is null) checks.Add("الحركة غير موجودة.");
            else
            {
                isSignedAdjustment = IsSignedAdjustment(movement.Description, null, amount);
                if (string.IsNullOrWhiteSpace(movement.Description)) checks.Add("وصف الحركة مطلوب.");
                if (string.IsNullOrWhiteSpace(movement.MovementType)) checks.Add("نوع حركة الموقع مطلوب.");
                if (string.IsNullOrWhiteSpace(movement.ExpenseCategory)) checks.Add("بند المصروف أو الحساب مطلوب.");
            }
        }
        else if (type == "excavation")
        {
            var record = await db.ExcavationRecords.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new { item.CalculationMethod, item.CalculatedQuantity, item.Unit, item.UnitPrice, item.TotalAmount, item.Description, item.AccountCategory }).FirstOrDefaultAsync(cancellationToken);
            if (record is null) checks.Add("سجل الحفر غير موجود.");
            else
            {
                isSignedAdjustment = IsSignedAdjustment(record.Description, record.CalculatedQuantity, record.TotalAmount);
                if (string.IsNullOrWhiteSpace(record.CalculationMethod)) checks.Add("طريقة حساب الحفر مطلوبة.");
                if (!isSignedAdjustment && record.CalculatedQuantity <= 0) checks.Add("كمية الحفر المحسوبة يجب أن تكون أكبر من صفر.");
                if (isSignedAdjustment && (record.CalculatedQuantity >= 0 || record.TotalAmount >= 0)) checks.Add("تسوية الحفر يجب أن تكون بكمية وقيمة سالبتين.");
                if (string.IsNullOrWhiteSpace(record.Unit)) checks.Add("وحدة الحفر مطلوبة.");
                if (record.UnitPrice < 0) checks.Add("سعر الحفر لا يمكن أن يكون سالبًا.");
                if (decimal.Round(record.CalculatedQuantity * record.UnitPrice, 2) != decimal.Round(record.TotalAmount, 2)) checks.Add("إجمالي الحفر لا يساوي الكمية المحسوبة × السعر.");
                if (string.IsNullOrWhiteSpace(record.Description)) checks.Add("بيان أعمال الحفر مطلوب.");
                if (string.IsNullOrWhiteSpace(record.AccountCategory)) checks.Add("بند حساب الحفر مطلوب.");
            }
        }
        else if (type == "concrete")
        {
            var record = await db.ConcreteRecords.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new { item.Quantity, item.Unit, item.UnitPrice, item.TotalAmount, item.Description, item.ConcreteType, item.ElementType }).FirstOrDefaultAsync(cancellationToken);
            if (record is null) checks.Add("سجل الخرسانات غير موجود.");
            else
            {
                isSignedAdjustment = IsSignedAdjustment(record.Description, record.Quantity, record.TotalAmount);
                if (!isSignedAdjustment && record.Quantity <= 0) checks.Add("كمية الخرسانة يجب أن تكون أكبر من صفر.");
                if (isSignedAdjustment && (record.Quantity >= 0 || record.TotalAmount >= 0)) checks.Add("تسوية الخرسانة يجب أن تكون بكمية وقيمة سالبتين.");
                if (string.IsNullOrWhiteSpace(record.Unit)) checks.Add("وحدة الخرسانة مطلوبة.");
                if (record.UnitPrice < 0) checks.Add("سعر الخرسانة لا يمكن أن يكون سالبًا.");
                if (decimal.Round(record.Quantity * record.UnitPrice, 2) != decimal.Round(record.TotalAmount, 2)) checks.Add("إجمالي الخرسانة لا يساوي الكمية × السعر.");
                if (string.IsNullOrWhiteSpace(record.ConcreteType)) checks.Add("نوع الخرسانة مطلوب.");
                if (string.IsNullOrWhiteSpace(record.ElementType)) checks.Add("العنصر المنفذ للخرسانة مطلوب.");
                if (string.IsNullOrWhiteSpace(record.Description)) checks.Add("بيان أعمال الخرسانة مطلوب.");
            }
        }
        else if (type == "steel")
        {
            var record = await db.SteelRecords.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new { item.Quantity, item.Unit, item.UnitPrice, item.TotalAmount, item.Description, item.OperationType, item.AccountCategory }).FirstOrDefaultAsync(cancellationToken);
            if (record is null) checks.Add("سجل الحديد غير موجود.");
            else
            {
                var isAdjustment = IsSteelAdjustment(record.OperationType);
                isSignedAdjustment = isAdjustment;
                if (!isAdjustment && record.Quantity <= 0) checks.Add("كمية الحديد يجب أن تكون أكبر من صفر.");
                if (isAdjustment && (record.Quantity >= 0 || record.TotalAmount >= 0)) checks.Add("تسوية الحديد يجب أن تكون بكمية وقيمة سالبتين.");
                if (string.IsNullOrWhiteSpace(record.Unit)) checks.Add("وحدة الحديد مطلوبة.");
                if (record.UnitPrice < 0) checks.Add("سعر الحديد لا يمكن أن يكون سالبًا.");
                if (decimal.Round(record.Quantity * record.UnitPrice, 2) != decimal.Round(record.TotalAmount, 2)) checks.Add("إجمالي الحديد لا يساوي الكمية × السعر.");
                if (string.IsNullOrWhiteSpace(record.OperationType)) checks.Add("نوع عملية الحديد مطلوب.");
                if (string.IsNullOrWhiteSpace(record.Description)) checks.Add("بيان أعمال الحديد مطلوب.");
                if (string.IsNullOrWhiteSpace(record.AccountCategory)) checks.Add("بند حساب الحديد مطلوب.");
            }
        }
        else if (type == "finishing")
        {
            var record = await db.FinishingRecords.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new { item.Quantity, item.Unit, item.UnitPrice, item.TotalAmount, item.Description, item.WorkType, item.AccountCategory }).FirstOrDefaultAsync(cancellationToken);
            if (record is null) checks.Add("سجل التشطيبات غير موجود.");
            else
            {
                isSignedAdjustment = IsSignedAdjustment(record.Description, record.Quantity, record.TotalAmount);
                if (!isSignedAdjustment && record.Quantity <= 0) checks.Add("كمية التشطيبات يجب أن تكون أكبر من صفر.");
                if (isSignedAdjustment && (record.Quantity >= 0 || record.TotalAmount >= 0)) checks.Add("تسوية التشطيبات يجب أن تكون بكمية وقيمة سالبتين.");
                if (string.IsNullOrWhiteSpace(record.Unit)) checks.Add("وحدة التشطيبات مطلوبة.");
                if (record.UnitPrice < 0) checks.Add("سعر التشطيبات لا يمكن أن يكون سالبًا.");
                if (decimal.Round(record.Quantity * record.UnitPrice, 2) != decimal.Round(record.TotalAmount, 2)) checks.Add("إجمالي التشطيبات لا يساوي الكمية × السعر.");
                if (string.IsNullOrWhiteSpace(record.WorkType)) checks.Add("نوع أعمال التشطيبات مطلوب.");
                if (string.IsNullOrWhiteSpace(record.Description)) checks.Add("بيان أعمال التشطيبات مطلوب.");
                if (string.IsNullOrWhiteSpace(record.AccountCategory)) checks.Add("بند حساب التشطيبات مطلوب.");
            }
        }
        else if (type == "supplier-supply")
        {
            var supply = await db.SupplierSupplies.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new { item.ItemName, item.Unit, item.Quantity, item.UnitPrice, item.TotalAmount, item.SupplyType, item.SupplierId, item.ProjectId }).FirstOrDefaultAsync(cancellationToken);
            if (supply is null) checks.Add("توريد المورد غير موجود.");
            else
            {
                if (supply.SupplierId <= 0 || supply.ProjectId <= 0) checks.Add("التوريد غير مرتبط بمورد ومشروع صحيحين.");
                if (string.IsNullOrWhiteSpace(supply.ItemName)) checks.Add("اسم الصنف أو البيان مطلوب.");
                if (string.IsNullOrWhiteSpace(supply.SupplyType)) checks.Add("نوع التوريد مطلوب.");
                if (string.IsNullOrWhiteSpace(supply.Unit)) checks.Add("وحدة التوريد مطلوبة.");
                if (supply.Quantity <= 0) checks.Add("كمية التوريد يجب أن تكون أكبر من صفر.");
                if (supply.UnitPrice < 0) checks.Add("سعر التوريد لا يمكن أن يكون سالبًا.");
                if (decimal.Round(supply.Quantity * supply.UnitPrice, 2) != decimal.Round(supply.TotalAmount, 2)) checks.Add("إجمالي التوريد لا يساوي الكمية × السعر.");
            }
        }
        else if (type == "supplier-payment")
        {
            var payment = await db.SupplierPayments.AsNoTracking().Where(item => item.Id == id)
                .Select(item => new { item.Amount, item.SupplierId, item.ProjectId, item.PaymentMethod }).FirstOrDefaultAsync(cancellationToken);
            if (payment is null) checks.Add("دفعة المورد غير موجودة.");
            else
            {
                if (payment.SupplierId <= 0 || payment.ProjectId is null) checks.Add("دفعة المورد يجب أن ترتبط بمورد ومشروع قبل الاعتماد.");
                if (payment.Amount <= 0) checks.Add("قيمة دفعة المورد يجب أن تكون أكبر من صفر.");
                if (string.IsNullOrWhiteSpace(payment.PaymentMethod)) checks.Add("طريقة دفع المورد مطلوبة.");
            }
        }
        if (amount <= 0 && !isSignedAdjustment)
            checks.Add("قيمة الحركة يجب أن تكون أكبر من صفر قبل الاعتماد أو الترحيل.");
        return checks;
    }

    private static string GetPermissionCode(string entityType, string operation) => entityType switch
    {
        "site-movement" => $"site-movements.{operation}",
        "advance-payment" => $"advances.{operation}",
        "supplier-supply" or "supplier-payment" => $"suppliers.{operation}",
        "excavation" => $"excavation.{operation}",
        "concrete" => $"concrete.{operation}",
        "steel" => $"steel.{operation}",
        "finishing" => $"finishing.{operation}",
        _ => $"workflow.{operation}"
    };

    private Task<WorkflowRecord?> GetRecordAsync(string type, int id, CancellationToken cancellationToken) => type switch
    {
        "site-movement" => db.SiteMovements.Where(item => item.Id == id)
            .Select(item => new WorkflowRecord(item.ProjectId, item.CreatedByUserId, item.Status, item.TotalAmount)).FirstOrDefaultAsync(cancellationToken),
        "excavation" => db.ExcavationRecords.Where(item => item.Id == id)
            .Select(item => new WorkflowRecord(item.ProjectId, item.CreatedByUserId, item.Status, item.TotalAmount)).FirstOrDefaultAsync(cancellationToken),
        "concrete" => db.ConcreteRecords.Where(item => item.Id == id)
            .Select(item => new WorkflowRecord(item.ProjectId, item.CreatedByUserId, item.Status, item.TotalAmount)).FirstOrDefaultAsync(cancellationToken),
        "steel" => db.SteelRecords.Where(item => item.Id == id)
            .Select(item => new WorkflowRecord(item.ProjectId, item.CreatedByUserId, item.Status, item.TotalAmount)).FirstOrDefaultAsync(cancellationToken),
        "finishing" => db.FinishingRecords.Where(item => item.Id == id)
            .Select(item => new WorkflowRecord(item.ProjectId, item.CreatedByUserId, item.Status, item.TotalAmount)).FirstOrDefaultAsync(cancellationToken),
        // Imported advances store the importing user's ID, which is not ownership of the source record.
        "advance-payment" => db.AdvancePayments.Where(item => item.Id == id)
            .Select(item => new WorkflowRecord(item.ProjectId, item.Notes != null && item.Notes.StartsWith("استيراد:") ? null : item.CreatedByUserId,
                item.Status, item.Amount)).FirstOrDefaultAsync(cancellationToken),
        "supplier-supply" => db.SupplierSupplies.Where(item => item.Id == id)
            .Select(item => new WorkflowRecord(item.ProjectId, item.CreatedByUserId, item.Status, item.TotalAmount,
                db.WorkflowActions.Where(history => history.EntityType == "supplier-supply" && history.EntityId == item.Id)
                    .OrderByDescending(history => history.Id).Select(history => (int?)history.Id).FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken),
        "supplier-payment" => db.SupplierPayments.Where(item => item.Id == id)
            .Select(item => new WorkflowRecord(item.ProjectId, item.CreatedByUserId, item.Status, item.Amount)).FirstOrDefaultAsync(cancellationToken),
        _ => Task.FromResult<WorkflowRecord?>(null)
    };

    // Keep the data used for the scope and limit checks consistent with the row being transitioned.
    private Task<int> SetStatusAsync(string type, int id, WorkflowRecord current, string status, CancellationToken cancellationToken) => type switch
    {
        "site-movement" => db.SiteMovements.Where(item => item.Id == id && item.Status == current.Status
            && item.ProjectId == current.ProjectId && item.TotalAmount == current.Amount && item.CreatedByUserId == current.OwnerUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status), cancellationToken),
        "excavation" => db.ExcavationRecords.Where(item => item.Id == id && item.Status == current.Status
            && item.ProjectId == current.ProjectId && item.TotalAmount == current.Amount && item.CreatedByUserId == current.OwnerUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status), cancellationToken),
        "concrete" => db.ConcreteRecords.Where(item => item.Id == id && item.Status == current.Status
            && item.ProjectId == current.ProjectId && item.TotalAmount == current.Amount && item.CreatedByUserId == current.OwnerUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status), cancellationToken),
        "steel" => db.SteelRecords.Where(item => item.Id == id && item.Status == current.Status
            && item.ProjectId == current.ProjectId && item.TotalAmount == current.Amount && item.CreatedByUserId == current.OwnerUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status), cancellationToken),
        "finishing" => db.FinishingRecords.Where(item => item.Id == id && item.Status == current.Status
            && item.ProjectId == current.ProjectId && item.TotalAmount == current.Amount && item.CreatedByUserId == current.OwnerUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status), cancellationToken),
        "advance-payment" => db.AdvancePayments.Where(item => item.Id == id && item.Status == current.Status
            && item.ProjectId == current.ProjectId && item.Amount == current.Amount
            && (item.Notes != null && item.Notes.StartsWith("استيراد:") ? null : item.CreatedByUserId) == current.OwnerUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status), cancellationToken),
        "supplier-supply" => db.SupplierSupplies.Where(item => item.Id == id && item.Status == current.Status
            && item.ProjectId == current.ProjectId && item.TotalAmount == current.Amount && item.CreatedByUserId == current.OwnerUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status), cancellationToken),
        "supplier-payment" => db.SupplierPayments.Where(item => item.Id == id && item.Status == current.Status
            && item.ProjectId == current.ProjectId && item.Amount == current.Amount && item.CreatedByUserId == current.OwnerUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status), cancellationToken),
        _ => throw new InvalidOperationException("Unsupported workflow entity.")
    };

    private static bool CanTransition(string type, string current, string action) => action switch
    {
        "إرسال" => current is "مسودة" or "مرفوض",
        "اعتماد" => type != "supplier-supply" && current == "للمراجعة",
        AcceptTechnically => type == "supplier-supply" && current == "للمراجعة",
        ApproveFinancially => type == "supplier-supply" && current == TechnicallyAccepted,
        "رفض" => current == "للمراجعة" || (type == "supplier-supply" && current == TechnicallyAccepted),
        "ترحيل" => current == "معتمد",
        _ => false
    };

    private static string NextStatus(string action) => action switch
    {
        "إرسال" => "للمراجعة",
        AcceptTechnically => TechnicallyAccepted,
        "اعتماد" or ApproveFinancially => "معتمد",
        "رفض" => "مرفوض",
        "ترحيل" => "مرحّل",
        _ => throw new InvalidOperationException("Unsupported workflow action.")
    };

    private IOrderedQueryable<WorkflowAction> SupplyHistory(int id) =>
        db.WorkflowActions.AsNoTracking().Where(item => item.EntityType == "supplier-supply" && item.EntityId == id)
            .OrderByDescending(item => item.Id);

    private Task<WorkflowAction?> ReadSupplyActionAsync(WorkflowRecord record, CancellationToken cancellationToken) =>
        record.LatestSupplyActionId is int actionId
            ? db.WorkflowActions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == actionId, cancellationToken)
            : Task.FromResult<WorkflowAction?>(null);

    private static bool IsTechnicalAcceptance(WorkflowAction? action) =>
        action is { Action: AcceptTechnically, FromStatus: "للمراجعة", ToStatus: TechnicallyAccepted, UserId: > 0 };

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static bool IsSteelAdjustment(string? operationType) =>
        !string.IsNullOrWhiteSpace(operationType) &&
        (operationType.Contains("تسوية", StringComparison.OrdinalIgnoreCase) ||
         operationType.Contains("خصم", StringComparison.OrdinalIgnoreCase) ||
         operationType.Contains("تعديل", StringComparison.OrdinalIgnoreCase));

    private static bool IsSignedAdjustment(string? text, decimal? quantity, decimal? amount) =>
        amount is < 0m && (quantity is null || quantity < 0m) &&
        !string.IsNullOrWhiteSpace(text) &&
        (text.Contains("خصم", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("هالك", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("تسوية", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("تعديل", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("مرتجع", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("استرداد", StringComparison.OrdinalIgnoreCase));
}
