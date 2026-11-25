using System.Data.Common;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using EMG.Api.Modules.FinalAccounts.Services;
using EMG.Api.Modules.Imports.Controllers;
using EMG.Api.Modules.Imports.Dtos;
using EMG.Api.Modules.Imports.Services;
using EMG.Api.Modules.Projects.Repositories;
using EMG.Api.Modules.Reports.Services;
using EMG.Api.Modules.Security.Data;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EMG.WebApi.Tests;

public sealed class ApprovedHistoryImportTests
{
    private const string QaConnection = "EMG_P0_APPROVED_IMPORT_TEST_POSTGRES";
    private const string PermissionCode = "project-imports.import-approved-history";
    private static readonly DateOnly Cutoff = new(2024, 6, 30);
    private static int nextPlot = 310000;

    [ApprovedImportPostgresFact]
    public async Task AssignedAccountantRecognizesTheWorkbookOnceWithAttachmentAndAttestation()
    {
        await using var qa = await ImportQa.CreateAsync();
        var target = await qa.ProjectAsync();
        var accountant = await qa.ActorAsync("accountant", target.Id);
        var content = Workbook(target.Code);
        var controller = qa.Controller(accountant, "accountant");
        var result = await ImportAsync(controller, content, target.Id);
        Assert.True(result is OkObjectResult, ResponseBody(result));
        var approved = Assert.IsType<ImportedProjectResult>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal((1, 1, 1, 1, 1), (approved.SiteMovementsImported, approved.SupplierSuppliesImported,
            approved.SupplierPaymentsImported, approved.AdvancePaymentsImported, approved.AdvanceAllocationsImported));

        var supply = await qa.Db.SupplierSupplies.AsNoTracking().SingleAsync(row => row.ProjectId == target.Id);
        var payment = await qa.Db.SupplierPayments.AsNoTracking().SingleAsync(row => row.ProjectId == target.Id);
        Assert.Equal((supply.Id, supply.SupplierId, 80m, "معتمد"), (payment.SupplyId!.Value, payment.SupplierId, payment.Amount, payment.Status));
        Assert.Equal((240m, "معتمد"), (supply.TotalAmount, supply.Status));
        var attachment = await qa.Db.Attachments.SingleAsync(row => row.Id == approved.AttachmentId);
        Assert.Equal(accountant, attachment.UploadedByUserId);
        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(qa.Root.FullName, attachment.RelativePath)));
        var log = await qa.Db.SecurityAuditLogs.SingleAsync(row => row.EntityType == "project-import" && row.EntityId == target.Id);
        Assert.Equal(accountant, log.UserId);
        Assert.Equal("اعتماد استيراد تاريخي", log.Action);
        Assert.InRange(log.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow);
        using var attestation = JsonDocument.Parse(log.Details);
        Assert.True(attestation.RootElement.GetProperty("confirmedApproved").GetBoolean());
        Assert.Equal("2024-06-30", attestation.RootElement.GetProperty("closedThrough").GetString());
        Assert.Equal(approved.AttachmentId, attestation.RootElement.GetProperty("attachmentId").GetInt32());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), attestation.RootElement.GetProperty("sha256").GetString());
        Assert.False(await qa.Db.WorkflowActions.AnyAsync(row => row.EntityType == "supplier-supply" && row.EntityId == supply.Id));

        var accounts = await new FinalAccountsService(qa.Db).GetAsync(target.Id, accountant, false, CancellationToken.None);
        Assert.NotNull(accounts);
        Assert.Equal((600m, 205m, 160m, 365m), (accounts.TotalIncoming, accounts.TotalExpenses, accounts.SupplierOutstanding, accounts.TotalOutgoing));
        Assert.Equal(125m, accounts.IncomingByCategory["الموقع"]);
        var report = await new ReportsService(qa.Db, new FinalAccountsService(qa.Db)).GetProjectReportAsync(target.Id, accountant, false, CancellationToken.None);
        Assert.NotNull(report);
        Assert.Equal(125m, Assert.Single(report.Movements).Amount);
        Assert.Equal(240m, Assert.Single(report.SupplierSupplies).TotalAmount);
        Assert.Equal(80m, Assert.Single(report.SupplierPayments).Amount);
        var advance = Assert.Single(report.Advances);
        Assert.Equal((600m, 125m, 475m), (advance.Amount, advance.Allocated, advance.Remaining));
        Assert.IsType<BadRequestObjectResult>(await ImportAsync(controller, content, target.Id));
        Assert.IsType<BadRequestObjectResult>(await ImportAsync(controller, Workbook(target.Code, "changed"), target.Id));
        using var ordinary = new MemoryStream(content);
        var ordinaryRetry = await controller.CommitExistingProject(Form(ordinary), target.Id, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(ordinaryRetry.Result);
        Assert.Equal(1, await qa.Db.SupplierSupplies.CountAsync(row => row.ProjectId == target.Id));
        Assert.Equal(1, await qa.Db.SupplierPayments.CountAsync(row => row.ProjectId == target.Id));
    }

    [ApprovedImportPostgresFact]
    public async Task AccountantGrantMustBelongToTheActorAndCoverAnActiveProjectAssignment()
    {
        await using var qa = await ImportQa.CreateAsync();
        var project = await qa.ProjectAsync();
        var otherProject = await qa.ProjectAsync();
        var accountant = await qa.ActorAsync("accountant", otherProject.Id);
        var mixed = await qa.ActorAsync("director", project.Id, extraRole: "accountant", activeAssignment: false);
        var manager = await qa.ActorAsync("project_manager", project.Id);
        await qa.SetGrantAsync("project_manager", "all");

        AssertForbidden(await ImportAsync(qa.Controller(accountant, "accountant"), Workbook(project.Code), project.Id));
        AssertForbidden(await ImportAsync(qa.Controller(mixed, "director", "accountant"), Workbook(project.Code), project.Id));
        AssertForbidden(await ImportAsync(qa.Controller(manager, "project_manager"), Workbook(project.Code), project.Id));
        AssertForbidden(await ImportAsync(qa.Controller(accountant, "accountant"), Workbook(project.Code), null));
        Assert.False(await qa.Db.Attachments.AnyAsync(row => row.EntityId == project.Id && row.EntityType == "project-import"));
    }

    [ApprovedImportPostgresFact]
    public async Task AnExplicitAllProjectsGrantCanCreateAProjectAndItsConfiguredCapApplies()
    {
        await using var qa = await ImportQa.CreateAsync();
        var accountant = await qa.ActorAsync("accountant");
        var controller = qa.Controller(accountant, "accountant");
        var plot = NextPlot();
        try
        {
            await qa.SetGrantAsync("accountant", "all", 200m);
            AssertForbidden(await ImportAsync(controller, Workbook(plot), null));
            Assert.False(await qa.Db.Projects.AnyAsync(row => row.Code == plot));
            qa.Db.ChangeTracker.Clear();
            await qa.SetGrantAsync("accountant", "all", 600m);
            Assert.IsType<OkObjectResult>(await ImportAsync(controller, Workbook(plot), null));
            Assert.True(await qa.Db.Projects.AnyAsync(row => row.Code == plot && row.Status == "نشط"));
        }
        finally
        {
            qa.Db.ChangeTracker.Clear();
            await qa.SetGrantAsync("accountant", "assigned");
        }
    }

    [ApprovedImportPostgresTheory]
    [InlineData("duplicate supply")]
    [InlineData("duplicate payment")]
    [InlineData("invalid payment")]
    [InlineData("overallocated")]
    [InlineData("recent row")]
    [InlineData("wrong project")]
    [InlineData("no attestation")]
    [InlineData("missing supplier")]
    public async Task InvalidHistoricalWorkbookOrAttestationCommitsNothing(string invalid)
    {
        await using var qa = await ImportQa.CreateAsync();
        var project = await qa.ProjectAsync();
        var accountant = await qa.ActorAsync("accountant", project.Id);
        var controller = qa.Controller(accountant, "accountant");
        var content = Workbook(invalid == "wrong project" ? NextPlot() : project.Code, invalid);
        var result = await ImportAsync(controller, content, project.Id, confirmed: invalid != "no attestation");
        Assert.IsType<BadRequestObjectResult>(result);
        var reason = invalid switch
        {
            "duplicate supply" or "duplicate payment" => "مكررة",
            "invalid payment" => "خلية مالية",
            "overallocated" => "يتجاوز",
            "recent row" => "تاريخ غير صالح",
            "wrong project" => "لا يطابق",
            "no attestation" => "أقر بأن",
            "missing supplier" => "اسم المورد مطلوب",
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        Assert.Contains(reason, ResponseBody(result));
        await AssertEmptyAsync(qa.Db, project.Id);
    }

    [ApprovedImportPostgresFact]
    public async Task FailedAuditInsertRollsBackTheProjectLedgerAndAttachment()
    {
        var failAudit = new FailAuditInsert();
        await using var qa = await ImportQa.CreateAsync(failAudit);
        var admin = await qa.ActorAsync("admin");
        var plot = NextPlot();
        failAudit.Enabled = true;
        var result = await ImportAsync(qa.Controller(admin, "admin"), Workbook(plot), null);
        Assert.Contains(Assert.IsAssignableFrom<ObjectResult>(result).StatusCode, new int?[] { 400, 409 });
        Assert.True(failAudit.Attempted, ResponseBody(result));
        Assert.False(await qa.Db.Projects.AnyAsync(row => row.Code == plot));
        Assert.Empty(Directory.EnumerateFiles(qa.Root.FullName, "*.xlsx", SearchOption.AllDirectories));
    }

    [ApprovedImportPostgresFact]
    public async Task SimultaneousImportsIntoTheSameProjectRecognizeOnlyOneBatch()
    {
        await using var qa = await ImportQa.CreateAsync();
        var project = await qa.ProjectAsync();
        var accountant = await qa.ActorAsync("accountant", project.Id);
        var content = Workbook(project.Code);
        await using var other = new EmgDbContext(qa.Options);
        var results = await Task.WhenAll(
            ImportAsync(qa.Controller(accountant, "accountant"), content, project.Id),
            ImportAsync(qa.Controller(accountant, "accountant", other), content, project.Id));
        Assert.True(results.Any(result => result is OkObjectResult), string.Join(Environment.NewLine, results.Select(ResponseBody)));
        Assert.Single(results, result => result is OkObjectResult);
        Assert.Single(results, result => result is BadRequestObjectResult);
        Assert.Equal(1, await qa.Db.SupplierSupplies.CountAsync(row => row.ProjectId == project.Id));
        Assert.Equal(1, await qa.Db.SupplierPayments.CountAsync(row => row.ProjectId == project.Id));
    }

    [ApprovedImportPostgresFact]
    public async Task OrdinaryImportAndLegacyProjectActivationCannotRecognizeLedgerRows()
    {
        await using var qa = await ImportQa.CreateAsync();
        var admin = await qa.ActorAsync("admin");
        var target = await qa.ProjectAsync();
        var controller = qa.Controller(admin, "admin");
        using var workbook = new MemoryStream(Workbook(target.Code));
        var response = await controller.CommitExistingProject(Form(workbook), target.Id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal("مسودة", await qa.Db.SupplierSupplies.Where(row => row.ProjectId == target.Id).Select(row => row.Status).SingleAsync());
        Assert.Equal("مسودة", await qa.Db.SupplierPayments.Where(row => row.ProjectId == target.Id).Select(row => row.Status).SingleAsync());

        // A separate ordinary project has attacker-editable source labels and a legacy attachment.
        var marked = await qa.ProjectAsync("تحت المراجعة");
        qa.Db.SiteMovements.Add(new SiteMovement { ProjectId = marked.Id, Description = "Ordinary QA site", TotalAmount = 70m, Reference = "استيراد: نص يدوي" });
        qa.Db.AdvancePayments.Add(new AdvancePayment { ProjectId = marked.Id, Amount = 150m, Notes = "استيراد: نص يدوي" });
        qa.Db.SupplierPayments.Add(new SupplierPayment { ProjectId = marked.Id, Supplier = new Supplier { Name = "QA ordinary supplier" }, Amount = 40m, Notes = "استيراد: نص يدوي" });
        qa.Db.Attachments.Add(new Attachment { EntityId = marked.Id, EntityType = "project-import", OriginalFileName = "ordinary.xlsx" });
        await qa.Db.SaveChangesAsync();
        Assert.NotNull(await new EfProjectsRepository(qa.Db).DecideImportAsync(marked.Id, true, null, admin, CancellationToken.None));
        Assert.Equal("مسودة", await qa.Db.SiteMovements.Where(row => row.ProjectId == marked.Id).Select(row => row.Status).SingleAsync());
        Assert.Equal("مسودة", await qa.Db.AdvancePayments.Where(row => row.ProjectId == marked.Id).Select(row => row.Status).SingleAsync());
        Assert.Equal("مسودة", await qa.Db.SupplierPayments.Where(row => row.ProjectId == marked.Id).Select(row => row.Status).SingleAsync());
    }

    private static void AssertForbidden(IActionResult? result) =>
        Assert.True(result is ObjectResult { StatusCode: 403 }, ResponseBody(result));

    private static string ResponseBody(IActionResult? result) =>
        result is ObjectResult { Value: { } payload } ? JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }) : result?.GetType().Name ?? "No result";

    private static async Task AssertEmptyAsync(EmgDbContext db, int projectId)
    {
        Assert.False(await db.SiteMovements.AnyAsync(row => row.ProjectId == projectId));
        Assert.False(await db.SupplierSupplies.AnyAsync(row => row.ProjectId == projectId));
        Assert.False(await db.SupplierPayments.AnyAsync(row => row.ProjectId == projectId));
        Assert.False(await db.AdvancePayments.AnyAsync(row => row.ProjectId == projectId));
        Assert.False(await db.Attachments.AnyAsync(row => row.EntityType == "project-import" && row.EntityId == projectId));
        Assert.False(await db.SecurityAuditLogs.AnyAsync(row => row.EntityType == "project-import" && row.EntityId == projectId));
    }

    private static async Task<IActionResult?> ImportAsync(ImportsController controller, byte[] content, int? target, bool confirmed = true)
    {
        using var stream = new MemoryStream(content);
        return (await controller.CommitApprovedHistory(Form(stream), target, confirmed, Cutoff, CancellationToken.None)).Result;
    }

    private static IFormFile Form(MemoryStream stream) => new FormFile(stream, 0, stream.Length, "file", "synthetic-approved.xlsx")
    {
        Headers = new HeaderDictionary(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    private static string NextPlot() => Interlocked.Increment(ref nextPlot).ToString();

    private static byte[] Workbook(string code, string change = "")
    {
        using var workbook = new XLWorkbook();
        var sourceDate = new DateTime(2024, 6, 5);
        var site = workbook.AddWorksheet("خاص باعمال الموقع");
        site.Cell("A1").Value = "رقم القطعة: " + code;
        site.Cell("A2").Value = "أرشيف تجريبي مقفل وموقع للاختبار";
        Header(site, 9, (2, "التاريخ"), (3, "البيان"), (4, "اسم المورد"), (5, "الوحدة"), (6, "السعر"), (8, "الكمية"), (10, "الإجمالي"));
        site.Cell("B10").Value = change == "recent row" ? new DateTime(2024, 7, 1) : sourceDate;
        site.Cell("C10").Value = change == "changed" ? "مصاريف جديدة" : "نقل مخلفات";
        site.Cell("F10").Value = 125;
        site.Cell("H10").Value = 1;
        site.Cell("J10").Value = 125;

        var finishing = workbook.AddWorksheet("اعمال المباني");
        Header(finishing, 9, (2, "التاريخ"), (3, "البيان"), (4, "اسم المورد"), (5, "الوحدة"), (6, "السعر"),
            (9, "الكمية"), (10, "الإجمالي"), (11, "الدفعات"));
        SupplyRow(10, "توريد طوب");
        if (change == "duplicate supply") SupplyRow(11, "توريد طوب");
        if (change == "duplicate payment") SupplyRow(11, "توريد بلاط");
        if (change == "invalid payment") finishing.Cell("K10").Value = "قيمة تالفة";

        var funding = workbook.AddWorksheet("دفعات تحت الحساب");
        Header(funding, 3, (1, "التاريخ"), (2, "دفعات تحت حساب الموقع"), (14, "إجمالي الدفعات"), (15, "الدافع"));
        funding.Cell("A4").Value = sourceDate;
        funding.Cell("B4").Value = change == "overallocated" ? 601 : 125;
        funding.Cell("N4").Value = 600;
        funding.Cell("O4").Value = "مالك تجريبي";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();

        void SupplyRow(int row, string label)
        {
            finishing.Cell(row, 2).Value = sourceDate;
            finishing.Cell(row, 3).Value = label;
            if (change != "missing supplier") finishing.Cell(row, 4).Value = "مورد QA " + code;
            finishing.Cell(row, 5).Value = "وحدة";
            finishing.Cell(row, 6).Value = 120;
            finishing.Cell(row, 9).Value = 2;
            finishing.Cell(row, 10).Value = 240;
            finishing.Cell(row, 11).Value = 80;
        }
    }

    private static void Header(IXLWorksheet sheet, int row, params (int Column, string Label)[] headers)
    {
        foreach (var (column, label) in headers) sheet.Cell(row, column).Value = label;
    }

    public sealed class ApprovedImportPostgresFactAttribute : FactAttribute
    {
        public ApprovedImportPostgresFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(QaConnection))) Skip = "Requires the disposable approved-import PostgreSQL fixture.";
        }
    }

    public sealed class ApprovedImportPostgresTheoryAttribute : TheoryAttribute
    {
        public ApprovedImportPostgresTheoryAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(QaConnection))) Skip = "Requires the disposable approved-import PostgreSQL fixture.";
        }
    }

    private sealed class ImportQa(DirectoryInfo root, WebApplication host, DbContextOptions<EmgDbContext> options) : IAsyncDisposable
    {
        public DirectoryInfo Root { get; } = root;
        public DbContextOptions<EmgDbContext> Options { get; } = options;
        public EmgDbContext Db { get; } = new(options);

        public static async Task<ImportQa> CreateAsync(params IInterceptor[] interceptors)
        {
            var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(QaConnection));
            Assert.Equal("127.0.0.1", connection.Host);
            Assert.Equal(5433, connection.Port);
            Assert.Matches("^emg_import_qa_[a-f0-9]{8}$", connection.Database!);
            Assert.Equal("emg_sec_p0_qa", connection.Username);
            Assert.False(connection.Pooling);
            await using (var probe = new NpgsqlConnection(connection.ConnectionString))
            {
                await probe.OpenAsync();
                await using var identity = new NpgsqlCommand("SELECT current_database(), current_setting('cluster_name')", probe);
                await using var row = await identity.ExecuteReaderAsync();
                Assert.True(await row.ReadAsync());
                Assert.Equal(connection.Database, row.GetString(0));
                Assert.Equal("emg-sec-p0-s2-" + connection.Database![^8..], row.GetString(1));
            }
            var directory = Directory.CreateTempSubdirectory("emg-approved-import-");
            var app = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = directory.FullName, EnvironmentName = "QA" });
            app.Configuration.Sources.Clear();
            app.Services.AddDbContext<EmgDbContext>(builder => builder.UseNpgsql(connection.ConnectionString));
            var options = new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(connection.ConnectionString).AddInterceptors(interceptors).Options;
            var qa = new ImportQa(directory, app.Build(), options);
            try
            {
                await qa.Db.Database.EnsureCreatedAsync();
                if (!await qa.Db.Permissions.AnyAsync(item => item.Code == PermissionCode))
                    await qa.host.SeedSecurityAsync();
                return qa;
            }
            catch
            {
                await qa.DisposeAsync();
                throw;
            }
        }

        private readonly WebApplication host = host;

        public async Task<Project> ProjectAsync(string status = "نشط")
        {
            var project = new Project { Name = "QA approved import", Code = NextPlot(), Status = status };
            Db.Projects.Add(project);
            await Db.SaveChangesAsync();
            return project;
        }

        public async Task<int> ActorAsync(string role, int? project = null, string? extraRole = null, bool activeAssignment = true)
        {
            var key = "qa-import-" + Guid.NewGuid().ToString("N");
            var actor = new AppUser { UserName = key, DisplayName = key, Email = key + "@example.test" };
            var roles = await Db.Roles.Where(item => item.Name == role || item.Name == extraRole).ToListAsync();
            foreach (var actorRole in roles) actor.UserRoles.Add(new UserRole { RoleId = actorRole.Id });
            if (project is int target) actor.ProjectAssignments.Add(new UserProjectAssignment { ProjectId = target, IsActive = activeAssignment });
            Db.Users.Add(actor);
            await Db.SaveChangesAsync();
            return actor.Id;
        }

        public async Task SetGrantAsync(string role, string scope, decimal? cap = null)
        {
            var grant = await Db.RolePermissions.SingleOrDefaultAsync(item => item.Role.Name == role && item.Permission.Code == PermissionCode);
            if (grant is null)
            {
                grant = new RolePermission
                {
                    RoleId = await Db.Roles.Where(item => item.Name == role).Select(item => item.Id).SingleAsync(),
                    PermissionId = await Db.Permissions.Where(item => item.Code == PermissionCode).Select(item => item.Id).SingleAsync()
                };
                Db.RolePermissions.Add(grant);
            }
            grant.Scope = scope;
            grant.AmountLimit = cap;
            await Db.SaveChangesAsync();
        }

        public ImportsController Controller(int userId, string role, EmgDbContext? context = null) => Controller(userId, [role], context);

        public ImportsController Controller(int userId, string firstRole, string extraRole) => Controller(userId, [firstRole, extraRole], null);

        private ImportsController Controller(int userId, string[] roles, EmgDbContext? context)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            return new ImportsController(new WorkbookImportService(context ?? Db, host.Environment))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "EmgSession")) }
                }
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await host.DisposeAsync();
            Root.Delete(recursive: true);
        }
    }

    private sealed class FailAuditInsert : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public bool Attempted { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("INSERT INTO security_audit_logs", StringComparison.OrdinalIgnoreCase))
            {
                Attempted = true;
                throw new InvalidOperationException("Synthetic audit database failure.");
            }
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
