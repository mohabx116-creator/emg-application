using EMG.Api.Modules.Advances.Controllers;
using EMG.Api.Modules.Advances.Dtos;
using EMG.Api.Modules.Advances.Services;
using EMG.Api.Modules.Security.Authorization;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class AdvanceAllocationTargetsTests
{
    [Fact]
    public async Task TargetSelectionPreservesCurrencyProjectAndInvoiceCapacityAcrossReceipts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Assigned", Code = "P1" };
        var other = new Project { Name = "Other", Code = "P2" };
        var actor = new AppUser { UserName = "engineer", Email = "engineer@test.invalid", DisplayName = "Assigned engineer" };
        var supplier = new Supplier { Name = "Supplier" };
        AdvancePayment Receipt(Project p, string currency = "EGP") => new() { Project = p, Currency = currency, Amount = 100, Status = "مرحّل" };
        SiteMovement Movement(Project p, string currency, string kind) => new() { Project = p, Currency = currency, MovementType = kind, TotalAmount = 100, Status = "مرحّل" };
        var first = Receipt(project);
        var second = Receipt(project);
        var usd = Receipt(project, "USD");
        var hidden = Receipt(other);
        var site = Movement(project, "EGP", "مصروفات");
        var usdSite = Movement(project, "USD", "مصروفات");
        var incoming = Movement(project, "EGP", "وارد");
        var otherSite = Movement(other, "EGP", "مصروفات");
        var invoice = new SupplierInvoice { Project = project, Supplier = supplier, SiteMovement = site, InvoiceNumber = "INV-1" };
        var office = new SupplierPayment { Invoice = invoice, Project = project, Supplier = supplier, PaymentSource = "مكتب", Amount = 70, Status = "مرحّل" };
        var owner = new SupplierPayment { Invoice = invoice, Project = project, Supplier = supplier, PaymentSource = "مالك مباشر", Amount = 30, Status = "مرحّل" };
        db.AddRange(first, second, usd, hidden, site, usdSite, incoming, otherSite, office, owner);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { ProjectId = project.Id, User = actor, IsActive = true });
        await db.SaveChangesAsync();
        var service = new AdvancesService(db);
        Task<AllocationSummary?> Allocate(AdvancePayment receipt, int? movement, int? payment, decimal amount) =>
            service.AllocateAsync(receipt.Id, new(movement, payment, amount, null, null, "الموقع"), actor.Id, false, default);
        var rule = Assert.Single(typeof(AdvancesController).GetMethod(nameof(AdvancesController.Targets))!
            .GetCustomAttributes(typeof(PermissionAuthorizeAttribute), true).Cast<PermissionAuthorizeAttribute>());
        Assert.Equal("Permission:advances.allocate", rule.Policy);
        Assert.Null(await service.GetAllocationTargetsAsync(hidden.Id, actor.Id, false, default));
        var egpTargets = Assert.IsAssignableFrom<IReadOnlyList<AllocationTarget>>(await service.GetAllocationTargetsAsync(first.Id, actor.Id, false, default));
        Assert.Equal(2, egpTargets.Count);
        Assert.All(egpTargets, t => { Assert.Equal("EGP", t.Currency); Assert.Equal(70m, t.Remaining); });
        Assert.Contains(egpTargets, t => t.Kind == "movement" && t.Id == site.Id);
        Assert.Contains(egpTargets, t => t.Kind == "supplier" && t.Id == office.Id);

        Assert.Null(await Allocate(usd, site.Id, null, 1));
        Assert.Null(await Allocate(usd, null, office.Id, 1));
        Assert.Null(await Allocate(first, usdSite.Id, null, 1));
        Assert.Null(await Allocate(first, incoming.Id, null, 1));
        Assert.Null(await Allocate(first, otherSite.Id, null, 1));
        Assert.Null(await Allocate(first, null, owner.Id, 1));
        Assert.NotNull(await Allocate(first, site.Id, null, 40));
        var remaining = await service.GetAllocationTargetsAsync(second.Id, actor.Id, false, default);
        Assert.All(remaining!, t => Assert.Equal(30m, t.Remaining));
        Assert.Null(await Allocate(second, null, office.Id, 31));
        Assert.NotNull(await Allocate(second, null, office.Id, 30));
        Assert.Empty((await service.GetAllocationTargetsAsync(first.Id, actor.Id, false, default))!);
        Assert.Null(await Allocate(second, site.Id, null, 1));
        Assert.NotNull(await Allocate(usd, usdSite.Id, null, 20));
        Assert.Equal(3, await db.AdvanceAllocations.CountAsync());
    }
}