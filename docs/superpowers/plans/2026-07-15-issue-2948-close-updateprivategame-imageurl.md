# Close UpdatePrivateGameCommand.ImageUrl + dead FE image-URL inputs (Issue #2948) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the last user-side external-image-URL channel (`PUT /private-games/{id}`) and delete the now-dead FE image-URL inputs, mirroring the Add-side compliance fix already merged in PR #2943.

**Architecture:** The private-game cover is set only via the cover-from-PDF materialization flow (feature #2943), never via a user-supplied URL. This plan strips `ImageUrl` from the `UpdatePrivateGameCommand` / `UpdatePrivateGameRequest` / handler / validator on the backend (so a PUT can no longer set an external URL), stops `PrivateGame.UpdateInfo` from mutating the `ImageUrl` field (so an update PRESERVES a PDF-materialized cover instead of wiping it), and removes the dead `imageUrl`/`thumbnailUrl` inputs from the three FE surfaces that feed the add/update endpoints. The domain field `PrivateGame.ImageUrl` itself is untouched, as are all admin shared-game forms (legitimate per ADR-059 §2).

**Tech Stack:** .NET 9 (ASP.NET Minimal APIs + MediatR CQRS, FluentValidation, xUnit + Moq + FluentAssertions), Next.js 16 / React 19 / TypeScript (Zod + react-hook-form, Vitest).

## Global Constraints

- **Branch:** `feature/issue-2948-close-updateprivategame-imageurl`, created from `main-dev` (HEAD `963ebbd65`). Pre-creation safety check: `git branch --show-current` MUST print `main-dev`, `git status` MUST be clean, `git pull --ff-only` MUST succeed, THEN `git checkout -b feature/issue-2948-close-updateprivategame-imageurl`.
- **PR target:** parent branch `main-dev` (NOT `main`). Set `git config branch.feature/issue-2948-close-updateprivategame-imageurl.parent main-dev`.
- **Commit convention:** `feat|fix|refactor|test|chore(scope): description`.
- **Compliance rationale (verbatim in every code comment that forces the closure):** `// BGG freeze #2123 / ADR-059`. External image URLs must not be settable via any user-side channel; BGG imagery stays admin-only, server-to-server (ADR-059 §2).
- **Do NOT touch:** the domain field `PrivateGame.ImageUrl` (property stays; only `UpdateInfo` stops writing it); read-only DTO/display usages (`GameInfoStep.tsx:222-225`, `PrivateGameCard` `imageUrl={game.imageUrl || undefined}`); `PrivateGameDtoSchema.imageUrl/thumbnailUrl` (response shape); `PrivateGame.SyncFromBgg` (admin/BGG-sync path); ALL admin shared-game forms (`shared-games/new/client.tsx`, `EditGameDrawer.tsx`, `GameForm.tsx`) and the `mode === 'admin'` branch of `GameCreationStep.tsx` (`api.games.create` / `api.games.update` / `uploadImage`).
- **.NET GOTCHAs:** Meziantou MA0025 blocks `throw new NotImplementedException()` stubs → do real TDD (red via a *real* failing assertion, then real minimal impl). SonarAnalyzer S1135 makes a `// TODO(...)` comment a BUILD ERROR → use `// Follow-up:` if annotating. Endpoints use ONLY `IMediator.Send()` (no direct service injection). Exceptions: `ConflictException`(409) / `NotFoundException`(404) / `ForbiddenException` — never `InvalidOperationException`(500). Kill any lingering `testhost` before running tests.
- **Test commands (this repo):** BE `cd apps/api/src/Api && dotnet test --filter "<FullyQualifiedName~...>"`. FE `cd apps/web && pnpm test <path>`.
- **Baseline rule:** PRs MUST NOT grow the unit-test fail count above the current zero baseline.

---

## File Structure

**Backend — modify (strip `ImageUrl` from the Update channel):**
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/PrivateGames/UpdatePrivateGameCommand.cs` — remove the `string? ImageUrl` record parameter + its XML doc line.
- `apps/api/src/Api/Routing/PrivateGameEndpoints.cs` — remove `ImageUrl` from `UpdatePrivateGameRequest` record and from the `new UpdatePrivateGameCommand(...)` mapping.
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdatePrivateGameCommandHandler.cs` — stop passing `imageUrl:` to `UpdateInfo`.
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/PrivateGames/UpdatePrivateGameCommandValidator.cs` — drop the `ImageUrl` rule.
- `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/PrivateGame.cs` — remove the `imageUrl` parameter from `UpdateInfo` and the `ImageUrl = imageUrl;` assignment (preserve the existing cover). Domain field untouched.

**Backend — create/modify tests:**
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/PrivateGames/UpdatePrivateGameNoExternalUrlTests.cs` — NEW regression test mirroring `AddPrivateGameNoExternalUrlTests.cs`.
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Validators/PrivateGames/UpdatePrivateGameCommandValidatorTests.cs` — remove `imageUrl` helper param + `ImageUrl` region + strip `ImageUrl:` args.
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Handlers/PrivateGames/UpdatePrivateGameCommandHandlerTests.cs` — strip `ImageUrl:` args, and change the 3 assertions that encoded "Update sets ImageUrl" to assert the pre-existing cover is PRESERVED.
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Domain/Entities/PrivateGameTests.cs` — strip the `imageUrl:` arg from the 3 `UpdateInfo(...)` calls (else `CS1739`/`CS1501`), and flip `UpdateInfo_WithValidParameters_UpdatesAllFields`'s `game.ImageUrl.Should().Be("https://example.com/new.jpg")` to `.Should().BeNull()` (cover is preserved: `CreateValidManualGame()` builds with a null cover and `UpdateInfo` no longer writes `ImageUrl`).
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Infrastructure/PrivateGameRepositoryIntegrationTests.cs` — strip the `imageUrl:` arg from the `existing!.UpdateInfo(...)` call in `UpdateAsync_ModifiesExistingGame` (else `CS1739`/`CS1501`); no assert change (its assert block never touches `ImageUrl`).

**Frontend — modify (remove dead image-URL inputs feeding add/update):**
- `apps/web/src/lib/api/schemas/private-games.schemas.ts` — remove `imageUrl`/`thumbnailUrl` from `AddPrivateGameRequestSchema` (lines 52-53) and `imageUrl` from `UpdatePrivateGameRequestSchema` (line 73). Keep `PrivateGameDtoSchema.imageUrl/thumbnailUrl` (response, read-only).
- `apps/web/src/components/library/AddPrivateGameForm.tsx` — remove the `imageUrl` schema field, its `defaultValues` entry, and the Image URL input block (lines 230-241).
- `apps/web/src/app/(authenticated)/library/private/PrivateGamesClient.tsx` — remove `imageUrl` from `handleEditGame` PUT body (:188), from `EditFormSchema` (:588), from `EditPrivateGameFormInner` `defaultValues` (:622), and the edit Image URL input block (:740-743).
- `apps/web/src/app/(authenticated)/admin/wizard/steps/GameCreationStep.tsx` — in the `mode === 'user'` branch only: stop passing `imageUrl`/`thumbnailUrl` to `addPrivateGame`, and delete the follow-up "set private-game cover via URL/upload" block that calls `updatePrivateGame({ ..., imageUrl })`. Also mode-gate the "Cover Image" `<Card>` (currently rendered unconditionally, lines 343-393) to `mode === 'admin'` so users no longer see an inert cover-URL/upload input. Admin branch (`api.games.*`) + admin-only Cover Image Card untouched (ADR-059 §2).
- `apps/web/src/locales/it.json` + `apps/web/src/locales/en.json` — prune the now-orphaned `privateGameForm.imageUrl` + `privateGameForm.imageUrlPlaceholder` keys.

---

## Task 1: Backend — strip ImageUrl from the Update command channel (command + request + handler + validator + domain)

This is one reviewer-gate unit: the command record, its request DTO, the endpoint mapping, the handler, the validator, and the domain `UpdateInfo` signature all change together and must compile as a set. The regression test drives it.

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/PrivateGames/UpdatePrivateGameNoExternalUrlTests.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/PrivateGames/UpdatePrivateGameCommand.cs:20-33`
- Modify: `apps/api/src/Api/Routing/PrivateGameEndpoints.cs:200-212` (command mapping) + `:456-466` (request DTO)
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdatePrivateGameCommandHandler.cs:51-60`
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/PrivateGames/UpdatePrivateGameCommandValidator.cs:62-65`
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/PrivateGame.cs:252-277`
- Test: the new `UpdatePrivateGameNoExternalUrlTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `UpdatePrivateGameCommand(Guid PrivateGameId, Guid UserId, string Title, int MinPlayers, int MaxPlayers, int? YearPublished, string? Description, int? PlayingTimeMinutes, int? MinAge, decimal? ComplexityRating)` — an `ICommand<PrivateGameDto>` with NO `ImageUrl` parameter. Task 2 (test updates) constructs it with exactly these 10 parameters.
  - `PrivateGame.UpdateInfo(string title, int minPlayers, int maxPlayers, int? yearPublished, string? description, int? playingTimeMinutes, int? minAge, decimal? complexityRating)` — 8 params, NO `imageUrl`; the method no longer writes `ImageUrl`. Task 2 relies on this to assert cover preservation.

- [ ] **Step 1: Write the failing regression test**

Create `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/PrivateGames/UpdatePrivateGameNoExternalUrlTests.cs`:

```csharp
using System.Linq;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Compliance regression test: UpdatePrivateGameCommand must not expose an external URL
/// input channel (ImageUrl). BGG freeze (#2123 / ADR-059) forbids arbitrary user-supplied
/// external image URLs on the PUT /private-games/{id} channel. The private-game cover is
/// set only via the cover-from-PDF flow (#2943).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdatePrivateGameNoExternalUrlTests
{
    [Fact]
    public void UpdatePrivateGameCommand_HasNoExternalUrlFields()
    {
        var props = typeof(UpdatePrivateGameCommand).GetProperties().Select(p => p.Name).ToArray();
        props.Should().NotContain("ImageUrl");
        props.Should().NotContain("ThumbnailUrl");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

First kill any lingering test host, then run:

```
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~UpdatePrivateGameNoExternalUrlTests"
```

Expected: FAIL. The assertion `props.Should().NotContain("ImageUrl")` fails because `UpdatePrivateGameCommand` still declares the `ImageUrl` property (record parameter). FluentAssertions message: `Expected props {…, "ImageUrl"} to not contain "ImageUrl"`.

- [ ] **Step 3: Remove `ImageUrl` from the command record**

In `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/PrivateGames/UpdatePrivateGameCommand.cs`, delete the `/// <param name="ImageUrl">...` doc line (line 20) and remove the trailing `,\n    string? ImageUrl` parameter so the record ends at `ComplexityRating`:

```csharp
/// <param name="ComplexityRating">Updated complexity rating</param>
internal record UpdatePrivateGameCommand(
    Guid PrivateGameId,
    Guid UserId,
    string Title,
    int MinPlayers,
    int MaxPlayers,
    int? YearPublished,
    string? Description,
    int? PlayingTimeMinutes,
    int? MinAge,
    decimal? ComplexityRating
) : ICommand<PrivateGameDto>;
```

- [ ] **Step 4: Remove `ImageUrl` from the request DTO**

In `apps/api/src/Api/Routing/PrivateGameEndpoints.cs`, change the `UpdatePrivateGameRequest` record (lines 456-466) to drop the last parameter:

```csharp
/// <summary>
/// Request DTO for updating a private game.
/// </summary>
internal record UpdatePrivateGameRequest(
    string Title,
    int MinPlayers,
    int MaxPlayers,
    int? YearPublished,
    string? Description,
    int? PlayingTimeMinutes,
    int? MinAge,
    decimal? ComplexityRating
);
```

- [ ] **Step 5: Remove the `ImageUrl` mapping in the endpoint**

In `apps/api/src/Api/Routing/PrivateGameEndpoints.cs`, the `new UpdatePrivateGameCommand(...)` (lines 200-212) must drop the `ImageUrl: request.ImageUrl` argument (and the trailing comma on the previous line):

```csharp
            var command = new UpdatePrivateGameCommand(
                PrivateGameId: id,
                UserId: userId,
                Title: request.Title,
                MinPlayers: request.MinPlayers,
                MaxPlayers: request.MaxPlayers,
                YearPublished: request.YearPublished,
                Description: request.Description,
                PlayingTimeMinutes: request.PlayingTimeMinutes,
                MinAge: request.MinAge,
                ComplexityRating: request.ComplexityRating
            );
```

- [ ] **Step 6: Remove the `imageUrl` parameter from `PrivateGame.UpdateInfo`**

In `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/PrivateGame.cs`, change `UpdateInfo` (lines 252-277) to drop the `imageUrl` parameter and the `ImageUrl = imageUrl;` assignment. This preserves any cover set by the PDF flow instead of overwriting it. The `ImageUrl` property/field is NOT removed:

```csharp
    /// <summary>
    /// Updates the game information.
    /// ImageUrl is intentionally NOT a parameter: the cover is owned by the
    /// cover-from-PDF flow (#2943), never by user-supplied input (BGG freeze #2123 / ADR-059).
    /// An update preserves the existing ImageUrl.
    /// </summary>
    public void UpdateInfo(
        string title,
        int minPlayers,
        int maxPlayers,
        int? yearPublished,
        string? description,
        int? playingTimeMinutes,
        int? minAge,
        decimal? complexityRating)
    {
        ValidateTitle(title);
        ValidatePlayers(minPlayers, maxPlayers);
        ValidateComplexityRating(complexityRating);

        Title = title;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
        YearPublished = yearPublished;
        Description = description;
        PlayingTimeMinutes = playingTimeMinutes;
        MinAge = minAge;
        ComplexityRating = complexityRating;
        UpdatedAt = DateTime.UtcNow;
    }
```

- [ ] **Step 7: Update the handler to stop passing `imageUrl`**

In `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdatePrivateGameCommandHandler.cs`, change the `UpdateInfo` call (lines 51-60) to drop the `imageUrl:` argument:

```csharp
        // Update game information.
        // ImageUrl is deliberately not passed: the cover is materialized by the
        // cover-from-PDF flow (#2943), never from user input (BGG freeze #2123 / ADR-059).
        privateGame.UpdateInfo(
            title: command.Title,
            minPlayers: command.MinPlayers,
            maxPlayers: command.MaxPlayers,
            yearPublished: command.YearPublished,
            description: command.Description,
            playingTimeMinutes: command.PlayingTimeMinutes,
            minAge: command.MinAge,
            complexityRating: command.ComplexityRating);
```

- [ ] **Step 8: Drop the `ImageUrl` rule from the validator**

In `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/PrivateGames/UpdatePrivateGameCommandValidator.cs`, delete the `RuleFor(x => x.ImageUrl)` block (lines 62-65). The `Description` rule immediately follows and stays:

```csharp
        RuleFor(x => x.MinAge)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MinAge cannot be negative")
            .When(x => x.MinAge.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("Description cannot exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
```

- [ ] **Step 9: Run the regression test to verify it passes**

```
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~UpdatePrivateGameNoExternalUrlTests"
```

Expected: PASS (1 test). If the build fails instead, it is because the existing test files in Task 2 still reference `ImageUrl:` (command construction) or `imageUrl:` (`UpdateInfo` calls) — that is expected; those are fixed in Task 2. If the *production* build (Api project) fails, re-check Steps 3-8. Note: the full solution build won't be green until Task 2 fixes the test files; the `--filter` run still compiles the whole test assembly, so a red compile here that points ONLY at `UpdatePrivateGameCommandHandlerTests.cs`, `UpdatePrivateGameCommandValidatorTests.cs`, `PrivateGameTests.cs`, and `PrivateGameRepositoryIntegrationTests.cs` is expected and resolved in Task 2. Proceed to Task 2 before claiming Task 1 done; commit after Task 2 compiles clean.

- [ ] **Step 10: Commit (after Task 2 makes the assembly compile)**

Deferred to Task 2 Step 10 — the test assembly cannot compile until the existing test files are fixed, so Task 1 + Task 2 share one commit. Do NOT commit here.

---

## Task 2: Backend — fix existing Update tests for the new command/handler shape

The command lost `ImageUrl` and `UpdateInfo` no longer writes it. Four existing test files must be updated so the assembly compiles and the assertions reflect cover-preservation: the validator + handler tests construct the command with `ImageUrl:` and assert the old "Update sets ImageUrl" behavior; the domain-entity tests + repository integration test call `UpdateInfo(..., imageUrl:)` (which no longer compiles) and one domain test asserts on `ImageUrl`.

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Validators/PrivateGames/UpdatePrivateGameCommandValidatorTests.cs:29-54, 330-347, 383`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Handlers/PrivateGames/UpdatePrivateGameCommandHandlerTests.cs` (all `ImageUrl:` constructions + 3 assertions)
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Domain/Entities/PrivateGameTests.cs:393-402, 413, 425-434, 448-457` — the 3 `UpdateInfo(...)` calls that pass `imageUrl:` no longer compile after Task 1 Step 6 (`CS1739`/`CS1501`), and the `UpdateInfo_WithValidParameters_UpdatesAllFields` assert on `ImageUrl` must flip to null-preservation.
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Infrastructure/PrivateGameRepositoryIntegrationTests.cs:424-433` — the `existing!.UpdateInfo(...)` call passes `imageUrl:` and no longer compiles after Task 1 Step 6 (`CS1739`/`CS1501`); no assert change needed (the assert block 443-446 does not touch `ImageUrl`).
- Test: all four files above + the new regression test.

**Interfaces:**
- Consumes: `UpdatePrivateGameCommand(...)` (10 params, no `ImageUrl`) and `PrivateGame.UpdateInfo(...)` (8 params, no `imageUrl`, does not write `ImageUrl`) from Task 1.
- Produces: nothing for later tasks.

- [ ] **Step 1: Fix the validator test helper + remove the ImageUrl region**

In `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Validators/PrivateGames/UpdatePrivateGameCommandValidatorTests.cs`:

(a) Remove the `imageUrl` helper parameter (line 40) and the `ImageUrl: imageUrl` argument (line 53). The `CreateValidCommand` helper becomes:

```csharp
    private static UpdatePrivateGameCommand CreateValidCommand(
        Guid? privateGameId = null,
        Guid? userId = null,
        string title = "Test Game",
        int minPlayers = 2,
        int maxPlayers = 4,
        int? yearPublished = null,
        string? description = null,
        int? playingTimeMinutes = null,
        int? minAge = null,
        decimal? complexityRating = null)
    {
        return new UpdatePrivateGameCommand(
            PrivateGameId: privateGameId ?? Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            Title: title,
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers,
            YearPublished: yearPublished,
            Description: description,
            PlayingTimeMinutes: playingTimeMinutes,
            MinAge: minAge,
            ComplexityRating: complexityRating);
    }
```

(b) Delete the entire `#region ImageUrl Validation` block (lines 330-347), i.e. the `ImageUrl_TooLong_HasValidationError` fact and its `#region`/`#endregion` markers.

(c) In `ValidUpdateCommand_NoValidationErrors` (around line 370-390), remove the `imageUrl:` argument from the `CreateValidCommand(...)` call so it ends at `complexityRating: 3.5m`:

```csharp
        var command = CreateValidCommand(
            title: "Updated Game",
            minPlayers: 2,
            maxPlayers: 6,
            yearPublished: 2024,
            description: "Updated description",
            playingTimeMinutes: 90,
            minAge: 12,
            complexityRating: 3.5m);
```

- [ ] **Step 2: Fix the handler test — strip `ImageUrl:` from every command construction**

In `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Handlers/PrivateGames/UpdatePrivateGameCommandHandlerTests.cs`, remove the `ImageUrl: ...` argument (and the trailing comma on the preceding `ComplexityRating:` line) from EVERY `new UpdatePrivateGameCommand(...)`. There are 8 constructions: at the tests `Handle_ValidUpdate_UpdatesAllFields` (~104-115), `Handle_PartialUpdate_ClearsOptionalFields` (~169-180), `Handle_BggGame_PreservesBggIdAndSource` (~228-239), `Handle_GameNotFound_ThrowsNotFoundException` (~274-285), `Handle_DifferentOwner_ThrowsForbiddenException` (~323-334), `Handle_SameOwner_UpdatesSuccessfully` (~367-378), `Handle_UpdatedGame_MapsAllFieldsToDto` (~432-443), and `UpdatePrivateGameCommand_CreatesWithAllProperties` (~494-505). Each construction ends at `ComplexityRating: <value>);` with no `ImageUrl` line. Example for `Handle_ValidUpdate_UpdatesAllFields`:

```csharp
        var command = new UpdatePrivateGameCommand(
            PrivateGameId: gameId,
            UserId: userId,
            Title: "Updated Title",
            MinPlayers: 3,
            MaxPlayers: 6,
            YearPublished: 2024,
            Description: "Updated description",
            PlayingTimeMinutes: 120,
            MinAge: 14,
            ComplexityRating: 3.5m);
```

- [ ] **Step 3: Fix the 3 assertions that encoded "Update sets ImageUrl"**

`UpdateInfo` no longer writes `ImageUrl`, so an update PRESERVES the game's pre-existing cover (null when created manually, or a PDF-materialized value). Fix each assertion to reflect preservation:

(a) In `Handle_ValidUpdate_UpdatesAllFields`, the game is a fresh `CreateManual` (cover null), so line 142 becomes a null-preservation assertion:

```csharp
        result.ComplexityRating.Should().Be(3.5m);
        result.ImageUrl.Should().BeNull(); // preserved: UpdateInfo no longer sets ImageUrl (BGG freeze #2123 / ADR-059)
        result.UpdatedAt.Should().NotBeNull();
```

(b) In `Handle_BggGame_PreservesBggIdAndSource`, the existing game was created with `imageUrl: "https://example.com/old.jpg"`. The update must PRESERVE it (previously the handler would have overwritten it with the command's `"https://example.com/new.jpg"`). Add an explicit preservation assertion next to the existing `ThumbnailUrl` one (around line 260):

```csharp
        result.ThumbnailUrl.Should().Be("https://example.com/old-thumb.jpg"); // Preserved (not updated by UpdateInfo)
        result.ImageUrl.Should().Be("https://example.com/old.jpg"); // Preserved: UpdateInfo no longer touches ImageUrl (BGG freeze #2123 / ADR-059)
        result.Title.Should().Be("Updated BGG Title");
```

(c) In `Handle_UpdatedGame_MapsAllFieldsToDto`, the game is a fresh `CreateManual` (cover null), so line 474 becomes:

```csharp
        result.ComplexityRating.Should().Be(4.0m);
        result.ImageUrl.Should().BeNull(); // preserved: UpdateInfo no longer sets ImageUrl (BGG freeze #2123 / ADR-059)
        result.ThumbnailUrl.Should().BeNull();
```

- [ ] **Step 4: Fix the domain entity tests — strip `imageUrl:` from the 3 `UpdateInfo` calls + flip the ImageUrl assert**

`PrivateGame.UpdateInfo` lost its final `imageUrl` parameter in Task 1 Step 6, so the 3 calls in `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Domain/Entities/PrivateGameTests.cs` that pass `imageUrl:` no longer compile (`CS1739: ... does not have a parameter named 'imageUrl'` on the named-arg calls). Fix all three, and flip the one `ImageUrl` assertion.

(a) In `UpdateInfo_WithValidParameters_UpdatesAllFields` (~lines 393-402), remove the trailing comma on `complexityRating: 3.5m` and delete the `imageUrl: "https://example.com/new.jpg"` argument. The `UpdateInfo` call becomes:

```csharp
        game.UpdateInfo(
            title: "Updated Title",
            minPlayers: 3,
            maxPlayers: 6,
            yearPublished: 2023,
            description: "Updated description",
            playingTimeMinutes: 90,
            minAge: 12,
            complexityRating: 3.5m);
```

Then, at line 413, the assertion `game.ImageUrl.Should().Be("https://example.com/new.jpg");` is now false: `CreateValidManualGame()` builds the game via `PrivateGame.CreateManual(...)` with NO `imageUrl` (cover null), and `UpdateInfo` no longer writes `ImageUrl`, so the cover stays null (preserved). Change the assert to:

```csharp
        game.ComplexityRating.Should().Be(3.5m);
        game.ImageUrl.Should().BeNull(); // preserved: UpdateInfo no longer sets ImageUrl (BGG freeze #2123 / ADR-059)
        game.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
```

(b) In `UpdateInfo_WithInvalidTitle_ThrowsArgumentException` (~lines 425-434), remove the trailing comma on `complexityRating: null` and delete the `imageUrl: null` argument. The call becomes:

```csharp
        var action = () => game.UpdateInfo(
            title: "",
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            yearPublished: null,
            description: null,
            playingTimeMinutes: null,
            minAge: null,
            complexityRating: null);
```

(c) In `UpdateInfo_WithInvalidPlayers_ThrowsArgumentException` (~lines 448-457), remove the trailing comma on `complexityRating: null` and delete the `imageUrl: null` argument. The call becomes:

```csharp
        var action = () => game.UpdateInfo(
            title: ValidTitle,
            minPlayers: 5,
            maxPlayers: 3,
            yearPublished: null,
            description: null,
            playingTimeMinutes: null,
            minAge: null,
            complexityRating: null);
```

- [ ] **Step 5: Fix the repository integration test — strip `imageUrl:` from the `UpdateInfo` call**

`UpdateAsync_ModifiesExistingGame` in `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Infrastructure/PrivateGameRepositoryIntegrationTests.cs` (~lines 424-433) calls `existing!.UpdateInfo(..., imageUrl: "https://example.com/updated.jpg")`, which no longer compiles after Task 1 Step 6. Remove the trailing comma on `complexityRating: 3.0m` and delete the `imageUrl:` argument. No assertion change is needed — the assert block (443-446) checks `Title`/`MinPlayers`/`MaxPlayers`/`UpdatedAt` only, never `ImageUrl`. The call becomes:

```csharp
        existing!.UpdateInfo(
            title: "Updated Title",
            minPlayers: 3,
            maxPlayers: 8,
            yearPublished: 2023,
            description: "Updated",
            playingTimeMinutes: 90,
            minAge: 12,
            complexityRating: 3.0m);
```

Note: this is an `[Trait("Category", "Integration")]` test (Testcontainers Postgres), so it will not run in the Backend Fast (no-Docker) lane, but it lives in the same test assembly and MUST compile — hence the edit here even though Step 6 below only executes the Unit slice.

- [ ] **Step 6: Run the full UpdatePrivateGame test set to verify it fails-then-passes cleanly**

```
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~UpdatePrivateGame"
```

Expected: PASS. This runs `UpdatePrivateGameCommandHandlerTests`, `UpdatePrivateGameCommandValidatorTests`, and `UpdatePrivateGameNoExternalUrlTests`. If a construction still has a stray `ImageUrl:` the build fails with `CS1739: The best overload for 'UpdatePrivateGameCommand' does not have a parameter named 'ImageUrl'` — fix the flagged line.

- [ ] **Step 7: Run the Add-side regression + validator tests to confirm no collateral breakage**

```
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~PrivateGame"
```

Expected: PASS. This includes `AddPrivateGameNoExternalUrlTests`, `AddPrivateGameCommandHandlerTests`, `AddPrivateGameCommandValidatorTests`, and all the Update tests. Confirms the shared `PrivateGame.UpdateInfo` signature change did not break Add-side or domain tests.

- [ ] **Step 8: Run the `PrivateGame` domain entity tests**

```
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~PrivateGameTests"
```

Expected: PASS. This runs `UpdateInfo_WithValidParameters_UpdatesAllFields` (now asserting `ImageUrl.Should().BeNull()` per Step 4a), `UpdateInfo_WithInvalidTitle_ThrowsArgumentException`, and `UpdateInfo_WithInvalidPlayers_ThrowsArgumentException` (both now calling `UpdateInfo` with 8 args per Steps 4b/4c). If a call still passes `imageUrl:`, the build fails with `CS1739: ... does not have a parameter named 'imageUrl'` — fix the flagged line. (Search for stragglers: `grep -rn "UpdateInfo(" apps/api/tests | grep -i imageurl`.)

- [ ] **Step 9: Verify no other production caller of `PrivateGame.UpdateInfo` broke**

```
cd apps/api/src/Api && dotnet build
```

Expected: BUILD SUCCEEDED. `PrivateGame.UpdateInfo` is called only from `UpdatePrivateGameCommandHandler.cs:51` (already fixed in Task 1 Step 7); the `UpdateInfo` calls in `SharedGameCatalog` operate on the `SharedGame` aggregate, not `PrivateGame`, and are unaffected. This also compiles the full test assembly, catching any remaining `imageUrl:` straggler in `PrivateGameTests.cs` or `PrivateGameRepositoryIntegrationTests.cs`.

- [ ] **Step 10: Commit Tasks 1 + 2 together**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/PrivateGames/UpdatePrivateGameCommand.cs apps/api/src/Api/Routing/PrivateGameEndpoints.cs apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdatePrivateGameCommandHandler.cs apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/PrivateGames/UpdatePrivateGameCommandValidator.cs apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/PrivateGame.cs apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/PrivateGames/UpdatePrivateGameNoExternalUrlTests.cs apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Validators/PrivateGames/UpdatePrivateGameCommandValidatorTests.cs apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Handlers/PrivateGames/UpdatePrivateGameCommandHandlerTests.cs apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Domain/Entities/PrivateGameTests.cs apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Infrastructure/PrivateGameRepositoryIntegrationTests.cs
git commit -m "fix(compliance): rimuove ImageUrl da UpdatePrivateGame (#2123)"
```

---

## Task 3: Frontend — remove `imageUrl`/`thumbnailUrl` from the private-games Zod request schemas

**Files:**
- Modify: `apps/web/src/lib/api/schemas/private-games.schemas.ts:52-53` (Add) + `:73` (Update)
- Test: `apps/web/src/lib/api/schemas` typecheck (no dedicated unit test file exists for these schemas; the FE compiler is the gate).

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `AddPrivateGameRequest` type WITHOUT `imageUrl`/`thumbnailUrl`.
  - `UpdatePrivateGameRequest` type WITHOUT `imageUrl`.
  - Both consumed by Task 4 (`AddPrivateGameForm`), Task 5 (`PrivateGamesClient`), and Task 6 (`GameCreationStep`).

- [ ] **Step 1: Write the failing typecheck expectation (grep gate)**

There is no Vitest file asserting schema shape; the gate is `tsc` + a grep proving the keys are gone. First confirm the keys are present (this is the RED state):

```
cd apps/web && grep -nE "imageUrl|thumbnailUrl" src/lib/api/schemas/private-games.schemas.ts
```

Expected (RED): prints lines 28, 29 (DTO — must STAY), 52, 53 (Add request — must GO), 73 (Update request — must GO).

- [ ] **Step 2: Remove the fields from the two request schemas**

In `apps/web/src/lib/api/schemas/private-games.schemas.ts`, delete lines 52-53 (`imageUrl` + `thumbnailUrl`) from `AddPrivateGameRequestSchema` and line 73 (`imageUrl`) from `UpdatePrivateGameRequestSchema`. Result — `AddPrivateGameRequestSchema` object ends at `complexityRating`:

```ts
export const AddPrivateGameRequestSchema = z
  .object({
    source: PrivateGameSourceSchema,
    bggId: z.number().int().positive().nullable().optional(),
    title: z.string().min(1).max(200),
    minPlayers: z.number().int().min(1).max(99),
    maxPlayers: z.number().int().min(1).max(99),
    yearPublished: z.number().int().min(1900).max(2100).nullable().optional(),
    description: z.string().max(5000).nullable().optional(),
    playingTimeMinutes: z.number().int().min(1).max(10000).nullable().optional(),
    minAge: z.number().int().min(0).max(99).nullable().optional(),
    complexityRating: z.number().min(0).max(5).nullable().optional(),
  })
  .refine(data => data.maxPlayers >= data.minPlayers, {
    message: 'Max players must be greater than or equal to min players',
    path: ['maxPlayers'],
  });
```

and `UpdatePrivateGameRequestSchema` object ends at `complexityRating`:

```ts
export const UpdatePrivateGameRequestSchema = z
  .object({
    title: z.string().min(1).max(200),
    minPlayers: z.number().int().min(1).max(99),
    maxPlayers: z.number().int().min(1).max(99),
    yearPublished: z.number().int().min(1900).max(2100).nullable().optional(),
    description: z.string().max(5000).nullable().optional(),
    playingTimeMinutes: z.number().int().min(1).max(10000).nullable().optional(),
    minAge: z.number().int().min(0).max(99).nullable().optional(),
    complexityRating: z.number().min(0).max(5).nullable().optional(),
  })
  .refine(data => data.maxPlayers >= data.minPlayers, {
    message: 'Max players must be greater than or equal to min players',
    path: ['maxPlayers'],
  });
```

Leave `PrivateGameDtoSchema.imageUrl`/`thumbnailUrl` (lines 28-29) untouched — that is the read-only response shape.

- [ ] **Step 3: Verify the request keys are gone but the DTO keys remain**

```
cd apps/web && grep -nE "imageUrl|thumbnailUrl" src/lib/api/schemas/private-games.schemas.ts
```

Expected (GREEN): prints ONLY lines 28-29 (the `PrivateGameDtoSchema` fields). No lines inside `AddPrivateGameRequestSchema`/`UpdatePrivateGameRequestSchema`.

- [ ] **Step 4: Typecheck — expect failures in the consuming files (drives Tasks 4-6)**

```
cd apps/web && pnpm typecheck
```

Expected: FAIL with errors in the files that still reference the removed request keys — `AddPrivateGameForm.tsx` (`imageUrl` in its own form schema is independent, so it may NOT error here) and specifically `GameCreationStep.tsx` (`imageUrl: null`/`thumbnailUrl: null` passed to `addPrivateGame`, and `imageUrl: finalImageUrl` passed to `updatePrivateGame`) — `TS2353: Object literal may only specify known properties`. `PrivateGamesClient.tsx` `handleEditGame` passing `imageUrl` to `updatePrivateGame` also errors. These are fixed in Tasks 4-6. Do NOT commit yet — the schema change and its consumers land together for a compiling tree.

---

## Task 4: Frontend — remove the dead Image URL input from `AddPrivateGameForm`

**Files:**
- Modify: `apps/web/src/components/library/AddPrivateGameForm.tsx:37` (schema field), `:82` (defaultValues), `:230-241` (input block)
- Test: `apps/web/src/components/library/AddPrivateGameForm.tsx` typecheck (no dedicated Vitest file; the play-records test stubs this component and does not reference `imageUrl`).

**Interfaces:**
- Consumes: nothing from Task 3 directly (this form has its OWN schema), but it produces the shared `AddPrivateGameFormData` type consumed by Task 5.
- Produces: `AddPrivateGameFormData` type WITHOUT `imageUrl`. Task 5's `EditPrivateGameFormInner` and `handleEditGame` rely on this narrowed type.

- [ ] **Step 1: Confirm the RED state (grep)**

```
cd apps/web && grep -nE "imageUrl|imageUrlPlaceholder|Image URL" src/components/library/AddPrivateGameForm.tsx
```

Expected (RED): prints lines 37 (schema), 82 (defaultValues), 232, 237, 240 (input block + label + error).

- [ ] **Step 2: Remove the `imageUrl` schema field**

In `apps/web/src/components/library/AddPrivateGameForm.tsx`, delete line 37 (`imageUrl: z.string().url('Invalid URL').nullable().optional().or(z.literal('')),`) from `AddPrivateGameFormSchema`. The object now ends at `description`:

```ts
    description: z.string().max(5000, 'Description too long').nullable().optional(),
  })
  .refine(data => data.maxPlayers >= data.minPlayers, {
```

- [ ] **Step 3: Remove the `imageUrl` defaultValues entry**

Delete line 82 (`imageUrl: initialValues?.imageUrl ?? undefined,`) from the `defaultValues` object in `useForm(...)`. It now ends at `description`:

```ts
      description: initialValues?.description ?? undefined,
    },
  });
```

- [ ] **Step 4: Remove the Image URL input block**

Delete the entire `{/* Image URL */}` block (lines 230-241):

```tsx
      {/* Image URL */}
      <div className="space-y-2">
        <Label htmlFor="imageUrl">{t('privateGameForm.imageUrl')}</Label>
        <Input
          id="imageUrl"
          type="url"
          {...register('imageUrl')}
          placeholder={t('privateGameForm.imageUrlPlaceholder')}
          disabled={isSubmitting}
        />
        {errors.imageUrl && <p className="text-sm text-destructive">{errors.imageUrl.message}</p>}
      </div>
```

so the `{/* Description */}` block is immediately followed by `{/* Form Actions */}`.

- [ ] **Step 5: Verify the field is gone**

```
cd apps/web && grep -nE "imageUrl" src/components/library/AddPrivateGameForm.tsx
```

Expected (GREEN): no matches.

- [ ] **Step 6: Run the component's typecheck via the play-records test that imports it**

```
cd apps/web && pnpm test src/components/play-records/__tests__/SessionCreateForm.test.tsx
```

Expected: PASS. This suite mocks `@/components/library/AddPrivateGameForm` and does not reference `imageUrl`, so removing the field must not break it. (Full-tree typecheck still shows Task 5/6 errors until those tasks land — that is expected.)

---

## Task 5: Frontend — remove the edit Image URL channel from `PrivateGamesClient`

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/private/PrivateGamesClient.tsx:188` (PUT body), `:588` (EditFormSchema), `:622` (defaultValues), `:740-743` (input block)
- Test: `apps/web/src/app/(authenticated)/library/private/PrivateGamesClient.tsx` typecheck (no dedicated Vitest file).

**Interfaces:**
- Consumes: `UpdatePrivateGameRequest` (no `imageUrl`) from Task 3; `AddPrivateGameFormData` (no `imageUrl`) from Task 4.
- Produces: nothing for later tasks.

- [ ] **Step 1: Confirm the RED state (grep)**

```
cd apps/web && grep -nE "imageUrl" "src/app/(authenticated)/library/private/PrivateGamesClient.tsx"
```

Expected (RED): prints lines 188 (PUT body), 542 (card `imageUrl={game.imageUrl || undefined}` — must STAY, read-only display), 588 (EditFormSchema), 622 (defaultValues), 741, 742 (input block).

- [ ] **Step 2: Remove `imageUrl` from the update PUT body**

In `handleEditGame` (lines 179-189), delete line 188 (`imageUrl: data.imageUrl || null,`). The `updatePrivateGame` call now ends at `description`:

```ts
      await api.library.updatePrivateGame(selectedGame.id, {
        title: data.title,
        minPlayers: data.minPlayers,
        maxPlayers: data.maxPlayers,
        yearPublished: data.yearPublished ?? null,
        playingTimeMinutes: data.playingTimeMinutes ?? null,
        minAge: data.minAge ?? null,
        complexityRating: data.complexityRating ?? null,
        description: data.description ?? null,
      });
```

- [ ] **Step 3: Remove `imageUrl` from `EditFormSchema`**

Delete line 588 (`imageUrl: z.string().url('Invalid URL').nullable().optional().or(z.literal('')),`). The `EditFormSchema` object now ends at `description`:

```ts
    description: z.string().max(5000, 'Description too long').nullable().optional(),
  })
  .refine(data => data.maxPlayers >= data.minPlayers, {
```

- [ ] **Step 4: Remove `imageUrl` from `EditPrivateGameFormInner` defaultValues**

Delete line 622 (`imageUrl: game.imageUrl ?? undefined,`). The `defaultValues` object now ends at `description`:

```ts
      description: game.description ?? undefined,
    },
  });
```

- [ ] **Step 5: Remove the edit Image URL input block**

Delete the block at lines 740-743:

```tsx
      <div className="space-y-2">
        <Label htmlFor="edit-imageUrl">{t('privateGameForm.imageUrl')}</Label>
        <Input id="edit-imageUrl" type="url" {...register('imageUrl')} disabled={isSubmitting} />
      </div>
```

so the `edit-description` block is immediately followed by the `{/* Form Actions */}` (`flex justify-end`) block.

- [ ] **Step 6: Verify only the read-only card usage remains**

```
cd apps/web && grep -nE "imageUrl" "src/app/(authenticated)/library/private/PrivateGamesClient.tsx"
```

Expected (GREEN): prints ONLY line ~542 (`imageUrl={game.imageUrl || undefined}` inside `PrivateGameCard` → `MeepleCard`). No form/schema/PUT references.

- [ ] **Step 7: Typecheck the library subtree**

```
cd apps/web && pnpm typecheck
```

Expected: the remaining error (if any) is only in `GameCreationStep.tsx` (fixed in Task 6). `PrivateGamesClient.tsx`, `AddPrivateGameForm.tsx`, and the schemas must now be clean.

---

## Task 6: Frontend — close the URL→PUT cover channel in `GameCreationStep` (user branch)

The `mode === 'user'` branch of `GameCreationStep.tsx` (under `admin/wizard/` but a shared user-facing component) passes `imageUrl`/`thumbnailUrl` to `addPrivateGame` and then sets the private-game cover via a user-typed URL / uploaded file through `updatePrivateGame({ ..., imageUrl })`. This is a live external-URL user-side channel into the PUT endpoint. Close it: the private-game cover is materialized only via the PDF flow (#2943). The `mode === 'admin'` branch (`api.games.create`/`update`/`uploadImage`) is legitimate (ADR-059 §2) and stays untouched.

Additionally, the **"Cover Image" `<Card>`** (the `imageMode`/`imageUrl`/`imageFile` URL+upload Tabs, currently rendered UNCONDITIONALLY in the JSX at lines 343-393) must be mode-gated to `mode === 'admin'` only. After Step 2 removes the user-branch cover-upload logic, a `mode === 'user'` user would still see a live cover-URL/upload input that no longer does anything — leaving an inert, misleading external-URL surface. Gating the Card to the admin branch completes the issue objective "the cover of a private game is set only via the PDF page, not via URL" (the admin branch still reads `imageMode`/`imageUrl`/`imageFile`, so the Card stays functional for admins). This is a design decision recorded here: the Cover Image Card is admin-only; user-mode cover comes exclusively from the PDF flow.

**Files:**
- Modify: `apps/web/src/app/(authenticated)/admin/wizard/steps/GameCreationStep.tsx:128-163` (user branch: the `addPrivateGame` call + the follow-up cover block), `:343-393` (mode-gate the Cover Image Card to `mode === 'admin'`), and `:221-234` (the `useCallback` dependency array — verify unchanged)
- Test: `apps/web/src/app/(authenticated)/admin/wizard/steps/GameCreationStep.tsx` typecheck + lint.

**Interfaces:**
- Consumes: `AddPrivateGameRequest` (no `imageUrl`/`thumbnailUrl`), `UpdatePrivateGameRequest` (no `imageUrl`) from Task 3.
- Produces: nothing for later tasks.

- [ ] **Step 1: Confirm the RED state**

```
cd apps/web && pnpm typecheck 2>&1 | grep GameCreationStep
```

Expected (RED): `TS2353` errors at the `addPrivateGame({ ..., imageUrl: null, thumbnailUrl: null })` call and the `updatePrivateGame(gameId, { ..., imageUrl: finalImageUrl })` call in the `mode === 'user'` branch.

- [ ] **Step 2: Strip `imageUrl`/`thumbnailUrl` from the user-branch `addPrivateGame` call and delete the URL→PUT cover block**

In `apps/web/src/app/(authenticated)/admin/wizard/steps/GameCreationStep.tsx`, replace the entire `mode === 'user'` branch body (lines 127-163) so it creates the private game without external-URL fields and no longer sets a cover via URL/upload. The private-game cover comes from the PDF flow (#2943):

```tsx
      if (mode === 'user') {
        // ── User wizard: create private game via /api/v1/private-games ──────────
        // No external image URL is sent: the private-game cover is materialized
        // by the cover-from-PDF flow (#2943), never from user input
        // (BGG freeze #2123 / ADR-059).
        const result = await api.library.addPrivateGame({
          source: 'Manual',
          title: gameName.trim(),
          yearPublished: yearPublished ?? null,
          // Required fields — user can edit them later
          minPlayers: 1,
          maxPlayers: 99,
        });
        gameId = result.id;
      } else {
```

This deletes the former lines 135-136 (`imageUrl: null, thumbnailUrl: null,`) and the entire cover block at lines 140-163 (the `finalImageUrl` computation, the `imageMode === 'url'`/`upload` branches, and the `updatePrivateGame(gameId, { ..., imageUrl: finalImageUrl })` call), while leaving the `} else {` that opens the admin branch intact.

- [ ] **Step 3: Mode-gate the "Cover Image" Card to the admin branch only**

In the `return (...)` JSX of `apps/web/src/app/(authenticated)/admin/wizard/steps/GameCreationStep.tsx`, the "Cover Image" `<Card>` (lines 343-393) currently renders unconditionally. Wrap it in `{mode === 'admin' && ( ... )}` so it only shows for admins. This is the from → to:

FROM (current — unconditional):

```tsx
      {/* Cover Image */}
      <Card className="p-4 space-y-4">
        <Label className="text-base font-medium">Immagine di Copertina</Label>
        <Tabs value={imageMode} onValueChange={v => setImageMode(v as ImageInputMode)}>
          {/* … URL + Upload TabsContent … */}
        </Tabs>
      </Card>
```

TO (admin-only):

```tsx
      {/* Cover Image — admin-only: user-mode covers come from the PDF flow (#2943),
          never from a user-supplied URL/upload (BGG freeze #2123 / ADR-059) */}
      {mode === 'admin' && (
        <Card className="p-4 space-y-4">
          <Label className="text-base font-medium">Immagine di Copertina</Label>
          <Tabs value={imageMode} onValueChange={v => setImageMode(v as ImageInputMode)}>
            {/* … URL + Upload TabsContent unchanged … */}
          </Tabs>
        </Card>
      )}
```

Wrap the WHOLE existing Card block (the opening `<Card ...>` through its matching `</Card>`, lines 343-393 in the current file) — do not alter the inner `<Tabs>`/`<TabsContent>` markup. Only the `{/* Icon */}` Card immediately above stays unconditional (icons are admin-specific too, but out of scope for this issue — leave it as-is). The result: for `mode === 'user'` the Cover Image Card no longer renders; for `mode === 'admin'` it renders exactly as before.

- [ ] **Step 4: Prune now-unused dependencies from the `useCallback` array**

The user branch no longer reads `imageMode`, `imageUrl`, or `imageFile`. The admin branch (unchanged) still reads them (lines 192-202), so they MUST remain in the dependency array. Verify: the admin branch at `:192-202` uses `imageMode`, `imageUrl`, `imageFile` → they stay. Therefore the dependency array (lines 221-234) is UNCHANGED. Do not remove any dependency. (This step is a verification, not an edit — confirm no lint `react-hooks/exhaustive-deps` warning appears in Step 6.)

- [ ] **Step 5: Confirm the user branch no longer references the removed keys**

```
cd apps/web && sed -n '126,150p' "src/app/(authenticated)/admin/wizard/steps/GameCreationStep.tsx"
```

Expected (GREEN): the `mode === 'user'` block shows the `addPrivateGame({...})` call ending at `maxPlayers: 99,` with no `imageUrl`/`thumbnailUrl`, immediately followed by `gameId = result.id;` and the `} else {` admin branch. No `updatePrivateGame` call in the user branch.

- [ ] **Step 6: Typecheck + lint the whole web app (all FE consumers now aligned)**

```
cd apps/web && pnpm typecheck && pnpm lint src/app/(authenticated)/admin/wizard/steps/GameCreationStep.tsx
```

Expected: PASS with no errors. `pnpm typecheck` is now fully green (Tasks 3-6 aligned the schemas and all consumers). No `react-hooks/exhaustive-deps` warning on `GameCreationStep.tsx`. The mode-gate from Step 3 keeps `imageMode`/`imageUrl`/`imageFile`/`setImageMode`/`setImageUrl` referenced (inside the admin-only Card + the admin branch), so no `@typescript-eslint/no-unused-vars` warning appears.

- [ ] **Step 7: Commit Tasks 3-6 (FE schema + consumers) together**

```bash
git add apps/web/src/lib/api/schemas/private-games.schemas.ts apps/web/src/components/library/AddPrivateGameForm.tsx "apps/web/src/app/(authenticated)/library/private/PrivateGamesClient.tsx" "apps/web/src/app/(authenticated)/admin/wizard/steps/GameCreationStep.tsx"
git commit -m "fix(web): rimuove input image URL morti da add/update private game (#2948)"
```

---

## Task 7: Prune orphaned i18n keys + final compliance grep

**Files:**
- Modify: `apps/web/src/locales/it.json:753-754`
- Modify: `apps/web/src/locales/en.json:3597-3598`
- Test: `apps/web/src` orphan-key grep + full FE build.

**Interfaces:**
- Consumes: nothing.
- Produces: nothing.

- [ ] **Step 1: Confirm the keys are now orphaned**

After Tasks 4-5, the only remaining `t('privateGameForm.imageUrl')` / `imageUrlPlaceholder` references were the two form inputs, now deleted. Confirm no code references them:

```
cd apps/web && grep -rnE "privateGameForm\.imageUrl|imageUrlPlaceholder" src --include=*.ts --include=*.tsx
```

Expected (RED for cleanup): matches ONLY inside `src/locales/it.json` and `src/locales/en.json` (the two key definitions). If any `.tsx` still references them, a form-input deletion was missed in Task 4 or 5 — go back and fix.

- [ ] **Step 2: Remove the orphaned keys from `it.json`**

In `apps/web/src/locales/it.json`, delete lines 753-754 (`"imageUrl": "URL Immagine",` and `"imageUrlPlaceholder": "https://esempio.com/immagine.jpg",`). The `privateGameForm` block now goes from `descriptionPlaceholder` straight to `adding`:

```json
    "description": "Descrizione",
    "descriptionPlaceholder": "Descrizione del gioco...",
    "adding": "Aggiunta in corso...",
    "addPrivateGame": "Aggiungi Gioco Privato",
    "addFromBgg": "Aggiungi dal catalogo"
  },
```

- [ ] **Step 3: Remove the orphaned keys from `en.json`**

In `apps/web/src/locales/en.json`, delete lines 3597-3598 (`"imageUrl": "Image URL",` and `"imageUrlPlaceholder": "https://example.com/image.jpg",`). The block now goes from `descriptionPlaceholder` straight to `adding`:

```json
    "description": "Description",
    "descriptionPlaceholder": "Game description...",
    "adding": "Adding...",
    "addPrivateGame": "Add Private Game",
    "addFromBgg": "Add from catalog"
  },
```

- [ ] **Step 4: Verify both JSON files still parse and the keys are gone**

```
cd apps/web && node -e "JSON.parse(require('fs').readFileSync('src/locales/it.json','utf8')); JSON.parse(require('fs').readFileSync('src/locales/en.json','utf8')); console.log('json-ok')" && grep -rnE "privateGameForm\.imageUrl|\"imageUrlPlaceholder\"" src
```

Expected: prints `json-ok` and NO grep matches (keys fully removed, both files valid JSON — a trailing-comma slip would throw here).

- [ ] **Step 5: Final compliance grep — no user-side external image URL channel remains**

Prove the whole issue objective: no user-side add/update path accepts an external image URL. Admin shared-game paths (`api.games.*`, `shared-games`, `EditGameDrawer`, `GameForm`) are expected to remain and are legitimate.

```
cd apps/web && grep -rnE "updatePrivateGame\(|addPrivateGame\(" src | grep -iE "imageUrl|thumbnailUrl"
```

Expected: NO matches (no `addPrivateGame`/`updatePrivateGame` call passes an image URL). Then:

```
cd apps/api/src/Api && grep -rnE "ImageUrl" BoundedContexts/UserLibrary/Application/Commands/PrivateGames/UpdatePrivateGameCommand.cs Routing/PrivateGameEndpoints.cs
```

Expected: NO matches for `ImageUrl` in the Update command or the two Update request/mapping regions of the endpoints file.

- [ ] **Step 6: Run the FE unit suite for the touched areas + BE PrivateGame suite**

```
cd apps/web && pnpm test src/components/play-records/__tests__/SessionCreateForm.test.tsx
```

Expected: PASS.

```
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~PrivateGame"
```

Expected: PASS (all Add + Update private-game handler/validator/regression tests green).

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "chore(web): prune chiavi i18n orfane privateGameForm.imageUrl (#2948)"
```

---

## Task 8: Full verification + PR

**Files:** none (verification + PR).

- [ ] **Step 1: Kill any lingering test host, then run the full UserLibrary BE unit slice**

```
cd apps/api/src/Api && dotnet test --filter "Category=Unit&FullyQualifiedName~UserLibrary"
```

Expected: PASS, 0 failures. Confirms the domain/handler/validator/command changes did not regress the UserLibrary context.

- [ ] **Step 2: Full FE typecheck + lint + targeted tests**

```
cd apps/web && pnpm typecheck && pnpm lint
```

Expected: PASS (0 errors). The token/no-bgg-host ESLint rules are unaffected (we removed inputs, added no hosts).

- [ ] **Step 3: Push the branch**

```bash
git push -u origin feature/issue-2948-close-updateprivategame-imageurl
```

- [ ] **Step 4: Open the PR against the parent branch `main-dev`**

```bash
gh pr create --base main-dev --title "fix(compliance): chiude UpdatePrivateGame.ImageUrl + FE dead inputs (#2948)" --body "$(cat <<'EOF'
Closes #2948.

Mirror del lato Add (PR #2943) sul canale Update + rimozione input FE morti.

## Backend
- `UpdatePrivateGameCommand` / `UpdatePrivateGameRequest`: rimosso `ImageUrl` (canale URL esterno via PUT).
- `UpdatePrivateGameCommandHandler`: non passa piu `imageUrl` a `UpdateInfo`.
- `PrivateGame.UpdateInfo`: rimosso il parametro `imageUrl`; un update ora PRESERVA la cover materializzata da PDF (#2943) invece di sovrascriverla. Il campo di dominio `PrivateGame.ImageUrl` resta invariato.
- Validator: droppata la regola `ImageUrl`.
- Nuovo `UpdatePrivateGameNoExternalUrlTests` (mirror di `AddPrivateGameNoExternalUrlTests`).

## Frontend
- `private-games.schemas.ts`: rimossi `imageUrl`/`thumbnailUrl` dai request schema Add/Update (DTO response invariato).
- `AddPrivateGameForm` + `PrivateGamesClient` (edit inline): rimossi input Image URL morti.
- `GameCreationStep` (branch `mode === 'user'`): chiuso il canale URL/upload → `updatePrivateGame({ imageUrl })`; branch admin (`api.games.*`) invariato (ADR-059 §2).
- Prune chiavi i18n orfane `privateGameForm.imageUrl` (it/en).

Compliance: BGG freeze #2123 / ADR-059. Nessun path utente accetta piu un URL immagine esterno; BGG resta admin-only server-to-server.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 5: Post-merge issue closure**

After the PR merges into `main-dev`: verify issue #2948 auto-closed by the `Closes #2948` in the PR body. If not, `gh issue close 2948 --repo meepleAi-app/meepleai-monorepo --comment "Chiuso da PR merge in main-dev"`. The branch auto-deletes on merge (repo-level setting).

---

## Self-Review

**1. Spec coverage** (every issue task → plan task):
- Issue task "Rimuovere `ImageUrl`/`ThumbnailUrl` da `UpdatePrivateGameCommand` + request DTO + handler/mapping" → Task 1 (Steps 3-8). Note: the Update command/request never had `ThumbnailUrl` (verified — only `ImageUrl`), so only `ImageUrl` is removed there; `ThumbnailUrl` removal applies to the FE Add request schema (Task 3).
- Issue task "il campo di dominio resta, smette solo di essere popolato da input utente" → Task 1 Step 6 keeps the `PrivateGame.ImageUrl` property, only removes the write in `UpdateInfo`. Additionally preserves PDF cover (design correction beyond the literal Add-mirror, justified inline).
- Issue task "Pulire il FE: rimuovere l'invio di `imageUrl`/`thumbnailUrl` da `AddPrivateGameForm` + schemas" → Tasks 3 (schemas) + 4 (`AddPrivateGameForm`). Extended to the true PUT channels (`PrivateGamesClient` edit form Task 5, `GameCreationStep` user branch Task 6) which the triage flagged as the real user-side PUT surface.
- Issue task "Verificare (grep) che nessun altro path utente accetti un URL immagine esterno" → Task 7 Step 5 (BE + FE grep gates).
- Issue task "La cover di un gioco privato si imposta solo via pagina-PDF, non via URL" → enforced by Task 1 Step 6 (Update preserves PDF cover) + Task 6 Step 2 (removed URL→PUT block) + Task 6 Step 3 (mode-gate the Cover Image Card to admin-only, so user-mode has no cover-URL/upload surface at all).
- i18n prune (from triage scope) → Task 7.

**2. Placeholder scan:** No "TBD/TODO/handle edge cases/similar to Task N". Every code step shows real before/after code copied from the verified source. No `// TODO(` (would trip Sonar S1135) — used `// Follow-up:`-style prose only where needed; actually used explanatory `//` comments, none are `TODO`.

**3. Type consistency:**
- `UpdatePrivateGameCommand` = 10 params (no `ImageUrl`) in Task 1 Step 3, and Task 2 constructs it with exactly those 10 (8 constructions in the handler test — Task 2 Step 2).
- `PrivateGame.UpdateInfo` = 8 params (no `imageUrl`) in Task 1 Step 6; the handler (Task 1 Step 7) calls it with those 8; Task 2 Steps 4-5 fix the 3 domain-test `UpdateInfo` calls + the 1 integration-test `UpdateInfo` call that still passed `imageUrl:`, and Step 9's `dotnet build` compiles the whole assembly to catch any straggler.
- `AddPrivateGameRequest` (no `imageUrl`/`thumbnailUrl`) and `UpdatePrivateGameRequest` (no `imageUrl`) from Task 3 consumed consistently by Tasks 5-6.
- `AddPrivateGameFormData` (no `imageUrl`) from Task 4 consumed by Task 5's edit form + `handleEditGame`.
- New test class name `UpdatePrivateGameNoExternalUrlTests` is used identically in the create step (Task 1) and the commit/run steps.
- Commit file lists match the files each task actually modifies (Task 2 commit now also stages `PrivateGameTests.cs` + `PrivateGameRepositoryIntegrationTests.cs`).

**4. Compile-completeness (verified against source):** After Task 1 removes the `imageUrl` param from `PrivateGame.UpdateInfo`, EVERY caller in the test assembly must drop the arg or the whole `Api.Tests` assembly fails to compile. Confirmed callers passing `imageUrl:`: `PrivateGameTests.cs:402/434/457` (3 calls, fixed Task 2 Step 4) and `PrivateGameRepositoryIntegrationTests.cs:433` (1 call, fixed Task 2 Step 5). `PrivateGameTests.cs:413` additionally asserted `ImageUrl.Should().Be("https://example.com/new.jpg")` — false after the change (source builds the game via `CreateManual` with null cover; `UpdateInfo` no longer writes `ImageUrl`) → flipped to `.Should().BeNull()`.

Notes locked during research: `PrivateGame.UpdateInfo` is called from exactly one production site (`UpdatePrivateGameCommandHandler.cs:51`); the two `UpdateInfo` calls in `SharedGameCatalog` are on the `SharedGame` aggregate and unaffected. `GameCreationStep.tsx` admin branch keeps `imageMode/imageUrl/imageFile` state (used by the admin create/upload logic AND the now admin-only Cover Image Card), so the `useCallback` deps array is unchanged (Task 6 Step 4 is verification-only). The Cover Image Card JSX (`:343-393`) was rendered unconditionally in the source — Task 6 Step 3 wraps it in `{mode === 'admin' && (...)}` so user-mode has no external-URL cover surface.
