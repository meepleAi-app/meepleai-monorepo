# BE-2 #1589 — `GET /agents?scope=my-library` Library-Scoped Agents Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `?scope=my-library` discriminator to the existing `GET /agents` endpoint so it returns agents whose `GameId ∈ caller's library` PLUS system agents (`GameId IS NULL`). The existing global `GET /agents` (no `scope` param) keeps its current behavior unchanged. Unblocks the `agents` tab in #1592 Phase 2b.

**Architecture:** Option A — extend the existing `GetAllAgentsQuery`/handler/endpoint (not a new query). Two new optional fields (`Scope`, `ScopeUserId`); the handler branches when `Scope == "my-library"`, fetching the caller's library gameIds via `IUserLibraryRepository.GetUserGamesAsync` and filtering the already-loaded agent list in-memory (consistent with the handler's existing in-memory Type/GameId filtering). A new `GetAllAgentsQueryValidator` constrains the new fields (auto-runs via the MediatR validation pipeline, confirmed active in #1588). `scope=null` default = global behavior verbatim.

**Tech Stack:** .NET 9 · MediatR · FluentValidation · EF Core (PostgreSQL) · xUnit · Testcontainers Postgres · Moq (existing unit tests)

**Key decisions (from /sc:spec-panel — issuecomment-4556012113):**
- Option A (extend), new fields `Scope` + `ScopeUserId` (NOT reusing the unused `OwnedByUserId`).
- System agents included via `!a.GameId.HasValue` (no explicit OR).
- Library gameIds via `GetUserGamesAsync(userId).Select(e => e.GameId).Distinct()`.
- userId extracted in the endpoint only when `scope=my-library`, via `session.Principal.Subject.Id`.

**⚠️ Brownfield risk:** Adding `IUserLibraryRepository` to `GetAllAgentsQueryHandler`'s constructor breaks the compilation of the existing mock-based `GetAllAgentsQueryHandlerTests` (they call the 2-arg ctor). Those constructor calls MUST be updated in the same commit as the handler change (Task 2). The new validator must not break OTHER `GetAllAgentsQuery` callers (e.g. `GET /games/{id}/agents`) — it only constrains the new fields (default null → valid), so existing constructions pass.

---

## File Structure

| File | Responsibility | Status |
|---|---|---|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQuery.cs` | +`Scope`, +`ScopeUserId` fields | Modify |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryValidator.cs` | New validator (Scope allowlist + ScopeUserId required when scoped) | Create |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryHandler.cs` | +`IUserLibraryRepository` DI + scope branch | Modify |
| `apps/api/src/Api/Routing/AgentsEndpoints.cs` | +`?scope=` param + conditional userId extraction | Modify |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryValidatorTests.cs` | Validator unit tests | Create |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/GetAllAgentsQueryHandlerTests.cs` | Update existing ctor calls (3rd mock) + add scope branch unit tests | Modify |
| `apps/api/tests/Api.Tests/Integration/.../AgentsEndpointsIntegrationTests.cs` | Add scope integration tests (Testcontainers) | Modify (or create if absent) |

---

## Verified reference facts (from BE exploration — do not re-discover)

- `GetAllAgentsQuery` (current): `record GetAllAgentsQuery(bool? ActiveOnly = null, string? Type = null, Guid? GameId = null, Guid? OwnedByUserId = null) : IRequest<List<AgentDto>>`. `OwnedByUserId` is **unused** (#4914 deferred).
- `GetAllAgentsQueryHandler`: ctor `(IAgentDefinitionRepository repository, ISharedGameRepository sharedGameRepository)`. Handle: `GetAllActiveAsync()`/`GetAllAsync()` → in-memory Type filter → in-memory GameId filter → bulk game-name fetch → `MapToDto`. Uses `IRequestHandler<,>` (predates the `IQueryHandler` convention — keep `IRequestHandler` to match the file, do NOT switch).
- `AgentDefinition.GameId` is `Guid?` (null = system agent).
- `IUserLibraryRepository.GetUserGamesAsync(Guid userId, GameStateType? state = null, CancellationToken ct = default) : Task<IReadOnlyList<UserLibraryEntry>>`.
- `UserLibraryEntry.GameId` is `Guid` (non-null, the SharedGameId alias).
- Endpoint `GET /agents` (`AgentsEndpoints.cs:127-159`): `[FromQuery] bool? activeOnly, [FromQuery] string? type`, `context.TryGetAuthenticatedUser()` → `(authenticated, session, error)`, `.RequireAuthenticatedUser()`. Currently discards the session (`_`).
- `TryGetAuthenticatedUser()` returns `(bool IsAuthenticated, SessionStatusDto? Session, IResult? ErrorResult)`; when authenticated, `Session.Principal.Subject.Id` is the caller's `Guid` userId (guaranteed non-null by the helper).
- No existing validator for `GetAllAgentsQuery`. The MediatR validation pipeline auto-runs any registered `AbstractValidator<T>` (confirmed in #1588: invalid input → 422 via `ApiExceptionHandlerMiddleware`).

---

### Task 1: Add `Scope` + `ScopeUserId` to the query + a validator

**Files:**
- Modify: `GetAllAgentsQuery.cs`
- Create: `GetAllAgentsQueryValidator.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryValidatorTests.cs`

- [ ] **Step 1: Write the failing validator tests**

`GetAllAgentsQueryValidatorTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAllAgentsQueryValidatorTests
{
    private readonly GetAllAgentsQueryValidator _sut = new();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Global_query_no_scope_passes() =>
        _sut.TestValidate(new GetAllAgentsQuery()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Global_query_with_filters_no_scope_passes() =>
        _sut.TestValidate(new GetAllAgentsQuery(ActiveOnly: true, Type: "Tutor"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Scope_my_library_with_userId_passes() =>
        _sut.TestValidate(new GetAllAgentsQuery(Scope: "my-library", ScopeUserId: UserId))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("global")]
    [InlineData("all")]
    [InlineData("mine")]
    public void Scope_unknown_value_fails(string scope) =>
        _sut.TestValidate(new GetAllAgentsQuery(Scope: scope, ScopeUserId: UserId))
            .ShouldHaveValidationErrorFor(x => x.Scope);

    [Fact]
    public void Scope_my_library_without_userId_fails() =>
        _sut.TestValidate(new GetAllAgentsQuery(Scope: "my-library", ScopeUserId: null))
            .ShouldHaveValidationErrorFor(x => x.ScopeUserId);

    [Fact]
    public void Scope_my_library_with_empty_userId_fails() =>
        _sut.TestValidate(new GetAllAgentsQuery(Scope: "my-library", ScopeUserId: Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.ScopeUserId);
}
```

If `TestCategories.Unit` isn't the constant (grep `apps/api/tests/Api.Tests/` for `TestCategories.Unit` — confirmed to exist in #1588), use the literal `"Unit"`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test --filter "FullyQualifiedName~GetAllAgentsQueryValidatorTests"`
Expected: FAIL — `Scope`/`ScopeUserId` not on the record, `GetAllAgentsQueryValidator` doesn't exist; compile error.

- [ ] **Step 3: Add the fields to `GetAllAgentsQuery.cs`**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get all agents with optional filtering.
/// Issue 866: AI Agents Entity and Configuration
/// Issue #4914: support gameId + userId filter for user-owned agents
/// Issue #1589 (BE-2): support scope=my-library — agents whose GameId is in the
/// caller's library plus system agents (GameId == null).
/// </summary>
internal record GetAllAgentsQuery(
    bool? ActiveOnly = null,
    string? Type = null,
    Guid? GameId = null,
    Guid? OwnedByUserId = null,
    string? Scope = null,
    Guid? ScopeUserId = null
) : IRequest<List<AgentDto>>;
```

- [ ] **Step 4: Create `GetAllAgentsQueryValidator.cs`**

```csharp
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Validator for <see cref="GetAllAgentsQuery"/> (BE-2 #1589).
/// Only constrains the scope fields; the legacy global fields stay unconstrained
/// so existing callers (global GET /agents, GET /games/{id}/agents) are unaffected.
/// </summary>
internal sealed class GetAllAgentsQueryValidator : AbstractValidator<GetAllAgentsQuery>
{
    private static readonly string[] AllowedScopes = ["my-library"];

    public GetAllAgentsQueryValidator()
    {
        RuleFor(x => x.Scope)
            .Must(s => s is null || AllowedScopes.Contains(s, StringComparer.Ordinal))
            .WithMessage($"Scope must be one of: {string.Join(", ", AllowedScopes)} (or null for global).");

        RuleFor(x => x.ScopeUserId)
            .Must(id => id.HasValue && id.Value != Guid.Empty)
            .When(x => string.Equals(x.Scope, "my-library", StringComparison.Ordinal))
            .WithMessage("ScopeUserId is required (non-empty) when Scope is 'my-library'.");
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test --filter "FullyQualifiedName~GetAllAgentsQueryValidatorTests"`
Expected: PASS — all 8 cases (6 declarations, `[Theory]` expands to 8 total).

- [ ] **Step 6: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend && git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQuery.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryValidator.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryValidatorTests.cs
git commit -m "feat(agents): #1589 add scope fields + validator to GetAllAgentsQuery (BE-2)"
```

Header ≤ 80 chars. Pre-commit does NOT run dotnet tests — you MUST run Step 5 before committing.

---

### Task 2: Handler scope branch + update existing unit tests (atomic — constructor change)

**Files:**
- Modify: `GetAllAgentsQueryHandler.cs` (add `IUserLibraryRepository` DI + scope branch)
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/GetAllAgentsQueryHandlerTests.cs` (update ctor calls + add scope unit tests)

> **⚠️ Atomic commit**: adding the 3rd constructor parameter breaks every `new GetAllAgentsQueryHandler(...)` in the existing test file. The handler change + the test-constructor update land together (else the test project won't compile).

- [ ] **Step 1: Read the existing handler tests first**

Read `GetAllAgentsQueryHandlerTests.cs` fully. Note every `new GetAllAgentsQueryHandler(mockRepo.Object, mockSharedGame.Object)` construction site (there's typically a shared setup/SUT field). Note how `AgentDefinition` test instances are built (factory `AgentDefinition.Create(...)`) — you'll reuse this for the scope tests. Check whether `UserLibraryEntry` has a public factory/constructor for building test instances (grep `UserLibraryEntry.Create` or read the entity) — if it's hard to construct, the scope-correctness assertions live in the integration test (Task 4) and the unit tests here just verify the global path is unaffected + the repo is/ isn't called.

- [ ] **Step 2: Update the existing unit tests to the new constructor (RED → keep green)**

Add a `Mock<IUserLibraryRepository>` field; update the SUT construction to pass `.Object` as the 3rd arg. By default set up `GetUserGamesAsync` to return an empty list (so any test that doesn't exercise scope is unaffected):

```csharp
private readonly Mock<IUserLibraryRepository> _mockLibraryRepository = new();

// in setup / SUT construction:
_mockLibraryRepository
    .Setup(r => r.GetUserGamesAsync(It.IsAny<Guid>(), It.IsAny<GameStateType?>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(Array.Empty<UserLibraryEntry>());

var handler = new GetAllAgentsQueryHandler(
    _mockRepository.Object,
    _mockSharedGameRepository.Object,
    _mockLibraryRepository.Object);
```

(Match the actual field names + setup style in the existing file. Add the `using` for `IUserLibraryRepository` and `UserLibraryEntry` / `GameStateType`.)

- [ ] **Step 3: Run the existing tests to confirm they still fail to COMPILE (expected RED at this point — handler not yet 3-arg)**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet build`
Expected: FAIL — the test project references a 3-arg ctor that the handler doesn't have yet. (This confirms the test update is wired; now implement the handler.)

- [ ] **Step 4: Implement the handler scope branch**

Replace `GetAllAgentsQueryHandler.cs` Handle + ctor:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetAllAgentsQuery — returns lightweight AgentDto list for user-facing endpoints.
/// Used by GET /api/v1/games/{id}/agents (chat panel agent resolution) and GET /agents.
/// </summary>
/// <remarks>
/// Issue #660: AgentDto.GameName populated via single bulk lookup against SharedGame catalog
/// (avoids N+1 queries when listing all agents).
/// Issue #1589 (BE-2): scope=my-library filters to agents whose GameId is in the caller's
/// library (via IUserLibraryRepository) plus system agents (GameId == null).
/// </remarks>
internal sealed class GetAllAgentsQueryHandler
    : IRequestHandler<GetAllAgentsQuery, List<AgentDto>>
{
    private const string MyLibraryScope = "my-library";

    private readonly IAgentDefinitionRepository _repository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUserLibraryRepository _userLibraryRepository;

    public GetAllAgentsQueryHandler(
        IAgentDefinitionRepository repository,
        ISharedGameRepository sharedGameRepository,
        IUserLibraryRepository userLibraryRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sharedGameRepository = sharedGameRepository
            ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _userLibraryRepository = userLibraryRepository
            ?? throw new ArgumentNullException(nameof(userLibraryRepository));
    }

    public async Task<List<AgentDto>> Handle(
        GetAllAgentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agents = request.ActiveOnly == true
            ? await _repository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false)
            : await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // BE-2 #1589: scope=my-library — agents in the caller's library games + system agents.
        if (string.Equals(request.Scope, MyLibraryScope, StringComparison.Ordinal)
            && request.ScopeUserId.HasValue)
        {
            var libraryGames = await _userLibraryRepository
                .GetUserGamesAsync(request.ScopeUserId.Value, null, cancellationToken)
                .ConfigureAwait(false);

            var libraryGameIds = libraryGames
                .Select(e => e.GameId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToHashSet();

            agents = agents
                .Where(a => !a.GameId.HasValue || libraryGameIds.Contains(a.GameId.Value))
                .ToList();
        }

        // Apply optional Type filter
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            agents = agents
                .Where(a => string.Equals(a.Type.Value, request.Type, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Apply optional GameId filter
        if (request.GameId.HasValue)
        {
            agents = agents.Where(a => a.GameId == request.GameId.Value).ToList();
        }

        // Issue #660: Bulk-fetch game names (single query, no N+1)
        var gameIds = agents
            .Where(a => a.GameId.HasValue)
            .Select(a => a.GameId!.Value)
            .Distinct()
            .ToList();

        var gameNames = gameIds.Count > 0
            ? await _sharedGameRepository.GetNamesByIdsAsync(gameIds, cancellationToken).ConfigureAwait(false)
            : new Dictionary<Guid, string>();

        return agents.Select(a => MapToDto(a, gameNames)).ToList();
    }

    private static AgentDto MapToDto(
        Domain.Entities.AgentDefinition agent,
        IReadOnlyDictionary<Guid, string> gameNames)
    {
        var recentThreshold = DateTime.UtcNow.AddHours(-24);
        var idleThreshold = DateTime.UtcNow.AddDays(-7);

        var gameName = agent.GameId.HasValue
            && gameNames.TryGetValue(agent.GameId.Value, out var name)
                ? name
                : null;

        return new AgentDto(
            Id: agent.Id,
            Name: agent.Name,
            Type: agent.Type.Value,
            StrategyName: agent.Strategy.Name,
            StrategyParameters: agent.Strategy.Parameters,
            IsActive: agent.IsActive,
            CreatedAt: agent.CreatedAt,
            LastInvokedAt: agent.LastInvokedAt,
            InvocationCount: agent.InvocationCount,
            IsRecentlyUsed: agent.LastInvokedAt.HasValue && agent.LastInvokedAt.Value > recentThreshold,
            IsIdle: !agent.LastInvokedAt.HasValue || agent.LastInvokedAt.Value < idleThreshold,
            GameId: agent.GameId,
            GameName: gameName,
            CreatedByUserId: null
        );
    }
}
```

Note: `using Api.BoundedContexts.UserLibrary.Domain.Repositories;` — verify the namespace of `IUserLibraryRepository` (per exploration it's `Api.BoundedContexts.UserLibrary.Domain.Repositories`). If `GetUserGamesAsync`'s `state` parameter type isn't `GameStateType?`, adapt the call (pass `null` positionally or use a named arg matching the real signature).

- [ ] **Step 5: Add scope-branch unit tests** (append to `GetAllAgentsQueryHandlerTests.cs`)

If `UserLibraryEntry` is constructible in tests (factory exists), add:

```csharp
[Fact]
public async Task Handle_ScopeMyLibrary_ReturnsLibraryGamesPlusSystemAgents()
{
    var catan = Guid.NewGuid();
    var azul = Guid.NewGuid();
    var userId = Guid.NewGuid();
    // a1 GameId=catan (in library), a2 GameId=azul (NOT in library), a3 GameId=null (system)
    _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<AgentDefinition> { /* a1 catan */, /* a2 azul */, /* a3 null */ });
    _mockLibraryRepository
        .Setup(r => r.GetUserGamesAsync(userId, It.IsAny<GameStateType?>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<UserLibraryEntry> { /* entry GameId=catan */ });

    var result = await _sut.Handle(
        new GetAllAgentsQuery(Scope: "my-library", ScopeUserId: userId), CancellationToken.None);

    // a1 (catan, in library) + a3 (system) — NOT a2 (azul, not in library)
    result.Should().HaveCount(2);
    result.Select(a => a.GameId).Should().BeEquivalentTo(new Guid?[] { catan, null });
}

[Fact]
public async Task Handle_NoScope_DoesNotCallLibraryRepository()
{
    _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<AgentDefinition>());
    await _sut.Handle(new GetAllAgentsQuery(), CancellationToken.None);
    _mockLibraryRepository.Verify(
        r => r.GetUserGamesAsync(It.IsAny<Guid>(), It.IsAny<GameStateType?>(), It.IsAny<CancellationToken>()),
        Times.Never);
}
```

Fill the `AgentDefinition` fixtures using the same factory the existing tests use (read them in Step 1). If `UserLibraryEntry` is NOT cleanly constructible in a unit test, **skip the first scope unit test** and rely on the integration test (Task 4) for scope correctness — but keep `Handle_NoScope_DoesNotCallLibraryRepository` (it only needs the repo mock, no UserLibraryEntry construction). Document the choice in the commit body.

- [ ] **Step 6: Run the handler tests + build**

Run:
```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test --filter "FullyQualifiedName~GetAllAgentsQueryHandlerTests"
```
Expected: PASS — all existing tests (now 3-arg) + the new scope test(s).

- [ ] **Step 7: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend && git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryHandler.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/GetAllAgentsQueryHandlerTests.cs
git commit -m "feat(agents): #1589 scope GetAllAgentsQuery by user library (BE-2)"
```

---

### Task 3: Endpoint `?scope=` param + conditional userId

**Files:**
- Modify: `apps/api/src/Api/Routing/AgentsEndpoints.cs` (`MapGetAgentsEndpoint`)

- [ ] **Step 1: Update the endpoint**

Replace `MapGetAgentsEndpoint` (lines 127-159) with the `?scope=` variant:

```csharp
private static void MapGetAgentsEndpoint(RouteGroupBuilder group)
{
    group.MapGet("/agents", async (
        [FromQuery] bool? activeOnly,
        [FromQuery] string? type,
        [FromQuery] string? scope,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct) =>
    {
        var (authenticated, session, error) = context.TryGetAuthenticatedUser();
        if (!authenticated) return error!;

        // BE-2 #1589: scope=my-library needs the caller's userId; global path does not.
        Guid? scopeUserId = null;
        if (string.Equals(scope, "my-library", StringComparison.Ordinal))
        {
            scopeUserId = session!.Principal!.Subject!.Id;
        }

        var query = new GetAllAgentsQuery(
            ActiveOnly: activeOnly,
            Type: type,
            Scope: scope,
            ScopeUserId: scopeUserId
        );
        var agents = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(new
        {
            success = true,
            agents,
            count = agents.Count
        });
    })
    .RequireAuthenticatedUser()
    .Produces(200)
    .Produces(401)
    .Produces(422)
    .WithTags("Agents")
    .WithSummary("List all agents")
    .WithDescription("Returns all agents. ?scope=my-library filters to agents whose game is in the caller's library plus system agents; no scope returns all agents (global). Optional activeOnly + type filters.")
    .WithOpenApi();
}
```

Verify `session.Principal.Subject.Id` is the correct accessor for the `Guid` userId — the `TryGetAuthenticatedUser` helper guarantees `Session.Principal?.Subject != null` when authenticated, so the `!` null-forgiving operators are safe. If the `Subject.Id` type or path differs, adapt by reading `SessionStatusDto`/`Principal`/`Subject` (mirror how the kb-docs endpoint from #1588 — `KnowledgeBaseEndpoints.HandleListUserKbDocs` — extracts `userId`; it used `session.Principal?.Subject?.Id is Guid userId`).

- [ ] **Step 2: Build + run existing AgentsEndpoints tests**

Run:
```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet build && dotnet test --filter "FullyQualifiedName~AgentsEndpoints&Category=Unit"
```
Expected: build clean; existing endpoint unit tests (if any) still pass. (If no unit test class, the integration tests in Task 4 cover it.)

- [ ] **Step 3: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend && git add apps/api/src/Api/Routing/AgentsEndpoints.cs
git commit -m "feat(agents): #1589 add ?scope=my-library to GET /agents (BE-2)"
```

---

### Task 4: Endpoint integration tests (Testcontainers, AC1-AC5)

**Files:**
- Modify (or Create if absent): `apps/api/tests/Api.Tests/Integration/.../AgentsEndpointsIntegrationTests.cs`

- [ ] **Step 1: Read the existing AgentsEndpointsIntegrationTests pattern**

Find the file (`grep -rl "AgentsEndpointsIntegrationTests" apps/api/tests/`). Read it FULLY: the fixture (`SharedTestcontainersFixture` + `IntegrationWebApplicationFactory`?), how it seeds agents (`TestSessionHelper.SeedAgentDefinitionsAsync`?), how it creates an authenticated request (`TestSessionHelper.CreateUserSessionAsync` + `CreateAuthenticatedRequest`, per #1588 Task 6), and how it seeds a user library entry (look for a library-seed helper or seed `UserLibraryEntryEntity` directly via the DbContext scope). Report the real helper names before coding.

- [ ] **Step 2: Add the scope integration tests**

Add these tests (adapt fixture/helper names to what Step 1 found; mirror the AC scenarios in #1589):

```csharp
[Fact]
public async Task GetAgents_ScopeMyLibrary_ReturnsLibraryGamesPlusSystemAgents()
{
    // Seed: game Catan + Azul; agent a1(GameId=Catan), a2(GameId=Azul), a3(GameId=null system).
    // Seed caller's library with Catan only.
    // GET /agents?scope=my-library with authenticated caller.
    // Assert: response agents == { a1, a3 } (a2 excluded; a3 system included).
}

[Fact]
public async Task GetAgents_NoScope_ReturnsAllAgentsGlobal()
{
    // Same seed. GET /agents (no scope).
    // Assert: response agents == { a1, a2, a3 } (global, unchanged — AC2).
}

[Fact]
public async Task GetAgents_ScopeMyLibrary_EmptyLibrary_ReturnsOnlySystemAgents()
{
    // Seed a1(Catan), a3(null). Caller has empty library.
    // GET /agents?scope=my-library.
    // Assert: response agents == { a3 } (only system — AC4).
}

[Fact]
public async Task GetAgents_Unauthenticated_Returns401()
{
    // Anonymous client GET /agents?scope=my-library → 401 (AC3).
}
```

For seeding the user library: if there's no `SeedLibraryEntryAsync` helper, add a `UserLibraryEntryEntity` row directly via a DbContext scope (mirror how #1588's `ListUserKbDocsQueryHandlerIntegrationTests` seeded entities). The `UserLibraryEntryEntity` requires at least `UserId` + `SharedGameId` (+ any NOT NULL fields — check the entity/config and set defaults).

- [ ] **Step 3: Run the integration tests** (Docker required)

Run:
```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test --filter "FullyQualifiedName~AgentsEndpointsIntegrationTests"
```
Expected: PASS — the 4 new scope tests + any pre-existing tests in the class. Use long-running command pattern (Testcontainers ~60-90s).

- [ ] **Step 4: Final BE-2 suite check**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test --filter "FullyQualifiedName~GetAllAgents|FullyQualifiedName~AgentsEndpoints"
```
Expected: all GetAllAgents* (validator + handler) + AgentsEndpoints* green.

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend && git add apps/api/tests/Api.Tests/Integration/
git commit -m "test(agents): #1589 add ?scope=my-library integration tests (BE-2)"
```

---

## Acceptance check for BE-2

- **AC1** — scope returns library agents + system → Task 2 unit + Task 4 `GetAgents_ScopeMyLibrary_ReturnsLibraryGamesPlusSystemAgents`
- **AC2** — global unchanged → Task 4 `GetAgents_NoScope_ReturnsAllAgentsGlobal` + existing handler tests (now 3-arg) still green
- **AC3** — 401 unauthenticated → Task 4 `GetAgents_Unauthenticated_Returns401`
- **AC4** — empty library → only system → Task 4 `GetAgents_ScopeMyLibrary_EmptyLibrary_ReturnsOnlySystemAgents`
- **AC5** — Testcontainers integration covering all + global equivalence → Task 4

Final check:
```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test --filter "FullyQualifiedName~GetAllAgents|FullyQualifiedName~AgentsEndpoints"
```

---

## Out of scope (Phase 2b #1592 / follow-up)

- FE `useAgents({ scope: 'my-library' })` wiring → Phase 2b #1592 (agents tab).
- The unused `OwnedByUserId` field (#4914) — left as-is, not touched.
- Pagination of agents — `GET /agents` returns the full list (unchanged); not in scope.
