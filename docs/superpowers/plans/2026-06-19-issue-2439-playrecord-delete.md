# PlayRecord DELETE (soft-delete) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the missing `DELETE /api/v1/play-records/{id}` backend endpoint (soft-delete, creator-only) so the existing FE delete affordance stops 405-ing in production (issue #2439).

**Architecture:** The FE is already complete (`deleteRecord` in `play-records.api.ts` + `useDeleteRecord` hook). Only the backend is missing. We add `IsDeleted`/`DeletedAt` to the `PlayRecord` aggregate + a `SoftDelete()` domain method (mirroring the canonical `GameBook.SoftDelete()` pattern in the same bounded context), an EF `HasQueryFilter(e => !e.IsDeleted)` so deleted records vanish from history/stats/get, a `DeletePlayRecordCommand` + handler reusing the `CanEditAsync` creator-only authz, and a `MapDelete` endpoint. We also fix the wrong `Results.Created` Location header found during analysis (bonus finding).

**Tech Stack:** .NET 9 · MediatR (CQRS) · EF Core 9 + PostgreSQL · FluentValidation · xUnit + Testcontainers

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `BoundedContexts/GameManagement/Domain/Entities/PlayRecord.cs` | Add `IsDeleted`/`DeletedAt` + `SoftDelete()` | Modify |
| `BoundedContexts/GameManagement/Domain/Events/PlayRecordDeletedEvent.cs` | New domain event | Create |
| `Infrastructure/Entities/GameManagement/PlayRecordEntity.cs` | Persistence columns | Modify |
| `Infrastructure/EntityConfigurations/GameManagement/PlayRecordEntityConfiguration.cs` | Column mapping + query filter | Modify |
| `BoundedContexts/GameManagement/Infrastructure/Persistence/PlayRecordRepository.cs` | Map new fields; restore on reconstitution | Modify |
| `BoundedContexts/GameManagement/Application/Commands/PlayRecords/DeletePlayRecordCommand.cs` | Command DTO | Create |
| `.../Commands/PlayRecords/DeletePlayRecordCommandHandler.cs` | Handler (authz + soft-delete) | Create |
| `.../Application/Validators/PlayRecords/DeletePlayRecordCommandValidator.cs` | Validator | Create |
| `Routing/PlayRecordEndpoints.cs` | `MapDelete` + Location header fix | Modify |
| `Infrastructure/Migrations/` | `AddSoftDeleteToPlayRecord` migration | Generate |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/PlayRecordTests.cs` | Domain unit tests | Modify |
| `.../Application/PlayRecords/DeletePlayRecordCommandHandlerTests.cs` | Handler unit tests | Create |
| `.../Integration/GameManagement/PlayRecordCommandTests.cs` | DELETE integration tests | Modify |

**Test command (backend):** `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PlayRecord"`

---

### Task 1: Domain — `IsDeleted`/`DeletedAt` + `SoftDelete()`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PlayRecord.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/PlayRecordTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `PlayRecordTests.cs`:

```csharp
[Fact]
[Trait("Category", "Unit")]
public void SoftDelete_SetsFlagsAndRaisesEvent()
{
    var record = PlayRecord.CreateFreeForm(
        Guid.NewGuid(), "Catan", Guid.NewGuid(), DateTime.UtcNow.AddDays(-1),
        PlayRecordVisibility.Private, SessionScoringConfig.CreateDefault());
    record.ClearDomainEvents();

    record.SoftDelete();

    Assert.True(record.IsDeleted);
    Assert.NotNull(record.DeletedAt);
    Assert.Contains(record.DomainEvents, e => e is PlayRecordDeletedEvent);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SoftDelete_SetsFlagsAndRaisesEvent"`
Expected: FAIL — `PlayRecord` does not contain `SoftDelete`/`IsDeleted`/`DeletedAt` (compile error).

- [ ] **Step 3: Add properties + method to `PlayRecord.cs`**

After the `Audit` region properties (`UpdatedAt`, line ~43), add:

```csharp
    // Soft Delete (issue #2439 — mirrors GameBook.SoftDelete pattern)
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
```

After the `Archive()` method (line ~341), add:

```csharp
    /// <summary>
    /// Soft-deletes the record. Idempotent at the persistence layer because the
    /// EF query filter hides deleted rows (a second delete resolves to NotFound).
    /// </summary>
    public void SoftDelete(TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        IsDeleted = true;
        DeletedAt = now;
        UpdatedAt = now;

        AddDomainEvent(new PlayRecordDeletedEvent(Id, CreatedByUserId));
    }
```

> Note: `PlayRecordDeletedEvent` is created in Task 2. If executing strictly in order, this step will not compile until Task 2 is done — that is expected; the test in Step 2 already failed at compile. Run Task 2 next before re-running.

- [ ] **Step 4: Run test to verify it passes** (after Task 2)

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SoftDelete_SetsFlagsAndRaisesEvent"`
Expected: PASS

- [ ] **Step 5: Commit** (combined with Task 2 — see Task 2 Step 5)

---

### Task 2: `PlayRecordDeletedEvent`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/PlayRecordDeletedEvent.cs`

- [ ] **Step 1: Inspect a sibling event for the exact base type**

Run: `cat apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/PlayRecordCompletedEvent.cs`
Expected: shows the namespace + the domain-event base interface/record it implements (e.g. `IDomainEvent`). Mirror it exactly.

- [ ] **Step 2: Create the event file**

```csharp
// Mirror the base type observed in Step 1 (e.g. : IDomainEvent).
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Raised when a play record is soft-deleted (issue #2439).
/// </summary>
internal sealed record PlayRecordDeletedEvent(Guid RecordId, Guid DeletedByUserId) : IDomainEvent;
```

> If Step 1 shows a different base (e.g. `DomainEventBase` abstract record, or a different namespace), match that instead — do NOT invent `IDomainEvent` if the siblings use something else.

- [ ] **Step 3: Build to verify compile**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: BUILD SUCCEEDED (Task 1 `SoftDelete` now resolves the event type).

- [ ] **Step 4: Run Task 1 test to verify it passes**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SoftDelete_SetsFlagsAndRaisesEvent"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/
git add apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/PlayRecordTests.cs
git commit -m "feat(play-records): add SoftDelete domain method + PlayRecordDeletedEvent (#2439)"
```

---

### Task 3: Persistence entity columns

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/GameManagement/PlayRecordEntity.cs`

- [ ] **Step 1: Add fields**

After the `Audit` block (`UpdatedAt`, line ~39), add:

```csharp
    // Soft Delete (issue #2439)
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
```

- [ ] **Step 2: Build to verify compile**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit** (combined with Task 4)

---

### Task 4: EntityConfiguration — columns + query filter

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/PlayRecordEntityConfiguration.cs`

- [ ] **Step 1: Add column mapping after the Audit block (after line 51 `builder.Property(e => e.UpdatedAt).IsRequired();`)**

```csharp
        // Soft Delete (issue #2439 — mirrors GameBook query-filter pattern)
        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");
```

- [ ] **Step 2: Add the query filter at the end of `Configure`, after the `HasMany(e => e.Players)` relationship block (after line 81)**

```csharp
        // Soft-delete query filter: deleted records are excluded from all queries
        // (history, statistics, get-by-id, can-view, can-edit).
        builder.HasQueryFilter(e => !e.IsDeleted);
```

- [ ] **Step 3: Build to verify compile**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/GameManagement/PlayRecordEntity.cs
git add apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/PlayRecordEntityConfiguration.cs
git commit -m "feat(play-records): add is_deleted/deleted_at columns + soft-delete query filter (#2439)"
```

---

### Task 5: Repository — map + restore new fields

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/PlayRecordRepository.cs`

- [ ] **Step 1: In `MapToPersistence` (object initializer, ~line 268), add the two fields after `SourceEventId = record.SourceEventId`**

```csharp
            SourceEventId = record.SourceEventId,
            IsDeleted = record.IsDeleted,
            DeletedAt = record.DeletedAt
```

- [ ] **Step 2: In `MapToDomain` (the `SetPrivateProperty` block, ~line 245), add after the `UpdatedAt` restore**

```csharp
        SetPrivateProperty(record, nameof(PlayRecord.IsDeleted), entity.IsDeleted);
        SetPrivateProperty(record, nameof(PlayRecord.DeletedAt), entity.DeletedAt);
```

- [ ] **Step 3: Build to verify compile**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/PlayRecordRepository.cs
git commit -m "feat(play-records): map is_deleted/deleted_at in repository (#2439)"
```

---

### Task 6: Migration

**Files:**
- Generate: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddSoftDeleteToPlayRecord.cs`

- [ ] **Step 1: Generate the migration**

Run (from repo root):
```bash
cd apps/api/src/Api && dotnet ef migrations add AddSoftDeleteToPlayRecord && cd -
```
Expected: creates `Infrastructure/Migrations/<timestamp>_AddSoftDeleteToPlayRecord.cs` + `.Designer.cs`.

- [ ] **Step 2: Review the generated SQL**

Run: `git diff --stat` then open the new migration file.
Expected: `AddColumn` for `is_deleted` (bool, default false) + `deleted_at` (timestamp, nullable) on `play_records`. No DROP/destructive ops. If extra unexpected changes appear (model drift), STOP and investigate.

- [ ] **Step 3: Apply to dev DB (if DB running) and build**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: BUILD SUCCEEDED. (DB apply happens in CI/dev via `dotnet ef database update`; not required to pass unit tests which use Testcontainers.)

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(play-records): migration AddSoftDeleteToPlayRecord (#2439)"
```

---

### Task 7: Command + Validator + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/DeletePlayRecordCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/DeletePlayRecordCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Validators/PlayRecords/DeletePlayRecordCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/DeletePlayRecordCommandHandlerTests.cs`

- [ ] **Step 1: Write the failing handler tests**

Create `DeletePlayRecordCommandHandlerTests.cs`. Mirror the mocking style of the existing `GetPlayRecordQueryHandlerTests.cs` (read it first for the exact `Moq`/`NSubstitute` flavor and `IUnitOfWork` setup).

```csharp
using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

public class DeletePlayRecordCommandHandlerTests
{
    private static PlayRecord MakeRecord(Guid creatorId) =>
        PlayRecord.CreateFreeForm(Guid.NewGuid(), "Catan", creatorId,
            DateTime.UtcNow.AddDays(-1), PlayRecordVisibility.Private,
            SessionScoringConfig.CreateDefault());

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_RecordNotFound_ThrowsNotFound()
    {
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayRecord?)null);
        var uow = new Mock<IUnitOfWork>();
        var checker = new PlayRecordPermissionChecker(repo.Object);
        var handler = new DeletePlayRecordCommandHandler(repo.Object, uow.Object, checker);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new DeletePlayRecordCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_NotCreator_ThrowsForbidden()
    {
        var creatorId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var record = MakeRecord(creatorId);
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(otherUser, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var uow = new Mock<IUnitOfWork>();
        var checker = new PlayRecordPermissionChecker(repo.Object);
        var handler = new DeletePlayRecordCommandHandler(repo.Object, uow.Object, checker);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new DeletePlayRecordCommand(record.Id, otherUser), CancellationToken.None));
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_Creator_SoftDeletesAndSaves()
    {
        var creatorId = Guid.NewGuid();
        var record = MakeRecord(creatorId);
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(creatorId, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var uow = new Mock<IUnitOfWork>();
        var checker = new PlayRecordPermissionChecker(repo.Object);
        var handler = new DeletePlayRecordCommandHandler(repo.Object, uow.Object, checker);

        await handler.Handle(new DeletePlayRecordCommand(record.Id, creatorId), CancellationToken.None);

        Assert.True(record.IsDeleted);
        repo.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DeletePlayRecordCommandHandlerTests"`
Expected: FAIL — `DeletePlayRecordCommand`/`DeletePlayRecordCommandHandler` do not exist (compile error).

- [ ] **Step 3: Create the command**

`DeletePlayRecordCommand.cs`:
```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to soft-delete a play record. Creator-only (issue #2439).
/// </summary>
internal record DeletePlayRecordCommand(Guid RecordId, Guid UserId) : ICommand;
```

- [ ] **Step 4: Create the validator**

`DeletePlayRecordCommandValidator.cs` (mirror `UpdatePlayRecordCommandValidator.cs` style):
```csharp
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

internal sealed class DeletePlayRecordCommandValidator
    : AbstractValidator<Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords.DeletePlayRecordCommand>
{
    public DeletePlayRecordCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
```

- [ ] **Step 5: Create the handler** (mirrors `UpdatePlayRecordCommandHandler.cs`)

`DeletePlayRecordCommandHandler.cs`:
```csharp
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Handles soft-deleting a play record. Creator-only (issue #2439).
/// </summary>
internal class DeletePlayRecordCommandHandler : ICommandHandler<DeletePlayRecordCommand>
{
    private readonly IPlayRecordRepository _recordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PlayRecordPermissionChecker _permissionChecker;

    public DeletePlayRecordCommandHandler(
        IPlayRecordRepository recordRepository,
        IUnitOfWork unitOfWork,
        PlayRecordPermissionChecker permissionChecker)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
    }

    public async Task Handle(DeletePlayRecordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = await _recordRepository.GetByIdAsync(command.RecordId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecord", command.RecordId.ToString());

        if (!await _permissionChecker.CanEditAsync(command.UserId, command.RecordId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("You do not have permission to delete this play record.");
        }

        record.SoftDelete();

        await _recordRepository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DeletePlayRecordCommandHandlerTests"`
Expected: PASS (3 tests). If `ICommandHandler`/`IUnitOfWork`/`ForbiddenException` namespaces differ, fix the `using` to match `UpdatePlayRecordCommandHandler.cs` (already verified imports).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/
git add apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/DeletePlayRecordCommandHandlerTests.cs
git commit -m "feat(play-records): DeletePlayRecordCommand + handler + validator (#2439)"
```

---

### Task 8: Endpoint `MapDelete` + Location header fix

**Files:**
- Modify: `apps/api/src/Api/Routing/PlayRecordEndpoints.cs`

- [ ] **Step 1: Add the endpoint mapping after the `MapPut` block (after line 79)**

```csharp
        group.MapDelete("/play-records/{recordId}", HandleDeleteRecord)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(401)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("PlayRecords")
            .WithSummary("Delete play record")
            .WithDescription("Soft-deletes a play record. Creator-only.");
```

- [ ] **Step 2: Add the handler in the Command Handlers region (after `HandleUpdateRecord`, line 192)**

```csharp
    private static async Task<IResult> HandleDeleteRecord(
        Guid recordId,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new DeletePlayRecordCommand(recordId, httpContext.User.GetUserId());
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
```

- [ ] **Step 3: Fix the wrong Location header (bonus finding) on line 132**

Change:
```csharp
        return Results.Created($"/api/v1/game-management/play-records/{recordId}", recordId);
```
to:
```csharp
        return Results.Created($"/api/v1/play-records/{recordId}", recordId);
```

- [ ] **Step 4: Build to verify compile**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/PlayRecordEndpoints.cs
git commit -m "feat(play-records): MapDelete endpoint + fix 201 Location header (#2439)"
```

---

### Task 9: Integration tests (endpoint contract + invisibility)

**Files:**
- Modify: `apps/api/tests/Api.Tests/Integration/GameManagement/PlayRecordCommandTests.cs`

- [ ] **Step 1: Read the existing integration test class header**

Run: `sed -n '1,80p' apps/api/tests/Api.Tests/Integration/GameManagement/PlayRecordCommandTests.cs`
Expected: shows the fixture (Testcontainers `HttpClient` auth helper, base route prefix `/api/v1/play-records`, how an authed user + a record are created). Reuse those helpers verbatim in the new tests below; adapt names to match.

- [ ] **Step 2: Write the failing integration tests**

Add three tests mirroring the existing create/update integration pattern (use the same auth + create-record helpers found in Step 1):

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task Delete_AsCreator_Returns204_AndRecordVanishes()
{
    var (client, _) = await CreateAuthenticatedClientAsync();      // existing helper
    var recordId = await CreatePlayRecordAsync(client);            // existing helper

    var del = await client.DeleteAsync($"/api/v1/play-records/{recordId}");
    Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

    var get = await client.GetAsync($"/api/v1/play-records/{recordId}");
    Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);        // hidden by query filter
}

[Fact]
[Trait("Category", "Integration")]
public async Task Delete_AsNonCreator_Returns403()
{
    var (creatorClient, _) = await CreateAuthenticatedClientAsync();
    var recordId = await CreatePlayRecordAsync(creatorClient);
    var (otherClient, _) = await CreateAuthenticatedClientAsync();  // different user

    var del = await otherClient.DeleteAsync($"/api/v1/play-records/{recordId}");
    Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);
}

[Fact]
[Trait("Category", "Integration")]
public async Task Delete_NonExistent_Returns404()
{
    var (client, _) = await CreateAuthenticatedClientAsync();
    var del = await client.DeleteAsync($"/api/v1/play-records/{Guid.NewGuid()}");
    Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
}
```

- [ ] **Step 3: Run tests to verify they pass** (requires Docker for Testcontainers)

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PlayRecordCommandTests&FullyQualifiedName~Delete"`
Expected: PASS (3 tests). If the helper names differ from the placeholders, fix to the real names from Step 1.

- [ ] **Step 4: Run the full PlayRecord suite to confirm the query filter didn't break existing tests**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PlayRecord"`
Expected: ALL PASS. The new `HasQueryFilter` excludes soft-deleted rows from history/stats/get — existing tests create non-deleted records so they are unaffected. If any pre-existing test now fails, investigate whether it relied on seeing a record it deleted.

- [ ] **Step 5: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/GameManagement/PlayRecordCommandTests.cs
git commit -m "test(play-records): DELETE endpoint integration tests (#2439)"
```

---

### Task 10: Verify FE delete flow is wired (no FE code change expected)

**Files:**
- Verify only: `apps/web/src/__tests__/mocks/handlers/play-records.handlers.ts`
- Verify only: `apps/web/src/lib/domain-hooks/usePlayRecords.ts` (already has `useDeleteRecord`)

- [ ] **Step 1: Confirm an MSW DELETE handler exists for tests**

Run: `grep -n "delete\|DELETE\|http.delete" apps/web/src/__tests__/mocks/handlers/play-records.handlers.ts`
Expected: a `http.delete(...play-records/:id...)` handler. If MISSING, add one mirroring the existing PUT handler:

```typescript
http.delete(`${API}/play-records/:id`, () => new HttpResponse(null, { status: 204 })),
```
(match the `API` base + import style already used in the file).

- [ ] **Step 2: Run the FE play-records tests**

Run: `cd apps/web && pnpm test -- play-records --run && cd -`
Expected: PASS. The FE `deleteRecord`/`useDeleteRecord` are already implemented; this only confirms no regression.

- [ ] **Step 3: Commit (only if the MSW handler was added)**

```bash
git add apps/web/src/__tests__/mocks/handlers/play-records.handlers.ts
git commit -m "test(play-records): add MSW DELETE handler (#2439)"
```

---

## Self-Review

**1. Spec coverage (issue #2439):**
- "no such backend endpoint exists / 405" → Task 8 adds `MapDelete`. ✅
- "Add DeletePlayRecordCommand + handler (soft-delete via IsDeleted/DeletedAt)" → Tasks 1, 3, 4, 7. ✅
- "creator-only authz (mirror the #2349 CanEditAsync pattern)" → Task 7 handler uses `PlayRecordPermissionChecker.CanEditAsync`. ✅
- Migration → Task 6. ✅
- Bonus: wrong 201 Location header → Task 8 Step 3. ✅

**2. Placeholder scan:** Test helper names in Task 7/9 are explicitly flagged as "mirror the real names from Step 1" with a read step first — not silent placeholders. Domain-event base type in Task 2 is read-then-mirror (Step 1) rather than assumed. No "TODO/TBD".

**3. Type consistency:** `DeletePlayRecordCommand(Guid RecordId, Guid UserId)` is defined in Task 7 and consumed identically in Task 8. `SoftDelete(TimeProvider?)` defined Task 1, called param-less in Task 7 handler. `PlayRecordDeletedEvent(Guid RecordId, Guid DeletedByUserId)` defined Task 2, raised in Task 1. `IsDeleted`/`DeletedAt` names consistent across domain (Task 1), entity (Task 3), config (Task 4), repo (Task 5).

**Risk flagged:** adding `HasQueryFilter` is a global behavior change for `play_records` queries — Task 9 Step 4 explicitly re-runs the full suite to catch any existing test that relied on seeing a deleted record.
