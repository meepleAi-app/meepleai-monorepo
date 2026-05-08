using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Base class for Authentication bounded context tests requiring real database semantics.
/// Provides Testcontainers PostgreSQL, FakeTimeProvider, IDataProtectionProvider, IMediator,
/// and a fully wired DI graph (repositories + UnitOfWork + TempSessionService).
///
/// Use for tests that need:
///   - Real DB unique constraints (e.g. duplicate email rejection)
///   - Multi-aggregate transactions
///   - Concurrency / race condition validation
///   - Auth flows that touch real session repository semantics
///
/// Heavier-weight than mock-based unit tests; prefer mocks for pure handler logic.
/// </summary>
[Trait("BoundedContext", "Authentication")]
public abstract class AuthBoundedContextTestBase : IAsyncLifetime
{
    /// <summary>Test bootstrap admin token. Configured via Authentication:BootstrapAdminToken.</summary>
    protected const string TestBootstrapAdminToken = "test-bootstrap-token-12345";

    private PostgreSqlContainer? _container;
    private ServiceProvider? _services;

    /// <summary>Resolved DbContext (scoped per test class lifetime).</summary>
    protected MeepleAiDbContext Db { get; private set; } = null!;

    /// <summary>FakeTimeProvider — tests can advance time without sleeping.</summary>
    protected FakeTimeProvider TimeProvider { get; private set; } = null!;

    /// <summary>Ephemeral data protection provider — keys do not persist between test runs.</summary>
    protected IDataProtectionProvider DataProtection { get; private set; } = null!;

    /// <summary>MediatR — exercises commands/queries through the registered handlers.</summary>
    protected IMediator Mediator { get; private set; } = null!;

    /// <summary>Resolved service provider for ad-hoc service resolution.</summary>
    protected IServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase($"auth_test_{Guid.NewGuid():N}")
            .Build();
        await _container.StartAsync().ConfigureAwait(false);

        var services = new ServiceCollection();

        // Configuration — bootstrap admin token used by the BootstrapAdmin handler.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:BootstrapAdminToken"] = TestBootstrapAdminToken,
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Logging.
        services.AddLogging(b => b.AddDebug());

        // Time provider (FakeTimeProvider for testability).
        TimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        services.AddSingleton<TimeProvider>(TimeProvider);

        // Data protection — ephemeral keys for tests.
        services.AddSingleton<IDataProtectionProvider, EphemeralDataProtectionProvider>();

        // Domain event collector (used by MeepleAiDbContext for raising events on SaveChanges).
        services.AddSingleton<IDomainEventCollector, DomainEventCollector>();

        // MediatR — register all command/query handlers from the Api assembly.
        // Anchor on a stable internal type (UserRepository lives in Api.dll) to scan handlers.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UserRepository).Assembly));

        // DbContext wired to the Testcontainers PostgreSQL instance.
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_container.GetConnectionString());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Authentication BC repositories (real implementations against the Testcontainers DB).
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IOAuthAccountRepository, OAuthAccountRepository>();

        // Unit of Work.
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Temp session service used by the 2FA flow.
        services.AddScoped<ITempSessionService, TempSessionService>();

        _services = services.BuildServiceProvider();
        Services = _services;
        Db = _services.GetRequiredService<MeepleAiDbContext>();
        Mediator = _services.GetRequiredService<IMediator>();
        DataProtection = _services.GetRequiredService<IDataProtectionProvider>();

        // Apply migrations against the freshly-spun container.
        await Db.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync().ConfigureAwait(false);
        }

        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Helper: persists a new active user with a real password hash via the User aggregate root.
    /// </summary>
    protected async Task<User> CreateUserWithPasswordAsync(
        string email,
        string password,
        Role? role = null)
    {
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: email.Split('@')[0],
            passwordHash: PasswordHash.Create(password),
            role: role ?? Role.User);

        var users = Services.GetRequiredService<IUserRepository>();
        await users.AddAsync(user, CancellationToken.None).ConfigureAwait(false);

        var uow = Services.GetRequiredService<IUnitOfWork>();
        await uow.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return user;
    }

    /// <summary>
    /// Helper: persists a user with no local password (OAuth-only).
    /// Constructs the aggregate via the standard ctor, then drops the password hash through
    /// the internal hydration hook to match the post-Task-1 nullable-PasswordHash schema.
    /// Replace with <c>User.CreateForOAuth</c> once introduced in the C3 OAuth task.
    /// </summary>
    protected async Task<User> CreateOAuthUserAsync(string email)
    {
        // Sentinel password is required by the public ctor but immediately cleared below.
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: email.Split('@')[0],
            passwordHash: PasswordHash.Create("OAuthPlaceholder123!"),
            role: Role.User);

        // Clear password hash to model an OAuth-only account (PasswordHash now nullable post-Task 1).
        user.RestorePasswordHash(null);

        var users = Services.GetRequiredService<IUserRepository>();
        await users.AddAsync(user, CancellationToken.None).ConfigureAwait(false);

        var uow = Services.GetRequiredService<IUnitOfWork>();
        await uow.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return user;
    }
}
