using System.Text.Json;
using System.Text.Json.Nodes;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;
using Api.BoundedContexts.Administration.Infrastructure.Health;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.BoundedContexts.SecurityAudit.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// #5156 — the bounded columns of <c>audit_logs</c> receive values that the client chooses (<c>user_agent</c>, the
/// <c>User-Agent</c> header) or that the command carries (<c>resource_id</c>, <c>details</c>). Postgres refuses a
/// longer value (22001) instead of cutting it. <c>AuditOutboxProcessor</c> saved its batch once, so one refused row
/// failed the whole batch, stayed Pending at the head of the queue, and held back every audit row after it, at every
/// pass. The same value lost the row written directly by <c>AuditService.LogAsync</c>, and the
/// <c>security_audit_logs</c> row of a failed login.
/// </summary>
/// <remarks>On Postgres: InMemory does not enforce the column lengths, and has no failing INSERT.</remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "5156")]
public sealed class AuditValueLongerThanItsColumnIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly SettableHttpContextAccessor _http = new();
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ServiceProvider Services => _serviceProvider!;

    public AuditValueLongerThanItsColumnIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_audit5156_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));
        services.AddScoped<IUserRepository, UserRepository>();
        // The identity handlers write to security_audit_logs through it; the test of that writer builds its own.
        services.AddSingleton(Moq.Mock.Of<IAuditLogger>());
        services.AddSingleton<IHttpContextAccessor>(_http);
        services.AddSingleton<IAuditOutboxHealthTracker, AuditOutboxHealthTracker>();
        services.AddSingleton<AuditOutboxProcessor>();
        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        await TestMigrationHelper.MigrateWithRetryAsync(scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();
        try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
        catch { /* ignore cleanup errors */ }
    }

    /// <summary>The reproduction the issue asks for: an audited command sent with a 300-character User-Agent.</summary>
    [Fact]
    public async Task ACommandSentWithALongUserAgent_ReachesAuditLogs_AndSoDoesTheCommandAfterIt()
    {
        var admin = await SeedUserAsync("admin-5156@meeple.test", "superadmin");
        await SeedUserAsync("second-admin-5156@meeple.test", "admin");
        var first = await SeedUserAsync("first-5156@example.com", "user");
        var second = await SeedUserAsync("second-5156@example.com", "user");
        var longUserAgent = "Mozilla/5.0 " + new string('x', 300);

        await SendAsync(new DeleteUserCommand(first.ToString(), admin.ToString()), longUserAgent);
        await SendAsync(new DeleteUserCommand(second.ToString(), admin.ToString()), "xunit");

        var processed = await Services.GetRequiredService<AuditOutboxProcessor>().RunOnceAsync(100, Ct);

        processed.Should().Be(2);
        var logs = await ReadAuditLogsAsync();
        logs.Should().HaveCount(2, "the long User-Agent no longer holds back its own row or the one after it");
        logs.Single(l => l.ResourceId == first.ToString()).UserAgent.Should()
            .Be(string.Concat(longUserAgent.AsSpan(0, 253), "..."), "user_agent is varchar(256): the start, then a visible cut");
        logs.Single(l => l.ResourceId == second.ToString()).UserAgent.Should().Be("xunit");
        (await ReadOutboxAsync()).Should().OnlyContain(r => r.Status == OutboxStatus.Sent);
    }

    [Fact]
    public async Task ARowLongerThanItsColumns_IsWrittenCutToEachOfThem()
    {
        var id = await SeedOutboxRowAsync(Payload(DateTimeOffset.UtcNow) with
        {
            Action = new string('a', 70),
            Resource = new string('r', 130),
            ResourceId = new string('i', 70),
            Result = new string('s', 40),
            Details = new string('d', 1100),
            IpAddress = new string('p', 70),
            UserAgent = new string('u', 300),
        });

        await Services.GetRequiredService<AuditOutboxProcessor>().RunOnceAsync(100, Ct);

        var log = (await ReadAuditLogsAsync()).Should().ContainSingle().Subject;
        log.Id.Should().Be(id);
        log.Should().BeEquivalentTo(new
        {
            Action = Cut('a', 64),
            Resource = Cut('r', 128),
            ResourceId = Cut('i', 64),
            Result = Cut('s', 32),
            Details = Cut('d', 1024),
            IpAddress = Cut('p', 64),
            UserAgent = Cut('u', 256),
        });
        (await ReadOutboxAsync()).Should().ContainSingle().Which.Status.Should().Be(OutboxStatus.Sent);
    }

    /// <summary>
    /// A row the database still refuses — here a null action, which no cut can make fit — is marked Failed with the
    /// database's error, on its own: the rows before and after it are written, and the next pass finds nothing left.
    /// </summary>
    [Fact]
    public async Task ARowTheDatabaseRefuses_IsMarkedFailed_AndTheRowsAroundItAreWritten()
    {
        var now = DateTimeOffset.UtcNow;
        var before = await SeedOutboxRowAsync(Payload(now));
        var refused = await SeedOutboxRowAsync(Payload(now.AddSeconds(1)), json => json["Action"] = null);
        var after = await SeedOutboxRowAsync(Payload(now.AddSeconds(2)));
        var processor = Services.GetRequiredService<AuditOutboxProcessor>();

        var processed = await processor.RunOnceAsync(100, Ct);

        processed.Should().Be(3);
        (await ReadAuditLogsAsync()).Select(l => l.Id).Should().BeEquivalentTo([before, after]);
        var outbox = (await ReadOutboxAsync()).ToDictionary(r => r.Id);
        outbox[before].Status.Should().Be(OutboxStatus.Sent);
        outbox[after].Status.Should().Be(OutboxStatus.Sent);
        outbox[refused].Status.Should().Be(OutboxStatus.Failed);
        outbox[refused].RetryCount.Should().Be(1);
        outbox[refused].LastError.Should().Contain("23502", "the database's own reason, a not-null violation");

        (await processor.RunOnceAsync(100, Ct)).Should().Be(0, "the refused row no longer sits Pending at the head of the queue");
    }

    [Fact]
    public async Task AuditServiceLogAsync_CutsEachValueToItsColumn()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AuditService>().LogAsync(
                userId: Guid.NewGuid().ToString(),
                action: new string('a', 70),
                resource: new string('r', 130),
                resourceId: new string('i', 70),
                result: new string('s', 40),
                details: new string('d', 1100),
                ipAddress: new string('p', 70),
                userAgent: new string('u', 300),
                cancellationToken: Ct);
        }

        (await ReadAuditLogsAsync()).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Action = Cut('a', 64),
            Resource = Cut('r', 128),
            ResourceId = Cut('i', 64),
            Result = Cut('s', 32),
            Details = Cut('d', 1024),
            IpAddress = Cut('p', 64),
            UserAgent = Cut('u', 256),
        });
    }

    /// <summary>
    /// <c>LogAsync</c> swallows its failure so that the audit never fails the caller, but it left the refused row in
    /// the caller's change tracker: the caller's next save tried to write it again, and failed.
    /// </summary>
    [Fact]
    public async Task AuditServiceLogAsync_ARowTheDatabaseRefuses_DoesNotFailTheCallersNextSave()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        await scope.ServiceProvider.GetRequiredService<AuditService>().LogAsync(
            userId: null, action: null!, resource: "User", resourceId: null, result: "Success", cancellationToken: Ct);

        var userId = Guid.NewGuid();
        db.Users.Add(NewUser(userId, "after-refused-audit-5156@example.com", "user"));
        var save = () => db.SaveChangesAsync(Ct);
        await save.Should().NotThrowAsync("the refused audit row is not the caller's to write again");

        db.ChangeTracker.Clear();
        (await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, Ct)).Should().BeTrue();
        (await db.AuditLogs.AsNoTracking().CountAsync(Ct)).Should().Be(0);
    }

    /// <summary>The repository's row is saved with the caller's own changes: refused, it failed them too.</summary>
    [Fact]
    public async Task AuditLogRepository_CutsEachValueToItsColumn()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var repository = new AuditLogRepository(
                db, scope.ServiceProvider.GetRequiredService<Api.SharedKernel.Application.Services.IDomainEventCollector>());

            await repository.AddAsync(new Api.BoundedContexts.Administration.Domain.Entities.AuditLog(
                Guid.NewGuid(),
                userId: null,
                action: new string('a', 70),
                resource: new string('r', 130),
                result: new string('s', 40),
                resourceId: new string('i', 70),
                details: new string('d', 1100),
                ipAddress: new string('p', 70),
                userAgent: new string('u', 300)), Ct);
            await db.SaveChangesAsync(Ct);
        }

        (await ReadAuditLogsAsync()).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Action = Cut('a', 64),
            Resource = Cut('r', 128),
            ResourceId = Cut('i', 64),
            Result = Cut('s', 32),
            Details = Cut('d', 1024),
            IpAddress = Cut('p', 64),
            UserAgent = Cut('u', 256),
        });
    }

    /// <summary>The details of a sync carry the outcome's text: refused, the handler reported the audit row missing.</summary>
    [Fact]
    public async Task DatabaseSyncAuditWriter_CutsTheDetailsToTheirColumn()
    {
        bool written;
        await using (var scope = Services.CreateAsyncScope())
        {
            written = await Api.BoundedContexts.DatabaseSync.Application.Commands.DatabaseSyncAuditWriter.TryWriteAsync(
                scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(),
                NullLogger.Instance,
                Guid.NewGuid(),
                action: "DatabaseSync",
                resource: "Database",
                operationId: Guid.NewGuid(),
                result: "Failed",
                details: new string('d', 1100));
        }

        written.Should().BeTrue();
        (await ReadAuditLogsAsync()).Should().ContainSingle().Which.Details.Should().Be(Cut('d', 1024));
    }

    /// <summary>
    /// <c>security_audit_logs.user_agent</c> is varchar(512): a failed login sent with a longer User-Agent left no row.
    /// </summary>
    [Fact]
    public async Task SecurityAuditLogger_KeepsTheRowOfARequestWhoseValuesAreLongerThanTheirColumns()
    {
        var logger = new AuditLogger(
            Services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<AuditLogger>.Instance);

        await logger.LogAsync(
            AuditEventType.LoginFailure,
            ipAddress: new string('p', 70),
            userAgent: new string('u', 600),
            metadata: "{\"reason\":\"unknown_email\"}",
            cancellationToken: Ct);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var row = (await db.SecurityAuditLogs.AsNoTracking().ToListAsync(Ct)).Should().ContainSingle().Subject;
        row.EventType.Should().Be(AuditEventType.LoginFailure);
        row.UserAgent.Should().Be(Cut('u', 512));
        row.IpAddress.Should().Be(Cut('p', 64));
    }

    private static string Cut(char c, int column) => new string(c, column - 3) + "...";

    private static AuditOutboxPayload Payload(DateTimeOffset timestamp) => new()
    {
        Action = "TestAction",
        Resource = "TestResource",
        UserId = Guid.NewGuid().ToString(),
        ResourceId = "resource-5156",
        Result = "Success",
        IpAddress = "127.0.0.1",
        UserAgent = "xunit",
        RequestType = "TestCommand",
        Details = "{}",
        Timestamp = timestamp,
    };

    private async Task<Guid> SeedOutboxRowAsync(AuditOutboxPayload payload, Action<JsonNode>? edit = null)
    {
        var json = JsonSerializer.SerializeToNode(payload)!;
        edit?.Invoke(json);
        var row = AuditOutboxEntity.CreatePending(Guid.NewGuid(), json.ToJsonString(), payload.Timestamp);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.AuditOutbox.Add(row);
        await db.SaveChangesAsync(Ct);
        return row.Id;
    }

    private async Task SendAsync(IBaseRequest command, string userAgent)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = userAgent;
        _http.HttpContext = context;
        try
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, Ct);
        }
        finally
        {
            _http.HttpContext = null;
        }
    }

    private async Task<Guid> SeedUserAsync(string email, string role)
    {
        var id = Guid.NewGuid();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Users.Add(NewUser(id, email, role));
        await db.SaveChangesAsync(Ct);
        return id;
    }

    private static UserEntity NewUser(Guid id, string email, string role) => new()
    {
        Id = id,
        Email = email,
        DisplayName = email.Split('@')[0],
        Role = role,
        Tier = "free",
        CreatedAt = DateTime.UtcNow,
    };

    private async Task<List<AuditLogEntity>> ReadAuditLogsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().AuditLogs.AsNoTracking().ToListAsync(Ct);
    }

    private async Task<List<AuditOutboxEntity>> ReadOutboxAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().AuditOutbox.AsNoTracking().ToListAsync(Ct);
    }

    /// <summary>The request the audited commands run in: the behavior reads the User-Agent header from it.</summary>
    private sealed class SettableHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
