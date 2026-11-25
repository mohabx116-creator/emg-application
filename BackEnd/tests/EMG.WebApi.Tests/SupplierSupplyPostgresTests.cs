using System.Data.Common;
using EMG.Api.Modules.Notifications.Services;
using EMG.Api.Modules.Projects.Repositories;
using EMG.Api.Modules.Workflow.Dtos;
using EMG.Api.Modules.Workflow.Services;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace EMG.WebApi.Tests;

public sealed partial class WorkflowAuthorizationTests
{
    private const string SupplierQaPostgres = "EMG_P0_SUPPLIER_TEST_POSTGRES";

    [SupplierPostgresFact]
    public async Task SupplierPostgresConcurrentTechnicalAndFinancialStagesEachCommitExactlyOnce()
    {
        var settings = await VerifySupplierPostgresAsync();
        await using var db = SupplierPostgresContext(settings);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "concurrent supply review", Code = "CONCURRENT-" + Guid.NewGuid().ToString("N") };
        var admin = new Role { Name = "admin" };
        var actors = Enumerable.Range(1, 4).Select(index => new AppUser
        {
            UserName = "supply-qa-" + index, DisplayName = "Synthetic reviewer", Email = $"reviewer{index}@example.test",
            UserRoles = [new UserRole { Role = admin }]
        }).ToArray();
        db.Add(project);
        db.Users.AddRange(actors);
        await db.SaveChangesAsync();
        var supply = await AddSupplierSupplyAsync(db, project.Id);

        async Task CompeteAsync(string action, int firstActor, int secondActor)
        {
            var startTogether = new TransitionBarrier();
            await using var first = SupplierPostgresContext(settings, startTogether);
            await using var second = SupplierPostgresContext(settings, startTogether);
            var results = await Task.WhenAll(
                ExecuteAsync(first, firstActor), ExecuteAsync(second, secondActor));
            Assert.Equal(2, startTogether.Arrivals);
            Assert.Single(results, result => result is not null);
            Assert.Single(results, result => result is null);

            async Task<WorkflowResult?> ExecuteAsync(EmgDbContext db, int actorId)
            {
                try
                {
                    return await new WorkflowService(db, new NotificationService(db))
                        .ExecuteAsync(SupplyType, supply.Id, action, null, actorId, true, Cancellation);
                }
                catch (WorkflowConcurrencyException)
                {
                    return null;
                }
            }
        }
        await CompeteAsync(TechnicalAction, actors[0].Id, actors[1].Id);
        Assert.Equal(TechnicalStatus, await db.SupplierSupplies.Where(row => row.Id == supply.Id).Select(row => row.Status).SingleAsync());
        await AssertOutstandingAsync(db, project.Id, supply.SupplierId, 0m);
        await using (var failed = SupplierPostgresContext(settings, new FailFinancialHistoryWrite()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new WorkflowService(failed, new NotificationService(failed))
                .ExecuteAsync(SupplyType, supply.Id, FinancialAction, null, actors[2].Id, true, Cancellation));
        }
        Assert.Equal(TechnicalStatus, await db.SupplierSupplies.Where(row => row.Id == supply.Id).Select(row => row.Status).SingleAsync());
        Assert.Equal(1, await db.WorkflowActions.CountAsync(row => row.EntityType == SupplyType && row.EntityId == supply.Id));
        await AssertOutstandingAsync(db, project.Id, supply.SupplierId, 0m);
        await CompeteAsync(FinancialAction, actors[2].Id, actors[3].Id);
        Assert.Equal(2, await db.WorkflowActions.CountAsync(row => row.EntityType == SupplyType && row.EntityId == supply.Id));
        Assert.Equal(8, await db.Notifications.CountAsync(row => row.EntityType == SupplyType && row.EntityId == supply.Id));
        Assert.Equal("معتمد", await db.SupplierSupplies.Where(row => row.Id == supply.Id).Select(row => row.Status).SingleAsync());
        await AssertOutstandingAsync(db, project.Id, supply.SupplierId, 100m);
    }

    [SupplierPostgresFact]
    public async Task SupplierPostgresImportLocksProtectRecognizedSupplyAndPendingBatch()
    {
        var settings = await VerifySupplierPostgresAsync();
        await using var db = SupplierPostgresContext(settings);
        await db.Database.EnsureCreatedAsync();
        await AssertReimportBlockedAsync(db, "معتمد", true);
        var project = new Project { Name = "QA pending batch", Code = "PENDING-QA", Status = "تحت المراجعة" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        db.Attachments.Add(new Attachment { EntityType = "project-import", EntityId = project.Id, OriginalFileName = "synthetic.xlsx" });
        await db.SaveChangesAsync();
        var supply = await AddSupplierSupplyAsync(db, project.Id, "مسودة", notes: "استيراد: QA");
        var denied = await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProjectsRepository(db).DecideImportAsync(project.Id, true, null, 1, Cancellation));
        Assert.Contains("القبول الفني والاعتماد المالي", denied.Message);
        Assert.Equal("مسودة", await db.SupplierSupplies.Where(row => row.Id == supply.Id).Select(row => row.Status).SingleAsync());
    }

    private static async Task<NpgsqlConnectionStringBuilder> VerifySupplierPostgresAsync()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(SupplierQaPostgres));
        Assert.Equal("127.0.0.1", settings.Host);
        Assert.Equal(5433, settings.Port);
        Assert.Matches("^emg_supplier_qa_[a-f0-9]{8}$", settings.Database!);
        Assert.Equal("emg_sec_p0_qa", settings.Username);
        Assert.False(settings.Pooling);
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT current_database(), current_setting('cluster_name')", connection);
        await using var identity = await command.ExecuteReaderAsync();
        Assert.True(await identity.ReadAsync());
        Assert.Equal(settings.Database, identity.GetString(0));
        Assert.Equal("emg-sec-p0-s2-" + settings.Database![^8..], identity.GetString(1));
        return settings;
    }

    private static EmgDbContext SupplierPostgresContext(NpgsqlConnectionStringBuilder settings, params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(settings.ConnectionString).AddInterceptors(interceptors).Options);

    public sealed class SupplierPostgresFactAttribute : FactAttribute
    {
        public SupplierPostgresFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SupplierQaPostgres)))
                Skip = "Requires the verified disposable supplier PostgreSQL fixture.";
        }
    }

    private sealed class TransitionBarrier : DbTransactionInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public int Arrivals => Volatile.Read(ref arrivals);

        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            // Only the competing workflow transactions participate. Notification saves happen after release.
            if (ready.Task.IsCompleted) return result;
            if (Interlocked.Increment(ref arrivals) == 2) ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }

    private sealed class FailFinancialHistoryWrite : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<WorkflowAction>().Any(entry => entry.State == EntityState.Added && entry.Entity.Action == FinancialAction))
                throw new InvalidOperationException("Injected QA failure while saving financial approval history.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
