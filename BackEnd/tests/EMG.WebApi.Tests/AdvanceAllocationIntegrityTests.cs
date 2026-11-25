using EMG.Api.Modules.Advances.Dtos;
using EMG.Api.Modules.Advances.Services;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class AdvanceAllocationIntegrityTests
{
    [Fact]
    public async Task Allocation_AllowsPartialAndExactBalance_ButRejectsExcess()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "test", Code = "TEST-ADV" };
        var advance = new AdvancePayment { Project = project, ClientName = "c", PayerName = "p", Amount = 100m, Status = "معتمد" };
        db.Add(advance);
        await db.SaveChangesAsync();

        var service = new AdvancesService(db);
        var partial = await service.AllocateAsync(advance.Id, Request(40m), null, true, CancellationToken.None);
        var exact = await service.AllocateAsync(advance.Id, Request(60m), null, true, CancellationToken.None);
        var excess = await service.AllocateAsync(advance.Id, Request(1m), null, true, CancellationToken.None);

        Assert.NotNull(partial);
        Assert.NotNull(exact);
        Assert.Null(excess);
        Assert.Equal(2, await db.AdvanceAllocations.CountAsync());
        Assert.Equal(100m, await db.AdvanceAllocations.SumAsync(item => item.Amount));
    }

    [Fact]
    public async Task ConcurrentAllocations_AgainstOneAdvance_OnlyOneCanSpendTheRemainingBalance()
    {
        var connectionString = Environment.GetEnvironmentVariable("EMG_P0_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var setup = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(connectionString).Options);
        await setup.Database.EnsureDeletedAsync();
        await setup.Database.EnsureCreatedAsync();
        setup.Add(new AdvancePayment { Project = new Project { Name = "test", Code = "TEST-CONCURRENT" }, ClientName = "c", PayerName = "p", Amount = 100m, Status = "معتمد" });
        await setup.SaveChangesAsync();
        var advanceId = await setup.AdvancePayments.Select(item => item.Id).SingleAsync();

        await using var first = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(connectionString).Options);
        await using var second = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(connectionString).Options);
        var results = await Task.WhenAll(
            new AdvancesService(first).AllocateAsync(advanceId, Request(60m), null, true, CancellationToken.None),
            new AdvancesService(second).AllocateAsync(advanceId, Request(60m), null, true, CancellationToken.None));

        Assert.Single(results, item => item is not null);
        await setup.Entry(await setup.AdvancePayments.SingleAsync(item => item.Id == advanceId)).Collection(item => item.Allocations).LoadAsync();
        Assert.Equal(60m, setup.AdvancePayments.Single(item => item.Id == advanceId).Allocations.Sum(item => item.Amount));
    }

    private static AllocateAdvanceRequest Request(decimal amount) => new(null, null, amount, null, "test", "المكتب");

    private static EmgDbContext CreateDb(SqliteConnection connection) => new(new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options);
}
