using System.Data;
using System.Data.Common;
using System.Security.Claims;
using EMG.Api.Modules.FinalAccounts.Services;
using EMG.Api.Modules.Notifications.Services;
using EMG.Api.Modules.Security.Data;
using EMG.Api.Modules.Workflow.Controllers;
using EMG.Api.Modules.Workflow.Dtos;
using EMG.Api.Modules.Workflow.Services;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.WebApi.Tests;

public sealed partial class WorkflowAuthorizationTests
{
    private static readonly CancellationToken Cancellation = CancellationToken.None;

    [Fact]
    public async Task CanonicalAdminCanApproveAndPostAcrossProjectsAndStillMustPassValidation()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var adminId = await qa.AddUserAsync("admin");
        var controller = qa.Controller(adminId, "admin");
        var project = await qa.AddProjectAsync("admin-project");
        var invalid = await qa.AddSiteMovementAsync(project, "للمراجعة", category: null);
        var valid = await qa.AddSiteMovementAsync(project, "للمراجعة");

        Assert.IsType<UnprocessableEntityObjectResult>((await controller.Execute("site-movement", invalid, "اعتماد", new(null), Cancellation)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Execute("site-movement", valid, "ترحيل", new(null), Cancellation)).Result);
        Assert.IsType<OkObjectResult>((await controller.Execute("site-movement", valid, "اعتماد", new(null), Cancellation)).Result);
        Assert.IsType<OkObjectResult>((await controller.Execute("site-movement", valid, "ترحيل", new(null), Cancellation)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Execute("site-movement", valid, "ترحيل", new(null), Cancellation)).Result);

        Assert.Equal("مرحّل", await qa.SiteStatusAsync(valid));
        Assert.Equal("للمراجعة", await qa.SiteStatusAsync(invalid));
        var history = await qa.Db.WorkflowActions.OrderBy(row => row.Id).ToListAsync();
        Assert.Collection(history,
            row => Assert.Equal(("للمراجعة", "معتمد", adminId), (row.FromStatus, row.ToStatus, row.UserId.GetValueOrDefault())),
            row => Assert.Equal(("معتمد", "مرحّل", adminId), (row.FromStatus, row.ToStatus, row.UserId.GetValueOrDefault())));
        Assert.Equal(2, await qa.Db.Notifications.CountAsync());
    }

    [Theory]
    [InlineData("site-movement", "site-movements", "project_manager", "accountant")]
    [InlineData("excavation", "excavation", "project_manager", "accountant")]
    [InlineData("concrete", "concrete", "project_manager", "accountant")]
    [InlineData("steel", "steel", "project_manager", "accountant")]
    [InlineData("finishing", "finishing", "project_manager", "accountant")]
    [InlineData("advance-payment", "advances", "accountant", "project_manager")]
    [InlineData("supplier-payment", "suppliers", "accountant", "project_manager")]
    public async Task ApprovalUsesV1RolePolicyForEachRecord(string type, string code, string reviewer, string nonReviewer)
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("role-policy");
        var movement = await qa.AddReviewRecordAsync(type, project);
        var deniedUser = await qa.AddUserAsync(nonReviewer);
        var reviewerId = await qa.AddUserAsync(reviewer);
        await qa.AssignAsync(deniedUser, project);
        await qa.AssignAsync(reviewerId, project);
        // A legacy or custom grant does not change which role can review this record type.
        await qa.GrantAsync(nonReviewer, code + ".view", "assigned");
        await qa.GrantAsync(nonReviewer, code + ".approve", "assigned");
        await qa.GrantAsync(nonReviewer, code + ".reject", "assigned");

        var denied = qa.Controller(deniedUser, nonReviewer);
        AssertForbidden((await denied.Execute(type, movement, "اعتماد", new(null), Cancellation)).Result);
        AssertForbidden((await denied.Execute(type, movement, "رفض", new(null), Cancellation)).Result);
        await qa.AssertNoSideEffectsAsync();
        Assert.IsType<OkObjectResult>((await qa.Controller(reviewerId, reviewer).Execute(type, movement, "اعتماد", new(null), Cancellation)).Result);
    }

    [Theory]
    [InlineData("engineer")]
    [InlineData("user")]
    public async Task EngineerAndBasicUserCannotApprove(string role)
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("operational");
        var actor = await qa.AddUserAsync(role);
        await qa.AssignAsync(actor, project);
        await qa.GrantAsync(role, "site-movements.view", "assigned");
        var id = await qa.AddSiteMovementAsync(project, "للمراجعة");

        AssertForbidden((await qa.Controller(actor, role).Execute("site-movement", id, "اعتماد", new(null), Cancellation)).Result);
        Assert.Equal("للمراجعة", await qa.SiteStatusAsync(id));
        await qa.AssertNoSideEffectsAsync();
    }

    [Theory]
    [InlineData("إرسال", "submit", "مسودة")]
    [InlineData("اعتماد", "approve", "للمراجعة")]
    [InlineData("رفض", "reject", "للمراجعة")]
    [InlineData("ترحيل", "post", "معتمد")]
    public async Task GenericWorkflowPermissionDoesNotReplaceTheExactActionGrant(string action, string permission, string status)
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("generic-grant");
        var actor = await qa.AddUserAsync("project_manager");
        await qa.AssignAsync(actor, project);
        await qa.RemoveGrantAsync("project_manager", "site-movements." + permission);
        var id = await qa.AddSiteMovementAsync(project, status);

        AssertForbidden((await qa.Controller(actor, "project_manager").Execute("site-movement", id, action, new(null), Cancellation)).Result);
        Assert.Equal(status, await qa.SiteStatusAsync(id));
        await qa.AssertNoSideEffectsAsync();
    }

    [Theory]
    [InlineData("إرسال", "submit", "مسودة", 101, false)]
    [InlineData("اعتماد", "approve", "للمراجعة", 100, true)]
    [InlineData("اعتماد", "approve", "للمراجعة", 101, false)]
    [InlineData("اعتماد", "approve", "للمراجعة", -100, true)]
    [InlineData("اعتماد", "approve", "للمراجعة", -101, false)]
    [InlineData("رفض", "reject", "للمراجعة", 101, false)]
    [InlineData("ترحيل", "post", "معتمد", 101, false)]
    public async Task ExactGrantLimitAppliesToTheAmountMagnitude(string action, string permission, string status, int amount, bool allowed)
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("capped-action");
        var director = await qa.AddUserAsync("director");
        await qa.GrantAsync("director", "site-movements.view", "all");
        await qa.GrantAsync("director", "site-movements." + permission, "all", 100m);
        var id = await qa.AddSiteMovementAsync(project, status, amount);

        var result = (await qa.Controller(director, "director").Execute("site-movement", id, action, new(null), Cancellation)).Result;
        if (allowed)
            Assert.IsType<OkObjectResult>(result);
        else
        {
            AssertForbidden(result);
            Assert.Equal(status, await qa.SiteStatusAsync(id));
            await qa.AssertNoSideEffectsAsync();
        }
    }

    [Fact]
    public async Task MixedRolesCannotBorrowScopeOrAnAmountLimitAcrossGrants()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var assigned = await qa.AddProjectAsync("assigned");
        var other = await qa.AddProjectAsync("other");
        var actor = await qa.AddUserAsync("director", "project_manager");
        await qa.AssignAsync(actor, assigned);
        await qa.AssignAsync(actor, other, active: false);
        var withinAssignment = await qa.AddSiteMovementAsync(assigned, "للمراجعة", amount: 80m);
        var outsideAssignment = await qa.AddSiteMovementAsync(other, "للمراجعة", amount: 80m);
        var withinDirectorCap = await qa.AddSiteMovementAsync(other, "للمراجعة", amount: 60m);
        await qa.GrantAsync("director", "site-movements.view", "all");
        await qa.GrantAsync("director", "site-movements.approve", "all", 60m);
        await qa.GrantAsync("project_manager", "site-movements.approve", "all", 200m);
        var controller = qa.Controller(actor, "director", "project_manager");

        var audit = Value<WorkflowAuditResult>((await controller.Audit("site-movement", outsideAssignment, Cancellation)).Result);
        Assert.False(audit.CanApprove);
        var queue = Value<IReadOnlyList<WorkflowReviewItem>>((await controller.ReviewQueue(Cancellation)).Result);
        Assert.False(Assert.Single(queue, row => row.EntityId == outsideAssignment).CanApprove);
        Assert.True(Assert.Single(queue, row => row.EntityId == withinAssignment).CanApprove);
        AssertForbidden((await controller.Execute("site-movement", outsideAssignment, "اعتماد", new(null), Cancellation)).Result);
        Assert.Equal("للمراجعة", await qa.SiteStatusAsync(outsideAssignment));
        await qa.AssertNoSideEffectsAsync();
        Assert.IsType<OkObjectResult>((await controller.Execute("site-movement", withinAssignment, "اعتماد", new(null), Cancellation)).Result);
        Assert.IsType<OkObjectResult>((await controller.Execute("site-movement", withinDirectorCap, "اعتماد", new(null), Cancellation)).Result);

        var managerId = await qa.AddUserAsync("project_manager");
        await qa.AssignAsync(managerId, assigned);
        var manager = qa.Controller(managerId, "project_manager");
        Assert.IsType<ForbidResult>((await manager.Audit("site-movement", withinDirectorCap, Cancellation)).Result);
        Assert.IsType<ForbidResult>((await manager.History("site-movement", withinDirectorCap, Cancellation)).Result);
        Assert.Single(Value<IReadOnlyList<WorkflowResult>>((await manager.History("site-movement", withinAssignment, Cancellation)).Result));
        var assignedQueue = Value<IReadOnlyList<WorkflowReviewItem>>((await manager.ReviewQueue(Cancellation)).Result);
        Assert.Equal(assigned, Assert.Single(assignedQueue).ProjectId);
    }

    [Fact]
    public async Task EngineerCanSubmitAndAnExplicitPostGrantDoesNotRequireApprovalAuthority()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("submission-and-posting");
        var engineer = await qa.AddUserAsync("engineer");
        var manager = await qa.AddUserAsync("project_manager");
        var accountant = await qa.AddUserAsync("accountant");
        await qa.AssignAsync(engineer, project);
        await qa.AssignAsync(manager, project);
        var site = await qa.AddSiteMovementAsync(project, "مسودة");
        var advance = await qa.AddReviewRecordAsync("advance-payment", project);
        var managerController = qa.Controller(manager, "project_manager");

        Assert.IsType<OkObjectResult>((await qa.Controller(engineer, "engineer").Execute("site-movement", site, "إرسال", new(null), Cancellation)).Result);
        Assert.IsType<OkObjectResult>((await managerController.Execute("site-movement", site, "رفض", new(null), Cancellation)).Result);
        Assert.IsType<OkObjectResult>((await qa.Controller(engineer, "engineer").Execute("site-movement", site, "إرسال", new(null), Cancellation)).Result);
        Assert.IsType<OkObjectResult>((await managerController.Execute("site-movement", site, "اعتماد", new(null), Cancellation)).Result);
        Assert.IsType<OkObjectResult>((await qa.Controller(accountant, "accountant").Execute("advance-payment", advance, "اعتماد", new(null), Cancellation)).Result);
        Assert.True(Value<WorkflowAuditResult>((await managerController.Audit("advance-payment", advance, Cancellation)).Result).CanPost);
        Assert.IsType<OkObjectResult>((await managerController.Execute("advance-payment", advance, "ترحيل", new(null), Cancellation)).Result);
        Assert.Equal("مرحّل", await qa.Db.AdvancePayments.Where(row => row.Id == advance).Select(row => row.Status).SingleAsync());
    }

    [Fact]
    public async Task DeniedSupplierAndAdvanceActionsLeaveStatusesAndRecognizedAmountsIntact()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var project = await qa.AddProjectAsync("finance-invariants");
        await qa.AddUserAsync("admin");
        var manager = await qa.AddUserAsync("project_manager");
        await qa.AssignAsync(manager, project);
        var supply = await qa.AddReviewRecordAsync("supplier-supply", project);
        var payment = await qa.AddReviewRecordAsync("supplier-payment", project);
        var advance = await qa.AddReviewRecordAsync("advance-payment", project);
        var accounts = new FinalAccountsService(qa.Db);
        var before = await accounts.GetAsync(project, manager, false, Cancellation);
        Assert.NotNull(before);

        var controller = qa.Controller(manager, "project_manager");
        Assert.IsType<BadRequestObjectResult>((await controller.Execute("supplier-supply", supply, "اعتماد", new(null), Cancellation)).Result);
        AssertForbidden((await controller.Execute("supplier-payment", payment, "اعتماد", new(null), Cancellation)).Result);
        AssertForbidden((await controller.Execute("advance-payment", advance, "اعتماد", new(null), Cancellation)).Result);
        await qa.AssertNoSideEffectsAsync();
        Assert.Equal(new[] { ("للمراجعة", 100m), ("للمراجعة", 100m), ("للمراجعة", 100m) },
            new[]
            {
                await qa.Db.SupplierSupplies.Select(row => new ValueTuple<string, decimal>(row.Status, row.TotalAmount)).SingleAsync(),
                await qa.Db.SupplierPayments.Select(row => new ValueTuple<string, decimal>(row.Status, row.Amount)).SingleAsync(),
                await qa.Db.AdvancePayments.Select(row => new ValueTuple<string, decimal>(row.Status, row.Amount)).SingleAsync()
            });
        var after = await accounts.GetAsync(project, manager, false, Cancellation);
        Assert.NotNull(after);
        Assert.Equal((0m, 0m, 0m), (before.SupplierOutstanding, before.TotalExpenses, before.TotalIncoming));
        Assert.Equal((before.SupplierOutstanding, before.TotalExpenses, before.TotalIncoming),
            (after.SupplierOutstanding, after.TotalExpenses, after.TotalIncoming));
    }

    [Fact]
    public async Task OwnScopeUsesNativeCreatorAndStillRequiresAssignment()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        var assigned = await qa.AddProjectAsync("owned");
        var other = await qa.AddProjectAsync("outside-own-scope");
        var manager = await qa.AddUserAsync("project_manager");
        await qa.AssignAsync(manager, assigned);
        await qa.GrantAsync("project_manager", "advances.submit", "own");
        var native = await qa.AddAdvanceAsync(assigned, manager);
        var imported = await qa.AddAdvanceAsync(assigned, manager, notes: "استيراد: QA sheet row 4");
        var outside = await qa.AddAdvanceAsync(other, manager);
        var controller = qa.Controller(manager, "project_manager");

        AssertForbidden((await controller.Execute("advance-payment", imported, "إرسال", new(null), Cancellation)).Result);
        AssertForbidden((await controller.Execute("advance-payment", outside, "إرسال", new(null), Cancellation)).Result);
        await qa.AssertNoSideEffectsAsync();
        Assert.IsType<OkObjectResult>((await controller.Execute("advance-payment", native, "إرسال", new(null), Cancellation)).Result);
    }

    [Fact]
    public async Task WorkflowUpdateAndHistoryAbortIfTheAuthorizedAmountChangesBeforeTheWrite()
    {
        var amountChange = new ChangeAmountBeforeTransition();
        await using var qa = await WorkflowQa.CreateAsync(amountChange);
        var project = await qa.AddProjectAsync("change-during-authorization");
        var actor = await qa.AddUserAsync("project_manager");
        await qa.AddUserAsync("admin");
        await qa.AssignAsync(actor, project);
        await qa.GrantAsync("project_manager", "site-movements.approve", "assigned", 100m);
        var id = await qa.AddSiteMovementAsync(project, "للمراجعة");
        amountChange.RecordId = id;

        var result = await qa.Controller(actor, "project_manager").Execute("site-movement", id, "اعتماد", new(null), Cancellation);
        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("WORKFLOW_CONCURRENCY_CONFLICT", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.True(amountChange.Applied);
        Assert.Equal("للمراجعة", await qa.SiteStatusAsync(id));
        Assert.Equal(100m, await qa.Db.SiteMovements.Where(row => row.Id == id).Select(row => row.TotalAmount).SingleAsync());
        await qa.AssertNoSideEffectsAsync();
    }

    [Fact]
    public async Task SecondStartupRetainsSubmitGrantsAndPreservesConfiguredScopeAndCap()
    {
        await using var qa = await WorkflowQa.CreateAsync();
        Assert.Equal("all", (await qa.GetGrantAsync("director", "advances.submit")).Scope);
        Assert.Equal("all", (await qa.GetGrantAsync("accountant", "advances.submit")).Scope);
        Assert.Equal("assigned", (await qa.GetGrantAsync("project_manager", "advances.submit")).Scope);
        await qa.GrantAsync("accountant", "advances.submit", "assigned", 70m);
        // An existing approval cap does not recreate a deliberately revoked rejection grant.
        await qa.GrantAsync("director", "site-movements.approve", "all", 75m);
        await qa.RemoveGrantAsync("director", "site-movements.reject");
        await qa.GrantAsync("director", "advances.manage", "assigned");
        await qa.RunStartupSeedingAsync();

        Assert.False(await qa.Db.RolePermissions.AnyAsync(grant => grant.Role.Name == "director" && grant.Permission.Code == "site-movements.reject"));
        var retained = await qa.GetGrantAsync("accountant", "advances.submit");
        Assert.Equal(("assigned", 70m), (retained.Scope, retained.AmountLimit.GetValueOrDefault()));
        Assert.Equal(3, await qa.Db.RolePermissions.CountAsync(grant => grant.Role.Name != "admin" && grant.Permission.Code == "advances.submit"));
        var assigned = await qa.AddProjectAsync("seed-assigned");
        var other = await qa.AddProjectAsync("seed-unassigned");
        foreach (var role in new[] { "director", "project_manager", "accountant" })
        {
            var actor = await qa.AddUserAsync(role);
            if (role != "director") await qa.AssignAsync(actor, assigned);
            var controller = qa.Controller(actor, role);
            var advance = await qa.AddAdvanceAsync(role == "director" ? other : assigned, actor, amount: 70m);
            Assert.IsType<OkObjectResult>((await controller.Execute("advance-payment", advance, "إرسال", new(null), Cancellation)).Result);
            if (role != "director")
            {
                var outside = await qa.AddAdvanceAsync(other, actor, amount: 70m);
                AssertForbidden((await controller.Execute("advance-payment", outside, "إرسال", new(null), Cancellation)).Result);
            }
        }
    }

    private static void AssertForbidden(IActionResult? result) =>
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);

    private static T Value<T>(IActionResult? result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result).Value);

    private sealed class ChangeAmountBeforeTransition : DbCommandInterceptor
    {
        public int? RecordId { get; set; }
        public bool Applied { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (RecordId is int id && command.CommandText.StartsWith("UPDATE \"site_movements\"", StringComparison.Ordinal))
            {
                var connection = Assert.IsType<SqliteConnection>(command.Connection);
                await using var change = connection.CreateCommand();
                change.Transaction = Assert.IsType<SqliteTransaction>(command.Transaction);
                change.CommandText = "UPDATE site_movements SET TotalAmount = $amount WHERE Id = $id";
                change.Parameters.AddWithValue("$amount", 125m);
                change.Parameters.AddWithValue("$id", id);
                Assert.Equal(1, await change.ExecuteNonQueryAsync(cancellationToken));
                Applied = true;
                RecordId = null;
            }
            return result;
        }
    }

    private sealed class WorkflowQa(SqliteConnection connection, DbContextOptions<EmgDbContext> options) : IAsyncDisposable
    {
        public EmgDbContext Db { get; } = new(options);

        public static async Task<WorkflowQa> CreateAsync(params IInterceptor[] interceptors)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).AddInterceptors(interceptors).Options;
            var qa = new WorkflowQa(connection, options);
            try
            {
                qa.AssertIsolated(qa.Db);
                await qa.Db.Database.EnsureCreatedAsync();
                await qa.RunStartupSeedingAsync();
                return qa;
            }
            catch
            {
                await qa.DisposeAsync();
                throw;
            }
        }

        public async Task RunStartupSeedingAsync()
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "QA" });
            builder.Configuration.Sources.Clear();
            builder.Services.AddDbContext<EmgDbContext>(options => options.UseSqlite(connection));
            await using var host = builder.Build();
            using var scope = host.Services.CreateScope();
            AssertIsolated(scope.ServiceProvider.GetRequiredService<EmgDbContext>());
            await host.SeedSecurityAsync();
        }

        private void AssertIsolated(EmgDbContext db)
        {
            Assert.True(db.Database.IsSqlite());
            Assert.Same(connection, db.Database.GetDbConnection());
            Assert.Equal(":memory:", new SqliteConnectionStringBuilder(connection.ConnectionString).DataSource);
            Assert.True(connection.DataSource is "" or ":memory:");
            Assert.Equal(ConnectionState.Open, connection.State);
        }

        public async Task<int> AddUserAsync(params string[] roles)
        {
            var name = "qa-" + Guid.NewGuid().ToString("N");
            var user = new AppUser { UserName = name, DisplayName = name, Email = name + "@example.test", MustChangePassword = false };
            foreach (var role in roles)
                user.UserRoles.Add(new UserRole { RoleId = await Db.Roles.Where(row => row.Name == role).Select(row => row.Id).SingleAsync() });
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            return user.Id;
        }

        public WorkflowController Controller(int userId, params string[] roles)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            return new WorkflowController(new WorkflowService(Db, new NotificationService(Db)))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "EmgSession")) }
                }
            };
        }

        public async Task<int> AddProjectAsync(string code)
        {
            var project = new Project { Code = code, Name = code };
            Db.Projects.Add(project);
            await Db.SaveChangesAsync();
            return project.Id;
        }

        public async Task AssignAsync(int user, int project, bool active = true)
        {
            Db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = user, ProjectId = project, IsActive = active });
            await Db.SaveChangesAsync();
        }

        public Task<RolePermission> GetGrantAsync(string role, string code) =>
            Db.RolePermissions.AsNoTracking().SingleAsync(grant => grant.Role.Name == role && grant.Permission.Code == code);

        public async Task GrantAsync(string role, string code, string scope, decimal? limit = null)
        {
            var grant = await Db.RolePermissions.SingleOrDefaultAsync(row => row.Role.Name == role && row.Permission.Code == code);
            if (grant is null)
            {
                grant = new RolePermission
                {
                    RoleId = await Db.Roles.Where(row => row.Name == role).Select(row => row.Id).SingleAsync(),
                    PermissionId = await Db.Permissions.Where(row => row.Code == code).Select(row => row.Id).SingleAsync()
                };
                Db.RolePermissions.Add(grant);
            }
            grant.Scope = scope;
            grant.AmountLimit = limit;
            await Db.SaveChangesAsync();
        }

        public async Task RemoveGrantAsync(string role, string code)
        {
            var grant = await Db.RolePermissions.SingleAsync(row => row.Role.Name == role && row.Permission.Code == code);
            Db.RolePermissions.Remove(grant);
            await Db.SaveChangesAsync();
        }

        public Task<int> AddSiteMovementAsync(int project, string status, decimal amount = 100m, string? category = "QA") =>
            AddAsync(new SiteMovement
            {
                ProjectId = project, Status = status, TotalAmount = amount, ExpenseCategory = category,
                Description = amount < 0 ? "تسوية QA" : "مصاريف QA"
            });

        public Task<int> AddAdvanceAsync(int project, int owner, string? notes = null, decimal amount = 100m) =>
            AddAsync(new AdvancePayment { ProjectId = project, CreatedByUserId = owner, Amount = amount, Notes = notes });

        public Task<int> AddReviewRecordAsync(string type, int project) => type switch
        {
            "site-movement" => AddSiteMovementAsync(project, "للمراجعة"),
            "excavation" => AddAsync(new ExcavationRecord { ProjectId = project, Status = "للمراجعة", Description = "QA", UnitPrice = 100m, TotalAmount = 100m, CalculatedQuantity = 1m }),
            "concrete" => AddAsync(new ConcreteRecord { ProjectId = project, Status = "للمراجعة", Description = "QA", ElementType = "قاعدة", UnitPrice = 100m, TotalAmount = 100m, Quantity = 1m }),
            "steel" => AddAsync(new SteelRecord { ProjectId = project, Status = "للمراجعة", Description = "QA", UnitPrice = 100m, TotalAmount = 100m, Quantity = 1m }),
            "finishing" => AddAsync(new FinishingRecord { ProjectId = project, Status = "للمراجعة", Description = "QA", WorkType = "عزل", UnitPrice = 100m, TotalAmount = 100m, Quantity = 1m }),
            "advance-payment" => AddAsync(new AdvancePayment { ProjectId = project, Status = "للمراجعة", Amount = 100m }),
            "supplier-supply" => AddAsync(new SupplierSupply { ProjectId = project, Supplier = new Supplier { Name = "QA supplier" }, Status = "للمراجعة", ItemName = "QA item", Unit = "وحدة", Quantity = 1m, UnitPrice = 100m, TotalAmount = 100m }),
            "supplier-payment" => AddAsync(new SupplierPayment { ProjectId = project, Supplier = new Supplier { Name = "QA supplier" }, Status = "للمراجعة", Amount = 100m }),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        private async Task<int> AddAsync(object entity)
        {
            var entry = Db.Add(entity);
            await Db.SaveChangesAsync();
            return (int)entry.Property("Id").CurrentValue!;
        }

        public Task<string> SiteStatusAsync(int id) => Db.SiteMovements.Where(row => row.Id == id).Select(row => row.Status).SingleAsync();

        public async Task AssertNoSideEffectsAsync()
        {
            Assert.Equal(0, await Db.WorkflowActions.CountAsync());
            Assert.Equal(0, await Db.Notifications.CountAsync());
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
