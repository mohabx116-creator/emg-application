using System.Net;
using EMG.Api.Modules.Projects;
using EMG.Api.Modules.Security;
using EMG.Api.Modules.Security.Data;
using EMG.Api.Modules.Suppliers;
using EMG.Api.Modules.Attachments;
using EMG.Api.Modules.SiteMovements;
using EMG.Api.Modules.Excavation;
using EMG.Api.Modules.Concrete;
using EMG.Api.Modules.Steel;
using EMG.Api.Modules.Finishing;
using EMG.Api.Modules.Advances;
using EMG.Api.Modules.FinalAccounts;
using EMG.Api.Modules.Workflow;
using EMG.Api.Modules.Attendance;
using EMG.Api.Modules.Reports;
using EMG.Api.Modules.Search;
using EMG.Api.Modules.Notifications;
using EMG.Api.Modules.Imports;
using EMG.Api.Modules.Intelligence;
using EMG.Api.Modules.AccountingClassification;
using EMG.Api.Modules.ConsolidatedStatements;
using EMG.Api.Modules.ReportBuilder;
using EMG.Api.Modules.UniversalImports;
using EMG.Api.Modules.Contractors;
using EMG.Api.Modules.Transactions;
using EMG.Infrastructure;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using EMG.Api.Modules.Security.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var trustedProxyValues = builder.Configuration.GetSection("ForwardedHeaders:TrustedProxies").Get<string[]>() ?? [];
var trustedProxyAddresses = trustedProxyValues
    .Select(value => IPAddress.TryParse(value, out var address) ? address : null)
    .Where(address => address is not null)
    .Cast<IPAddress>()
    .ToArray();
if (builder.Environment.IsProduction() && trustedProxyAddresses.Length == 0)
    throw new InvalidOperationException("ForwardedHeaders:TrustedProxies must contain the host proxy address in Production.");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var address in trustedProxyAddresses) options.KnownProxies.Add(address);
});

if (builder.Configuration.GetValue<bool>("DataProtection:PersistKeys"))
{
    var keyDirectory = builder.Configuration["DataProtection:KeyDirectory"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
    Directory.CreateDirectory(keyDirectory);
    builder.Services.AddDataProtection()
        .SetApplicationName("EMG.WebApi")
        .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
}

builder.Services.AddProjectsModule(builder.Configuration);
builder.Services.AddSecurityModule();
builder.Services.AddSuppliersModule();
builder.Services.AddAttachmentsModule();
builder.Services.AddSiteMovementsModule();
builder.Services.AddExcavationModule();
builder.Services.AddConcreteModule();
builder.Services.AddSteelModule();
builder.Services.AddFinishingModule();
builder.Services.AddAdvancesModule();
builder.Services.AddFinalAccountsModule();
builder.Services.AddWorkflowModule();
builder.Services.AddAttendanceModule();
builder.Services.AddReportsModule();
builder.Services.AddAccountingClassificationModule();
builder.Services.AddConsolidatedStatementsModule();
builder.Services.AddReportBuilderModule();
builder.Services.AddSearchModule();
builder.Services.AddNotificationsModule();
builder.Services.AddImportsModule();
builder.Services.AddUniversalImportModule();
builder.Services.AddContractorsModule();
builder.Services.AddIntelligenceModule();
builder.Services.AddTransactionCorrections();
builder.Services.AddEmgInfrastructure(builder.Configuration);
builder.Services.AddAuthentication(SessionAuthenticationHandler.Scheme)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.Scheme, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthRateLimiting.PolicyName, AuthRateLimiting.Partition);
});

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        var origins = configuredOrigins.Length > 0
            ? configuredOrigins
            : [
                "http://localhost:3100",
                "http://127.0.0.1:3100",
                "http://localhost:3000",
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:4173",
                "http://127.0.0.1:4173",
                "http://127.0.0.1:53103",
                "http://localhost:53103"
            ];

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var db = migrationScope.ServiceProvider.GetRequiredService<EmgDbContext>();
    await db.Database.MigrateAsync();
}

await app.SeedSecurityAsync();

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
var profileImagesPath = Path.Combine(uploadsPath, "profiles");
Directory.CreateDirectory(profileImagesPath);

app.UseCors("Frontend");
// Only profile images are exposed as static assets. Project attachments remain
// private and are served through the authorized download endpoint.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(profileImagesPath),
    RequestPath = "/profile-images"
});
app.UseRouting();
app.UseRateLimiter();
app.UseRouting();
app.Use(async (context, next) =>
{
    var action = context.GetEndpoint()?.Metadata
        .GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
    if (action is not null && HttpMethods.IsPost(context.Request.Method) &&
        (action.ControllerName == "HistoricalImportPosting" && action.ActionName is "Stage" or "Approve" or "Post" or "Reject" ||
         action.ControllerName == "Imports" && action.ActionName is "CommitNewProject" or "CommitExistingProject" or "CommitApprovedHistory" ||
         action.ControllerName == "Projects" && action.ActionName is "ApproveImport" or "RejectImport"))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { code = "HISTORICAL_MUTATIONS_DISABLED" });
        return;
    }
    await next(context);
});
app.UseAuthentication();
app.UseMiddleware<CsrfProtectionMiddleware>();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "EMG.Api" }));
app.MapControllers();

app.Run();
