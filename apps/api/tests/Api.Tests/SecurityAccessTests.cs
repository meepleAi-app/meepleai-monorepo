using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public class SecurityAccessTests
{
    [Fact]
    public async Task AdminUser_CanUpdateUserProfile_InSingleTenant()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new MeepleAiDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();

            var tenant = new TenantEntity { Id = "meepleai", Name = "MeepleAI" };
            var admin = new UserEntity
            {
                Id = "admin-1",
                TenantId = "meepleai",
                Email = "admin@example.com",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                Tenant = tenant
            };

            var user = new UserEntity
            {
                Id = "user-1",
                TenantId = "meepleai",
                Email = "user@example.com",
                PasswordHash = "hash",
                Role = UserRole.User,
                Tenant = tenant
            };

            setup.Tenants.Add(tenant);
            setup.Users.AddRange(admin, user);
            await setup.SaveChangesAsync();
        }

        await using var context = new MeepleAiDbContext(options, Options.Create(new SingleTenantOptions { TenantId = "meepleai" }));

        var userToUpdate = await context.Users.FirstAsync(u => u.Id == "user-1");
        userToUpdate.DisplayName = "Updated by admin";
        await context.SaveChangesAsync();

        var updatedUser = await context.Users.AsNoTracking().FirstAsync(u => u.Id == "user-1");
        Assert.Equal("Updated by admin", updatedUser.DisplayName);
    }

    [Fact]
    public async Task UnauthorizedAction_IsLoggedWithDefaultTenant()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new MeepleAiDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        await using var context = new MeepleAiDbContext(options, Options.Create(new SingleTenantOptions { TenantId = "meepleai" }));
        var auditService = new AuditService(context, NullLogger<AuditService>.Instance, Options.Create(new SingleTenantOptions { TenantId = "meepleai" }));

        await auditService.LogTenantAccessDeniedAsync(
            userTenantId: "meepleai",
            requestedTenantId: "meepleai",
            userId: "user-1",
            resource: "admin/seed",
            cancellationToken: default);

        var entry = await context.AuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal("meepleai", entry.TenantId);
        Assert.Equal("admin/seed", entry.Resource);
        Assert.Contains("attempted to access", entry.Details);
    }
}
