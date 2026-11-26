using EMG.Api.Modules.Suppliers.Dtos;
using EMG.Api.Modules.Suppliers.Services;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;

namespace EMG.WebApi.Tests;

public sealed class SupplierInvoiceTests
{
    private static IWebHostEnvironment TestEnvironment() => new TestWebHostEnvironment(AppContext.BaseDirectory);

    [Theory]
    [InlineData(39223, 39223, 0)]
    [InlineData(39223, 0, 39223)]
    [InlineData(39223, 20000, 19223)]
    public async Task InvoiceTotalsPreserveCashCreditAndPartialPayment(decimal supplied, decimal paid, decimal outstanding)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Supplier invoice QA", Code = $"INV-QA-{paid}" };
        var supplier = new Supplier { Name = $"Invoice supplier {paid}" };
        db.AddRange(project, supplier);
        await db.SaveChangesAsync();

        var result = await new SupplierInvoiceService(db, TestEnvironment()).CreateAsync(new CreateSupplierInvoiceRequest(
            supplier.Id, project.Id, null, null, [new SupplierInvoiceLineRequest("خامات", "مقطوعية", 1m, supplied, "خامات")],
            paid, paid == supplied ? "نقدي" : paid == 0 ? "آجل" : "جزئي", $"INV-QA-{paid}", null), 7, true, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(supplied, result!.TotalAmount);
        Assert.Equal(paid, result.PaidAmount);
        Assert.Equal(outstanding, result.OutstandingAmount);
        Assert.StartsWith($"INV-{project.Id}-{supplier.Id}-", result.InvoiceNumber);
        Assert.Single(result.Lines);
        Assert.Equal(paid > 0, await db.SupplierPayments.AnyAsync(item => item.InvoiceId == result.Id && item.Amount == paid));
        Assert.Equal(1, await db.SupplierSupplies.CountAsync(item => item.InvoiceId == result.Id));
        Assert.Equal(supplied, await db.SiteMovements.Where(item => item.Id == result.SiteMovementId).Select(item => item.TotalAmount).SingleAsync());
    }

    [Fact]
    public async Task SiteMovementCanHaveOnlyOneCanonicalSupplierInvoice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Linked invoice QA", Code = "LINKED-INVOICE-QA" };
        var supplier = new Supplier { Name = "Linked invoice supplier" };
        db.AddRange(project, supplier);
        await db.SaveChangesAsync();
        var movement = new SiteMovement { ProjectId = project.Id, Description = "شراء الموقع", MovementType = "مصروفات", TotalAmount = 39223m, Status = "مرحّل" };
        db.SiteMovements.Add(movement);
        await db.SaveChangesAsync();
        var service = new SupplierInvoiceService(db, TestEnvironment());
        var request = new CreateSupplierInvoiceRequest(supplier.Id, project.Id, movement.Id, null,
            [new SupplierInvoiceLineRequest("خامات", "مقطوعية", 1m, 39223m, null)], 0, "آجل", null, null);
        var first = await service.CreateAsync(request, 7, true, CancellationToken.None);
        Assert.NotNull(first);
        await Assert.ThrowsAsync<DuplicateSupplierTransactionException>(() => service.CreateAsync(request, 7, true, CancellationToken.None));
        Assert.Equal(1, await db.SupplierInvoices.CountAsync(item => item.SiteMovementId == movement.Id));
    }

    [Fact]
    public async Task SubsequentPaymentByInvoiceLineUpdatesInvoiceOutstanding()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Subsequent payment QA", Code = "INVOICE-PAYMENT-QA" };
        var supplier = new Supplier { Name = "Subsequent payment supplier" };
        db.AddRange(project, supplier);
        await db.SaveChangesAsync();
        var invoice = await new SupplierInvoiceService(db, TestEnvironment()).CreateAsync(new CreateSupplierInvoiceRequest(supplier.Id, project.Id, null, null,
            [new SupplierInvoiceLineRequest("خامات", "مقطوعية", 1m, 39223m, null)], 0, "آجل", null, null), 7, true, CancellationToken.None);
        var line = Assert.Single(invoice!.Lines);
        await new SuppliersService(db).AddPaymentAsync(supplier.Id,
            new CreateSupplierPaymentRequest(project.Id, line.SupplyId, 20000m, null, "جزئي", "FOLLOW-UP-1", null), 7, true, CancellationToken.None);

        var refreshed = await new SupplierInvoiceService(db, TestEnvironment()).GetAsync(invoice.Id, 7, true, CancellationToken.None);
        Assert.NotNull(refreshed);
        Assert.Equal(20000m, refreshed!.PaidAmount);
        Assert.Equal(19223m, refreshed.OutstandingAmount);
        Assert.Equal(invoice.Id, await db.SupplierPayments.Where(item => item.Reference == "FOLLOW-UP-1").Select(item => item.InvoiceId).SingleAsync());
    }

    [Fact]
    public async Task CreditInvoiceCanBeCorrectedAndDeletedAsOneAggregate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using var db = new EmgDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { Name = "Invoice lifecycle QA", Code = "INVOICE-LIFECYCLE-QA" };
        var supplier = new Supplier { Name = "Lifecycle supplier" };
        db.AddRange(project, supplier);
        await db.SaveChangesAsync();
        var service = new SupplierInvoiceService(db, TestEnvironment());
        var invoice = await service.CreateAsync(new CreateSupplierInvoiceRequest(supplier.Id, project.Id, null, null,
            [new SupplierInvoiceLineRequest("بند أول", "طن", 1m, 1000m, null)], 0, "آجل", null, null), 7, true, CancellationToken.None);
        var updated = await service.UpdateAsync(invoice!.Id, new UpdateSupplierInvoiceRequest(
            [new SupplierInvoiceLineRequest("بند مصحح", "طن", 2m, 500m, null)], null, "تصحيح"), 7, true, CancellationToken.None);
        Assert.Equal(1000m, updated!.TotalAmount);
        Assert.Equal("بند مصحح", updated.Lines.Single().Description);
        Assert.True(await service.DeleteAsync(invoice.Id, 7, true, CancellationToken.None));
        Assert.False(await db.SupplierInvoices.AnyAsync());
        Assert.False(await db.SupplierSupplies.AnyAsync());
        Assert.False(await db.SupplierInvoiceLines.AnyAsync());
    }
}

file sealed class TestWebHostEnvironment(string root) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "EMG.WebApi.Tests";
    public string EnvironmentName { get; set; } = "QA";
    public string WebRootPath { get; set; } = root;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = root;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
