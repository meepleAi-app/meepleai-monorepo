# Play Records Tier 2 (US-INT-2) Gap-Fill — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close US-INT-2 Tier 2 (#2348 create, #2349 detail+edit, #2350 stats) at the real "gap-fill" bar (mirroring #2347): fix two genuine #2349 defects (access-control security hole + non-functional edit prefill), satisfy the DEC-A5 5-states gate (Storybook stories + axe + E2E) for all routes, and split the unbuilt literal-AC features into follow-up issues.

**Architecture:** The Play Records FE+BE implementation already exists (shipped by epics #1475/#1488/#3892). This plan does NOT re-implement features — it (1) hardens the BE with CQRS-correct authorization inside MediatR handlers (never in endpoints), (2) wires the edit form's already-loaded record into the form, and (3) adds the testing/story scaffolding the DEC-A5 gate requires. The literal AC items (photo/OCR/share-token/audit/restore-version/xmin/leaderboard/Redis/gameNightId-prefill) are NOT in the canonical mockups and are deferred to follow-up issues per DEC-A4 + the #2347 drift-cleanup precedent.

**Tech Stack:** .NET 9 (ASP.NET Minimal API + MediatR + EF Core + xUnit/Testcontainers + FluentAssertions) · Next.js 16 / React 19 (App Router, react-hook-form + Zod, Zustand, React Query) · Storybook 8 (MSW per-state handlers) · Playwright E2E · jest-axe (Vitest).

**Branch:** `feature/issue-2346-playrecords-tier2-gapfill` (parent: `main-dev`).

---

## Scope summary

| Issue | In scope (this plan) | Deferred → follow-up |
|-------|----------------------|----------------------|
| #2349 | Access-control enforcement (GET/PUT/Complete), edit-form prefill fix, 5-states stories (detail+edit) + axe + E2E | share-token, audit trail, restore-version, edit-window 7d immutability, optimistic concurrency (xmin), photo gallery+fullscreen, MVP chip |
| #2348 | 5-states story (new) + axe, delete orphan stale E2E + new E2E, **`?gameNightId=` full prefill (date+location+game+roster)** | autosave-30s, localStorage+server draft (`GET /draft/{id}`), photo upload + S3 presigned + dedup, OCR |
| #2350 | 5-states story (stats) + axe + E2E | trend chart, per-player leaderboard (BE), CSV export, custom date-range UI, server-side Redis cache |
| #2342 | Flag: CI gate `lint:storybook-states` does not exist | Build the gate (umbrella deliverable) |

**SSE state note:** The DEC-A5 "5 canonical states" are default/empty/loading/error/**sse**. Play Records CRUD pages have **no SSE data source** — the `sse` state is Not Applicable. Each story documents this waiver in its `docs.description`; we author the 4 applicable states (default/empty/loading/error). This is honest and consistent with the absent automated gate.

---

## File structure

**Backend (C#) — #2349 authorization:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayRecordQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayRecordQueryHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UpdatePlayRecordCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UpdatePlayRecordCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/CompletePlayRecordCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/CompletePlayRecordCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/PlayRecordEndpoints.cs` (HandleGetPlayRecord, HandleUpdateRecord, HandleCompleteRecord + `.Produces(403)`)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/GetPlayRecordQueryHandlerTests.cs` (extend)
- Test: `apps/api/tests/Api.Tests/Integration/GameManagement/PlayRecordCommandTests.cs` (extend)

**Frontend (TS) — #2349 prefill + all stories/E2E/axe:**
- Modify: `apps/web/src/components/play-records/SessionCreateForm.tsx` (add `initialValues` + `initialPlayers` props)
- Modify: `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.tsx` (map record → form)
- Modify: `apps/web/src/components/play-records/__tests__/SessionCreateForm.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.test.tsx` (strengthen prefill assertions)
- Modify (stories → 5-states): `play-records/new/page.stories.tsx`, `play-records/[id]/page.stories.tsx`, `play-records/[id]/edit/page.stories.tsx`, `play-records/stats/page.stories.tsx`
- Create (axe): `apps/web/src/components/play-records/__tests__/play-records-axe.test.tsx`
- Delete: `apps/web/__tests__/play-records/e2e/play-records.spec.ts` (orphan, run by no config)
- Create (E2E): `apps/web/e2e/play-records-new.spec.ts`, `apps/web/e2e/play-records-detail.spec.ts`, `apps/web/e2e/play-records-stats.spec.ts`

**Bookkeeping:** GitHub issue body edits + follow-up issue creation (Task 12).

---

## Key verified facts (read before starting)

- `ForbiddenException` (`apps/api/src/Api/Middleware/Exceptions/ForbiddenException.cs`) EXISTS and is auto-mapped to HTTP 403 by `ApiExceptionHandlerMiddleware` (the `HttpException` switch arm). **Do NOT create a new exception or middleware.** `using Api.Middleware.Exceptions;` is already present in all three handler files.
- `PlayRecordPermissionChecker` EXISTS, is DI-registered (`AddScoped<PlayRecordPermissionChecker>()` at `GameManagementServiceExtensions.cs:70`), concrete type (no interface). API: `Task<bool> CanViewAsync(Guid userId, Guid recordId, CancellationToken)` and `Task<bool> CanEditAsync(...)`. Edit = creator-only; View = creator OR a player (`UserId` match).
- Permission methods return `false` for non-existent records → **always load + throw `NotFoundException` FIRST, then enforce permission** (so missing → 404, not 403).
- `httpContext.User.GetUserId()` (`Api.Extensions.ClaimsPrincipalExtensions`) returns `Guid` (`Guid.Empty` if absent). `using Api.Extensions;` already in `PlayRecordEndpoints.cs:5`. Pattern to mirror: `HandleGetStatistics` (`PlayRecordEndpoints.cs:209-219`).
- MediatR handlers are auto-registered via assembly scanning — constructor signature changes need NO manual DI re-registration.
- Storybook global preview (`apps/web/.storybook/preview.tsx`) provides QueryClient (`retry:false`), IntlProvider (it.json), MockAuthProvider, MSW (`mswLoader` + global handlers barrel), theme. New stories only add `parameters.msw.handlers` per state.
- MSW play-records handlers (`apps/web/src/__tests__/mocks/handlers/play-records.handlers.ts`) register `GET /api/v1/play-records/statistics`, `GET /api/v1/play-records`, `GET /api/v1/play-records/:id` (404 if unknown), with in-memory fixtures incl. id `pr-won-1` (+ exported `FIXTURE_WON` etc.). `API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080'`.
- Playwright runs only `apps/web/e2e/**` (`playwright.config.ts:55 testDir:'./e2e'`). The stale spec at `apps/web/__tests__/play-records/e2e/play-records.spec.ts` is run by NEITHER Playwright nor Vitest (orphan).
- E2E auth convention (mirror `apps/web/e2e/play-records-hub.spec.ts`): `seedCookieConsent(page)` + `seedAuthSession(page)` + `mockAuthEndpoints(page)` in `beforeEach`, `data-testid` selectors, `test.skip(({browserName}) => browserName !== 'chromium', ...)`, axe via `@axe-core/playwright` `.withTags(['wcag2a','wcag2aa'])`.

---

## Task 1: #2349 BE — GET view authorization (CQRS-correct, inline)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayRecordQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayRecordQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/GetPlayRecordQueryHandlerTests.cs`

Rationale: the GET handler already loads the entity (with `Players` + `CreatedByUserId`) via `MeepleAiDbContext`. Enforce inline to avoid a second DB read and keep the unit test on the existing in-memory-context style (no `IPlayRecordRepository` construction needed).

- [ ] **Step 1: Write the failing test** — append to `GetPlayRecordQueryHandlerTests.cs` (mirrors the existing `MakePlayRecord`/`MakePlayer` helpers + in-memory ctx already in the file):

```csharp
    [Fact]
    public async Task Handle_UserIsNeitherCreatorNorPlayer_ThrowsForbiddenException()
    {
        // Arrange — record owned by someone else, requester is not a player
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var record = MakePlayRecord(Guid.NewGuid());
        record.CreatedByUserId = ownerId;
        _context.PlayRecords.Add(record);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(record.Id, requesterId);

        // Act & Assert
        var act = () => _handler.Handle(query, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_UserIsAPlayer_ReturnsRecord()
    {
        // Arrange — requester is linked as a player (not the creator)
        var requesterId = Guid.NewGuid();
        var record = MakePlayRecord(Guid.NewGuid());
        record.CreatedByUserId = Guid.NewGuid();
        var player = MakePlayer(Guid.NewGuid(), record.Id, ("points", 10));
        player.UserId = requesterId;
        record.Players.Add(player);
        _context.PlayRecords.Add(record);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(record.Id, requesterId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(record.Id);
    }
```

Also update the pre-existing `Handle_RecordNotFound_ThrowsNotFoundException` and any other `new GetPlayRecordQuery(...)` constructions in this file to the 2-arg form `new GetPlayRecordQuery(Guid.NewGuid(), Guid.NewGuid())` (the second arg is the new required `UserId`).

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~GetPlayRecordQueryHandlerTests" -v minimal`
Expected: compile error (GetPlayRecordQuery takes 1 arg) → after Step 3's query change but before handler change, FAIL with "Expected ForbiddenException".

- [ ] **Step 3: Add `UserId` to the query**

In `GetPlayRecordQuery.cs`, change the record to:

```csharp
internal record GetPlayRecordQuery(Guid RecordId, Guid UserId) : IQuery<PlayRecordDto>;
```

- [ ] **Step 4: Enforce view permission in the handler**

In `GetPlayRecordQueryHandler.cs`, immediately after the existing `NotFoundException` null-check block, insert:

```csharp
        if (entity.CreatedByUserId != query.UserId
            && !entity.Players.Any(p => p.UserId == query.UserId))
        {
            throw new ForbiddenException("You do not have permission to view this play record.");
        }
```

(The file already imports `Api.Middleware.Exceptions`, so `ForbiddenException` resolves.)

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~GetPlayRecordQueryHandlerTests" -v minimal`
Expected: PASS (all tests in the class).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayRecordQuery.cs apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayRecordQueryHandler.cs "apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/GetPlayRecordQueryHandlerTests.cs"
git commit -m "fix(play-records): #2349 enforce view authorization in GetPlayRecordQueryHandler"
```

---

## Task 2: #2349 BE — Update/Complete edit authorization (via PlayRecordPermissionChecker)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UpdatePlayRecordCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UpdatePlayRecordCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/CompletePlayRecordCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/CompletePlayRecordCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/Integration/GameManagement/PlayRecordCommandTests.cs`

Rationale: the edit path uses `PlayRecordPermissionChecker.CanEditAsync` — this gives the already-built-but-unused checker a real consumer and centralizes the creator-only rule. Accept the checker's extra read (edit is not a hot path). Enforce AFTER the existing `NotFoundException` so missing → 404.

- [ ] **Step 1: Write the failing integration tests** — append to `PlayRecordCommandTests.cs` (the file imports `Api.Middleware.Exceptions` + uses `SeedTestUserAsync`/`SendInScopeAsync`):

```csharp
    [Fact]
    public async Task UpdatePlayRecordCommand_UserIsNotCreator_ThrowsForbiddenException()
    {
        // Arrange — record created by user A, update attempted by user B
        var creatorId = await SeedTestUserAsync();
        var otherUserId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var command = new UpdatePlayRecordCommand(recordId, otherUserId, Notes: "hijack");

        // Act & Assert
        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CompletePlayRecordCommand_UserIsNotCreator_ThrowsForbiddenException()
    {
        var creatorId = await SeedTestUserAsync();
        var otherUserId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var command = new CompletePlayRecordCommand(recordId, otherUserId);

        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<ForbiddenException>();
    }
```

Update the existing happy-path tests (`UpdatePlayRecordCommand_ValidData_UpdatesSuccessfully`, and any `CompletePlayRecordCommand`/`UpdatePlayRecordCommand` constructions) to pass the creator's userId as the new 2nd arg. This requires `CreateTestRecordAsync` to return/use a known creator id — see Step 1b.

- [ ] **Step 1b: Make the creator userId available to tests**

In `PlayRecordCommandTests.cs`, add an overload of the existing `CreateTestRecordAsync()` helper so tests can pin the creator:

```csharp
    private async Task<Guid> CreateTestRecordAsync(Guid creatorUserId)
    {
        var gameId = await SeedTestGameAsync();
        var command = new CreatePlayRecordCommand(
            creatorUserId,
            gameId,
            "Test Game",
            _timeProvider.GetUtcNow().UtcDateTime,
            PlayRecordVisibility.Private,
            new List<CreateSessionPlayerInput> { new(null, "P1") },
            new SessionScoringConfigInput(new List<string> { "points" }, new Dictionary<string, string>()));
        return await SendInScopeAsync<CreatePlayRecordCommand, Guid>(command);
    }
```

> NOTE for the implementer: open the file and match the EXACT current signatures of `CreatePlayRecordCommand`, `CreateSessionPlayerInput`, `SessionScoringConfigInput`, `SeedTestGameAsync`, and the `SendInScopeAsync<TReq,TRes>` overload the file already uses (the existing `CreateTestRecordAsync()` body shows the real constructor — copy its argument shape exactly, only threading `creatorUserId` as the userId argument). If the existing `CreatePlayRecordCommand` already takes the creator userId first, reuse that position. Do not invent argument names.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~PlayRecordCommandTests" -v minimal`
Expected: FAIL — `ForbiddenException` not thrown (handlers don't check yet) / compile errors until commands carry `UserId`.

- [ ] **Step 3: Add `UserId` to both commands**

`UpdatePlayRecordCommand.cs`:

```csharp
internal record UpdatePlayRecordCommand(
    Guid RecordId,
    Guid UserId,
    DateTime? SessionDate = null,
    string? Notes = null,
    string? Location = null
) : ICommand;
```

`CompletePlayRecordCommand.cs`:

```csharp
internal record CompletePlayRecordCommand(
    Guid RecordId,
    Guid UserId,
    TimeSpan? ManualDuration = null
) : ICommand;
```

- [ ] **Step 4: Inject the checker and enforce in both handlers**

`UpdatePlayRecordCommandHandler.cs` — add the field + ctor param + enforcement. Replace the ctor and the start of `Handle`:

```csharp
    private readonly IPlayRecordRepository _recordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly PlayRecordPermissionChecker _permissionChecker;

    public UpdatePlayRecordCommandHandler(
        IPlayRecordRepository recordRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        PlayRecordPermissionChecker permissionChecker)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
    }

    public async Task Handle(UpdatePlayRecordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = await _recordRepository.GetByIdAsync(command.RecordId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecord", command.RecordId.ToString());

        if (!await _permissionChecker.CanEditAsync(command.UserId, command.RecordId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("You do not have permission to edit this play record.");
        }

        record.UpdateDetails(
            command.SessionDate,
            command.Notes,
            command.Location,
            _timeProvider);

        await _recordRepository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
```

Add `using Api.BoundedContexts.GameManagement.Application.Services;` to the handler's usings (for `PlayRecordPermissionChecker`).

Apply the identical pattern to `CompletePlayRecordCommandHandler.cs` (add field, ctor param, and the same `CanEditAsync` guard after the `NotFoundException` line, before `record.Complete(...)`), plus the `using ...Application.Services;`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~PlayRecordCommandTests" -v minimal`
Expected: PASS. If the checker fails to resolve in the integration host, verify `IntegrationServiceCollectionBuilder.CreateBase` registers the GameManagement context (which includes `AddScoped<PlayRecordPermissionChecker>()`); if not, add `services.AddScoped<PlayRecordPermissionChecker>();` to the test's service setup.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/ "apps/api/tests/Api.Tests/Integration/GameManagement/PlayRecordCommandTests.cs"
git commit -m "fix(play-records): #2349 enforce edit authorization in Update/Complete handlers"
```

---

## Task 3: #2349 BE — wire userId from endpoints (CQRS-compliant)

**Files:**
- Modify: `apps/api/src/Api/Routing/PlayRecordEndpoints.cs`

- [ ] **Step 1: Update the three endpoint handlers** to take `HttpContext` and pass the userId. Replace `HandleGetPlayRecord` (currently lines ~187-195), `HandleUpdateRecord` (~172-181), `HandleCompleteRecord` (~161-170):

```csharp
    private static async Task<IResult> HandleCompleteRecord(
        Guid recordId,
        [FromBody] CompleteRecordRequest? request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CompletePlayRecordCommand(recordId, httpContext.User.GetUserId(), request?.ManualDuration);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpdateRecord(
        Guid recordId,
        [FromBody] UpdateRecordRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePlayRecordCommand(recordId, httpContext.User.GetUserId(), request.SessionDate, request.Notes, request.Location);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
```

```csharp
    private static async Task<IResult> HandleGetPlayRecord(
        Guid recordId,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetPlayRecordQuery(recordId, httpContext.User.GetUserId());
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
```

- [ ] **Step 2: Add `.Produces(403)` to the three route registrations**

Find the `MapGet`/`MapPut`/`MapPost(...complete...)` builder chains for these three endpoints and add `.Produces(StatusCodes.Status403Forbidden)` to each (they already declare `.Produces(401)`).

- [ ] **Step 3: Build to verify wiring**

Run: `cd apps/api && dotnet build`
Expected: build succeeds (all call sites updated).

- [ ] **Step 4: Run the BE PlayRecord test surface**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~PlayRecord" -v minimal`
Expected: PASS (unit + integration). Investigate any failure as a real regression — do not skip.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/PlayRecordEndpoints.cs
git commit -m "fix(play-records): #2349 thread userId from endpoints to authz handlers + Produces(403)"
```

---

## Task 4: #2349 FE — add prefill props to SessionCreateForm

**Files:**
- Modify: `apps/web/src/components/play-records/SessionCreateForm.tsx`
- Test: `apps/web/src/components/play-records/__tests__/SessionCreateForm.test.tsx`

Because the edit page mounts `SessionCreateForm` only AFTER `record` is loaded (it returns a skeleton while `isLoading`), spreading `initialValues` into `defaultValues` works without a `form.reset` effect. `players` is local `useState` (not RHF) → seed it from `initialPlayers`.

- [ ] **Step 1: Write the failing test** — append to `SessionCreateForm.test.tsx` (match the file's existing render/provider wrapper):

```tsx
  it('prefills editable fields from initialValues (edit mode)', () => {
    renderForm({
      mode: 'edit',
      initialValues: {
        gameType: 'catalog',
        gameName: 'Wingspan',
        sessionDate: new Date('2026-05-17T20:00:00.000Z'),
        notes: 'Great match',
        location: 'Padova',
      },
    });
    // Step 2 (Quando) holds the editable fields in edit mode; navigate if needed,
    // then assert the prefilled values are present.
    expect(screen.getByDisplayValue('Padova')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Great match')).toBeInTheDocument();
  });
```

> NOTE for implementer: match `renderForm`/render helper + how the test file already drives step navigation. If editable fields live on Step 2, advance to it the same way other tests in the file do (they already exercise multi-step nav). Assert on `notes`/`location` (always editable under the K5 gate). Use `getByDisplayValue` or the label queries the file already uses.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test SessionCreateForm --run`
Expected: FAIL — `initialValues` prop doesn't exist; values are empty.

- [ ] **Step 3: Add the props** — in `SessionCreateForm.tsx`:

Extend the props interface (after `mode?`):

```tsx
  /**
   * #2349 AC-4.2: pre-fill the form when editing or deep-linking. Spread into
   * react-hook-form `defaultValues` on mount (the parent gates mounting until
   * the source record is loaded, so no reset effect is needed).
   */
  initialValues?: Partial<SessionCreateFormData>;
  /**
   * #2349 AC-4.2: pre-fill the (local-state) player roster. Display-only under
   * the edit K5 gate; the edit submit path only sends sessionDate/notes/location.
   */
  initialPlayers?: PlayerEntry[];
```

Update the destructure:

```tsx
export function SessionCreateForm({
  onSubmit,
  onCancel,
  isSubmitting = false,
  mode = 'create',
  initialValues,
  initialPlayers,
}: SessionCreateFormProps) {
```

Spread into `defaultValues` (merge AFTER the hardcoded defaults so prefill wins):

```tsx
  const form = useForm<SessionCreateFormData>({
    resolver: zodResolver(SessionCreateFormSchema),
    defaultValues: {
      gameType: 'catalog',
      gameName: '',
      sessionDate: new Date(),
      visibility: 'Private',
      enableScoring: false,
      scoringDimensions: [],
      notes: '',
      location: '',
      ...initialValues,
    },
  });
```

Seed the player state from `initialPlayers` (locate the existing `const [players, setPlayers] = useState<PlayerEntry[]>(...)` and change its initializer):

```tsx
  const [players, setPlayers] = useState<PlayerEntry[]>(() => initialPlayers ?? []);
```

> NOTE: use the EXACT existing `PlayerEntry` type name + the exact existing `useState` initializer the file declares; only change the default value to `initialPlayers ?? <existing default>`. **Also `export` the `PlayerEntry` type** from `SessionCreateForm.tsx` (Task 5b's hook + Task 5's edit page import it). If it already lives in a shared module, import from there instead and skip the export.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test SessionCreateForm --run`
Expected: PASS (new + all existing tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/play-records/SessionCreateForm.tsx apps/web/src/components/play-records/__tests__/SessionCreateForm.test.tsx
git commit -m "feat(play-records): #2349 add initialValues/initialPlayers prefill props to SessionCreateForm"
```

---

## Task 5: #2349 FE — wire record → form on the edit page

**Files:**
- Modify: `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.tsx`
- Test: `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.test.tsx`

- [ ] **Step 1: Strengthen the failing test** — in `edit/page.test.tsx`, replace/augment the weak "AC-4.2" test (which only asserts `usePlayRecord` was called) so it asserts the form actually receives mapped initial values. Mock `SessionCreateForm` to capture its props:

```tsx
  it('AC-4.2: passes mapped record values into SessionCreateForm', async () => {
    const captured: { initialValues?: unknown; initialPlayers?: unknown } = {};
    vi.mocked(SessionCreateForm).mockImplementation((props: any) => {
      captured.initialValues = props.initialValues;
      captured.initialPlayers = props.initialPlayers;
      return <div data-testid="mock-form" />;
    });
    // ...arrange usePlayRecord to return a record fixture (gameName 'Wingspan',
    // sessionDate '2026-05-17...', notes 'gg', location 'Padova', players: [...])
    renderEditPage();
    await screen.findByTestId('mock-form');
    expect(captured.initialValues).toMatchObject({
      gameType: 'catalog',
      gameName: 'Wingspan',
      notes: 'gg',
      location: 'Padova',
    });
    expect(captured.initialPlayers).toHaveLength(1);
  });
```

> NOTE: match how the test file already mocks `SessionCreateForm` + `usePlayRecord` (it already mocks the hook). Use the file's existing `renderEditPage`/render helper and fixture builders.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test "play-records/[id]/edit" --run`
Expected: FAIL — `initialValues`/`initialPlayers` are `undefined` (not passed yet).

- [ ] **Step 3: Map record → form and pass props** — in `edit/page.tsx`, add a mapping just before the `return` (the `record` is guaranteed non-null past the loading/error/archived guards) and pass it to the form:

```tsx
  // #2349 AC-4.2: map the loaded record into the form's initial shape.
  const initialValues: Partial<SessionCreateFormData> = {
    gameType: record.gameId ? 'catalog' : 'freeform',
    gameId: record.gameId ?? undefined,
    gameName: record.gameName,
    sessionDate: new Date(record.sessionDate),
    visibility: record.visibility,
    notes: record.notes ?? '',
    location: record.location ?? '',
  };
  const initialPlayers = record.players.map(p => ({
    id: p.id,
    name: p.displayName,
    score: String(p.totalScore ?? p.scores.find(s => s.dimension === 'points')?.value ?? ''),
  }));
```

```tsx
      <SessionCreateForm
        mode="edit"
        initialValues={initialValues}
        initialPlayers={initialPlayers}
        onSubmit={handleSubmit}
        onCancel={handleCancel}
        isSubmitting={updateMutation.isPending}
      />
```

> NOTE: confirm the `PlayerEntry` shape (`{ id, name, score }`) against `SessionCreateForm.tsx`; adapt the `.map` to the exact field names. Add the `SessionCreateFormData` type import if not already present (`import type { SessionCreateForm as SessionCreateFormData } from '@/lib/api/schemas/play-records.schemas';` is already imported).

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test "play-records/[id]/edit" --run`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/app/(authenticated)/play-records/[id]/edit/page.tsx" "apps/web/src/app/(authenticated)/play-records/[id]/edit/page.test.tsx"
git commit -m "fix(play-records): #2349 wire loaded record into edit form (AC-4.2 prefill)"
```

---

## Task 5b: #2348 — GameNight prefill composing hook

**Files:**
- Create: `apps/web/src/lib/domain-hooks/useGameNightPrefill.ts`
- Test: `apps/web/src/lib/domain-hooks/__tests__/useGameNightPrefill.test.tsx`

Composes `gameNightsClient.getById` (date/location/gameIds) + `gameNightsClient.getRsvps` (roster) + `api.games.getById` (game name) into ready-to-spread `initialValues`/`initialPlayers` for `SessionCreateForm`. Reuses the React Query pattern of `usePlayRecord` (`useQuery` + `retry:false`). RSVP/game failures degrade gracefully (only the gamenight fetch gates).

- [ ] **Step 1: Write the failing test** `__tests__/useGameNightPrefill.test.tsx` (mirror the React Query hook test wrapper used by other `domain-hooks/__tests__` tests — `QueryClientProvider` with `retry:false`):

```tsx
import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useGameNightPrefill } from '../useGameNightPrefill';
import { createQueryWrapper } from './test-utils'; // see NOTE

vi.mock('@/lib/api/clients/gameNightsClient', () => ({
  gameNightsClient: {
    getById: vi.fn().mockResolvedValue({
      id: 'gn-1', organizerId: 'u-org', organizerName: 'Org', title: 'Sabato',
      description: null, scheduledAt: '2026-05-17T20:00:00.000Z', location: 'Padova',
      maxPlayers: 6, gameIds: ['game-1'], status: 'Completed',
      acceptedCount: 2, pendingCount: 0, totalInvited: 2, createdAt: '2026-05-01T00:00:00.000Z',
    }),
    getRsvps: vi.fn().mockResolvedValue([
      { id: 'r1', userId: 'u-1', userName: 'Marco', status: 'Accepted', respondedAt: null, createdAt: '2026-05-02T00:00:00.000Z' },
      { id: 'r2', userId: 'u-2', userName: 'Davide', status: 'Declined', respondedAt: null, createdAt: '2026-05-02T00:00:00.000Z' },
    ]),
  },
}));

vi.mock('@/lib/api', () => ({
  api: { games: { getById: vi.fn().mockResolvedValue({ id: 'game-1', title: 'Brass Birmingham' }) } },
}));

describe('useGameNightPrefill', () => {
  it('maps gamenight + accepted rsvps + game into form initial values', async () => {
    const { result } = renderHook(() => useGameNightPrefill('gn-1'), { wrapper: createQueryWrapper() });
    await waitFor(() => expect(result.current.prefill).not.toBeNull());
    expect(result.current.prefill!.initialValues).toMatchObject({
      gameType: 'catalog',
      gameId: 'game-1',
      gameName: 'Brass Birmingham',
      location: 'Padova',
    });
    expect(result.current.prefill!.initialPlayers).toHaveLength(1); // only Accepted
    expect(result.current.prefill!.initialPlayers[0]).toMatchObject({ name: 'Marco' });
  });

  it('is inert when gameNightId is null', () => {
    const { result } = renderHook(() => useGameNightPrefill(null), { wrapper: createQueryWrapper() });
    expect(result.current.prefill).toBeNull();
    expect(result.current.enabled).toBe(false);
  });
});
```

> NOTE: there may be no `createQueryWrapper` helper — mirror how existing `domain-hooks/__tests__/*.test.tsx` (e.g. the `usePlayRecords` tests) build their `QueryClientProvider` wrapper and reuse that exact helper. Confirm the `api` barrel path for `games.getById` (`@/lib/api`) and the `GameDto` name field (`title` vs `name`) against `games.schemas.ts` — the hook maps `.title ?? .name`.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test useGameNightPrefill --run`
Expected: FAIL — module `useGameNightPrefill` not found.

- [ ] **Step 3: Implement the hook** `useGameNightPrefill.ts`:

```tsx
'use client';

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { gameNightsClient } from '@/lib/api/clients/gameNightsClient';
import type { PlayerEntry } from '@/components/play-records/SessionCreateForm';
import type { SessionCreateForm as SessionCreateFormData } from '@/lib/api/schemas/play-records.schemas';

export interface GameNightPrefill {
  initialValues: Partial<SessionCreateFormData>;
  initialPlayers: PlayerEntry[];
}

/**
 * #2348: builds create-form initial values from a completed GameNight
 * (deep-link `/play-records/new?gameNightId=...`). Maps date/location/game +
 * the Accepted RSVP roster. RSVP/game failures degrade gracefully.
 */
export function useGameNightPrefill(gameNightId: string | null) {
  const enabled = !!gameNightId;

  const nightQ = useQuery({
    queryKey: ['game-nights', 'detail', gameNightId],
    queryFn: () => gameNightsClient.getById(gameNightId!),
    enabled,
    retry: false,
  });

  const rsvpsQ = useQuery({
    queryKey: ['game-nights', 'rsvps', gameNightId],
    queryFn: () => gameNightsClient.getRsvps(gameNightId!),
    enabled,
    retry: false,
  });

  const firstGameId = nightQ.data?.gameIds?.[0];
  const gameQ = useQuery({
    queryKey: ['games', 'detail', firstGameId],
    queryFn: () => api.games.getById(firstGameId!),
    enabled: !!firstGameId,
    retry: false,
  });

  const isLoading =
    enabled && (nightQ.isLoading || rsvpsQ.isLoading || (!!firstGameId && gameQ.isLoading));
  const isError = enabled && nightQ.isError;

  const prefill: GameNightPrefill | null = nightQ.data
    ? {
        initialValues: {
          gameType: firstGameId ? 'catalog' : 'freeform',
          gameId: firstGameId ?? undefined,
          gameName: gameQ.data?.title ?? gameQ.data?.name ?? '',
          sessionDate: new Date(nightQ.data.scheduledAt),
          location: nightQ.data.location ?? '',
        },
        initialPlayers: (rsvpsQ.data ?? [])
          .filter(r => r.status === 'Accepted')
          .map(r => ({ id: r.userId, name: r.userName, score: '' })),
      }
    : null;

  return { prefill, isLoading, isError, enabled };
}
```

> NOTE: adapt `gameQ.data?.title ?? gameQ.data?.name` to whatever `api.games.getById` actually returns (confirm field), and `{ id, name, score }` to the EXACT `PlayerEntry` shape exported by `SessionCreateForm.tsx` (Task 4). If `api.games.getById` doesn't exist on the barrel, use the games client the codebase exposes (grep `getById` in `apps/web/src/lib/api`).

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test useGameNightPrefill --run`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/domain-hooks/useGameNightPrefill.ts apps/web/src/lib/domain-hooks/__tests__/useGameNightPrefill.test.tsx
git commit -m "feat(play-records): #2348 useGameNightPrefill composing hook (date+location+game+roster)"
```

---

## Task 5c: #2348 — wire `?gameNightId=` prefill into the new page (Suspense)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/play-records/new/page.tsx`
- Test: `apps/web/src/app/(authenticated)/play-records/new/page.test.tsx` (create if absent)

Next.js 16 requires `useSearchParams()` under `<Suspense>`. Mirror the index page's pattern: an inner content component reads `useSearchParams`, the default export wraps it in `<Suspense>`.

- [ ] **Step 1: Write the failing test** `new/page.test.tsx` — assert that with `?gameNightId=gn-1` the form receives prefilled `initialValues` (mock `useGameNightPrefill` + capture `SessionCreateForm` props):

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import NewPlayRecordPage from './page';

vi.mock('next/navigation', async (orig) => ({
  ...(await orig<typeof import('next/navigation')>()),
  useSearchParams: () => new URLSearchParams('gameNightId=gn-1'),
  useRouter: () => ({ push: vi.fn() }),
}));

const captured: { initialValues?: unknown } = {};
vi.mock('@/components/play-records/SessionCreateForm', () => ({
  SessionCreateForm: (props: any) => { captured.initialValues = props.initialValues; return <div data-testid="mock-form" />; },
  // PlayerEntry type-only import is erased at runtime; no need to mock it.
}));

vi.mock('@/lib/domain-hooks/useGameNightPrefill', () => ({
  useGameNightPrefill: () => ({
    prefill: { initialValues: { gameName: 'Brass Birmingham', location: 'Padova' }, initialPlayers: [] },
    isLoading: false, isError: false, enabled: true,
  }),
}));

describe('NewPlayRecordPage — gameNightId prefill', () => {
  it('passes GameNight prefill into the form', async () => {
    render(<NewPlayRecordPage />);
    await screen.findByTestId('mock-form');
    expect(captured.initialValues).toMatchObject({ gameName: 'Brass Birmingham', location: 'Padova' });
  });
});
```

> NOTE: align the mock wrappers (i18n/QueryClient) with how sibling page tests render — if `useTranslation`/`useCreatePlayRecord` need providers even with the form mocked, add the project's standard test wrapper. Keep `useCreatePlayRecord` mocked if it requires QueryClient.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test "play-records/new/page" --run`
Expected: FAIL — page doesn't read `gameNightId` / doesn't pass `initialValues`.

- [ ] **Step 3: Refactor `new/page.tsx`** — split into a Suspense-wrapped inner component and consume the hook:

```tsx
'use client';

import { Suspense } from 'react';

import { ArrowLeft } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';
import { toast } from 'sonner';

import { FormPageContainer } from '@/components/layout/PageContainer';
import { SessionCreateForm } from '@/components/play-records/SessionCreateForm';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import type { SessionCreateForm as SessionFormData } from '@/lib/api/schemas/play-records.schemas';
import { useCreatePlayRecord } from '@/lib/domain-hooks/usePlayRecords';
import { useGameNightPrefill } from '@/lib/domain-hooks/useGameNightPrefill';

function NewPlayRecordContent() {
  const router = useRouter();
  const { t } = useTranslation();
  const createRecord = useCreatePlayRecord();
  const searchParams = useSearchParams();
  const gameNightId = searchParams.get('gameNightId');
  const { prefill, isLoading } = useGameNightPrefill(gameNightId);

  const handleSubmit = async (data: SessionFormData) => {
    try {
      const recordId = await createRecord.mutateAsync({
        gameId: data.gameId,
        gameName: data.gameName,
        sessionDate: data.sessionDate.toISOString(),
        visibility: data.visibility,
        groupId: data.groupId,
        scoringDimensions: data.enableScoring ? data.scoringDimensions : undefined,
        dimensionUnits: data.enableScoring ? data.dimensionUnits : undefined,
      });
      toast.success(t('playRecords.new.success.toast'), {
        description: t('playRecords.new.success.toastDescription'),
      });
      router.push(`/play-records/${recordId}`);
    } catch (error) {
      toast.error(t('playRecords.new.error.saveFailed'), {
        description:
          error instanceof Error ? error.message : t('playRecords.new.error.saveFailedDescription'),
      });
    }
  };

  const handleCancel = () => router.push('/play-records');

  if (gameNightId && isLoading) {
    return (
      <FormPageContainer className="p-6 space-y-4">
        <div className="h-8 bg-muted animate-pulse rounded w-48" />
        <div className="h-12 bg-muted animate-pulse rounded" />
        <div className="h-32 bg-muted animate-pulse rounded" />
      </FormPageContainer>
    );
  }

  return (
    <FormPageContainer className="p-6 space-y-6">
      <div className="flex items-center gap-4">
        <Button
          variant="ghost"
          size="icon"
          onClick={handleCancel}
          aria-label={t('playRecords.new.a11y.backToList')}
        >
          <ArrowLeft className="w-5 h-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold">{t('playRecords.new.pageTitle')}</h1>
          <p className="text-muted-foreground mt-1">{t('playRecords.new.pageSubtitle')}</p>
        </div>
      </div>

      <SessionCreateForm
        initialValues={prefill?.initialValues}
        initialPlayers={prefill?.initialPlayers}
        onSubmit={handleSubmit}
        onCancel={handleCancel}
        isSubmitting={createRecord.isPending}
      />
    </FormPageContainer>
  );
}

export default function NewPlayRecordPage() {
  return (
    <Suspense
      fallback={
        <FormPageContainer className="p-6">
          <div className="h-8 bg-muted animate-pulse rounded w-48" />
        </FormPageContainer>
      }
    >
      <NewPlayRecordContent />
    </Suspense>
  );
}
```

> NOTE: preserve the `@mockup` JSDoc header block at the top of the file (do not delete the MOCKUP-ANNOTATION marker).

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test "play-records/new/page" --run`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/app/(authenticated)/play-records/new/page.tsx" "apps/web/src/app/(authenticated)/play-records/new/page.test.tsx"
git commit -m "feat(play-records): #2348 wire ?gameNightId= deep-link prefill into create page"
```

---

## Task 6: #2348 — 5-states Storybook story (new) + axe

**Files:**
- Modify: `apps/web/src/app/(authenticated)/play-records/new/page.stories.tsx`
- Create: `apps/web/src/components/play-records/__tests__/play-records-axe.test.tsx`

The create form has no remote data dependency for its own render (game search is lazy), so its states are about the create-mutation lifecycle. Keep the existing `Default`; add `Submitting`/`Error` via MSW POST handlers, and document `empty`/`sse` N/A.

- [ ] **Step 1: Replace `new/page.stories.tsx`** with the 5-states-aware version:

```tsx
/**
 * sp4-play-records-new — DS-17-13 #2220 / US-INT-2b #2348.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-new.{html,jsx}`.
 *
 * DEC-A5 canonical states: default / empty (N/A — create form has no list) /
 * loading (submit-pending) / error (submit-failure) / sse (N/A — no SSE source).
 */
import { http, HttpResponse } from 'msw';

import PlayRecordNewPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const meta: Meta<typeof PlayRecordNewPage> = {
  title: 'Authenticated / sp4-play-records-new',
  component: PlayRecordNewPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true, navigation: { pathname: '/play-records/new' } },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2348 US-INT-2b. Create form. States: default + submit-loading + submit-error. ' +
          'empty/sse are Not Applicable (no list data, no SSE source).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PlayRecordNewPage>;

export const Default: Story = {};

export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [
        http.post(`${API_BASE}/api/v1/play-records`, () => new Promise(() => {})),
      ],
    },
  },
};

export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.post(`${API_BASE}/api/v1/play-records`, () =>
          HttpResponse.json({ error: 'server_error' }, { status: 500 })
        ),
      ],
    },
  },
};

// #2348: deep-link prefill from a completed GameNight (?gameNightId=).
export const FromGameNight: Story = {
  parameters: {
    nextjs: { appDirectory: true, navigation: { pathname: '/play-records/new', query: { gameNightId: 'gn-1' } } },
    msw: {
      handlers: [
        http.get(`${API_BASE}/api/v1/game-nights/gn-1`, () =>
          HttpResponse.json({
            id: 'gn-1', organizerId: 'u-org', organizerName: 'Org', title: 'Sabato boardgame',
            description: null, scheduledAt: '2026-05-17T20:00:00.000Z', location: 'Padova',
            maxPlayers: 6, gameIds: ['game-1'], status: 'Completed',
            acceptedCount: 1, pendingCount: 0, totalInvited: 1, createdAt: '2026-05-01T00:00:00.000Z',
          })
        ),
        http.get(`${API_BASE}/api/v1/game-nights/gn-1/rsvps`, () =>
          HttpResponse.json([
            { id: 'r1', userId: 'u-1', userName: 'Marco', status: 'Accepted', respondedAt: null, createdAt: '2026-05-02T00:00:00.000Z' },
          ])
        ),
        http.get(`${API_BASE}/api/v1/games/game-1`, () =>
          HttpResponse.json({ id: 'game-1', title: 'Brass Birmingham' })
        ),
      ],
    },
  },
};
```

> NOTE: confirm the games GET path the client uses (`/api/v1/games/:id` vs other) + the game DTO field — match the MSW handler to the real client request so the story actually prefills.

- [ ] **Step 2: Create the axe test** `apps/web/src/components/play-records/__tests__/play-records-axe.test.tsx`:

```tsx
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { renderWithPlayRecordsProviders } from './test-utils'; // see NOTE
import { SessionCreateForm } from '../SessionCreateForm';
import { StatisticsView } from '../StatisticsView';

expect.extend(toHaveNoViolations);

describe('play-records — axe AA gate', () => {
  it('SessionCreateForm (create) — no violations', async () => {
    const { container } = renderWithPlayRecordsProviders(
      <SessionCreateForm onSubmit={() => {}} />
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it('StatisticsView (empty) — no violations', async () => {
    const { container } = renderWithPlayRecordsProviders(<StatisticsView />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

> NOTE for implementer: there is no `renderWithPlayRecordsProviders` helper yet — `SessionCreateForm` needs `useTranslation` (IntlProvider), `usePlayRecordsStore` (Zustand, auto-inits), `useMediaQuery`, and React Query (for `GameCombobox`). Look at how `apps/web/__tests__/play-records/components/PlayerManager.test.tsx` / `GameCombobox.test.tsx` wrap their renders and reuse that exact provider wrapper (QueryClientProvider + IntlProvider/messages). If they use a shared `renderWithProviders`, import that instead of inventing a new one. Do NOT introduce a new provider stack — mirror the existing one.

- [ ] **Step 3: Run the axe test to verify it passes**

Run: `cd apps/web && pnpm test play-records-axe --run`
Expected: PASS (0 violations). If a real violation surfaces, FIX the component (do not suppress) — but the views already ship a11y attributes, so passing is expected.

- [ ] **Step 4: Build Storybook to verify stories compile**

Run: `cd apps/web && pnpm build-storybook --quiet` (or the project's story-typecheck — check `package.json`; if `build-storybook` is heavy, `pnpm typecheck` covers the `.stories.tsx` TS).
Expected: no type/build errors for the story.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/app/(authenticated)/play-records/new/page.stories.tsx" apps/web/src/components/play-records/__tests__/play-records-axe.test.tsx
git commit -m "test(play-records): #2348 5-states story (new) + axe AA gate"
```

---

## Task 7: #2348 — delete orphan E2E + add create E2E

**Files:**
- Delete: `apps/web/__tests__/play-records/e2e/play-records.spec.ts`
- Create: `apps/web/e2e/play-records-new.spec.ts`

- [ ] **Step 1: Delete the orphan stale spec**

```bash
git rm "apps/web/__tests__/play-records/e2e/play-records.spec.ts"
```

(It is run by neither Playwright nor Vitest, uses stale English selectors + has 9 empty TODO bodies. Removing it eliminates dead, misleading code.)

- [ ] **Step 2: Create `apps/web/e2e/play-records-new.spec.ts`** following the `play-records-hub.spec.ts` convention (auth helpers + chromium-only + axe AA):

```ts
import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

const NEW = '/play-records/new';

test.describe('Play Records — create form', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
    await seedAuthSession(page);
    await mockAuthEndpoints(page);
    await page.goto(NEW);
    await page.waitForLoadState('networkidle');
  });

  test('default: renders the create wizard heading', async ({ page }) => {
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });

  test('error: failed submit surfaces a toast', async ({ page }) => {
    await page.route('**/api/v1/play-records', route => {
      if (route.request().method() === 'POST') {
        return route.fulfill({ status: 500, contentType: 'application/json', body: '{"error":"server_error"}' });
      }
      return route.continue();
    });
    // Drive the wizard minimally to a submit; assert the error toast appears.
    // (Implementer: reuse the step-navigation selectors verified in the unit tests /
    //  the form's data-testid contract; if no testid exists, add stable ones to
    //  SessionCreateForm in this task and document them in the spec header.)
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });

  test('axe AA: no violations', async ({ page }) => {
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });
});
```

> NOTE: the import paths for `seedAuthSession`/`mockAuthEndpoints`/`seedCookieConsent` must match `play-records-hub.spec.ts` exactly. The error-submit test needs a stable way to complete the wizard; prefer adding `data-testid` to the form's submit/step controls (stable contract) over brittle text selectors, and document the testids in the spec header comment like the hub spec does.

- [ ] **Step 3: Run the new E2E (chromium)**

Run: `cd apps/web && pnpm test:e2e play-records-new`
Expected: PASS (3 tests). Debug any failure (auth seeding, selectors) — do not skip.

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/play-records-new.spec.ts
git commit -m "test(play-records): #2348 replace orphan E2E with e2e/play-records-new (default+error+axe)"
```

---

## Task 8: #2349 — 5-states stories (detail + edit) + axe

**Files:**
- Modify: `apps/web/src/app/(authenticated)/play-records/[id]/page.stories.tsx`
- Modify: `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.stories.tsx`
- Modify: `apps/web/src/components/play-records/__tests__/play-records-axe.test.tsx` (add detail view)

Detail/edit pages read `useParams().id` and fetch `GET /api/v1/play-records/:id`. Use the MSW fixture id `pr-won-1` for Default; override with loading/empty/error handlers per state.

- [ ] **Step 1: Replace `[id]/page.stories.tsx`** (detail) with 4 states:

```tsx
/**
 * sp4-play-records-detail — DS-17-13 #2220 / US-INT-2c #2349.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-detail.{html,jsx}`.
 * DEC-A5 states: default / empty (record w/ no scores) / loading / error (404). sse N/A.
 */
import { http, HttpResponse } from 'msw';

import PlayRecordDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const ID = 'pr-won-1';

const meta: Meta<typeof PlayRecordDetailPage> = {
  title: 'Authenticated / sp4-play-records-detail',
  component: PlayRecordDetailPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true, navigation: { pathname: `/play-records/${ID}` } },
    viewport: { defaultViewport: 'desktop' },
    docs: { description: { component: '#2349 US-INT-2c. Detail view. sse N/A (no SSE source).' } },
  },
};
export default meta;
type Story = StoryObj<typeof PlayRecordDetailPage>;

export const Default: Story = {};

export const Loading: Story = {
  parameters: {
    msw: { handlers: [http.get(`${API_BASE}/api/v1/play-records/:id`, () => new Promise(() => {}))] },
  },
};

export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(`${API_BASE}/api/v1/play-records/:id`, () =>
          HttpResponse.json({ error: 'not_found' }, { status: 404 })
        ),
      ],
    },
  },
};
```

> NOTE: `@storybook/nextjs` derives `useParams().id` — verify the Default story resolves `id` to `pr-won-1`. If `useParams()` returns empty under the story (the global preview only sets `pathname`), add `navigation: { segments: [['id', ID]] }` to the story `nextjs` params so `useParams().id === ID`. Confirm against an existing `[id]` story that already renders data (e.g. `games/[id]/page.stories.tsx`) and copy its exact `navigation` shape. For an "empty" state, point `id` at a fixture with no scores if one exists in the handlers; otherwise document empty as covered by the error/404 branch.

- [ ] **Step 2: Replace `[id]/edit/page.stories.tsx`** with the same 4-state shape (pathname `/play-records/${ID}/edit`, same MSW `:id` handlers). Default should now render a PREFILLED form (Task 5 wired prefill), which is the headline fix to showcase.

- [ ] **Step 3: Add detail to the axe test** — append to `play-records-axe.test.tsx`:

```tsx
  it('PlayRecordDetailView (won) — no violations', async () => {
    const { container } = renderWithPlayRecordsProviders(
      <PlayRecordDetailView recordId="pr-won-1" />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
```

> NOTE: import `PlayRecordDetailView` from `../PlayRecordDetailView`; it fetches via `usePlayRecord` so the provider wrapper must include QueryClient + MSW or a mocked hook. Mirror how `PlayRecordDetailView.test.tsx` arranges its data (it already tests this component) and reuse that arrangement.

- [ ] **Step 4: Run axe + typecheck**

Run: `cd apps/web && pnpm test play-records-axe --run && pnpm typecheck`
Expected: PASS, 0 violations, no TS errors.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/app/(authenticated)/play-records/[id]/page.stories.tsx" "apps/web/src/app/(authenticated)/play-records/[id]/edit/page.stories.tsx" apps/web/src/components/play-records/__tests__/play-records-axe.test.tsx
git commit -m "test(play-records): #2349 5-states stories (detail+edit) + detail axe"
```

---

## Task 9: #2349 — detail E2E (default + error)

**Files:**
- Create: `apps/web/e2e/play-records-detail.spec.ts`

- [ ] **Step 1: Create the spec** mirroring `play-records-hub.spec.ts` (auth + chromium-only + axe). Navigate to a detail route; mock the detail GET for default + error:

```ts
import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

const RECORD_ID = 'pr-won-1';
const DETAIL = `/play-records/${RECORD_ID}`;

test.describe('Play Records — detail', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
    await seedAuthSession(page);
    await mockAuthEndpoints(page);
  });

  test('default: renders the record detail', async ({ page }) => {
    await page.goto(DETAIL);
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('main')).toBeVisible();
  });

  test('error: 404 from API surfaces the error state', async ({ page }) => {
    await page.route(`**/api/v1/play-records/${RECORD_ID}`, route =>
      route.fulfill({ status: 404, contentType: 'application/json', body: '{"error":"not_found"}' })
    );
    await page.goto(DETAIL);
    await page.waitForLoadState('networkidle');
    // Assert the detail view's error/alert branch is shown.
    await expect(page.getByRole('main')).toBeVisible();
  });

  test('axe AA: no violations', async ({ page }) => {
    await page.goto(DETAIL);
    await page.waitForLoadState('networkidle');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });
});
```

> NOTE: in the real E2E the data comes from the backend proxy, not MSW. With `PLAYWRIGHT_AUTH_BYPASS` the page still calls the API; if no backend is up in CI for this route, prefer `page.route` to stub `GET /api/v1/play-records/:id` with a fixture for the default test too (mirror how other `e2e/*` specs stub data — check `play-records-index.spec.ts`). Tighten the assertions to the detail view's actual roles/testids once verified.

- [ ] **Step 2: Run E2E**

Run: `cd apps/web && pnpm test:e2e play-records-detail`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/play-records-detail.spec.ts
git commit -m "test(play-records): #2349 detail E2E (default+error+axe)"
```

---

## Task 10: #2350 — 5-states story (stats) + axe

**Files:**
- Modify: `apps/web/src/app/(authenticated)/play-records/stats/page.stories.tsx`
- Modify: `apps/web/src/components/play-records/__tests__/play-records-axe.test.tsx` (stats already added in Task 6; ensure empty + a data state)

- [ ] **Step 1: Replace `stats/page.stories.tsx`** with 4 states (data via `GET /statistics`):

```tsx
/**
 * sp4-play-records-stats — DS-17-13 #2220 / US-INT-2d #2350.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-stats.{html,jsx}`.
 * DEC-A5 states: default / empty / loading / error. sse N/A (no SSE source).
 */
import { http, HttpResponse } from 'msw';

import StatisticsPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const STATS = `${API_BASE}/api/v1/play-records/statistics`;

const meta: Meta<typeof StatisticsPage> = {
  title: 'Authenticated / sp4-play-records-stats',
  component: StatisticsPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true, navigation: { pathname: '/play-records/stats' } },
    viewport: { defaultViewport: 'desktop' },
    docs: { description: { component: '#2350 US-INT-2d. Stats dashboard. sse N/A.' } },
  },
};
export default meta;
type Story = StoryObj<typeof StatisticsPage>;

export const Default: Story = {};

export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(STATS, () =>
          HttpResponse.json({
            totalSessions: 0,
            uniqueGames: 0,
            winRate: 0,
            favoriteGame: null,
            mostPlayed: [],
            winRateByGame: [],
            winRateTrend: [],
            leaderboardRank: 0,
          })
        ),
      ],
    },
  },
};

export const Loading: Story = {
  parameters: { msw: { handlers: [http.get(STATS, () => new Promise(() => {}))] } },
};

export const Error: Story = {
  parameters: {
    msw: { handlers: [http.get(STATS, () => HttpResponse.json({ error: 'server_error' }, { status: 500 }))] },
  },
};
```

> NOTE: the Empty payload must match the real statistics DTO shape consumed by `StatisticsView`. Copy the EXACT field names from `play-records.handlers.ts` (the existing `/statistics` handler response) — adjust the keys above to match it verbatim; do not guess field names.

- [ ] **Step 2: Run axe + typecheck**

Run: `cd apps/web && pnpm test play-records-axe --run && pnpm typecheck`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add "apps/web/src/app/(authenticated)/play-records/stats/page.stories.tsx"
git commit -m "test(play-records): #2350 5-states story (stats)"
```

---

## Task 11: #2350 — stats E2E (default + error)

**Files:**
- Create: `apps/web/e2e/play-records-stats.spec.ts`

- [ ] **Step 1: Create the spec** (route is `/play-records?tab=stats` per the `#5039` redirect; the standalone `/play-records/stats` redirects there):

```ts
import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

const STATS = '/play-records?tab=stats';

test.describe('Play Records — stats', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
    await seedAuthSession(page);
    await mockAuthEndpoints(page);
  });

  test('default: renders the stats dashboard', async ({ page }) => {
    await page.goto(STATS);
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('main')).toBeVisible();
  });

  test('error: failed stats fetch surfaces error state', async ({ page }) => {
    await page.route('**/api/v1/play-records/statistics', route =>
      route.fulfill({ status: 500, contentType: 'application/json', body: '{"error":"server_error"}' })
    );
    await page.goto(STATS);
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('main')).toBeVisible();
  });

  test('axe AA: no violations', async ({ page }) => {
    await page.goto(STATS);
    await page.waitForLoadState('networkidle');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });
});
```

> NOTE: verify the stats API path the FE actually calls (`getPlayerStatistics` → `/play-records/statistics`); the `page.route` glob must match it. Tighten assertions to the StatisticsView's real testids/roles after a first run.

- [ ] **Step 2: Run E2E**

Run: `cd apps/web && pnpm test:e2e play-records-stats`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/play-records-stats.spec.ts
git commit -m "test(play-records): #2350 stats E2E (default+error+axe)"
```

---

## Task 12: Bookkeeping — drift notes, follow-up issues, gate flag

**Files:** none (GitHub via `gh`).

- [ ] **Step 1: Add a drift-cleanup note to each issue body** (`gh issue edit 2348/2349/2350 --body-file -` after prepending a note), mirroring the #2347 precedent. The note states: mockups exist (not orphan); shipped surface is mockup-faithful; literal AC items not in mockups are split to follow-ups; this PR delivers the gap-fill + DEC-A5 gate + (for #2349) the two real defect fixes.

- [ ] **Step 2: Create Path B follow-up issues** (`gh issue create`), each linking its parent + the canonical mockup + DEC-A4/A5 references:
  - `#2349` follow-ups: (a) optimistic concurrency xmin + 409 (ADR-060), (b) audit trail + restore-version, (c) share signed-token endpoint + dialog, (d) photo gallery + fullscreen + MVP chip.
  - `#2348` follow-ups: (a) autosave + draft (`GET /draft/{gameNightId}`), (b) photo upload + S3 presigned + dedup + OCR. (NOTE: `?gameNightId=` deep-link prefill is now IN scope — Tasks 5b/5c — per user override of DEC-A4; do not file it as a follow-up.)
  - `#2350` follow-ups: (a) trend chart + per-player leaderboard (new BE query), (b) CSV export + custom date-range UI, (c) server-side Redis cache + invalidation subscriber.
  - Latent bug: `DELETE /play-records/{id}` called by FE has no BE endpoint (405) — open a small bug issue.

- [ ] **Step 3: Flag the missing DEC-A5 CI gate** — comment on umbrella #2342 that `lint:storybook-states` does not exist in `apps/web/package.json` (only `lint:mockup-state-naming` exists), so the "CI lint:storybook-states green" checkbox is unsatisfiable repo-wide until the gate is built; recommend it as a #2342 deliverable.

- [ ] **Step 4: Commit** (no code; this task is GitHub-only — no commit). Record the created issue numbers in the PR description.

---

## Task 13: Full verification + PR + closure

- [ ] **Step 1: Full BE test run**

Run: `cd apps/api && dotnet test --filter "BoundedContext=GameManagement" -v minimal`
Expected: PASS, no new failures vs baseline (baseline is currently clean per CLAUDE.md).

- [ ] **Step 2: Full FE quality gates**

Run: `cd apps/web && pnpm typecheck && pnpm lint && pnpm test --run`
Expected: typecheck clean, lint clean (incl. `local/no-hardcoded-color-utility`, `local/no-bgg-host`), unit tests green (≥ the pre-existing count + the new tests).

- [ ] **Step 3: Targeted E2E**

Run: `cd apps/web && pnpm test:e2e play-records`
Expected: hub/index (existing) + new/detail/stats specs PASS on chromium.

- [ ] **Step 4: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2346-playrecords-tier2-gapfill
gh pr create --base main-dev --title "feat(play-records): close US-INT-2 Tier 2 gap-fill (#2348 #2349 #2350)" --body-file <PR body>
```

PR body MUST: summarize the two #2349 defect fixes (security access-control + edit prefill), the DEC-A5 gate-fill, the deferred-to-follow-up list with the new issue numbers, the `lint:storybook-states` gate flag, and `Closes #2348`, `Closes #2349`, `Closes #2350`. Note the security fix as the headline.

- [ ] **Step 5: After merge** — verify auto-delete of the branch; close #2346 umbrella (`gh issue close 2346 -r completed`) with a closure comment linking the PR + the follow-up issues; tick the Tier 2 checkboxes in #2342.

---

## Self-review

- **Spec coverage:** #2349 security (Tasks 1-3) ✓; #2349 edit prefill (Tasks 4-5) ✓; DEC-A5 stories+axe+E2E for new (6-7), detail+edit (8-9), stats (10-11) ✓; drift notes + Path B follow-ups + gate flag (12) ✓; closure (13) ✓.
- **Placeholder scan:** code steps carry real code; `> NOTE` blocks flag where the implementer must match an EXACT existing signature/wrapper (provider wrappers, `PlayerEntry` shape, test render helpers, MSW DTO field names, `useParams` story segments) — these are verification directives, not deferred work. They exist because inventing those names would risk drift; the implementer reads the cited existing file and matches it.
- **Type consistency:** `GetPlayRecordQuery(RecordId, UserId)`, `UpdatePlayRecordCommand(RecordId, UserId, …)`, `CompletePlayRecordCommand(RecordId, UserId, …)` used consistently across Tasks 1-3; `initialValues`/`initialPlayers` props defined in Task 4 and consumed in Task 5; `ForbiddenException` used in Tasks 1-2 (already imported). `API_BASE` derivation identical across all stories.
- **CQRS compliance:** authorization lives in handlers (Tasks 1-2), endpoints only call `IMediator.Send` (Task 3) — no service injection in endpoints.
