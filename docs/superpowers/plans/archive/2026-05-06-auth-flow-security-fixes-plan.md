# Auth Flow Security Fixes — Implementation Plan (Phase A + B)

**Status**: ✅ COMPLETED (PR #818 hotfix Phase A+B+C+D)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fixare i 7 bug critici (C1-C7) + 1 critical aggiunto (C8 CSRF) della security review del bounded context Authentication, preceduti da foundation work (commit #01-#02) per setup schema + test infrastructure.

**Architecture:** TDD obbligatorio per ogni fix critico (red→commit→green→commit). Single-source-of-truth per session token hashing. DDD aggregate integrity (User aggregate, factory methods). Event-driven side effects (PasswordChangedEvent → revoke). Cookie versioning lazy-migration per C4 evitare big-bang invalidation.

**Tech Stack:** .NET 9 · ASP.NET Minimal APIs · MediatR · EF Core 9 (PostgreSQL 16) · xUnit + FluentAssertions + NSubstitute · Testcontainers (Postgres) · FakeTimeProvider · BenchmarkDotNet · Coverlet · Stryker.NET (mutation testing)

**Spec:** `docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md`

**Plan version**: v1.1 (post-review fixes applied 2026-05-06)
- v1.0: initial plan
- v1.1: code-reviewer findings addressed (architectural conflicts in Task 8/9, missing migration field for C5 single-use, IMediator in test base, frontend CSRF tests, migration Down test)

**Test scaffold convention**: Where test bodies show `// ...` ellipsis, executor MUST implement following AAA pattern (Arrange/Act/Assert) using `AuthBoundedContextTestBase` helpers. Each ellipsis represents 5-15 lines of standard test setup, not architectural decisions. Architectural decisions are fully specified in code.

**Branch:**
- Parent: `main` (hotfix per security)
- Feature: `hotfix/auth-flow-security-fixes-2026-05-06`

**Scope di questo plan:** 10 commit (Phase A foundation #01-#02 + Phase B critical fixes #03-#10). Phase C (Important Hardening) e Phase D (Code Quality) avranno piani follow-up dopo merge.

---

## File Structure

### Backend — Files Created

| File | Responsibility |
|------|----------------|
| `apps/api/src/Api/Infrastructure/Migrations/<ts>_AuthSecurityFixesSchemaPrep.cs` | EF migration consolidata: PasswordHash nullable, TempSession FailedAttempts, AuditLog table, EmailOutbox table |
| `apps/api/src/Api/BoundedContexts/Authentication/Domain/ValueObjects/SessionTokenHasher.cs` | Static helper centralizzato per hash session token |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetCurrentUserQuery.cs` | Query record per /auth/me |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetCurrentUserQueryHandler.cs` | Handler GetCurrentUserQuery |
| `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/UserCreatedViaOAuthEvent.cs` | Domain event per OAuth user creation |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/PasswordChangedSessionRevocationHandler.cs` | Handler revoke sessioni post-password-change |
| `apps/api/src/Api/Infrastructure/Filters/AntiforgeryEndpointFilter.cs` | Endpoint filter CSRF |
| `apps/api/tests/Api.Tests/Infrastructure/AuthBoundedContextTestBase.cs` | Test base class con Testcontainers + FakeTimeProvider |
| `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Endpoints/SessionExtendEndpointTests.cs` | Test C1 |
| `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Endpoints/RevokeAllSessionsEndpointTests.cs` | Test C1 |
| `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler_PendingUserTests.cs` | Test C2 |
| `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/OAuth/HandleOAuthCallback_DomainAggregateTests.cs` | Test C3 |
| `apps/api/tests/Api.Tests/Routing/CookieHelpersHmacTests.cs` | Test C4 |
| `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommandHandler_RaceConditionTests.cs` | Test C5 (Testcontainers) |
| `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/TwoFactor/Verify2FACommandHandler_LockoutTests.cs` | Test C6 |
| `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/UserProfile/ChangePasswordCommandHandler_SessionRevokeTests.cs` | Test C7 |
| `apps/api/tests/Api.Tests/Infrastructure/Filters/AntiforgeryEndpointFilterTests.cs` | Test C8 |
| `apps/api/tests/Api.Tests/Benchmarks/PasswordHashBenchmark.cs` | BenchmarkDotNet baseline |

### Backend — Files Modified

| File | Change |
|------|--------|
| `apps/api/src/Api/BoundedContexts/Authentication/Domain/ValueObjects/SessionToken.cs` | Refactor `ComputeHash` per usare `SessionTokenHasher` |
| `apps/api/src/Api/Routing/AuthenticationEndpoints.cs` | Replace inline hash con `SessionTokenHasher.HashFromCookie()` (C1); refactor `/auth/me` per CQRS (#02 portion); 401 logout (parte di R5 → in Phase D) |
| `apps/api/src/Api/Services/TempSessionService.cs` | Use `SessionTokenHasher` instead of CryptographyHelper; aggiungi `FailedAttemptCount` tracking (C6) |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler.cs` | Use `CanAuthenticate()`; sposta `RecordSuccessfulLogin()` post-2FA (C2 + C6) |
| `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs` | `PasswordHash` nullable; `VerifyPassword` null-safe; `CreateForOAuth` factory (C2 + C3) |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/OAuth/HandleOAuthCallbackCommandHandler.cs` | Use `User.CreateForOAuth()` invece di `UserEntity` direct; rimuovi `GenerateRandomPasswordHash()` (C3) |
| `apps/api/src/Api/Routing/CookieHelpers.cs` | HMAC role cookie via IDataProtectionProvider con versioning v1/v2 lazy migration (C4) |
| `apps/api/src/Api/Routing/AuthenticationEndpoints.cs:73-116` | Bootstrap admin token + ConflictException su unique violation (C5) |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommand.cs` | Aggiungere `BootstrapToken: string?` |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/TwoFactor/Verify2FACommandHandler.cs` | RecordFailed2FA logic + temp session invalidation (C6) |
| `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Entities/TempSessionEntity.cs` | Aggiungere `FailedAttemptCount` column (C6) |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/UserProfile/ChangePasswordCommandHandler.cs` | Emette `PasswordChangedEvent`; passa current session ID (C7) |
| `apps/api/src/Api/Routing/AuthenticationEndpoints.cs:178-220` | ChangePassword endpoint passa current session ID (C7) |
| `apps/api/src/Api/Program.cs` | AddAntiforgery + AddRateLimiter policies + AddDataProtection PersistKeys (C4 + C8) |

### Frontend — Files Modified

| File | Change |
|------|--------|
| `apps/web/src/lib/api/clients/authClient.ts` | Aggiungere CSRF token in headers (C8) |
| `apps/web/src/lib/api/csrf.ts` (NEW) | Helper per leggere CSRF token da cookie |
| `apps/web/src/middleware.ts` | Auto-fetch /auth/me se cookie role v2 mancante (C4 fallback) |

---

## Task 0: Setup Branch + Worktree

**Files:** N/A (git operations)

- [ ] **Step 1: Verify safe pre-creation state**

```bash
git branch --show-current
# Expected: main-dev (or main, frontend-dev — NOT a feature branch)

git status
# Expected: clean tree (only untracked dns.png ok)
```

- [ ] **Step 2: Switch to main, pull latest**

```bash
git checkout main
git pull --ff-only origin main
```

Expected: success, up-to-date with origin/main.

- [ ] **Step 3: Create hotfix branch + track parent**

```bash
git checkout -b hotfix/auth-flow-security-fixes-2026-05-06
git config branch.hotfix/auth-flow-security-fixes-2026-05-06.parent main
```

- [ ] **Step 4: Cherry-pick spec commit from main-dev**

```bash
git cherry-pick 718123f17
```

Expected: clean cherry-pick (spec doc is new file).

---

## Task 1 (Commit #01): Foundation — SessionTokenHasher + EF Migration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/ValueObjects/SessionTokenHasher.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Domain/ValueObjects/SessionToken.cs`
- Modify: `apps/api/src/Api/Services/TempSessionService.cs`
- Create: `apps/api/src/Api/Infrastructure/Migrations/<ts>_AuthSecurityFixesSchemaPrep.cs` (auto-generated)
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs` (PasswordHash nullable)
- Modify: `apps/api/src/Api/Infrastructure/Entities/UserEntity.cs` (PasswordHash nullable)
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Entities/TempSessionEntity.cs` (FailedAttemptCount)
- Create: `apps/api/src/Api/BoundedContexts/SecurityAudit/Infrastructure/Entities/AuditLogEntity.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Entities/EmailOutboxEntity.cs`

- [ ] **Step 1: Write SessionTokenHasher utility (no test needed — pure refactor)**

Create `apps/api/src/Api/BoundedContexts/Authentication/Domain/ValueObjects/SessionTokenHasher.cs`:

```csharp
using System.Security.Cryptography;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Centralized session token hashing.
/// Single source of truth: storage and validation must use identical hash computation.
/// Resolves C1: hash mismatch between SessionToken.ComputeHash and inline endpoint hashing.
/// </summary>
public static class SessionTokenHasher
{
    /// <summary>
    /// Hash a session token from cookie value.
    /// Throws ValidationException if token format is invalid (not base-64).
    /// </summary>
    public static string HashFromCookie(string cookieValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue))
            throw new ValidationException(nameof(cookieValue), "Session token cannot be empty");

        // Validate Base-64 format and decode
        const int MaxDecodedBytes = 256;
        Span<byte> tokenBytes = stackalloc byte[MaxDecodedBytes];
        if (cookieValue.Length > MaxDecodedBytes * 4 / 3 + 4 ||
            !Convert.TryFromBase64String(cookieValue, tokenBytes, out int bytesWritten))
        {
            throw new ValidationException(nameof(cookieValue), "Session token is not a valid format");
        }

        // SHA256 of decoded raw bytes (matches Session.TokenHash storage)
        var hashBytes = SHA256.HashData(tokenBytes[..bytesWritten]);
        return Convert.ToBase64String(hashBytes);
    }
}
```

- [ ] **Step 2: Refactor SessionToken.ComputeHash to delegate to hasher**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Domain/ValueObjects/SessionToken.cs:65-70`:

```csharp
public string ComputeHash()
{
    return SessionTokenHasher.HashFromCookie(Value);
}
```

- [ ] **Step 3: Make User.PasswordHash nullable**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs:18`:

```csharp
public PasswordHash? PasswordHash { get; private set; }
```

Edit `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs:243-246`:

```csharp
public bool VerifyPassword(string plaintextPassword)
{
    if (PasswordHash == null) return false;  // OAuth-only users (C2 fix)
    return PasswordHash.Verify(plaintextPassword);
}
```

- [ ] **Step 4: Make UserEntity.PasswordHash nullable**

Edit `apps/api/src/Api/Infrastructure/Entities/UserEntity.cs` — find `public string PasswordHash { get; set; }` and change to:

```csharp
public string? PasswordHash { get; set; }
```

- [ ] **Step 5: Add FailedAttemptCount to TempSessionEntity**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Entities/TempSessionEntity.cs` — add property:

```csharp
public int FailedAttemptCount { get; set; }  // C6 fix
```

Default value 0 in constructor.

- [ ] **Step 5b: Add BootstrapAdminCreated flag to SystemConfiguration (C5 single-use)**

Edit `apps/api/src/Api/Infrastructure/Entities/SystemConfigurationEntity.cs` (or equivalent) — add property:

```csharp
public bool BootstrapAdminCreated { get; set; }  // C5: single-use enforcement
public DateTime? BootstrapAdminCreatedAt { get; set; }
```

If table doesn't exist, create entity in `BoundedContexts/SystemConfiguration/Infrastructure/Entities/`. EF migration will add column.

- [ ] **Step 6: Create AuditLogEntity in new SecurityAudit BC**

```bash
mkdir -p apps/api/src/Api/BoundedContexts/SecurityAudit/Infrastructure/Entities
mkdir -p apps/api/src/Api/BoundedContexts/SecurityAudit/Application/Interfaces
```

Create `apps/api/src/Api/BoundedContexts/SecurityAudit/Infrastructure/Entities/AuditLogEntity.cs`:

```csharp
namespace Api.BoundedContexts.SecurityAudit.Infrastructure.Entities;

public class AuditLogEntity
{
    public Guid Id { get; init; }
    public Guid? ActorUserId { get; init; }
    public Guid? TargetUserId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime Timestamp { get; init; }
    public string? Metadata { get; init; }
    public string? CorrelationId { get; init; }
}
```

- [ ] **Step 7: Create EmailOutboxEntity in UserNotifications BC**

```bash
mkdir -p apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Entities
```

Create `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Entities/EmailOutboxEntity.cs`:

```csharp
namespace Api.BoundedContexts.UserNotifications.Infrastructure.Entities;

public class EmailOutboxEntity
{
    public Guid Id { get; init; }
    public string ToEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string BodyHtml { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; }
    public DateTime? SentAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; init; }
    public string Status { get; set; } = "Pending";  // Pending | Sent | FailedPermanent
}
```

- [ ] **Step 8: Update DbContext with new entities**

Edit `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` — add:

```csharp
public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
public DbSet<EmailOutboxEntity> EmailOutbox => Set<EmailOutboxEntity>();
```

- [ ] **Step 9: Generate consolidated EF migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AuthSecurityFixesSchemaPrep
```

Verify generated migration contains:
- `AlterColumn` for `Users.PasswordHash` nullable
- `AddColumn` for `TempSessions.FailedAttemptCount` (default 0)
- `AddColumn` for `SystemConfiguration.BootstrapAdminCreated` (default false) and `BootstrapAdminCreatedAt` (nullable)
- `CreateTable` for `AuditLogs` with indexes on (`ActorUserId`, `Timestamp`), (`TargetUserId`, `Timestamp`), (`EventType`, `Timestamp`)
- `CreateTable` for `EmailOutbox` with indexes on (`Status`, `ScheduledAt`), (`IdempotencyKey` UNIQUE)

- [ ] **Step 10: Apply migration to local dev DB**

```bash
cd apps/api/src/Api
dotnet ef database update
```

Expected: success.

- [ ] **Step 10b: Verify migration Down() works (rollback test)**

```bash
# Note current migration name
dotnet ef migrations list | tail -3
# Rollback to previous
dotnet ef database update <PreviousMigrationName>
# Verify schema returned to previous state
psql -d meepleai_dev -c "\d Users" | grep PasswordHash
# Should show: password_hash | text | NOT NULL (previous state)
# Re-apply forward
dotnet ef database update
# Verify migration is idempotent
```

Expected: rollback + reapply both succeed without errors.

- [ ] **Step 11: Run all existing tests to ensure no regression**

```bash
cd apps/api/src/Api
dotnet test --filter "BoundedContext=Authentication"
```

Expected: all existing tests pass (refactor-only commit, no behavior change yet).

- [ ] **Step 12: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/ValueObjects/SessionTokenHasher.cs \
        apps/api/src/Api/BoundedContexts/Authentication/Domain/ValueObjects/SessionToken.cs \
        apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs \
        apps/api/src/Api/Infrastructure/Entities/UserEntity.cs \
        apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Entities/TempSessionEntity.cs \
        apps/api/src/Api/BoundedContexts/SecurityAudit/Infrastructure/Entities/AuditLogEntity.cs \
        apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Entities/EmailOutboxEntity.cs \
        apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs \
        apps/api/src/Api/Infrastructure/Migrations/

git commit -m "$(cat <<'EOF'
refactor(auth): foundation for security fixes — schema + utilities

- New SessionTokenHasher utility (single source of truth for session hashing)
- SessionToken.ComputeHash delegates to hasher (no behavior change)
- User.PasswordHash made nullable (prep for C3 OAuth fix)
- VerifyPassword null-safe (prep for C2)
- TempSessionEntity.FailedAttemptCount added (prep for C6)
- AuditLogEntity in new SecurityAudit BC (prep for I10)
- EmailOutboxEntity in UserNotifications BC (prep for I5)
- EF migration consolidata: AuthSecurityFixesSchemaPrep

Refs: spec docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 2 (Commit #02): Test Infrastructure + CQRS /auth/me

**Files:**
- Create: `apps/api/tests/Api.Tests/Infrastructure/AuthBoundedContextTestBase.cs`
- Create: `apps/api/tests/Api.Tests/Benchmarks/PasswordHashBenchmark.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetCurrentUserQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetCurrentUserQueryHandler.cs`
- Modify: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs:196-208` (refactor /auth/me to CQRS)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Queries/GetCurrentUserQueryHandlerTests.cs`

- [ ] **Step 1: Create AuthBoundedContextTestBase with full DI**

Create `apps/api/tests/Api.Tests/Infrastructure/AuthBoundedContextTestBase.cs`:

```csharp
using Api.Infrastructure;
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
/// Base class for Authentication BC tests requiring real database semantics.
/// Provides Testcontainers PostgreSQL, FakeTimeProvider, IDataProtectionProvider, IMediator.
/// Use for: tests exercising unique constraints, multi-aggregate transactions, concurrency.
/// </summary>
public abstract class AuthBoundedContextTestBase : IAsyncLifetime
{
    protected PostgreSqlContainer Container { get; private set; } = null!;
    protected MeepleAiDbContext Db { get; private set; } = null!;
    protected FakeTimeProvider TimeProvider { get; private set; } = null!;
    protected IDataProtectionProvider DataProtection { get; private set; } = null!;
    protected IMediator Mediator { get; private set; } = null!;
    protected IServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("auth_test")
            .Build();
        await Container.StartAsync();

        var services = new ServiceCollection();

        // Configuration
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:BootstrapAdminToken"] = "test-bootstrap-token-12345"
            });
        services.AddSingleton<IConfiguration>(configBuilder.Build());

        // Database
        services.AddDbContext<MeepleAiDbContext>(opt =>
            opt.UseNpgsql(Container.GetConnectionString()));

        // Logging
        services.AddLogging(b => b.AddDebug());

        // Time provider (for testability)
        TimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        services.AddSingleton<TimeProvider>(TimeProvider);

        // Data protection (ephemeral for tests)
        services.AddDataProtection();
        services.AddSingleton<IDataProtectionProvider, EphemeralDataProtectionProvider>();

        // MediatR — register all handlers from Api assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Api.Program>());

        // Domain repositories + services (use real implementations against Testcontainers DB)
        services.AddScoped<Api.BoundedContexts.Authentication.Infrastructure.Persistence.IUserRepository,
            Api.BoundedContexts.Authentication.Infrastructure.Repositories.UserRepository>();
        services.AddScoped<Api.BoundedContexts.Authentication.Infrastructure.Persistence.ISessionRepository,
            Api.BoundedContexts.Authentication.Infrastructure.Repositories.SessionRepository>();
        services.AddScoped<Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork,
            Api.SharedKernel.Infrastructure.Persistence.UnitOfWork>();
        services.AddScoped<Api.Services.ITempSessionService, Api.Services.TempSessionService>();

        Services = services.BuildServiceProvider();
        Db = Services.GetRequiredService<MeepleAiDbContext>();
        Mediator = Services.GetRequiredService<IMediator>();
        DataProtection = Services.GetRequiredService<IDataProtectionProvider>();

        await Db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        await Container.DisposeAsync();
    }

    /// <summary>Helper: create active user with password.</summary>
    protected async Task<Api.BoundedContexts.Authentication.Domain.Entities.User> CreateUserWithPasswordAsync(
        string email, string password, Api.BoundedContexts.Authentication.Domain.ValueObjects.Role? role = null)
    {
        var user = new Api.BoundedContexts.Authentication.Domain.Entities.User(
            id: Guid.NewGuid(),
            email: new Api.SharedKernel.Domain.ValueObjects.Email(email),
            displayName: email.Split('@')[0],
            passwordHash: Api.BoundedContexts.Authentication.Domain.ValueObjects.PasswordHash.Create(password),
            role: role ?? Api.BoundedContexts.Authentication.Domain.ValueObjects.Role.User);
        var repo = Services.GetRequiredService<Api.BoundedContexts.Authentication.Infrastructure.Persistence.IUserRepository>();
        await repo.AddAsync(user, CancellationToken.None);
        var uow = Services.GetRequiredService<Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork>();
        await uow.SaveChangesAsync(CancellationToken.None);
        return user;
    }

    /// <summary>Helper: create OAuth-only user (PasswordHash null).</summary>
    protected async Task<Api.BoundedContexts.Authentication.Domain.Entities.User> CreateOAuthUserAsync(string email)
    {
        var user = Api.BoundedContexts.Authentication.Domain.Entities.User.CreateForOAuth(
            id: Guid.NewGuid(),
            email: new Api.SharedKernel.Domain.ValueObjects.Email(email),
            displayName: email.Split('@')[0],
            role: Api.BoundedContexts.Authentication.Domain.ValueObjects.Role.User,
            tier: null,
            oauthProvider: "google",
            timeProvider: TimeProvider);
        var repo = Services.GetRequiredService<Api.BoundedContexts.Authentication.Infrastructure.Persistence.IUserRepository>();
        await repo.AddAsync(user, CancellationToken.None);
        var uow = Services.GetRequiredService<Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork>();
        await uow.SaveChangesAsync(CancellationToken.None);
        return user;
    }
}
```

- [ ] **Step 2: Create PasswordHashBenchmark**

Create `apps/api/tests/Api.Tests/Benchmarks/PasswordHashBenchmark.cs`:

```csharp
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using BenchmarkDotNet.Attributes;

namespace Api.Tests.Benchmarks;

[MemoryDiagnoser]
public class PasswordHashBenchmark
{
    private const string TestPassword = "BenchmarkPassword123!";
    private PasswordHash _hash = null!;

    [GlobalSetup]
    public void Setup()
    {
        _hash = PasswordHash.Create(TestPassword);
    }

    [Benchmark]
    public PasswordHash Create_210k() => PasswordHash.Create(TestPassword);

    [Benchmark]
    public bool Verify_210k() => _hash.Verify(TestPassword);
}
```

Run baseline (will be re-run after I7 to compare):

```bash
cd apps/api/tests/Api.Tests
dotnet run -c Release --project Api.Tests.Benchmarks.csproj
```

Save baseline metrics in `docs/superpowers/plans/baseline-passwordhash-210k.txt`.

- [ ] **Step 3: Create GetCurrentUserQuery**

```csharp
namespace Api.BoundedContexts.Authentication.Application.Queries;

internal record GetCurrentUserQuery(Guid SessionId) : IQuery<UserDto?>;
```

- [ ] **Step 4: Write failing test for GetCurrentUserQueryHandler**

Create `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Queries/GetCurrentUserQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

public class GetCurrentUserQueryHandlerTests
{
    [Fact]
    public async Task Handle_ValidSessionId_ReturnsUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var sessionRepo = Substitute.For<ISessionRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        // ... setup mocks for session.UserId = userId
        var handler = new GetCurrentUserQueryHandler(sessionRepo, userRepo);

        // Act
        var result = await handler.Handle(new GetCurrentUserQuery(sessionId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_NonExistentSession_ReturnsNull()
    {
        // ... similar setup, but session lookup returns null
        // Assert result.Should().BeNull();
    }
}
```

- [ ] **Step 5: Run test, verify FAIL**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~GetCurrentUserQueryHandlerTests" -v normal
```

Expected: FAIL — `GetCurrentUserQueryHandler` not found.

- [ ] **Step 6: Implement GetCurrentUserQueryHandler**

Create `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetCurrentUserQueryHandler.cs`:

```csharp
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

internal class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, UserDto?>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;

    public GetCurrentUserQueryHandler(ISessionRepository sessionRepo, IUserRepository userRepo)
    {
        _sessionRepository = sessionRepo;
        _userRepository = userRepo;
    }

    public async Task<UserDto?> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, ct).ConfigureAwait(false);
        if (session == null) return null;

        var user = await _userRepository.GetByIdAsync(session.UserId, ct).ConfigureAwait(false);
        if (user == null) return null;

        return new UserDto(
            Id: user.Id,
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            Tier: user.Tier.Value,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: user.IsTwoFactorEnabled,
            TwoFactorEnabledAt: user.TwoFactorEnabledAt,
            Level: user.Level,
            ExperiencePoints: user.ExperiencePoints,
            EmailVerified: user.EmailVerified,
            EmailVerifiedAt: user.EmailVerifiedAt,
            VerificationGracePeriodEndsAt: user.VerificationGracePeriodEndsAt,
            OnboardingCompleted: user.OnboardingCompleted,
            OnboardingSkipped: user.OnboardingSkipped
        );
    }
}
```

- [ ] **Step 7: Run test, verify PASS**

```bash
dotnet test --filter "FullyQualifiedName~GetCurrentUserQueryHandlerTests" -v normal
```

Expected: PASS.

- [ ] **Step 8: Refactor /auth/me endpoint to use CQRS**

Edit `apps/api/src/Api/Routing/AuthenticationEndpoints.cs:196-208`:

```csharp
private static void MapMeEndpoint(RouteGroupBuilder group)
{
    group.MapGet("/auth/me", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
    {
        var (authenticated, session, _) = context.TryGetActiveSession();
        if (!authenticated) return Results.Unauthorized();

        var userDto = await mediator.Send(new GetCurrentUserQuery(session.SessionId!.Value), ct);
        if (userDto == null) return Results.Unauthorized();

        return Results.Json(new { user = userDto, expiresAt = session.ExpiresAt });
    });
}
```

Note: `SessionStatusDto` deve esporre `SessionId` — verifica già presente o aggiungerlo.

- [ ] **Step 9: Run all auth tests**

```bash
dotnet test --filter "BoundedContext=Authentication" -v normal
```

Expected: all pass (no regression).

- [ ] **Step 10: Commit**

```bash
git add apps/api/tests/Api.Tests/Infrastructure/AuthBoundedContextTestBase.cs \
        apps/api/tests/Api.Tests/Benchmarks/ \
        apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetCurrentUserQuery.cs \
        apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetCurrentUserQueryHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Queries/GetCurrentUserQueryHandlerTests.cs \
        apps/api/src/Api/Routing/AuthenticationEndpoints.cs

git commit -m "$(cat <<'EOF'
refactor(auth): test infrastructure + CQRS /auth/me

- AuthBoundedContextTestBase: Testcontainers + FakeTimeProvider standard
- PasswordHashBenchmark: BenchmarkDotNet baseline (210k iter)
- GetCurrentUserQuery + Handler (replaces inline /auth/me logic)
- /auth/me endpoint now uses IMediator.Send (CQRS standardization)

Refs: spec section 4 (Group F — CQRS Standardization)

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 3 (Commit #03): C1 — Hash Mismatch Fix (TDD)

**Files:**
- Modify: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs:268-269`, `:456-457`
- Modify: `apps/api/src/Api/Services/TempSessionService.cs:67`, `:138`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Endpoints/SessionExtendEndpointTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Endpoints/RevokeAllSessionsEndpointTests.cs`

- [ ] **Step 1: Write failing test for ExtendSession (RED)**

Create `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Endpoints/SessionExtendEndpointTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Endpoints;

public class SessionExtendEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SessionExtendEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExtendSession_WithValidCookie_ShouldExtendExpiration()
    {
        // Arrange — register + login to get valid session cookie
        var client = _factory.CreateClient();
        var registerResp = await client.PostAsJsonAsync("/api/v1/auth/register", new {
            email = $"test-{Guid.NewGuid()}@test.com",
            password = "ValidPassword123!",
            displayName = "Test User"
        });
        var sessionCookie = registerResp.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("meepleai_session="));

        // Act
        var extendReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/session/extend");
        extendReq.Headers.Add("Cookie", sessionCookie);
        var extendResp = await client.SendAsync(extendReq);

        // Assert — currently FAILS because hash mismatch returns Unauthorized
        extendResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExtendSession_HashComputationMatchesStorage()
    {
        // Regression test — verify SessionTokenHasher used consistently
        // Direct test: extract token from cookie, hash with SessionTokenHasher.HashFromCookie(),
        // assert hash equals what's in DB session.TokenHash column
        // ... implementation
    }

    [Fact]
    public async Task ExtendSession_WithMalformedBase64Cookie_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var extendReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/session/extend");
        extendReq.Headers.Add("Cookie", "meepleai_session=NOT_VALID_BASE64!@#");
        var extendResp = await client.SendAsync(extendReq);

        extendResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExtendSession_WithEmptyToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var extendReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/session/extend");
        extendReq.Headers.Add("Cookie", "meepleai_session=");
        var extendResp = await client.SendAsync(extendReq);

        extendResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Run test, verify FAIL**

```bash
dotnet test --filter "FullyQualifiedName~SessionExtendEndpointTests" -v normal
```

Expected: `ExtendSession_WithValidCookie_ShouldExtendExpiration` FAILS with 401 (hash mismatch bug).

- [ ] **Step 3: Commit failing test**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Authentication/Endpoints/SessionExtendEndpointTests.cs
git commit -m "test(auth): add failing tests for C1 hash mismatch (extend session)"
```

- [ ] **Step 4: Apply C1 fix in AuthenticationEndpoints.cs (extend)**

Edit `apps/api/src/Api/Routing/AuthenticationEndpoints.cs:268-272`:

```csharp
// Replace inline hash with SessionTokenHasher
string tokenHash;
try
{
    tokenHash = SessionTokenHasher.HashFromCookie(token);
}
catch (Api.SharedKernel.Domain.Exceptions.ValidationException)
{
    return Results.Unauthorized();
}

// Look up session by token hash via CQRS query
var dbSession = await mediator.Send(new GetSessionByTokenHashQuery(tokenHash), ct).ConfigureAwait(false);
```

- [ ] **Step 5: Apply same fix in revoke-all endpoint**

Edit `apps/api/src/Api/Routing/AuthenticationEndpoints.cs:454-458`:

```csharp
string? currentTokenHash = null;
var sessionCookieName = CookieHelpers.GetSessionCookieName(context);
if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) && !string.IsNullOrWhiteSpace(token))
{
    try
    {
        currentTokenHash = SessionTokenHasher.HashFromCookie(token);
    }
    catch (Api.SharedKernel.Domain.Exceptions.ValidationException)
    {
        currentTokenHash = null;  // malformed token = treat as no current session exclusion
    }
}
```

- [ ] **Step 6: Update TempSessionService.HashToken to use SessionTokenHasher**

Edit `apps/api/src/Api/Services/TempSessionService.cs:136-139`:

```csharp
private static string HashToken(string token)
{
    return SessionTokenHasher.HashFromCookie(token);
}
```

- [ ] **Step 7: Run tests, verify GREEN**

```bash
dotnet test --filter "FullyQualifiedName~SessionExtendEndpointTests" -v normal
```

Expected: ALL PASS.

- [ ] **Step 8: Run full Authentication BC test suite (no regression)**

```bash
dotnet test --filter "BoundedContext=Authentication"
```

Expected: all pass.

- [ ] **Step 9: Add RevokeAll tests**

Create `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Endpoints/RevokeAllSessionsEndpointTests.cs`:

```csharp
[Fact]
public async Task RevokeAllSessions_WithCurrentSession_ShouldExcludeCurrent()
{
    // 1. Register user, get session A (current)
    // 2. Login from second client, get session B
    // 3. POST /auth/sessions/revoke-all from session A with includeCurrentSession=false
    // 4. Verify: session A still works, session B revoked
    // ... full test impl
}
```

- [ ] **Step 10: Run new tests, verify pass**

```bash
dotnet test --filter "FullyQualifiedName~RevokeAllSessionsEndpointTests"
```

- [ ] **Step 11: Commit fix**

```bash
git add apps/api/src/Api/Routing/AuthenticationEndpoints.cs \
        apps/api/src/Api/Services/TempSessionService.cs \
        apps/api/tests/Api.Tests/BoundedContexts/Authentication/Endpoints/RevokeAllSessionsEndpointTests.cs

git commit -m "$(cat <<'EOF'
fix(auth): hash mismatch in session extend/revoke-all (C1)

The /auth/session/extend and /auth/sessions/revoke-all endpoints were
computing session token hash by hashing UTF-8 bytes of the base64
string, while storage and ValidateSessionQuery use SessionToken.ComputeHash
which decodes base64 first then hashes raw bytes. Different hash → DB
lookup always failed → endpoints silently broken.

Fix: replace inline hash computation with SessionTokenHasher.HashFromCookie()
(introduced in commit #01) used consistently across all paths.

TempSessionService.HashToken also updated for consistency.

Refs: spec section 4 Group A — Session Token Hash & Lifecycle

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 4 (Commit #04): C2 — Login Status Check + Null-Safe VerifyPassword (TDD)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler.cs:70-72`
- Note: `User.VerifyPassword` already null-safe from commit #01
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler_PendingUserTests.cs`

- [ ] **Step 1: Write failing tests (RED)**

Create test file:

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.Login;

public class LoginCommandHandler_PendingUserTests
{
    [Fact]
    public async Task Login_PendingUser_ShouldReturnAccountUnavailable_NoNullRefException()
    {
        // Arrange — create user via CreatePending (PasswordHash = null!)
        var pendingUser = User.CreatePending(
            email: new Email("pending@test.com"),
            displayName: "Pending User",
            role: Role.User,
            tier: UserTier.Free,
            invitedByUserId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            timeProvider: TimeProvider.System
        );
        var userRepo = Substitute.For<IUserRepository>();
        userRepo.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(pendingUser);

        var handler = new LoginCommandHandler(userRepo, /* ... other deps ... */);

        // Act
        var act = async () => await handler.Handle(
            new LoginCommand("pending@test.com", "AnyPassword123", "127.0.0.1", "TestAgent"),
            CancellationToken.None);

        // Assert — must NOT throw NullReferenceException
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Account is not available");
    }

    [Fact]
    public async Task Login_SuspendedUser_ShouldReturnAccountUnavailable()
    {
        var user = CreateActiveUser();
        user.Suspend("Test suspension");
        // ... mock setup
        var handler = new LoginCommandHandler(/* ... */);
        var act = async () => await handler.Handle(/* ... */);
        await act.Should().ThrowAsync<DomainException>().WithMessage("Account is not available");
    }

    [Fact]
    public async Task Login_BannedUser_ShouldReturnAccountUnavailable()
    {
        var user = CreateActiveUser();
        user.Ban("Test ban");
        // ... assertion same
    }

    [Fact]
    public async Task Login_PendingUser_DoesNotIncrementFailedLoginCounter()
    {
        var pendingUser = User.CreatePending(/* ... */);
        var initialFailedCount = pendingUser.FailedLoginAttempts;
        // ... act login (should fail before reaching VerifyPassword)
        // Assert
        pendingUser.FailedLoginAttempts.Should().Be(initialFailedCount);
    }

    private User CreateActiveUser() { /* ... helper */ }
}
```

- [ ] **Step 2: Run, verify FAIL**

Expected: `Login_PendingUser_ShouldReturnAccountUnavailable_NoNullRefException` throws NullReferenceException (not DomainException).

- [ ] **Step 3: Commit failing tests**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler_PendingUserTests.cs
git commit -m "test(auth): add failing tests for C2 login pending user NRE"
```

- [ ] **Step 4: Apply fix in LoginCommandHandler**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler.cs:70-72`:

```csharp
// Issue #2886 + C2: Check if user can authenticate (covers Pending, Suspended, Banned, Deleted)
if (!user.CanAuthenticate())
    throw new DomainException("Account is not available");
```

Move this check **before** `IsLockedOut()` check (line 47), so we don't even compute lockout for pending users.

Updated order:
1. Email lookup
2. **CanAuthenticate check** (NEW)
3. IsLockedOut check
4. VerifyPassword
5. Record success/failure
6. 2FA check
7. Create session

- [ ] **Step 5: Run, verify GREEN**

```bash
dotnet test --filter "FullyQualifiedName~LoginCommandHandler_PendingUserTests"
```

Expected: ALL PASS.

- [ ] **Step 6: Run full Login test suite**

```bash
dotnet test --filter "FullyQualifiedName~LoginCommandHandlerTests"
```

Expected: all pass — existing test for `IsSuspended` still passes (CanAuthenticate covers it).

- [ ] **Step 7: Commit fix**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler.cs

git commit -m "$(cat <<'EOF'
fix(auth): login uses CanAuthenticate() instead of IsSuspended (C2)

The login handler only checked IsSuspended, missing Status.Pending,
Banned, and (future) Deleted. Pending users have null PasswordHash
which caused NullReferenceException → 500 → user enumeration leak.

Fix: replace IsSuspended check with CanAuthenticate() which validates
Status == Active. Move check BEFORE IsLockedOut to avoid amplification
on pending users.

VerifyPassword null-safe already in place (commit #01) as defense-in-depth.

Refs: spec section 4 Group B — Login & Lockout

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 5 (Commit #05): C3 + I8 — OAuth Domain Aggregate (TDD)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs` (add CreateForOAuth factory)
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/UserCreatedViaOAuthEvent.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/OAuth/HandleOAuthCallbackCommandHandler.cs` (use User.CreateForOAuth, remove GenerateRandomPasswordHash)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/OAuth/HandleOAuthCallback_DomainAggregateTests.cs`

- [ ] **Step 1: Create UserCreatedViaOAuthEvent**

```csharp
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

public record UserCreatedViaOAuthEvent(Guid UserId, string Email, string Provider) : IDomainEvent;
```

- [ ] **Step 2: Add CreateForOAuth factory to User**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs` — add after `CreatePending` factory:

```csharp
/// <summary>
/// Creates a user authenticated via OAuth provider (no password).
/// PasswordHash = null marker indicates OAuth-only user.
/// EmailVerified = true (provider has verified the email).
/// </summary>
public static User CreateForOAuth(
    Guid id,
    Email email,
    string displayName,
    Role role,
    UserTier? tier,
    string oauthProvider,
    TimeProvider timeProvider)
{
    ArgumentNullException.ThrowIfNull(email);
    ArgumentNullException.ThrowIfNull(displayName);
    ArgumentNullException.ThrowIfNull(role);
    ArgumentException.ThrowIfNullOrWhiteSpace(oauthProvider);
    ArgumentNullException.ThrowIfNull(timeProvider);

    var now = timeProvider.GetUtcNow().UtcDateTime;
    var user = new User
    {
        Id = id,
        Email = email,
        DisplayName = displayName,
        PasswordHash = null,  // OAuth-only marker
        Role = role,
        Tier = tier ?? UserTier.Free,
        Status = UserAccountStatus.Active,
        CreatedAt = now,
        EmailVerified = true,           // OAuth provider verified
        EmailVerifiedAt = now,
        VerificationGracePeriodEndsAt = null,
        Language = "en",
        EmailNotifications = true,
        Theme = "system",
        DataRetentionDays = 90,
        ShowProfile = true,
        ShowActivity = true,
        ShowLibrary = true,
        Level = 1,
        ExperiencePoints = 0,
        FailedLoginAttempts = 0,
        LockedUntil = null,
        IsTwoFactorEnabled = false,
    };

    user.AddDomainEvent(new UserCreatedViaOAuthEvent(id, email.Value, oauthProvider));
    return user;
}
```

- [ ] **Step 3: Write failing tests (RED)**

```csharp
public class HandleOAuthCallback_DomainAggregateTests : AuthBoundedContextTestBase
{
    [Fact]
    public async Task OAuthCallback_NewUser_PasswordHashIsNull()
    {
        // Arrange — mock OAuth service to return user info
        // Act — invoke HandleOAuthCallbackCommand
        // Assert — Db.Users new user has PasswordHash = null
        var newUser = await Db.Users.SingleAsync(u => u.Email == "newuser@oauth.test");
        newUser.PasswordHash.Should().BeNull();
    }

    [Fact]
    public async Task OAuthCallback_NewUser_DomainEventEmitted()
    {
        // Verify UserCreatedViaOAuthEvent published
    }

    [Fact]
    public async Task OAuthOnlyUser_LoginWithPassword_ReturnsUnauthorized()
    {
        // Arrange — create OAuth-only user
        // Act — try LoginCommand with any password
        // Assert — DomainException "Invalid email or password" (not NRE)
    }

    [Fact]
    public async Task OAuthOnlyUser_ChangePassword_ThrowsConflictException()
    {
        // Arrange — OAuth-only user
        // Act — ChangePasswordCommand
        // Assert — ConflictException "Cannot change password for OAuth-only user"
    }

    [Fact]
    public async Task LegacyOAuthUser_WithRandomBase64Hash_ChangePassword_RejectsAsCorrupted()
    {
        // Arrange — manually create user with random base64 hash (simulating legacy bug data)
        // Act — try ChangePasswordCommand with old password
        // Assert — fails at PasswordHash format check, no rehash
    }
}
```

- [ ] **Step 4: Run, verify FAIL**

```bash
dotnet test --filter "FullyQualifiedName~HandleOAuthCallback_DomainAggregateTests"
```

Expected: tests fail because OAuth handler still uses `UserEntity` direct.

- [ ] **Step 5: Commit failing tests**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs \
        apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/UserCreatedViaOAuthEvent.cs \
        apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/OAuth/HandleOAuthCallback_DomainAggregateTests.cs

git commit -m "test(auth): add failing tests for C3+I8 OAuth domain aggregate"
```

- [ ] **Step 6: Refactor HandleOAuthCallbackCommandHandler.FindOrCreateUserAsync**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/OAuth/HandleOAuthCallbackCommandHandler.cs:454-481`:

```csharp
// OLD code creating UserEntity direct REPLACED with:
if (user == null)
{
    var emailParts = userInfo!.Email?.Split('@') ?? Array.Empty<string>();
    var emailPrefix = emailParts.Length > 0 && !string.IsNullOrEmpty(emailParts[0])
        ? emailParts[0]
        : "User";

    // C3+I8: Use User aggregate factory instead of UserEntity direct
    var domainUser = User.CreateForOAuth(
        id: Guid.NewGuid(),
        email: new Email(userInfo.Email!.ToLowerInvariant()),
        displayName: userInfo.Name ?? emailPrefix,
        role: Role.User,
        tier: null,
        oauthProvider: provider,
        timeProvider: _timeProvider);

    await _userRepository.AddAsync(domainUser, cancellationToken).ConfigureAwait(false);
    await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    // Re-fetch as UserEntity for OAuth account linking (legacy code path)
    user = await _db.Users.AsTracking().SingleAsync(u => u.Id == domainUser.Id, cancellationToken);
    isNewUser = true;
    _logger.LogInformation("Created new user via OAuth. Provider: {Provider}, UserId: {UserId}", provider, user.Id);
}
```

Note: Add `IUserRepository` and `IUnitOfWork` as constructor dependencies if not already present.

- [ ] **Step 7: Remove GenerateRandomPasswordHash method**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/OAuth/HandleOAuthCallbackCommandHandler.cs:586-594`:

```csharp
// DELETE entire GenerateRandomPasswordHash method
```

- [ ] **Step 8: Add ChangePassword guard for null PasswordHash**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs:251-258`:

```csharp
public void ChangePassword(string currentPassword, PasswordHash newPasswordHash)
{
    if (PasswordHash == null)
        throw new ConflictException("Cannot change password for OAuth-only user. Set a password via account settings first.");

    if (!VerifyPassword(currentPassword))
        throw new DomainException("Current password is incorrect");

    PasswordHash = newPasswordHash;
    AddDomainEvent(new PasswordChangedEvent(Id));
}
```

- [ ] **Step 9: Run tests, verify GREEN**

```bash
dotnet test --filter "FullyQualifiedName~HandleOAuthCallback_DomainAggregateTests"
```

Expected: ALL PASS.

- [ ] **Step 10: Run full OAuth test suite (no regression)**

```bash
dotnet test --filter "FullyQualifiedName~HandleOAuthCallbackCommandHandlerTests"
```

Expected: pass.

- [ ] **Step 11: Commit fix**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/OAuth/HandleOAuthCallbackCommandHandler.cs \
        apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs

git commit -m "$(cat <<'EOF'
fix(auth): OAuth callback uses User aggregate (C3+I8)

C3: GenerateRandomPasswordHash() returned 32 random base64 bytes,
NOT a valid PBKDF2 hash. Replaced with PasswordHash=null marker for
OAuth-only users. ChangePassword on OAuth user now throws
ConflictException with clear message.

I8: HandleOAuthCallback was creating UserEntity directly, bypassing:
- Domain validation (Email/Role value objects)
- Domain events (UserCreatedViaOAuthEvent now emitted)
- Default values from User constructor (Tier, Language, etc.)

Fix: introduce User.CreateForOAuth() factory method as single
authoritative path for OAuth user creation. Emits new domain event
UserCreatedViaOAuthEvent for downstream listeners.

Refs: spec section 4 Group C — OAuth Integrity

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 6 (Commit #06): C4 — Cookie Role HMAC + Versioning (TDD)

**Files:**
- Modify: `apps/api/src/Api/Routing/CookieHelpers.cs`
- Modify: `apps/api/src/Api/Program.cs` (add data protection persistence)
- Create: `apps/api/tests/Api.Tests/Routing/CookieHelpersHmacTests.cs`
- Modify: `apps/web/src/middleware.ts` (auto-fetch /auth/me on missing v2)

- [ ] **Step 1: Write failing tests (RED)**

```csharp
using Api.Routing;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Xunit;

public class CookieHelpersHmacTests : AuthBoundedContextTestBase
{
    [Fact]
    public void WriteUserRoleCookie_ValueIsProtected_NotPlaintext()
    {
        var context = new DefaultHttpContext();
        // Inject DataProtection into context.RequestServices
        // ... setup

        CookieHelpers.WriteUserRoleCookie(context, "admin", DateTime.UtcNow.AddDays(30));

        var cookie = context.Response.Headers["Set-Cookie"].Single();
        cookie.Should().NotContain("=admin;");  // value is not plaintext
        cookie.Should().Contain("meepleai_user_role_v2=");  // v2 used
    }

    [Fact]
    public void WriteUserRoleCookie_DeletesV1Cookie()
    {
        // After write v2, verify v1 is also deleted via Set-Cookie expires
        // ...
    }

    [Fact]
    public void ReadUserRoleCookie_TamperedV2_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = "meepleai_user_role_v2=TAMPERED_VALUE";
        var role = CookieHelpers.ReadUserRoleCookie(context);
        role.Should().BeNull();
    }

    [Fact]
    public void ReadUserRoleCookie_ValidV2_ReturnsRole()
    {
        // Write then read — should round-trip correctly
    }

    [Fact]
    public void ReadUserRoleCookie_FallbackV1WithinGracePeriod_ReturnsRole()
    {
        // Set v1 cookie only, no v2 — should still return value
        // (mock current date < sunset date)
    }

    [Fact]
    public void ReadUserRoleCookie_WrongPurpose_ReturnsNull()
    {
        // Encrypt with different purpose, verify rejection
    }
}
```

- [ ] **Step 2: Run, verify FAIL**

Expected: tests fail — `ReadUserRoleCookie` doesn't exist, `WriteUserRoleCookie` writes plaintext.

- [ ] **Step 3: Commit failing tests**

```bash
git add apps/api/tests/Api.Tests/Routing/CookieHelpersHmacTests.cs
git commit -m "test(auth): add failing tests for C4 cookie HMAC versioning"
```

- [ ] **Step 4: Update CookieHelpers.cs**

Edit `apps/api/src/Api/Routing/CookieHelpers.cs:14-148` — replace section:

```csharp
private const string UserRoleCookieNameV1 = "meepleai_user_role";
private const string UserRoleCookieNameV2 = "meepleai_user_role_v2";
private const string DataProtectionPurpose = "MeepleAi.UserRoleCookie.v2";

// Hardcoded sunset date for v1 grace period (7 days post-deploy)
private static readonly DateTime V1SunsetDate = new(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc);

public static void WriteUserRoleCookie(HttpContext context, string role, DateTime expiresAt)
{
    ArgumentNullException.ThrowIfNull(context);
    var protector = context.RequestServices
        .GetRequiredService<IDataProtectionProvider>()
        .CreateProtector(DataProtectionPurpose);
    var protectedRole = protector.Protect(role.ToLowerInvariant());

    var configuration = GetSessionCookieConfiguration(context);
    var options = BuildRoleCookieOptions(context, expiresAt);

    // Write v2
    context.Response.Cookies.Append(UserRoleCookieNameV2, protectedRole, options);

    // Delete v1 (lazy migration cleanup)
    var deleteOptions = new CookieOptions
    {
        Path = options.Path,
        Domain = options.Domain,
        Expires = DateTimeOffset.UnixEpoch
    };
    context.Response.Cookies.Delete(UserRoleCookieNameV1, deleteOptions);
}

public static string? ReadUserRoleCookie(HttpContext context)
{
    ArgumentNullException.ThrowIfNull(context);

    // Try v2 first
    if (context.Request.Cookies.TryGetValue(UserRoleCookieNameV2, out var v2Value) &&
        !string.IsNullOrWhiteSpace(v2Value))
    {
        try
        {
            var protector = context.RequestServices
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(DataProtectionPurpose);
            return protector.Unprotect(v2Value);
        }
        catch (CryptographicException)
        {
            return null;  // tampered or wrong purpose
        }
    }

    // Fallback v1 (grace period)
    if (DateTime.UtcNow < V1SunsetDate &&
        context.Request.Cookies.TryGetValue(UserRoleCookieNameV1, out var v1Value) &&
        !string.IsNullOrWhiteSpace(v1Value))
    {
        return v1Value.ToLowerInvariant();
    }

    return null;
}

public static void RemoveUserRoleCookie(HttpContext context)
{
    ArgumentNullException.ThrowIfNull(context);
    var configuration = GetSessionCookieConfiguration(context);
    var path = string.IsNullOrWhiteSpace(configuration.Path) ? "/" : configuration.Path;

    var options = new CookieOptions
    {
        HttpOnly = true,
        Path = path,
        Expires = DateTimeOffset.UnixEpoch
    };
    if (!string.IsNullOrWhiteSpace(configuration.Domain))
        options.Domain = configuration.Domain;

    context.Response.Cookies.Delete(UserRoleCookieNameV1, options);
    context.Response.Cookies.Delete(UserRoleCookieNameV2, options);
}

private static CookieOptions BuildRoleCookieOptions(HttpContext context, DateTime expiresAt)
{
    var configuration = GetSessionCookieConfiguration(context);
    var isHttps = context.Request.IsHttps;

    if (!isHttps && configuration.UseForwardedProto &&
        context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto) &&
        forwardedProto.Any(p => string.Equals(p, "https", StringComparison.OrdinalIgnoreCase)))
    {
        isHttps = true;
    }

    var secure = configuration.Secure ?? isHttps;
    var path = string.IsNullOrWhiteSpace(configuration.Path) ? "/" : configuration.Path;
    var sameSite = configuration.SameSite ?? SameSiteMode.Lax;

    var options = new CookieOptions
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = sameSite,
        Path = path,
        Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero)
    };

    if (!string.IsNullOrWhiteSpace(configuration.Domain))
        options.Domain = configuration.Domain;

    return options;
}
```

- [ ] **Step 5: Configure DataProtection persistence in Program.cs**

Edit `apps/api/src/Api/Program.cs` (search for existing DataProtection or add new):

```csharp
// Verify if exists; otherwise add:
services.AddDataProtection()
    .SetApplicationName("MeepleAi")
    .PersistKeysToFileSystem(new DirectoryInfo("/var/lib/meepleai/dataprotection-keys"))
    // For multi-instance: .PersistKeysToStackExchangeRedis(...)
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
```

For dev mode, persistence to filesystem is fine. For prod multi-instance, configure Redis (out of scope for this fix — verify infra/secrets/dataprotection.secret exists or create).

- [ ] **Step 6: Run tests, verify GREEN**

```bash
dotnet test --filter "FullyQualifiedName~CookieHelpersHmacTests"
```

Expected: ALL PASS.

- [ ] **Step 7: Update Next.js middleware (frontend)**

Edit `apps/web/src/middleware.ts` — add fallback logic:

```typescript
// If meepleai_user_role_v2 missing but meepleai_session present, fetch /auth/me to populate role
const v2Role = request.cookies.get('meepleai_user_role_v2');
const sessionCookie = request.cookies.get('meepleai_session');

if (!v2Role && sessionCookie) {
  // Server-side fetch /auth/me to refresh role
  // ... implementation
}
```

- [ ] **Step 8: Run frontend tests**

```bash
cd apps/web
pnpm test middleware
```

Expected: pass.

- [ ] **Step 9: Commit fix**

```bash
git add apps/api/src/Api/Routing/CookieHelpers.cs \
        apps/api/src/Api/Program.cs \
        apps/web/src/middleware.ts

git commit -m "$(cat <<'EOF'
fix(auth): cookie role HMAC with versioning lazy migration (C4)

The meepleai_user_role cookie was plaintext. Attacker with cookie-edit
ability (proxy, malicious extension) could escalate to admin role for
client-side UI gating.

Fix: HMAC protection via IDataProtectionProvider with cookie versioning:
- v1 (plaintext) deprecated, accepted for 7 days grace period
- v2 (HMAC) new format, written on every login/role change
- Lazy migration: read v2 first, fallback v1 within grace period
- After sunset date (2026-05-13), v1 cookies ignored

Avoids big-bang invalidation of existing user sessions.

DataProtection configured with PersistKeysToFileSystem for stable keys
across restarts. Multi-instance Redis persistence is future work.

Refs: spec section 4 Group D — Cookie Security

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 7 (Commit #07): C5 — Race Condition + Bootstrap Admin Token (TDD)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommand.cs` (add BootstrapToken field)
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommandHandler.cs`
- Modify: `apps/api/src/Api/Models/RegisterPayload.cs` (add BootstrapToken)
- Modify: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs:73-115` (pass BootstrapToken to command)
- Create: `infra/secrets/bootstrap_admin_token.secret.example`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommandHandler_RaceConditionTests.cs` (Testcontainers)

- [ ] **Step 1: Write failing tests (RED)**

```csharp
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.Registration;

public class RegisterCommandHandler_RaceConditionTests : AuthBoundedContextTestBase
{
    [Fact]
    public async Task Register_DuplicateEmail_5ConcurrentRequests_ExactlyOneSuccess()
    {
        // Arrange — same email for 5 concurrent registers
        var email = "race@test.com";
        var commands = Enumerable.Range(0, 5)
            .Select(_ => new RegisterCommand(email, "Password123!", "User", null, null, null, null))
            .ToArray();

        // Act — fire all in parallel
        var tasks = commands.Select(cmd => Task.Run(async () => {
            try { await Mediator.Send(cmd); return true; }
            catch (ConflictException) { return false; }
            catch (DomainException) { return false; }
        }));
        var results = await Task.WhenAll(tasks);

        // Assert — exactly one success, four conflicts
        results.Count(r => r).Should().Be(1);
        results.Count(r => !r).Should().Be(4);
    }

    [Fact]
    public async Task Register_WithBootstrapAdminToken_AssignsAdminRole()
    {
        // Arrange — config bootstrap token = "secret-token"
        // Act — RegisterCommand with BootstrapToken="secret-token"
        // Assert — user.Role = Admin
    }

    [Fact]
    public async Task Register_WithoutBootstrapToken_AssignsUserRole()
    {
        // Even if first user, no token → role = User
    }

    [Fact]
    public async Task Register_WithWrongBootstrapToken_AssignsUserRole_NotAdmin()
    {
        // Wrong token → no privilege escalation
    }

    [Fact]
    public async Task Register_FirstUser_WithoutBootstrapToken_AssignsUserRole_NotAdmin()
    {
        // Regression for removed "first user is admin" logic
    }

    [Fact]
    public async Task BootstrapAdminToken_ConstantTimeCompare_NoTimingLeak()
    {
        // Measure time for valid vs near-miss token, assert variance < threshold
        // (smoke test, not deterministic security proof)
    }
}
```

- [ ] **Step 2: Run, verify FAIL**

```bash
dotnet test --filter "FullyQualifiedName~RegisterCommandHandler_RaceConditionTests"
```

Expected: tests fail (current handler does HasAnyUsersAsync, no bootstrap token).

- [ ] **Step 3: Commit failing tests**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommandHandler_RaceConditionTests.cs
git commit -m "test(auth): add failing tests for C5 race condition + bootstrap admin"
```

- [ ] **Step 4: Add BootstrapToken to RegisterCommand**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommand.cs`:

```csharp
public record RegisterCommand(
    string Email,
    string Password,
    string DisplayName,
    string? Role,
    string? IpAddress,
    string? UserAgent,
    string? BootstrapToken  // NEW
) : ICommand<RegisterResponse>;
```

- [ ] **Step 5: Add BootstrapToken to RegisterPayload**

Edit `apps/api/src/Api/Models/RegisterPayload.cs`:

```csharp
public record RegisterPayload(
    string Email,
    string Password,
    string? DisplayName,
    string? BootstrapToken  // NEW
);
```

- [ ] **Step 6: Update endpoint to pass BootstrapToken**

Edit `apps/api/src/Api/Routing/AuthenticationEndpoints.cs:99-105`:

```csharp
var command = new DddRegisterCommand(
    Email: payload.Email,
    Password: payload.Password,
    DisplayName: displayName,
    Role: null,
    IpAddress: context.Connection.RemoteIpAddress?.ToString(),
    UserAgent: context.Request.Headers.UserAgent.ToString(),
    BootstrapToken: payload.BootstrapToken);  // NEW
```

- [ ] **Step 7: Refactor RegisterCommandHandler**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommandHandler.cs`:

```csharp
// Add to constructor:
private readonly IConfiguration _configuration;
private readonly IAuditLogger _auditLogger;  // Add when I10 lands

public RegisterCommandHandler(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IUnitOfWork unitOfWork,
    IEmailVerificationService emailVerificationService,
    IConfiguration configuration,
    ILogger<RegisterCommandHandler> logger)
{
    // ... existing assignments
    _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
}

public async Task<RegisterResponse> Handle(RegisterCommand command, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(command);

    Guard.AgainstNullOrWhiteSpace(command.DisplayName, nameof(command.DisplayName));
    Guard.AgainstNullOrWhiteSpace(command.Password, nameof(command.Password));
    Guard.AgainstTooShort(command.Password, nameof(command.Password), 8);

    var email = new Email(command.Email);

    // Determine role from bootstrap token (no race-prone HasAnyUsersAsync)
    var role = DetermineRoleFromBootstrapToken(command.BootstrapToken);

    var passwordHash = PasswordHash.Create(command.Password);
    var userId = Guid.NewGuid();
    var user = new User(
        id: userId,
        email: email,
        displayName: command.DisplayName.Trim(),
        passwordHash: passwordHash,
        role: role);

    user.SetVerificationGracePeriod(DateTime.UtcNow.AddDays(7));

    var sessionId = Guid.NewGuid();
    var sessionToken = SessionToken.Generate();
    var session = new Session(sessionId, userId, sessionToken, command.IpAddress, command.UserAgent);

    await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
    await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);

    try
    {
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException ex) when (IsUniqueViolation(ex, "Email"))
    {
        throw new ConflictException("Email is already registered");  // 409 instead of 500
    }

    // Email verification fire-and-forget (will be replaced by outbox in I5)
    try
    {
        await _emailVerificationService.SendVerificationEmailAsync(
            userId, email.Value, command.DisplayName.Trim(), cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to send verification email for user {UserId}", userId);
    }

    return new RegisterResponse(MapToUserDto(user), sessionToken.Value, session.ExpiresAt);
}

private async Task<Role> DetermineRoleFromBootstrapTokenAsync(string? providedToken, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(providedToken))
        return Role.User;

    var configuredToken = _configuration["Authentication:BootstrapAdminToken"];
    if (string.IsNullOrWhiteSpace(configuredToken))
        return Role.User;

    // Constant-time compare to prevent timing attacks
    var configBytes = Encoding.UTF8.GetBytes(configuredToken);
    var inputBytes = Encoding.UTF8.GetBytes(providedToken);
    if (configBytes.Length != inputBytes.Length)
        return Role.User;
    if (!CryptographicOperations.FixedTimeEquals(configBytes, inputBytes))
        return Role.User;

    // Single-use enforcement: check SystemConfiguration.BootstrapAdminCreated
    var sysConfig = await _db.SystemConfiguration.FirstOrDefaultAsync(ct).ConfigureAwait(false);
    if (sysConfig != null && sysConfig.BootstrapAdminCreated)
    {
        _logger.LogWarning("Bootstrap admin token used after admin already created — rejected");
        return Role.User;  // already used, ignore
    }

    // Mark as used atomically (will be saved with user creation in same transaction)
    if (sysConfig == null)
    {
        sysConfig = new SystemConfigurationEntity { Id = Guid.NewGuid() };
        _db.SystemConfiguration.Add(sysConfig);
    }
    sysConfig.BootstrapAdminCreated = true;
    sysConfig.BootstrapAdminCreatedAt = _timeProvider.GetUtcNow().UtcDateTime;

    return Role.Admin;
}

private static bool IsUniqueViolation(DbUpdateException ex, string column)
{
    if (ex.InnerException is Npgsql.PostgresException pgEx)
    {
        return pgEx.SqlState == "23505" &&  // unique_violation
               (pgEx.ConstraintName?.Contains(column, StringComparison.OrdinalIgnoreCase) ?? false);
    }
    return false;
}
```

- [ ] **Step 8: Create secret template**

Create `infra/secrets/bootstrap_admin_token.secret.example`:

```
# Bootstrap Admin Token — used ONLY for first admin registration
# Generate: openssl rand -base64 48
# Set in env: AUTHENTICATION__BOOTSTRAPADMINTOKEN=<value>
# After first admin created, set to empty string (defense in depth)
REPLACE_WITH_GENERATED_TOKEN
```

- [ ] **Step 9: Run tests, verify GREEN**

```bash
dotnet test --filter "FullyQualifiedName~RegisterCommandHandler_RaceConditionTests"
```

Expected: ALL PASS.

- [ ] **Step 10: Commit fix**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/ \
        apps/api/src/Api/Models/RegisterPayload.cs \
        apps/api/src/Api/Routing/AuthenticationEndpoints.cs \
        infra/secrets/bootstrap_admin_token.secret.example

git commit -m "$(cat <<'EOF'
fix(auth): race condition + bootstrap admin token (C5)

Two TOCTOU bugs fixed:
1. HasAnyUsersAsync + first-user-is-admin: race made multiple admins
   on concurrent first registrations
2. GetByEmailAsync check + insert: race let duplicate emails through

Fix:
- Replaced HasAnyUsersAsync with explicit BootstrapAdminToken from
  config (env: AUTHENTICATION__BOOTSTRAPADMINTOKEN). First admin
  registered with valid token. Subsequent admins via SuperAdmin assign.
- Catch DbUpdateException on SaveChanges, throw ConflictException
  (409) instead of letting unique violation surface as 500.
- Constant-time token compare (CryptographicOperations.FixedTimeEquals)
  prevents timing attacks.

Audit log for BootstrapAdminCreated event will land with I10 commit.

Refs: spec section 4 Group E — Registration & Race Conditions

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 8 (Commit #08): C6 — 2FA Brute-Force Lockout DoS-Aware (TDD)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Entities/TempSessionEntity.cs` (FailedAttemptCount already added in #01)
- Modify: `apps/api/src/Api/Services/TempSessionService.cs` (track failures, invalidate at 5)
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler.cs:68` (move RecordSuccessfulLogin POST 2FA)
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/TwoFactor/Verify2FACommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/TwoFactorEndpoints.cs:111-115` (add IP rate limit)
- Modify: `apps/api/src/Api/Program.cs` (add AuthVerify2FA rate limit policy)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/TwoFactor/Verify2FACommandHandler_LockoutTests.cs`

- [ ] **Step 1: Write failing tests (RED)**

```csharp
public class Verify2FACommandHandler_LockoutTests : AuthBoundedContextTestBase
{
    [Fact]
    public async Task Verify2FA_5FailedAttempts_InvalidatesTempSession()
    {
        // Setup user with 2FA, create temp session
        // Submit 5 wrong codes
        // Assert: 6th attempt with VALID code is rejected (temp session invalidated)
    }

    [Fact]
    public async Task Verify2FA_AfterTempSessionInvalidated_RequiresNewLogin()
    {
        // Same setup, after invalidation
        // Try submit valid code → "Invalid or expired session"
    }

    [Fact]
    public async Task Verify2FA_IpRateLimit_BlocksAfter10Attempts()
    {
        // 10 verify attempts from same IP in <15min → 429
    }

    [Fact]
    public async Task Login_FailedLoginCounterNotResetUntil2FAVerified()
    {
        // Login OK + wrong 2FA → user.FailedLoginAttempts NOT reset
        // Login OK + valid 2FA → user.FailedLoginAttempts reset
    }

    [Fact]
    public async Task Verify2FA_FailedAttemptDuringLockout_DoesNotExtendLockout()
    {
        // After temp session invalidated, further attempts don't reset its TTL
    }

    [Fact]
    public async Task Verify2FA_BoundaryTiming_ValidCodeAcceptedExactlyAtUnlock()
    {
        // Lockout expires at T, valid code at T+1ms accepted
    }
}
```

- [ ] **Step 2: Run, verify FAIL**

- [ ] **Step 3: Commit failing tests**

```bash
git commit -m "test(auth): add failing tests for C6 2FA brute-force lockout"
```

- [ ] **Step 4: Refactor TempSessionService — split ValidateAndConsume into 3 methods**

**ARCHITECTURAL DECISION**: split `ValidateAndConsumeTempSessionAsync` into 3 distinct operations to enable failed-attempt tracking BEFORE consumption.

Edit `apps/api/src/Api/Services/ITempSessionService.cs`:

```csharp
public interface ITempSessionService
{
    Task<string> CreateTempSessionAsync(Guid userId, string? ipAddress = null);
    Task<Guid?> ValidateTempSessionAsync(string token, CancellationToken ct = default);
    Task ConsumeTempSessionAsync(string token, CancellationToken ct = default);
    Task<bool> RecordFailedAttemptAsync(string token, CancellationToken ct = default);
    Task CleanupExpiredSessionsAsync(CancellationToken ct = default);
}
```

Edit `apps/api/src/Api/Services/TempSessionService.cs` — replace `ValidateAndConsumeTempSessionAsync` with:

```csharp
/// <summary>
/// Validates temp session WITHOUT consuming it.
/// Returns userId if valid + not expired + not used + not max-failures, null otherwise.
/// </summary>
public async Task<Guid?> ValidateTempSessionAsync(string token, CancellationToken ct = default)
{
    const int MaxFailedAttempts = 5;
    var tokenHash = HashToken(token);
    var now = _timeProvider.GetUtcNow().UtcDateTime;

    var session = await _dbContext.TempSessions
        .FirstOrDefaultAsync(
            ts => ts.TokenHash == tokenHash && !ts.IsUsed && ts.ExpiresAt > now,
            ct).ConfigureAwait(false);

    if (session == null) return null;
    if (session.FailedAttemptCount >= MaxFailedAttempts) return null;  // locked

    return session.UserId;
}

/// <summary>
/// Marks temp session as used (single-use enforcement). Call on successful 2FA verify.
/// </summary>
public async Task ConsumeTempSessionAsync(string token, CancellationToken ct = default)
{
    var tokenHash = HashToken(token);
    using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct).ConfigureAwait(false);
    try
    {
        var session = await _dbContext.TempSessions
            .FirstOrDefaultAsync(ts => ts.TokenHash == tokenHash && !ts.IsUsed, ct)
            .ConfigureAwait(false);
        if (session != null)
        {
            session.IsUsed = true;
            session.UsedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
    catch (DbUpdateException)
    {
        await transaction.RollbackAsync(ct).ConfigureAwait(false);
        throw;
    }
}

/// <summary>
/// Records a failed 2FA attempt. Returns true if this attempt invalidated the session (max reached).
/// </summary>
public async Task<bool> RecordFailedAttemptAsync(string token, CancellationToken ct = default)
{
    const int MaxFailedAttempts = 5;
    var tokenHash = HashToken(token);

    using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct).ConfigureAwait(false);
    try
    {
        var session = await _dbContext.TempSessions
            .FirstOrDefaultAsync(ts => ts.TokenHash == tokenHash && !ts.IsUsed, ct)
            .ConfigureAwait(false);
        if (session == null) { await transaction.CommitAsync(ct); return true; }  // already invalid

        session.FailedAttemptCount++;
        bool invalidatedNow = session.FailedAttemptCount >= MaxFailedAttempts;
        if (invalidatedNow)
        {
            session.IsUsed = true;
            session.UsedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return invalidatedNow;
    }
    catch (DbUpdateException ex)
    {
        _logger.LogError(ex, "Failed to record 2FA failed attempt");
        await transaction.RollbackAsync(ct).ConfigureAwait(false);
        return false;
    }
}
```

Mark `ValidateAndConsumeTempSessionAsync` as `[Obsolete]` and have it call `ValidateTempSessionAsync + ConsumeTempSessionAsync` for backward compat (or remove if no other consumers).

- [ ] **Step 5: Update Verify2FACommandHandler — use new 3-step flow**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/TwoFactor/Verify2FACommandHandler.cs`:

```csharp
public async Task<Verify2FAResult> Handle(Verify2FACommand command, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(command);

    // Step 1: Validate temp session (does NOT consume)
    var userId = await _tempSessionService.ValidateTempSessionAsync(command.SessionToken, ct);
    if (userId == null)
    {
        return new Verify2FAResult { Success = false, ErrorMessage = "Invalid or expired session" };
    }

    var user = await _userRepository.GetByIdAsync(userId.Value, ct);
    if (user == null)
    {
        return new Verify2FAResult { Success = false, ErrorMessage = "User not found" };
    }

    // Step 2: Verify TOTP or backup code
    var isValid = _totpService.VerifyTotp(user.TotpSecret, command.Code) ||
                  user.HasUnusedBackupCode(HashBackupCode(command.Code));

    if (!isValid)
    {
        // Step 3a: Record failed attempt (may invalidate temp session at 5 failures)
        var nowInvalidated = await _tempSessionService.RecordFailedAttemptAsync(command.SessionToken, ct);
        var msg = nowInvalidated
            ? "Too many failed attempts. Please log in again."
            : "Invalid verification code";
        return new Verify2FAResult { Success = false, ErrorMessage = msg };
    }

    // Step 3b: Consume temp session (success path)
    await _tempSessionService.ConsumeTempSessionAsync(command.SessionToken, ct);

    // Mark backup code used if applicable
    if (user.HasUnusedBackupCode(HashBackupCode(command.Code)))
    {
        user.UseBackupCode(HashBackupCode(command.Code), _timeProvider.GetUtcNow().UtcDateTime);
    }

    // 2FA verified — NOW reset failed login counter
    user.RecordSuccessfulLogin();
    await _userRepository.UpdateAsync(user, ct);
    await _unitOfWork.SaveChangesAsync(ct);

    return new Verify2FAResult { Success = true, UserId = user.Id };
}
```

- [ ] **Step 6: Move RecordSuccessfulLogin from LoginCommandHandler**

Edit `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler.cs:68`:

```csharp
// REMOVE:
// user.RecordSuccessfulLogin();

// Counter reset is now done in Verify2FACommandHandler (after 2FA verified)
// or after session creation if 2FA not required (no-op if counter is already 0)
```

For non-2FA path, after session create:

```csharp
// If 2FA not required, reset counter here (immediately after session)
if (!user.RequiresTwoFactor())
{
    user.RecordSuccessfulLogin();
    await _userRepository.UpdateAsync(user, ct);
    await _unitOfWork.SaveChangesAsync(ct);
}
```

- [ ] **Step 7: Add AuthVerify2FA rate limit policy**

Edit `apps/api/src/Api/Program.cs`:

```csharp
services.AddRateLimiter(options => {
    // ... existing policies
    options.AddPolicy("AuthVerify2FA", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(15),
                TokensPerPeriod = 10,
                AutoReplenishment = true
            }));
});
```

- [ ] **Step 8: Apply policy in TwoFactorEndpoints**

Edit `apps/api/src/Api/Routing/TwoFactorEndpoints.cs:154` (Verify2FA endpoint):

```csharp
// ... existing endpoint definition
.RequireRateLimiting("AuthVerify2FA")  // NEW: IP-based rate limit
```

- [ ] **Step 9: Run tests, verify GREEN**

```bash
dotnet test --filter "FullyQualifiedName~Verify2FACommandHandler_LockoutTests"
```

- [ ] **Step 10: Commit fix**

```bash
git commit -m "$(cat <<'EOF'
fix(auth): 2FA brute-force lockout DoS-aware (C6)

Two related fixes:
1. RecordSuccessfulLogin was called before 2FA verify → failed 2FA
   attempts didn't count toward lockout. Brute force via re-login
   bypassed rate limit (3/min/session-token).
2. Naive lockout-by-userId would create DoS vector (attacker locks
   victim 5 attempts/15min indefinitely).

Fix:
- Move RecordSuccessfulLogin AFTER 2FA verified (in Verify2FACommandHandler)
- TempSession tracks FailedAttemptCount, invalidates at 5 → user must
  re-login (no permanent lockout, no DoS vector)
- IP-based rate limit (10/15min) on /auth/2fa/verify endpoint
  (additional layer beyond per-session-token limit)

Refs: spec section 4 Group B — Login & Lockout

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 9 (Commit #09): C7 — Password Change Session Revoke (TDD)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/UserProfile/ChangePasswordCommand.cs` (add CurrentSessionId)
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/UserProfile/ChangePasswordCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/PasswordChangedSessionRevocationHandler.cs`
- Modify: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs` or wherever change-password endpoint lives
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/UserProfile/ChangePasswordCommandHandler_SessionRevokeTests.cs`

- [ ] **Step 1: Write failing tests (RED)**

```csharp
public class ChangePasswordCommandHandler_SessionRevokeTests : AuthBoundedContextTestBase
{
    [Fact]
    public async Task ChangePassword_RevokesAllOtherSessions_KeepsCurrent()
    {
        // Setup: user with 3 sessions (A, B, C). Current = A.
        // Act: ChangePassword from session A
        // Assert: B and C revoked, A still valid
    }

    [Fact]
    public async Task ChangePassword_WithIncludeCurrentFlag_RevokesAllSessions()
    {
        // For "I think I'm compromised" UX flag
    }

    [Fact]
    public async Task ChangePassword_PublishesPasswordChangedEvent()
    {
        // Use IPublisher mock or domain event capture, verify event published
    }

    [Fact]
    public async Task RevokedSession_ApiCall_Returns401()
    {
        // After revoke, call /auth/me with old cookie → 401
    }
}
```

- [ ] **Step 2: Run, verify FAIL**

- [ ] **Step 3: Commit failing tests**

```bash
git commit -m "test(auth): add failing tests for C7 password change session revoke"
```

- [ ] **Step 4: Add CurrentSessionId to ChangePasswordCommand**

```csharp
public record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword,
    Guid? CurrentSessionId,    // NEW: exclude this session from revoke
    bool IncludeCurrentInRevoke // NEW: opt-in to revoke current too
) : ICommand;
```

- [ ] **Step 5: Create PasswordChangedAuditLogHandler (NOT session revocation!)**

**ARCHITECTURAL DECISION**: Session revocation happens in `ChangePasswordCommandHandler` directly (atomic with password update + has access to CurrentSessionId). The event handler does NOT duplicate this work.

The `PasswordChangedEvent` handler is reserved for cross-cutting concerns: audit logging (when I10 lands), email notification (when I5 outbox lands).

For Phase B (this plan), we add a STUB handler that just logs:

```csharp
using Api.BoundedContexts.Authentication.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles PasswordChangedEvent for cross-cutting concerns (audit, notifications).
/// Session revocation is NOT done here (handled atomically in ChangePasswordCommandHandler
/// to access CurrentSessionId for exclusion).
/// Future: I10 will add audit log persistence; I5 will add email notification.
/// </summary>
internal class PasswordChangedEventHandler : INotificationHandler<PasswordChangedEvent>
{
    private readonly ILogger<PasswordChangedEventHandler> _logger;

    public PasswordChangedEventHandler(ILogger<PasswordChangedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PasswordChangedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation("PasswordChanged event received for user {UserId}", notification.UserId);
        // TODO I10: persist audit log entry
        // TODO I5: enqueue email notification "Your password was changed"
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 6: Update ChangePasswordCommandHandler**

```csharp
public async Task Handle(ChangePasswordCommand command, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(command);
    if (string.IsNullOrWhiteSpace(command.NewPassword))
        throw new ValidationException(nameof(command.NewPassword), "New password cannot be empty");

    var user = await _userRepository.GetByIdAsync(command.UserId, ct);
    if (user == null) throw new DomainException("User not found");

    var newPasswordHash = PasswordHash.Create(command.NewPassword);
    user.ChangePassword(command.CurrentPassword, newPasswordHash);  // emits PasswordChangedEvent

    await _userRepository.UpdateAsync(user, ct);

    // Revoke all sessions EXCEPT current (unless IncludeCurrentInRevoke)
    if (command.IncludeCurrentInRevoke || command.CurrentSessionId == null)
    {
        await _sessionRepository.RevokeAllByUserIdAsync(command.UserId, ct);
    }
    else
    {
        await _sessionRepository.RevokeAllByUserIdExceptAsync(
            command.UserId, command.CurrentSessionId.Value, ct);
    }

    await _unitOfWork.SaveChangesAsync(ct);  // commits user update + session revocations
    // Event handler PasswordChangedSessionRevocationHandler will run as side effect
    // (its action is now redundant — refactor: keep handler for audit log only)
}
```

Note: refactor handler to be just "log event" since command handler does revocation directly. Cleaner: only command handler does revocation, handler does audit logging when I10 lands.

- [ ] **Step 7: Update endpoint to pass CurrentSessionId**

Find change-password endpoint (likely in UserProfile endpoints or AuthEndpoints):

```csharp
group.MapPost("/auth/change-password", async (
    ChangePasswordPayload payload,
    HttpContext context,
    IMediator mediator,
    CancellationToken ct) =>
{
    var (authenticated, session, error) = context.TryGetActiveSession();
    if (!authenticated) return error!;

    var command = new ChangePasswordCommand(
        UserId: session.User!.Id,
        CurrentPassword: payload.CurrentPassword,
        NewPassword: payload.NewPassword,
        CurrentSessionId: session.SessionId,
        IncludeCurrentInRevoke: payload.LogoutEverywhere ?? false);

    await mediator.Send(command, ct);
    return Results.Ok(new { message = "Password changed. Other devices logged out." });
});
```

- [ ] **Step 8: Run tests, verify GREEN**

- [ ] **Step 9: Commit fix**

```bash
git commit -m "$(cat <<'EOF'
fix(auth): password change revokes other sessions (C7)

Password change now revokes all other user sessions (keeping current
by default). Optional flag IncludeCurrentInRevoke for "I think I'm
compromised" scenarios where user wants to logout everywhere.

Implementation:
- ChangePasswordCommand carries CurrentSessionId from endpoint
- Command handler does revocation atomically with password update
- PasswordChangedEvent still emitted (for future audit log handler)
- Returned to user with clear message

Refs: spec section 4 Group A — Session Token Hash & Lifecycle

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Task 10 (Commit #10): C8 — CSRF Protection (TDD)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Filters/AntiforgeryEndpointFilter.cs`
- Modify: `apps/api/src/Api/Program.cs` (AddAntiforgery + UseAntiforgery)
- Modify: All state-changing auth endpoints (apply filter)
- Modify: `apps/web/src/lib/api/clients/authClient.ts` (send CSRF token)
- Create: `apps/web/src/lib/api/csrf.ts`
- Create: `apps/api/tests/Api.Tests/Infrastructure/Filters/AntiforgeryEndpointFilterTests.cs`

- [ ] **Step 1: Write failing tests (RED)**

```csharp
public class AntiforgeryEndpointFilterTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task ChangePassword_WithoutCsrfToken_Returns400()
    {
        var client = _factory.CreateClient();
        // Login first to get session
        var resp = await client.PostAsJsonAsync("/api/v1/auth/change-password", new {
            currentPassword = "old", newPassword = "newpass123"
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("CSRF");
    }

    [Fact]
    public async Task ChangePassword_WithValidCsrfToken_Succeeds()
    {
        // Login, fetch CSRF token from /auth/csrf-token, send in X-XSRF-TOKEN header
    }

    [Fact]
    public async Task RevokeAllSessions_WithoutCsrfToken_Returns400()
    {
        // ... similar
    }

    [Fact]
    public async Task OAuthUnlink_WithoutCsrfToken_Returns400()
    {
        // ... similar
    }
}
```

- [ ] **Step 2: Run, verify FAIL**

- [ ] **Step 3: Commit failing tests**

- [ ] **Step 4: Configure AddAntiforgery in Program.cs**

```csharp
services.AddAntiforgery(opt =>
{
    opt.HeaderName = "X-XSRF-TOKEN";
    opt.Cookie.Name = "X-XSRF-TOKEN";
    opt.Cookie.HttpOnly = false;  // JS reads cookie to send in header (double-submit pattern)
    opt.Cookie.SameSite = SameSiteMode.Lax;
    opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// In middleware pipeline (after UseSessionAuthentication):
app.UseAntiforgery();
```

- [ ] **Step 5: Create AntiforgeryEndpointFilter**

```csharp
using Microsoft.AspNetCore.Antiforgery;

namespace Api.Infrastructure.Filters;

public class AntiforgeryEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new { error = "CSRF token validation failed" });
        }
        return await next(context);
    }
}
```

- [ ] **Step 6: Apply filter to state-changing endpoints**

In `AuthenticationEndpoints.cs`, `OAuthEndpoints.cs`, `TwoFactorEndpoints.cs`:

```csharp
group.MapPost("/auth/change-password", ...)
    .AddEndpointFilter<AntiforgeryEndpointFilter>();

group.MapPost("/auth/sessions/revoke-all", ...)
    .AddEndpointFilter<AntiforgeryEndpointFilter>();

// And: /auth/2fa/disable, /auth/oauth/{provider}/unlink, /auth/logout-all-devices
```

- [ ] **Step 7: Add /auth/csrf-token endpoint**

```csharp
group.MapGet("/auth/csrf-token", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Json(new { token = tokens.RequestToken });
});
```

- [ ] **Step 8: Frontend — read and send token**

Create `apps/web/src/lib/api/csrf.ts`:

```typescript
export function getCsrfToken(): string | null {
  const cookie = document.cookie.split('; ').find(c => c.startsWith('X-XSRF-TOKEN='));
  return cookie ? decodeURIComponent(cookie.split('=')[1]) : null;
}

export async function ensureCsrfToken(): Promise<string> {
  let token = getCsrfToken();
  if (!token) {
    await fetch('/api/v1/auth/csrf-token', { credentials: 'include' });
    token = getCsrfToken();
    if (!token) throw new Error('Failed to obtain CSRF token');
  }
  return token;
}
```

Modify `apps/web/src/lib/api/clients/authClient.ts` to include `X-XSRF-TOKEN` header on POST/PUT/DELETE:

```typescript
async function fetchWithCsrf(url: string, options: RequestInit = {}) {
  const csrfToken = await ensureCsrfToken();
  return fetch(url, {
    ...options,
    headers: {
      ...options.headers,
      'X-XSRF-TOKEN': csrfToken
    },
    credentials: 'include'
  });
}
```

Use `fetchWithCsrf` for changePassword, revokeAllSessions, disable2FA, unlinkOAuth.

- [ ] **Step 9: Run backend tests, verify GREEN**

```bash
dotnet test --filter "FullyQualifiedName~AntiforgeryEndpointFilterTests"
```

- [ ] **Step 10: Add frontend unit tests for CSRF helper**

Create `apps/web/src/lib/api/__tests__/csrf.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { getCsrfToken, ensureCsrfToken } from '../csrf';

describe('csrf', () => {
  beforeEach(() => {
    document.cookie = '';
    vi.restoreAllMocks();
  });

  it('getCsrfToken returns null when cookie absent', () => {
    expect(getCsrfToken()).toBeNull();
  });

  it('getCsrfToken returns token when cookie present', () => {
    document.cookie = 'X-XSRF-TOKEN=abc123';
    expect(getCsrfToken()).toBe('abc123');
  });

  it('getCsrfToken decodes URL-encoded value', () => {
    document.cookie = 'X-XSRF-TOKEN=' + encodeURIComponent('abc/123+xyz=');
    expect(getCsrfToken()).toBe('abc/123+xyz=');
  });

  it('ensureCsrfToken fetches from /auth/csrf-token if missing', async () => {
    const fetchMock = vi.fn().mockImplementation(async () => {
      // Simulate server setting cookie
      document.cookie = 'X-XSRF-TOKEN=fetched-token';
      return new Response('{}');
    });
    global.fetch = fetchMock;

    const token = await ensureCsrfToken();
    expect(token).toBe('fetched-token');
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/auth/csrf-token', { credentials: 'include' });
  });

  it('ensureCsrfToken throws when fetch fails to set cookie', async () => {
    global.fetch = vi.fn().mockResolvedValue(new Response('{}'));
    await expect(ensureCsrfToken()).rejects.toThrow('Failed to obtain CSRF token');
  });
});
```

Create `apps/web/src/lib/api/clients/__tests__/authClient.csrf.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { authClient } from '../authClient';

describe('authClient CSRF integration', () => {
  beforeEach(() => {
    document.cookie = 'X-XSRF-TOKEN=test-token';
  });

  it('changePassword sends X-XSRF-TOKEN header', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('{}'));
    global.fetch = fetchMock;

    await authClient.changePassword({ currentPassword: 'old', newPassword: 'newpass1234567' });

    const call = fetchMock.mock.calls[0];
    const headers = call[1].headers as Record<string, string>;
    expect(headers['X-XSRF-TOKEN']).toBe('test-token');
  });

  it('revokeAllSessions sends X-XSRF-TOKEN header', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('{}'));
    global.fetch = fetchMock;

    await authClient.revokeAllSessions({ includeCurrentSession: false });

    const call = fetchMock.mock.calls[0];
    const headers = call[1].headers as Record<string, string>;
    expect(headers['X-XSRF-TOKEN']).toBe('test-token');
  });
});
```

- [ ] **Step 11: Run frontend tests**

```bash
cd apps/web
pnpm test csrf
pnpm test authClient.csrf
```

Expected: ALL PASS.

- [ ] **Step 12: Run E2E test**

```bash
pnpm test:e2e auth-flow.spec.ts
```

Expected: scenario change-password ancora passa con nuovo CSRF flow.

- [ ] **Step 13: Commit fix**

```bash
git commit -m "$(cat <<'EOF'
fix(auth): CSRF protection on state-changing endpoints (C8)

ASP.NET Core's IAntiforgery used via custom AntiforgeryEndpointFilter.
Double-submit cookie pattern (cookie X-XSRF-TOKEN, header X-XSRF-TOKEN).

Endpoints protected:
- POST /auth/change-password
- POST /auth/sessions/revoke-all
- POST /auth/2fa/disable
- DELETE /auth/oauth/{provider}/unlink
- POST /auth/logout-all-devices

Frontend authClient updated to fetch+send token on each protected call.
GET /auth/csrf-token endpoint for initial token bootstrap.

SameSite=Lax on session cookie was insufficient — top-level form POSTs
from cross-origin pages could still trigger state changes.

Refs: spec section 4 Group I — Security Hardening (NEW v1.1)

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## Final Verification (Phase A + B Complete)

- [ ] **Step 1: Run full test suite**

```bash
cd apps/api/src/Api
dotnet test --filter "BoundedContext=Authentication" -v normal
```

Expected: ALL TESTS PASS (~35-40 new tests + all existing).

- [ ] **Step 2: Run coverage report**

```bash
dotnet test --filter "BoundedContext=Authentication" \
  /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura \
  /p:Threshold=90 /p:ThresholdType=branch /p:ThresholdStat=total
```

Expected: ≥90% branch coverage on Authentication BC.

- [ ] **Step 3: Frontend tests**

```bash
cd apps/web
pnpm test
pnpm typecheck
pnpm lint
```

Expected: all pass.

- [ ] **Step 4: E2E test**

```bash
pnpm test:e2e
```

Expected: 4 spec Playwright pass (auth-flow, oauth-flow, 2fa-recovery, session-revocation).

- [ ] **Step 5: Push branch + open PR**

```bash
git push -u origin hotfix/auth-flow-security-fixes-2026-05-06
```

Open draft PR (do NOT merge until Phase C and D complete):

```bash
gh pr create --draft --base main --title "hotfix(auth): security fixes Phase A+B (C1-C8)" \
  --body "$(cat <<'EOF'
## Summary

Implementa fix per i 7 bug critici originali (C1-C7) + 1 critical aggiunto da review (C8 CSRF) + foundation work (commits #01-#02).

**Scope**: Phase A + B di un PR multi-fase. Phase C (Important Hardening) e Phase D (Code Quality) seguiranno in commit successivi su questo stesso branch prima del merge.

## Issues fixate

### Phase A — Foundation
- [x] #01: SessionTokenHasher utility + EF migration consolidata (PasswordHash nullable, AuditLog table, EmailOutbox, TempSession.FailedAttemptCount)
- [x] #02: Test infrastructure (AuthBoundedContextTestBase, BenchmarkDotNet baseline) + CQRS /auth/me

### Phase B — Critical Security Fixes (TDD)
- [x] C1 (#03): Hash mismatch in /auth/session/extend, /auth/sessions/revoke-all
- [x] C2 (#04): Login uses CanAuthenticate() instead of IsSuspended
- [x] C3+I8 (#05): OAuth uses User.CreateForOAuth aggregate factory
- [x] C4 (#06): Cookie role HMAC with versioning lazy migration
- [x] C5 (#07): Race condition + bootstrap admin token (constant-time)
- [x] C6 (#08): 2FA brute-force lockout DoS-aware
- [x] C7 (#09): Password change revokes other sessions
- [x] C8 (#10): CSRF protection on state-changing endpoints (NEW from review)

## Test plan

- [ ] All 35-40 new tests pass
- [ ] Authentication BC coverage ≥90% branch
- [ ] Existing tests still pass (no regression)
- [ ] E2E auth-flow.spec.ts passes
- [ ] Performance benchmark P95 login < 250ms (baseline before I7 lands)

## Spec

`docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Open Questions / Follow-up

Phase A+B addresses all 7 original critical issues + C8 from review. **Not yet in this plan**:
- Phase C (Important Hardening): I1, I2, I3, I4, I5, I6, I7, I9, I10, I11
- Phase D (Code Quality): R1, R2, R3, R4, R5, R6

Phase C/D plans will be created after Phase A+B PR is reviewed and ready to merge. Same branch, additional commits.

After Phase C/D plans complete, full PR will have ~27 commits and be ready for staging deployment.
