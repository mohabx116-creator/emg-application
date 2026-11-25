using System.Collections.Concurrent;
using System.Data.Common;
using EMG.Api.Modules.Notifications.Dtos;
using EMG.Api.Modules.Notifications.Services;
using EMG.Api.Modules.Workflow.Controllers;
using EMG.Api.Modules.Workflow.Dtos;
using EMG.Api.Modules.Workflow.Services;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EMG.WebApi.Tests;

public sealed class WorkflowAtomicTransitionTests
{
    [Fact]
    public async Task PostgresConcurrentTransitionsAreAtomicAcrossAllSupportedEntityTypes()
    {
        var connectionString = Environment.GetEnvironmentVariable("EMG_WORKFLOW_QA_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Set EMG_WORKFLOW_QA_CONNECTION to run the isolated PostgreSQL concurrency test.");

        var notificationSpy = new NotificationSpy();
        await using (var setup = CreateContext(connectionString))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.EnsureCreatedAsync();
            var project = new Project { Name = "QA Workflow", Code = $"QA-WF-{Guid.NewGuid():N}", OfficeSharePercentage = 10 };
            var supplier = new Supplier { Name = $"QA Supplier {Guid.NewGuid():N}", SupplierType = "مورد" };
            setup.Projects.Add(project);
            setup.Suppliers.Add(supplier);
            await setup.SaveChangesAsync();
            AddEntities(setup, project.Id, supplier.Id);
            await setup.SaveChangesAsync();
        }

        try
        {
            foreach (var (entityType, entityId) in await GetEntityIds(connectionString))
            {
                var gate = new StatusReadGate(entityType);
                var action = entityType == "supplier-supply" ? "قبول فني" : "اعتماد";
                var expectedStatus = entityType == "supplier-supply" ? "مقبول فنياً" : "معتمد";
                var results = await Task.WhenAll(
                    Execute(connectionString, notificationSpy, gate, entityType, entityId, action),
                    Execute(connectionString, notificationSpy, gate, entityType, entityId, action));

                Assert.Equal(1, results.Count(result => result.Success));
                Assert.True(results.Count(result => result.ConcurrencyConflict) == 1,
                    $"{entityType}/{entityId}: {string.Join(", ", results)}");
                await using var verify = CreateContext(connectionString);
                Assert.Equal(expectedStatus, await ReadStatus(verify, entityType, entityId));
                Assert.Equal(1, await verify.WorkflowActions.CountAsync(item => item.EntityType == entityType && item.EntityId == entityId && item.Action == action));
            }

            var posting = await GetEntityIds(connectionString);
            var siteMovementId = posting.Single(item => item.EntityType == "site-movement").EntityId;
            await using (var reset = CreateContext(connectionString))
            {
                await reset.SiteMovements.Where(item => item.Id == siteMovementId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "معتمد"));
                await reset.WorkflowActions.Where(item => item.EntityType == "site-movement" && item.EntityId == siteMovementId).ExecuteDeleteAsync();
            }

            var postGate = new StatusReadGate("site-movement");
            var postResults = await Task.WhenAll(
                Execute(connectionString, notificationSpy, postGate, "site-movement", siteMovementId, "ترحيل"),
                Execute(connectionString, notificationSpy, postGate, "site-movement", siteMovementId, "ترحيل"));
            Assert.Equal(1, postResults.Count(result => result.Success));
            Assert.Equal(1, postResults.Count(result => result.ConcurrencyConflict));
            await using (var verifyPost = CreateContext(connectionString))
            {
                Assert.Equal("مرحّل", await ReadStatus(verifyPost, "site-movement", siteMovementId));
                Assert.Equal(1, await verifyPost.WorkflowActions.CountAsync(item => item.EntityType == "site-movement" && item.EntityId == siteMovementId && item.Action == "ترحيل"));
            }

            await using (var invalid = CreateContext(connectionString))
            {
                await invalid.SiteMovements.Where(item => item.Id == siteMovementId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "مرحّل"));
                var service = new WorkflowService(invalid, notificationSpy);
                Assert.Null(await service.ExecuteAsync("site-movement", siteMovementId, "اعتماد", null, 1, true, CancellationToken.None));
            }

            Assert.Equal(9, notificationSpy.Count);
        }
        finally
        {
            await using var cleanup = CreateContext(connectionString);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task WorkflowControllerReturnsStable409ForConcurrencyConflict()
    {
        var controller = new WorkflowController(new ConflictWorkflowService());
        var result = await controller.Execute("site-movement", 7, "اعتماد", new WorkflowActionRequest(null), CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var payload = conflict.Value!;
        Assert.Equal("WORKFLOW_CONCURRENCY_CONFLICT", payload.GetType().GetProperty("code")!.GetValue(payload));
        Assert.Equal(409, conflict.StatusCode);
    }

    private static async Task<ExecutionResult> Execute(string connectionString, NotificationSpy notifications, StatusReadGate gate, string entityType, int entityId, string action)
    {
        await using var db = CreateContext(connectionString, gate);
        var service = new WorkflowService(db, notifications);
        try
        {
            var result = await service.ExecuteAsync(entityType, entityId, action, "QA", 1, true, CancellationToken.None);
            return new ExecutionResult(result is not null, false);
        }
        catch (WorkflowConcurrencyException)
        {
            return new ExecutionResult(false, true);
        }
    }

    private static EmgDbContext CreateContext(string connectionString, StatusReadGate? gate = null)
    {
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(connectionString);
        if (gate is not null) options.AddInterceptors(gate);
        return new EmgDbContext(options.Options);
    }

    private static void AddEntities(EmgDbContext db, int projectId, int supplierId)
    {
        db.SiteMovements.Add(new SiteMovement { ProjectId = projectId, Description = "حركة QA", MovementType = "مصروفات", ExpenseCategory = "الموقع", TotalAmount = 10, Status = "للمراجعة" });
        db.ExcavationRecords.Add(new ExcavationRecord { ProjectId = projectId, Description = "حفر QA", AccountCategory = "العرب", CalculationMethod = "كمية مباشرة", CalculatedQuantity = 1, Unit = "م3", UnitPrice = 10, TotalAmount = 10, Status = "للمراجعة" });
        db.ConcreteRecords.Add(new ConcreteRecord { ProjectId = projectId, Description = "خرسانة QA", ConcreteType = "مسلحة", ElementType = "قاعدة", Quantity = 1, Unit = "م3", UnitPrice = 10, TotalAmount = 10, Status = "للمراجعة" });
        db.SteelRecords.Add(new SteelRecord { ProjectId = projectId, Description = "حديد QA", OperationType = "توريد حديد", AccountCategory = "الحديد", Quantity = 1, Unit = "طن", UnitPrice = 10, TotalAmount = 10, Status = "للمراجعة" });
        db.FinishingRecords.Add(new FinishingRecord { ProjectId = projectId, Description = "مباني QA", WorkType = "مباني", AccountCategory = "المباني", Quantity = 1, Unit = "م2", UnitPrice = 10, TotalAmount = 10, Status = "للمراجعة" });
        db.AdvancePayments.Add(new AdvancePayment { ProjectId = projectId, ClientName = "عميل QA", PayerName = "دافع QA", Amount = 10, Status = "للمراجعة" });
        db.SupplierSupplies.Add(new SupplierSupply { ProjectId = projectId, SupplierId = supplierId, ItemName = "خامة QA", SupplyType = "خامات", Unit = "وحدة", Quantity = 1, UnitPrice = 10, TotalAmount = 10, Status = "للمراجعة" });
        db.SupplierPayments.Add(new SupplierPayment { ProjectId = projectId, SupplierId = supplierId, Amount = 10, PaymentMethod = "نقدي", Status = "للمراجعة" });
    }

    private static async Task<IReadOnlyList<(string EntityType, int EntityId)>> GetEntityIds(string connectionString)
    {
        await using var db = CreateContext(connectionString);
        return new[]
        {
            ("site-movement", await db.SiteMovements.Select(item => item.Id).SingleAsync()),
            ("excavation", await db.ExcavationRecords.Select(item => item.Id).SingleAsync()),
            ("concrete", await db.ConcreteRecords.Select(item => item.Id).SingleAsync()),
            ("steel", await db.SteelRecords.Select(item => item.Id).SingleAsync()),
            ("finishing", await db.FinishingRecords.Select(item => item.Id).SingleAsync()),
            ("advance-payment", await db.AdvancePayments.Select(item => item.Id).SingleAsync()),
            ("supplier-supply", await db.SupplierSupplies.Select(item => item.Id).SingleAsync()),
            ("supplier-payment", await db.SupplierPayments.Select(item => item.Id).SingleAsync())
        };
    }

    private static async Task<string?> ReadStatus(EmgDbContext db, string entityType, int id) => entityType switch
    {
        "site-movement" => await db.SiteMovements.Where(item => item.Id == id).Select(item => item.Status).SingleAsync(),
        "excavation" => await db.ExcavationRecords.Where(item => item.Id == id).Select(item => item.Status).SingleAsync(),
        "concrete" => await db.ConcreteRecords.Where(item => item.Id == id).Select(item => item.Status).SingleAsync(),
        "steel" => await db.SteelRecords.Where(item => item.Id == id).Select(item => item.Status).SingleAsync(),
        "finishing" => await db.FinishingRecords.Where(item => item.Id == id).Select(item => item.Status).SingleAsync(),
        "advance-payment" => await db.AdvancePayments.Where(item => item.Id == id).Select(item => item.Status).SingleAsync(),
        "supplier-supply" => await db.SupplierSupplies.Where(item => item.Id == id).Select(item => item.Status).SingleAsync(),
        "supplier-payment" => await db.SupplierPayments.Where(item => item.Id == id).Select(item => item.Status).SingleAsync(),
        _ => null
    };

    private readonly record struct ExecutionResult(bool Success, bool ConcurrencyConflict);

    private sealed class NotificationSpy : INotificationService
    {
        private int count;
        public int Count => count;
        public Task<IReadOnlyList<NotificationSummary>> GetAsync(int userId, bool unreadOnly, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NotificationSummary>>([]);
        public Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task NotifyUserAsync(int userId, string type, string title, string message, string? entityType, int? entityId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task NotifyAdminsAsync(string type, string title, string message, string? entityType, int? entityId, CancellationToken cancellationToken) { Interlocked.Increment(ref count); return Task.CompletedTask; }
    }

    private sealed class StatusReadGate(string entityType) : DbCommandInterceptor
    {
        private int reads;
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains($"\"{Table(entityType)}\"", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("LIMIT 1", StringComparison.OrdinalIgnoreCase))
            {
                var readNumber = Interlocked.Increment(ref reads);
                if (readNumber == 2) released.TrySetResult();
                await released.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }

        private static string Table(string type) => type switch
        {
            "site-movement" => "site_movements",
            "excavation" => "excavation_records",
            "concrete" => "concrete_records",
            "steel" => "steel_records",
            "finishing" => "finishing_records",
            "advance-payment" => "advance_payments",
            "supplier-supply" => "supplier_supplies",
            "supplier-payment" => "supplier_payments",
            _ => string.Empty
        };
    }

    private sealed class ConflictWorkflowService : IWorkflowService
    {
        public Task<bool> CorrectSupplyAsync(int entityId, CorrectSupplyRequest request, int? userId, bool isCanonicalAdmin, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkflowResult?> ExecuteAsync(string entityType, int entityId, string action, string? notes, int? userId, bool isAdmin, CancellationToken cancellationToken) => throw new WorkflowConcurrencyException(entityType, entityId, "للمراجعة");
        public Task<WorkflowAuditResult?> AuditAsync(string entityType, int entityId, int userId, bool isAdmin, CancellationToken cancellationToken) => Task.FromResult<WorkflowAuditResult?>(null);
        public Task<IReadOnlyList<WorkflowReviewItem>> GetReviewQueueAsync(int userId, bool isAdmin, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WorkflowReviewItem>>([]);
        public Task<IReadOnlyList<WorkflowResult>> GetHistoryAsync(string entityType, int entityId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WorkflowResult>>([]);
        public Task<bool> CanViewHistoryAsync(string entityType, int entityId, int userId, bool isAdmin, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
