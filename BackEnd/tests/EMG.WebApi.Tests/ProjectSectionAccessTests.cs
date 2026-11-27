using System.Reflection;
using System.Security.Claims;
using EMG.Api.Modules.Projects.Controllers;
using EMG.Api.Modules.Projects.Services;
using EMG.Api.Modules.Security.Authorization;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class ProjectSectionAccessTests
{
    [Fact]
    public async Task EachSectionAcceptsAdminAndAssignedEngineerWithoutReportsPermission()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var history = new Project { Code = "history", Name = "Historical test" };
        var current = new Project { Code = "new", Name = "New test" };
        var admin = new UserRole { User = new AppUser { UserName = "admin-fixture", Email = "admin@example.test" }, Role = new Role { Name = "admin" } };
        var engineer = new UserRole { User = new AppUser { UserName = "engineer-fixture", Email = "engineer@example.test" }, Role = new Role { Name = "engineer" } };
        db.AddRange(history, current, admin, engineer);
        await db.SaveChangesAsync();
        db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = engineer.UserId, ProjectId = history.Id, IsActive = true });
        var batch = new HistoricalImportBatch { Project = history, SourceSha256 = new string('b', 64), SourceFileName = "fixture.xlsx", Status = "POSTED", CreatedByUserId = admin.UserId };
        var sections = new[] { ("Movements", "site", "site-movements.view"), ("Excavation", "excavation", "excavation.view"),
            ("Concrete", "concrete", "concrete.view"), ("Steel", "steel", "steel.view"), ("Finishing", "finishing", "finishing.view") };
        foreach (var (name, family, permission) in sections)
        {
            db.HistoricalWorkLines.Add(new HistoricalWorkLine { Batch = batch, Family = family, Category = family, Sheet = name,
                Cell = "J10", Amount = -7.25m, Currency = "EGP", Description = name });
            db.RolePermissions.Add(new RolePermission { Role = engineer.Role, Permission = new Permission { Code = permission }, Scope = "assigned" });
        }
        await db.SaveChangesAsync();

        var authorization = new PermissionAuthorizationHandler(db);
        ClaimsPrincipal Principal(UserRole role) => new(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, role.UserId.ToString()), new Claim(ClaimTypes.Role, role.Role.Name)], "test"));
        async Task<bool> Allowed(ClaimsPrincipal principal, string code)
        {
            var context = new AuthorizationHandlerContext([new PermissionRequirement(code)], principal, null);
            await authorization.HandleAsync(context);
            return context.HasSucceeded;
        }
        foreach (var role in new[] { admin, engineer })
        {
            var principal = Principal(role);
            var controller = new ProjectWorkController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } } };
            foreach (var (name, family, permission) in sections)
            {
                var method = typeof(ProjectWorkController).GetMethod(name)!;
                var attribute = Assert.Single(method.GetCustomAttributes<PermissionAuthorizeAttribute>());
                Assert.Equal("Permission:" + permission, attribute.Policy);
                Assert.True(await Allowed(principal, permission));
                async Task<IActionResult> Call(int projectId) =>
                    (await (Task<ActionResult<HistoricalSectionWorks>>)method.Invoke(controller, [projectId, CancellationToken.None])!).Result!;
                var payload = Assert.IsType<HistoricalSectionWorks>(Assert.IsType<OkObjectResult>(await Call(history.Id)).Value);
                var row = Assert.Single(payload.Rows);
                Assert.Equal((family, "J10", -7.25m), (row.Family, row.Cell, row.Amount));
                if (role == admin)
                    Assert.Empty(Assert.IsType<HistoricalSectionWorks>(Assert.IsType<OkObjectResult>(await Call(current.Id)).Value).Rows);
                else
                    Assert.IsType<NotFoundResult>(await Call(current.Id));
                Assert.IsType<NotFoundResult>(await Call(int.MaxValue));
            }
        }
        Assert.False(await Allowed(Principal(engineer), "reports.view"));
        Assert.False(await Allowed(new ClaimsPrincipal(new ClaimsIdentity()), "steel.view"));
    }
}
