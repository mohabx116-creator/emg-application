using EMG.Api.Modules.Security.Data;
using EMG.Domain.Entities;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.WebApi.Tests;

public sealed class SecuritySeederPreservationTests
{
    [Fact]
    public async Task ExistingCustomizedSecurityStateSurvivesRepeatedStartupSeeding()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;

        await using (var setup = new EmgDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        await SeedAsync(connection);

        int engineerId;
        int projectsViewId;
        int suppliersApproveId;
        await using (var db = new EmgDbContext(options))
        {
            var engineer = await db.Roles.SingleAsync(row => row.Name == "engineer");
            engineerId = engineer.Id;
            projectsViewId = await db.Permissions.Where(row => row.Code == "projects.view").Select(row => row.Id).SingleAsync();
            suppliersApproveId = await db.Permissions.Where(row => row.Code == "suppliers.approve").Select(row => row.Id).SingleAsync();
            var customPermission = new Permission { Code = "qa.custom.permission", DisplayName = "QA custom" };
            db.Permissions.Add(customPermission);
            await db.SaveChangesAsync();

            var projectsGrant = await db.RolePermissions.SingleAsync(row => row.RoleId == engineerId && row.PermissionId == projectsViewId);
            projectsGrant.Scope = "project";
            projectsGrant.AmountLimit = 1234.50m;
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = engineerId,
                PermissionId = suppliersApproveId,
                Scope = "assigned",
                AmountLimit = 2500m
            });
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = engineerId,
                PermissionId = customPermission.Id,
                Scope = "all",
                AmountLimit = 9999m
            });

            var user = await db.Users.SingleAsync(row => row.UserName == "seeded-admin");
            user.PasswordHash = "preserve-this-hash";
            user.IsActive = true;
            user.MustChangePassword = false;
            var project = new Project
            {
                Name = "QA project",
                Code = "SEC-SEEDER-QA",
                Status = "مفتوح",
                OfficeSharePercentage = 5,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            db.UserProjectAssignments.Add(new UserProjectAssignment
            {
                UserId = user.Id,
                ProjectId = project.Id,
                AssignedAtUtc = DateTime.UtcNow,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        await SeedAsync(connection);
        await SeedAsync(connection);

        await using (var db = new EmgDbContext(options))
        {
            var engineer = await db.Roles.SingleAsync(row => row.Name == "engineer");
            var grants = await db.RolePermissions.Where(row => row.RoleId == engineer.Id).Include(row => row.Permission).ToListAsync();
            Assert.Equal("project", grants.Single(row => row.Permission.Code == "projects.view").Scope);
            Assert.Equal(1234.50m, grants.Single(row => row.Permission.Code == "projects.view").AmountLimit);
            Assert.Equal("assigned", grants.Single(row => row.Permission.Code == "suppliers.approve").Scope);
            Assert.Equal(2500m, grants.Single(row => row.Permission.Code == "suppliers.approve").AmountLimit);
            Assert.Equal("all", grants.Single(row => row.Permission.Code == "qa.custom.permission").Scope);
            Assert.Equal("preserve-this-hash", (await db.Users.SingleAsync(row => row.UserName == "seeded-admin")).PasswordHash);
            Assert.True(await db.UserProjectAssignments.AnyAsync());
        }
    }

    [Fact]
    public async Task RevokedGrantAndDisabledOrDeletedUsersAreNotRecreated()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        await using (var setup = new EmgDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        await SeedAsync(connection);
        await using (var db = new EmgDbContext(options))
        {
            var engineer = await db.Roles.SingleAsync(row => row.Name == "engineer");
            var view = await db.Permissions.SingleAsync(row => row.Code == "projects.view");
            db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(row => row.RoleId == engineer.Id && row.PermissionId == view.Id));
            var disabled = new AppUser
            {
                UserName = "disabled-user",
                Email = "disabled-user@example.test",
                DisplayName = "Disabled",
                PasswordHash = "disabled-hash",
                IsActive = false,
                MustChangePassword = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            var deleted = new AppUser
            {
                UserName = "deleted-user",
                Email = "deleted-user@example.test",
                DisplayName = "Deleted",
                PasswordHash = "deleted-hash",
                IsActive = true,
                MustChangePassword = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.AddRange(disabled, deleted);
            await db.SaveChangesAsync();
            db.Users.Remove(deleted);
            await db.SaveChangesAsync();
        }

        await SeedAsync(connection);
        await SeedAsync(connection);

        await using (var db = new EmgDbContext(options))
        {
            var engineer = await db.Roles.SingleAsync(row => row.Name == "engineer");
            var view = await db.Permissions.SingleAsync(row => row.Code == "projects.view");
            Assert.False(await db.RolePermissions.AnyAsync(row => row.RoleId == engineer.Id && row.PermissionId == view.Id));
            var disabled = await db.Users.SingleAsync(row => row.UserName == "disabled-user");
            Assert.False(disabled.IsActive);
            Assert.False(await db.Users.AnyAsync(row => row.UserName == "deleted-user"));
        }
    }

    private static async Task SeedAsync(SqliteConnection connection)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "QA" });
        builder.Configuration.Sources.Clear();
        builder.Services.AddDbContext<EmgDbContext>(options => options.UseSqlite(connection));
        await using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EmgDbContext>();
        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new AppUser
            {
                UserName = "seeded-admin",
                Email = "seeded-admin@example.test",
                DisplayName = "Seeded Admin",
                PasswordHash = "initial-hash",
                IsActive = true,
                MustChangePassword = false,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await host.SeedSecurityAsync();
    }
}
