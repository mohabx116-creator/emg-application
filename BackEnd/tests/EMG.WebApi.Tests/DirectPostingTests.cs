using EMG.Api.Modules.SiteMovements.Dtos;
using EMG.Api.Modules.SiteMovements.Services;
using EMG.Api.Modules.Suppliers.Dtos;
using EMG.Api.Modules.Suppliers.Services;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class DirectPostingTests
{
    [Fact]
    public async Task NewOperationalRecordsArePostedImmediatelyAndAudited()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var project = new Project { Name = "Direct posting QA", Code = "DIRECT-POST-QA" };
        var supplier = new Supplier { Name = "Direct posting supplier" };
        db.AddRange(project, supplier);
        await db.SaveChangesAsync();

        var movement = await new SiteMovementsService(db).CreateAsync(
            new CreateSiteMovementRequest(project.Id, supplier.Id, "مصروفات", "Direct expense", "مقطوعية", null, null, 125m,
                null, "DP-1", null, "موقع", null, "نقدي", true), null, true, CancellationToken.None);
        var supply = await new SuppliersService(db).AddSupplyAsync(
            supplier.Id, new CreateSupplyRequest(project.Id, "خامات", "أسمنت", "طن", 2m, 300m, null, "DP-2", null), null, true, CancellationToken.None);
        var payment = await new SuppliersService(db).AddPaymentAsync(
            supplier.Id, new CreateSupplierPaymentRequest(project.Id, supply!.Id, 200m, null, "نقدي", "DP-PAY", null), null, true, CancellationToken.None);

        Assert.Equal("مرحّل", movement!.Status);
        Assert.Equal("مرحّل", supply!.Status);
        Assert.Equal("مرحّل", payment!.Status);
        Assert.Contains(await db.WorkflowActions.ToListAsync(), action => action.EntityType == "site-movement" && action.EntityId == movement.Id && action.Action == "ترحيل مباشر");
        Assert.Contains(await db.WorkflowActions.ToListAsync(), action => action.EntityType == "supplier-supply" && action.EntityId == supply.Id && action.Action == "ترحيل مباشر");
        Assert.Contains(await db.WorkflowActions.ToListAsync(), action => action.EntityType == "supplier-payment" && action.EntityId == payment.Id && action.Action == "ترحيل مباشر");
    }
}
