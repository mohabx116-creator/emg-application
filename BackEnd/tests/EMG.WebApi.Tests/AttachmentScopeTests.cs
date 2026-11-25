using System.Security.Claims;
using EMG.Api.Modules.Attachments.Controllers;
using EMG.Api.Modules.Attachments.Dtos;
using EMG.Api.Modules.Attachments.Services;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace EMG.WebApi.Tests;

public sealed class AttachmentScopeTests
{
    private static readonly CancellationToken Cancellation = CancellationToken.None;

    [Theory]
    [InlineData("project_manager", "assigned", true, true)]
    [InlineData("project_manager", "all", false, false)]
    [InlineData("accountant", "assigned", true, true)]
    [InlineData("accountant", "assigned", false, false)]
    [InlineData("accountant", "project", true, true)]
    [InlineData("accountant", "all", false, true)]
    [InlineData("admin", null, false, true)]
    public async Task ArchiveListAndDirectDownloadRespectTheOwningProject(
        string role, string? scope, bool assigned, bool allowed)
    {
        await using var fixture = new Fixture();
        var project = await fixture.ProjectAsync("ARCHIVE");
        var otherProject = await fixture.ProjectAsync("OTHER");
        var archive = await fixture.StoreAsync("project-import", project);
        var controller = await fixture.ControllerAsync(role, scope, assigned ? project : otherProject);

        var list = await ListAsync(controller, "project-import", project);
        var download = await controller.Download(archive.Id, Cancellation);

        if (allowed)
        {
            Assert.Equal(archive.Id, Assert.Single(list).Id);
            var file = Assert.IsType<PhysicalFileResult>(download);
            Assert.Equal(archive.OriginalFileName, file.FileDownloadName);
            Assert.Equal(Fixture.Contents, await File.ReadAllBytesAsync(file.FileName, Cancellation));
        }
        else
        {
            Assert.Empty(list);
            Assert.IsType<NotFoundResult>(download);
        }
    }

    [Theory]
    [InlineData("admin", null)]
    [InlineData("accountant", "all")]
    [InlineData("project_manager", "assigned")]
    public async Task MissingProjectOrUnknownEntityDoesNotDiscloseAnAttachment(string role, string? scope)
    {
        await using var fixture = new Fixture();
        var project = await fixture.ProjectAsync("KNOWN");
        var orphan = await fixture.StoreAsync("project-import", project + 10);
        var unknown = await fixture.StoreAsync("unmapped", project);
        var controller = await fixture.ControllerAsync(role, scope, project);

        foreach (var attachment in new[] { orphan, unknown })
        {
            Assert.Empty(await ListAsync(controller, attachment.EntityType, attachment.EntityId));
            Assert.IsType<NotFoundResult>(await controller.Download(attachment.Id, Cancellation));
        }
    }

    [Fact]
    public async Task OwnScopeFiltersListAndDownloadToTheAssignedUsersFiles()
    {
        await using var fixture = new Fixture();
        var project = await fixture.ProjectAsync("OWN");
        var controller = await fixture.ControllerAsync("accountant", "own", project);
        var own = await fixture.StoreAsync("project-import", project, fixture.ActorId);
        var other = await fixture.StoreAsync("project-import", project);

        Assert.Equal(own.Id, Assert.Single(await ListAsync(controller, "project-import", project)).Id);
        Assert.IsType<PhysicalFileResult>(await controller.Download(own.Id, Cancellation));
        Assert.IsType<NotFoundResult>(await controller.Download(other.Id, Cancellation));
    }

    [Fact]
    public async Task PaymentAttachmentCanUseItsSupplyProjectButAnUnlinkedPaymentHasNoScope()
    {
        await using var fixture = new Fixture();
        var project = await fixture.ProjectAsync("SUPPLY");
        var supplier = new Supplier { Name = "QA supplier" };
        var supply = new SupplierSupply { Supplier = supplier, ProjectId = project };
        var linked = new SupplierPayment { Supplier = supplier, Supply = supply };
        var unlinked = new SupplierPayment { Supplier = supplier };
        fixture.Db.SupplierPayments.AddRange(linked, unlinked);
        await fixture.Db.SaveChangesAsync(Cancellation);
        var invoice = await fixture.StoreAsync("supplier-payment", linked.Id);
        var orphan = await fixture.StoreAsync("supplier-payment", unlinked.Id);
        var controller = await fixture.ControllerAsync("engineer", "assigned", project);

        Assert.Equal(invoice.Id, Assert.Single(await ListAsync(controller, "supplier-payment", linked.Id)).Id);
        Assert.IsType<PhysicalFileResult>(await controller.Download(invoice.Id, Cancellation));
        Assert.Empty(await ListAsync(controller, "supplier-payment", unlinked.Id));
        Assert.IsType<NotFoundResult>(await controller.Download(orphan.Id, Cancellation));
    }

    [Fact]
    public async Task AssignedActorCanUploadAnOrdinaryAttachmentWithTheExistingStorageLayout()
    {
        await using var fixture = new Fixture();
        var project = await fixture.ProjectAsync("ORDINARY");
        var movement = new SiteMovement { ProjectId = project, Description = "QA movement" };
        fixture.Db.SiteMovements.Add(movement);
        await fixture.Db.SaveChangesAsync(Cancellation);
        var controller = await fixture.ControllerAsync("engineer", "assigned", project);
        using var input = new MemoryStream(Fixture.Contents);
        var form = new FormFile(input, 0, input.Length, "file", "receipt.pdf")
        {
            Headers = new HeaderDictionary(), ContentType = "application/pdf"
        };
        var uploaded = await controller.Upload("site-movement", movement.Id, form, Cancellation);
        var attachment = Assert.IsType<AttachmentSummary>(Assert.IsType<OkObjectResult>(uploaded.Result).Value);
        var stored = await fixture.Db.Attachments.SingleAsync(Cancellation);

        Assert.Equal(Path.Combine("uploads", "site-movement", movement.Id.ToString(), stored.StoredFileName), stored.RelativePath);
        Assert.Equal(attachment.Id, Assert.Single(await ListAsync(controller, "site-movement", movement.Id)).Id);
        var download = Assert.IsType<PhysicalFileResult>(await controller.Download(attachment.Id, Cancellation));
        Assert.Equal(Fixture.Contents, await File.ReadAllBytesAsync(download.FileName, Cancellation));
        Assert.IsType<BadRequestObjectResult>((await controller.Upload("project-import", project, form, Cancellation)).Result);
    }

    private static async Task<IReadOnlyList<AttachmentSummary>> ListAsync(
        AttachmentsController controller, string type, int id)
    {
        var result = await controller.Get(type, id, Cancellation);
        return Assert.IsAssignableFrom<IReadOnlyList<AttachmentSummary>>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal static readonly byte[] Contents = [80, 75, 3, 4];
        private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("emg-attachments-h1-");
        internal EmgDbContext Db { get; } = new(new DbContextOptionsBuilder<EmgDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        internal int ActorId { get; private set; }

        internal async Task<int> ProjectAsync(string code)
        {
            var project = new Project { Code = code, Name = code };
            Db.Projects.Add(project);
            await Db.SaveChangesAsync(Cancellation);
            return project.Id;
        }

        internal async Task<AttachmentsController> ControllerAsync(string name, string? scope, int assignment)
        {
            var role = new Role { Name = name, DisplayName = name };
            if (scope is not null)
            {
                foreach (var code in new[] { "attachments.view", "attachments.upload" })
                    role.RolePermissions.Add(new RolePermission
                    {
                        Permission = new Permission { Code = code, DisplayName = code }, Scope = scope
                    });
            }

            var actor = new AppUser { Email = "qa@example.test", UserName = "qa" };
            actor.UserRoles.Add(new UserRole { Role = role });
            actor.ProjectAssignments.Add(new UserProjectAssignment { ProjectId = assignment });
            Db.Users.Add(actor);
            await Db.SaveChangesAsync(Cancellation);
            ActorId = actor.Id;

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim(ClaimTypes.Role, name)], "test"));
            return new AttachmentsController(new AttachmentsService(Db, new TestEnvironment(root.FullName)))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
            };
        }

        internal async Task<Attachment> StoreAsync(string type, int entityId, int? uploaderId = null)
        {
            var relativePath = Path.Combine("uploads", Guid.NewGuid().ToString("N") + ".xlsx");
            Directory.CreateDirectory(Path.Combine(root.FullName, "uploads"));
            await File.WriteAllBytesAsync(Path.Combine(root.FullName, relativePath), Contents, Cancellation);
            var attachment = new Attachment
            {
                EntityType = type, EntityId = entityId, OriginalFileName = "qa.xlsx", RelativePath = relativePath,
                UploadedByUserId = uploaderId
            };
            Db.Attachments.Add(attachment);
            await Db.SaveChangesAsync(Cancellation);
            return attachment;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            root.Delete(recursive: true);
        }
    }

    private sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AttachmentScopeTests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
