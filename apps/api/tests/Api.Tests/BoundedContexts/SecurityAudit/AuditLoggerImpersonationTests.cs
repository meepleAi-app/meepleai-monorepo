using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.BoundedContexts.SecurityAudit.Infrastructure.Services;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SecurityAudit;

/// <summary>
/// #3838 — during an impersonation the security audit row names the real admin, as <see cref="Principal"/> requires
/// of every audit row. The identity handlers pass the user they act for (the session's subject, who owns the
/// password or the devices), and cannot tell that someone else is acting: without this, an admin impersonating a
/// user and changing their password would be recorded as the user changing it.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SecurityAudit")]
[Trait("Issue", "3838")]
public sealed class AuditLoggerImpersonationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public async Task DuringAnImpersonation_TheRowNamesTheAdmin_AndKeepsTheImpersonatedUser()
    {
        var (logger, read) = Build(impersonating: true);

        await logger.LogAsync(
            AuditEventType.PasswordChanged,
            actorUserId: UserId,
            targetUserId: UserId,
            metadata: "{\"byAdmin\":false}",
            cancellationToken: TestContext.Current.CancellationToken);

        var row = await read();
        row.ActorUserId.Should().Be(AdminId);
        row.TargetUserId.Should().Be(UserId);
        row.Metadata.Should().Contain($"\"impersonatedUserId\":\"{UserId}\"").And.Contain("\"byAdmin\":false");
    }

    [Fact]
    public async Task DuringAnImpersonation_WithoutMetadata_TheImpersonatedUserIsStillKept()
    {
        var (logger, read) = Build(impersonating: true);

        await logger.LogAsync(
            AuditEventType.Logout, actorUserId: UserId, targetUserId: UserId,
            cancellationToken: TestContext.Current.CancellationToken);

        (await read()).Metadata.Should().Be($"{{\"impersonatedUserId\":\"{UserId}\"}}");
    }

    [Fact]
    public async Task WithoutAnImpersonation_TheRowIsWrittenAsGiven()
    {
        var (logger, read) = Build(impersonating: false);

        await logger.LogAsync(
            AuditEventType.PasswordChanged, actorUserId: UserId, targetUserId: UserId,
            cancellationToken: TestContext.Current.CancellationToken);

        var row = await read();
        row.ActorUserId.Should().Be(UserId);
        row.Metadata.Should().BeNull();
    }

    private static (AuditLogger Logger, Func<Task<Api.BoundedContexts.SecurityAudit.Infrastructure.Entities.AuditLogEntity>> Read)
        Build(bool impersonating)
    {
        var dbName = $"audit-impersonation-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddScoped(_ => TestDbContextFactory.CreateInMemoryDbContext(dbName));
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext();
        var subject = User(UserId, "user");
        var principal = new Principal(subject, impersonating ? User(AdminId, "superadmin") : null);
        httpContext.Items[nameof(SessionStatusDto)] = new SessionStatusDto(true, principal, null, null);

        var logger = new AuditLogger(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            NullLogger<AuditLogger>.Instance,
            new HttpContextAccessor { HttpContext = httpContext });

        async Task<Api.BoundedContexts.SecurityAudit.Infrastructure.Entities.AuditLogEntity> Read()
        {
            await using var db = TestDbContextFactory.CreateInMemoryDbContext(dbName);
            return await db.SecurityAuditLogs.AsNoTracking().SingleAsync();
        }

        return (logger, Read);
    }

    private static UserDto User(Guid id, string role) =>
        new(id, $"{id:N}@test.local", "Test", role, "free", DateTime.UtcNow, false, null, 1, 0);
}
