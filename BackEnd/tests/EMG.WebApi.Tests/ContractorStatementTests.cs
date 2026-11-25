using EMG.Api.Modules.Contractors.Dtos;
using EMG.Api.Modules.Contractors.Services;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class ContractorStatementTests
{
    [Fact]
    public async Task DraftUsesOnlyExplicitApprovedMeasurementAndDoesNotPostFinancially()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request(1m, 100m, 1);
        var draft = await new ContractorStatementService(fixture.Db).CreateDraftAsync(request, fixture.UserId, false, CancellationToken.None);

        Assert.NotNull(draft);
        Assert.Equal("مسودة", draft!.Status);
        Assert.Equal(100m, draft.GrossAmount);
        Assert.Equal(100m, draft.NetAmount);
        Assert.Single(draft.Lines);
        Assert.Equal(0, await fixture.Db.SupplierPayments.CountAsync());
        Assert.Equal(100m, await fixture.Db.SiteMovements.Where(row => row.Status == "معتمد" || row.Status == "مرحّل").SumAsync(row => row.TotalAmount));
        Assert.Contains(await fixture.Db.WorkflowActions.ToListAsync(), row => row.EntityType == "contractor-statement" && row.EntityId == draft.Id && row.ToStatus == "مسودة");
    }

    [Fact]
    public async Task CertifiedSourceCannotBeUsedInAnotherDraft()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new ContractorStatementService(fixture.Db);
        await service.CreateDraftAsync(fixture.Request(1m, 100m, 1), fixture.UserId, false, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDraftAsync(fixture.Request(1m, 100m, 1), fixture.UserId, false, CancellationToken.None));
    }

    [Fact]
    public async Task DraftRequiresProjectScope()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await new ContractorStatementService(fixture.Db).CreateDraftAsync(fixture.Request(1m, 100m, 1), null, false, CancellationToken.None);
        Assert.Null(result);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private SqliteConnection connection { get; init; } = null!;
        public EmgDbContext Db { get; private init; } = null!;
        public int UserId { get; private init; }
        public int ProjectId { get; private init; }
        public int ContractId { get; private init; }
        public int ItemId { get; private init; }
        public int MovementId { get; private init; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
            var db = new EmgDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var user = new EMG.Domain.Security.AppUser { UserName = "contractor-qa", Email = "contractor-qa@example.test", DisplayName = "Contractor QA", PasswordHash = "fixture" };
            var project = new Project { Name = "Contractor QA", Code = "CONTRACTOR-QA" };
            var contractor = new Contractor { Name = "Synthetic Contractor" };
            db.AddRange(user, project, contractor);
            await db.SaveChangesAsync();
            db.UserProjectAssignments.Add(new EMG.Domain.Security.UserProjectAssignment { UserId = user.Id, ProjectId = project.Id, IsActive = true });
            var contract = new ContractorContract { ProjectId = project.Id, ContractorId = contractor.Id, ContractNumber = "C-QA-001", Currency = "EGP", CreatedByUserId = user.Id };
            db.ContractorContracts.Add(contract);
            await db.SaveChangesAsync();
            var item = new ContractorContractItem { ContractId = contract.Id, Description = "Synthetic measured work", Unit = "م2", ApprovedQuantity = 10m, UnitRate = 100m };
            var movement = new SiteMovement { ProjectId = project.Id, Description = "Synthetic approved work", MovementType = "أعمال", Unit = "م2", Quantity = 1m, UnitPrice = 100m, TotalAmount = 100m, Status = "معتمد" };
            db.AddRange(item, movement);
            await db.SaveChangesAsync();
            return new Fixture { connection = connection, Db = db, UserId = user.Id, ProjectId = project.Id, ContractId = contract.Id, ItemId = item.Id, MovementId = movement.Id };
        }

        public CreateStatementRequest Request(decimal quantity, decimal rate, int sourceId) => new(ProjectId, Db.ContractorContracts.Select(row => row.ContractorId).Single(), ContractId, DateTime.UtcNow.Date, DateTime.UtcNow.Date, "EGP", "synthetic QA", [new StatementLineRequest(ItemId, "site-movement", sourceId == 1 ? MovementId : sourceId, quantity, rate, "QA:site-movement")]);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
