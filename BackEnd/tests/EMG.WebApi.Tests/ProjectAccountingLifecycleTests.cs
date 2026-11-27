using System.IO.Compression;
using System.Xml.Linq;
using EMG.Api.Modules.AccountingClassification.Services;
using EMG.Api.Modules.Advances.Services;
using EMG.Api.Modules.ConsolidatedStatements.Services;
using EMG.Api.Modules.Contractors.Dtos;
using EMG.Api.Modules.Contractors.Services;
using EMG.Api.Modules.FinalAccounts.Services;
using EMG.Api.Modules.Projects.Repositories;
using EMG.Api.Modules.Projects.Services;
using EMG.Api.Modules.Reports.Services;
using EMG.Api.Modules.Suppliers.Dtos;
using EMG.Api.Modules.Suppliers.Services;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;

namespace EMG.WebApi.Tests;

public sealed class ProjectAccountingLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OperationalCorrectionsAndPaymentsReconcileAtEachStep(bool historical)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var user = new AppUser { UserName = "accounting-test", Email = "accounting@example.test", DisplayName = "Test account" };
        var project = new Project { Name = "Synthetic contract", Code = "QA", OfficeSharePercentage = 10 };
        var supplier = new Supplier { Name = "Synthetic supplier" };
        var contractor = new Contractor { Name = "Synthetic contractor" };
        db.AddRange(user, project, supplier, contractor);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = user.Id, ProjectId = project.Id, IsActive = true });
        if (historical)
        {
            var batch = new HistoricalImportBatch { Project = project, SourceFileName = "fixture.xlsx",
                SourceSha256 = new string('a', 64), MappingVersion = "universal-v1.1", Status = "POSTED", CreatedByUserId = user.Id };
            db.Add(new HistoricalWorkLine { Batch = batch, Cell = "J10", Sheet = "Site", Family = "site",
                Category = "الموقع", Currency = "EGP", Amount = 25m, Description = "Source work" });
            db.Add(new HistoricalFundingLine { Batch = batch, Sheet = "Funding", Cell = "H23", Stream = "source_control",
                Basis = "verified_zero", Currency = "EGP", Amount = 0m });
        }
        await db.SaveChangesAsync();

        var accounts = new FinalAccountsService(db);
        var projects = new ProjectsService(new EfProjectsRepository(db), accounts);
        var advances = new AdvancesService(db);
        var supplierAccounts = new SuppliersService(db);
        var invoices = new SupplierInvoiceService(db, new InvoiceTestEnvironment());
        var consolidated = new ConsolidatedStatementService(db, new AccountingClassificationService(db));
        var reports = new ReportsService(db, accounts);
        var contracts = new ContractorStatementService(db);
        var day = new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc);
        async Task State(decimal newWorks, decimal funding, decimal allocation, decimal supplies, decimal supplierPaid)
        {
            var works = newWorks + (historical ? 25 : 0);
            var balance = funding - works;
            var fee = works / 10;
            var account = (await accounts.GetAsync(project.Id, user.Id, false, default))!;
            Assert.Equal((works, funding, balance), (account.TotalWorks, account.TotalPayments, account.CashBalance));
            Assert.Equal(fee, account.OfficeNetDue);
            Assert.Equal(supplies - supplierPaid, account.SupplierOutstanding);
            var section = account.Sections.Single(s => s.Category == "الموقع" && s.Currency == "EGP");
            Assert.Equal((works, allocation, allocation - works), (section.Works, section.AllocatedFunding, section.FundingLessWorks));
            var card = (await projects.GetProjectAsync(project.Id, user.Id, false, default))!;
            Assert.Equal((works, funding, balance), (card.TotalWorks, card.TotalPayments, card.CashBalance));
            var ledger = (await advances.GetLedgerAsync(project.Id, user.Id, false, default))!;
            var egp = ledger.Totals.Single(s => s.Currency == "EGP");
            Assert.Equal((works, funding, allocation, balance), (egp.Works, egp.Payments, egp.Allocated, egp.CashBalance));
            var party = (await supplierAccounts.GetSuppliersAsync(user.Id, false, default)).Single();
            Assert.Equal((supplies, supplierPaid, supplies - supplierPaid), (party.TotalSupplies, party.TotalPaid, party.Remaining));
            var statement = (await consolidated.PreviewAsync(user.Id, false, default)).Projects.Single();
            Assert.Equal((works, funding, balance), (statement.TotalWorks, statement.TotalPayments, statement.CashBalance));
            var bytes = await reports.ExportProjectWorkbookAsync(project.Id, user.Id, false, default);
            using var zip = new ZipArchive(new MemoryStream(bytes!));
            XNamespace xml = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            Dictionary<string, decimal> Sheet(int number)
            {
                using var entry = zip.GetEntry($"xl/worksheets/sheet{number}.xml")!.Open();
                return XDocument.Load(entry).Descendants(xml + "c").Where(c => c.Attribute("t") == null && c.Element(xml + "v") != null)
                    .ToDictionary(c => (string)c.Attribute("r")!, c => (decimal)c.Element(xml + "v")!);
            }
            var final = Sheet(6);
            Assert.Equal((works, funding, balance, fee), (final["K9"], final["K10"], final["K15"], final["I13"]));
            var site = Sheet(4);
            Assert.Equal(works, Enumerable.Range(10, 42).Sum(r => site.GetValueOrDefault("J" + r)));
            var schedules = Sheet(5);
            Assert.Equal(funding, Enumerable.Range(4, 14).Sum(r => schedules.GetValueOrDefault("N" + r)));
            Assert.Equal(allocation, Enumerable.Range(4, 14).Sum(r => schedules.GetValueOrDefault("B" + r)));
        }

        await State(0, 0, 0, 0, 0);
        var receipt = await advances.CreateAsync(new(project.Id, "Client", "Payer", 500, day, null, "receipt", null), user.Id, false, default);
        Assert.NotNull(receipt);
        await State(0, 500, 0, 0, 0);
        Assert.NotNull(await advances.AllocateAsync(receipt.Id, new(null, null, 200, day, null, "الموقع"), user.Id, false, default));
        await State(0, 500, 200, 0, 0);

        SupplierInvoiceLineRequest Line(decimal price) => new("Materials", "m", 1m, price, null);
        CreateSupplierInvoiceRequest Invoice(decimal price, decimal pay) =>
            new(supplier.Id, project.Id, null, day, [Line(price)], pay, null, null, null);
        var unpaid = (await invoices.CreateAsync(Invoice(50, 0), user.Id, false, default))!;
        await State(50, 500, 200, 50, 0);
        Assert.True(await invoices.DeleteAsync(unpaid.Id, user.Id, false, default));
        await State(0, 500, 200, 0, 0);

        var invoice = (await invoices.CreateAsync(Invoice(100, 30), user.Id, false, default))!;
        await State(100, 500, 200, 100, 30);
        var corrected = await invoices.UpdateAsync(invoice.Id, new([Line(140)], day.AddDays(1), "Correction"), user.Id, false, default);
        Assert.Equal(140m, corrected!.TotalAmount);
        Assert.Equal(day.AddDays(1), await db.SiteMovements.Where(m => m.Id == invoice.SiteMovementId).Select(m => m.MovementDate).SingleAsync());
        await State(140, 500, 200, 140, 30);
        await Assert.ThrowsAsync<InvalidOperationException>(() => invoices.UpdateAsync(invoice.Id, new([Line(20)], null, null), user.Id, false, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => invoices.DeleteAsync(invoice.Id, user.Id, false, default));
        var settlement = new CreateSupplierPaymentRequest(project.Id, invoice.Lines.Single().SupplyId, 110, day, null, "settlement", null, "مالك مباشر");
        await Assert.ThrowsAsync<DuplicateSupplierTransactionException>(() => supplierAccounts.AddPaymentAsync(supplier.Id, settlement with { Amount = 111 }, user.Id, false, default));
        Assert.NotNull(await supplierAccounts.AddPaymentAsync(supplier.Id, settlement, user.Id, false, default));
        Assert.True(await db.SiteMovements.Where(m => m.Id == invoice.SiteMovementId).Select(m => m.IsPaid).SingleAsync());
        await State(140, 610, 200, 140, 140);

        var cash = (await invoices.CreateAsync(Invoice(15, 15), user.Id, false, default))!;
        Assert.Equal(0, cash.OutstandingAmount);
        await State(155, 610, 200, 155, 155);
        var contract = new ContractorContract { ProjectId = project.Id, ContractorId = contractor.Id, ContractNumber = "C", Currency = "EGP" };
        db.Add(contract);
        await db.SaveChangesAsync();
        var part = new ContractorContractItem { ContractId = contract.Id, Description = "Work", Unit = "m", ApprovedQuantity = 1, UnitRate = 100 };
        var work = new SiteMovement { ProjectId = project.Id, Description = "Contracted work", MovementType = "أعمال",
            Unit = "m", Quantity = 1, UnitPrice = 100, TotalAmount = 100, Status = "مرحّل" };
        db.AddRange(part, work);
        await db.SaveChangesAsync();
        var statement = await contracts.CreateDraftAsync(new(project.Id, contractor.Id, contract.Id, day, day, "EGP", null,
            [new(part.Id, "site-movement", work.Id, 1, 100, "work")]), user.Id, false, default);
        Assert.NotNull(statement);
        await State(255, 610, 200, 155, 155);
        var installment = new CreateContractorPaymentRequest(project.Id, statement.Id, 30, "EGP", day, null, "first", null, "مكتب");
        Assert.NotNull(await contracts.AddPaymentAsync(contractor.Id, installment, user.Id, false, default));
        await State(255, 610, 200, 155, 155);
        Assert.NotNull(await contracts.AddPaymentAsync(contractor.Id, installment with { Amount = 70, Reference = "last", PaymentSource = "مالك مباشر" }, user.Id, false, default));
        await State(255, 680, 200, 155, 155);
        Assert.Equal(0, await db.ContractorStatements.Select(s => s.NetAmount).SingleAsync());
    }

    private sealed class InvoiceTestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "EMG.WebApi.Tests";
        public string EnvironmentName { get; set; } = "Test";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
