# Catan SUMMARY flavor (#3022) — Implementation Plan (rev. 2 — VP reali / BE bridge)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Ship a Catan SUMMARY flavor (winner hero + final standings with REAL per-player scores + colors) on `/sessions/[id]`, backed by a BE read-model that bridges GameSession→LiveGameSession to surface score + aligned players.

**Architecture:** Extend `IHistorySessionScoreProvider` with `GetScoreboardAsync` (score + `SessionPlayers` aligned to `scoreData.playerId`). `GetGameSessionByIdQueryHandler` injects it (+ `IGameCoreDataProvider` for slug) and enriches `GameSessionDto` with `ScoringType/ScoreData/ScorePlayers/GameSlug/GameName`. FE adds a twin `SummaryFlavorRenderer` + `CatanSummaryFlavor` that joins scoreData↔scorePlayers by id.

**Tech Stack:** .NET 9 (xUnit + Moq + FluentAssertions + EF InMemory), Next.js 16 / React 19, Zod, react-intl, Vitest.

## Global Constraints

- **Backend test path**: `apps/api/tests/Api.Tests`.
- **Identity fact**: `scoreData.scores[].playerId` == `SessionPlayerEntity.Id` (LiveGameSession, table `session_players`). Bridge: `GameSession.Id ← LiveGameSession.CorrelatedGameSessionId`, `LiveGameSession.Id = SessionPlayers.LiveGameSessionId`.
- **i18n**: new keys in BOTH catalogs, parity, correct Italian accents. Placeholders via native ICU `t(key, { name })`.
- **Design tokens**: semantic + entity utilities; raw inline HSL only via `catanPieceColor()`.
- **Mount gate**: flavor mounts only when `status === 'Completed'` AND `hasSummaryFlavor(gameSlug)` AND `sessionQuery.data != null`.
- **TDD**: RED → GREEN → REFACTOR, frequent commits.
- **Branch**: `feature/issue-3022-catan-summary-flavor`.

---

## File Structure

**Backend**
- Modify `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/IHistorySessionScoreProvider.cs` — `GetScoreboardAsync` + `SessionScoreboard` + `ScorePlayerReadModel`.
- Modify `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/HistorySessionScoreProvider.cs` — impl.
- Modify `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameSessionDto.cs` — `GameSlug?`, `GameName?`, `ScorePlayers?`, `ScorePlayerDto`.
- Modify `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetGameSessionByIdQueryHandler.cs` — 2 deps + enrichment.
- Test: `.../Handlers/GetGameSessionByIdQueryHandlerTests.cs` (unit) + new `.../Persistence/HistorySessionScoreProviderScoreboardTests.cs` (integration InMemory).

**Frontend**
- Modify `apps/web/src/lib/api/schemas/games.schemas.ts`.
- Create `apps/web/src/components/features/session-live/SummaryFlavorRenderer.tsx`.
- Create `apps/web/src/components/features/session-live/flavors/catan/catan-summary-standings.ts`.
- Create `apps/web/src/components/features/session-live/flavors/catan/CatanSummaryFlavor.tsx`.
- Modify `apps/web/src/app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx`.
- Modify `apps/web/src/locales/it.json` + `en.json`.
- Create i18n guard test.

---

## Task 1: BE — `GetScoreboardAsync` on the score provider

**Files:**
- Modify: `IHistorySessionScoreProvider.cs`
- Modify: `HistorySessionScoreProvider.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/Persistence/HistorySessionScoreProviderScoreboardTests.cs`

**Interfaces:**
- Produces: `Task<SessionScoreboard?> GetScoreboardAsync(Guid, CancellationToken)`; `readonly record struct SessionScoreboard(string ScoringType, string ScoreData, IReadOnlyList<ScorePlayerReadModel> Players)`; `readonly record struct ScorePlayerReadModel(Guid Id, string DisplayName, string Color)`.

- [ ] **Step 1: Add interface members + records**

In `IHistorySessionScoreProvider.cs`, add to the interface and below `HistorySessionScore`:

```csharp
    /// <summary>
    /// Resolves score + the players aligned to scoreData (LiveGameSession SessionPlayers,
    /// whose Id == scoreData.scores[].playerId) for a single GameSession. Null when the
    /// session has no correlated live/tracking session with a score. (#3022)
    /// </summary>
    Task<SessionScoreboard?> GetScoreboardAsync(Guid gameSessionId, CancellationToken cancellationToken);
```

```csharp
/// <summary>Score + players aligned to scoreData for one session (#3022).</summary>
internal readonly record struct SessionScoreboard(
    string ScoringType,
    string ScoreData,
    IReadOnlyList<ScorePlayerReadModel> Players);

/// <summary>A player whose Id matches scoreData.scores[].playerId (#3022).</summary>
internal readonly record struct ScorePlayerReadModel(Guid Id, string DisplayName, string Color);
```

- [ ] **Step 2: Write the failing integration test**

Create `HistorySessionScoreProviderScoreboardTests.cs`. Seed a LiveGameSession (with `CorrelatedGameSessionId` + `TrackingSessionId` + 2 `SessionPlayers`) and a SessionTracking session (with `ScoringType`/`ScoreData`), then assert the scoreboard aligns. Use `TestDbContextFactory.CreateInMemoryDbContext()` (precedent: `GetGameByIdQueryHandlerTests.cs:42`).

```csharp
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Persistence;

[Trait("Category", TestCategories.Unit)]
public class HistorySessionScoreProviderScoreboardTests
{
    [Fact]
    public async Task GetScoreboardAsync_ReturnsScoreAndAlignedPlayers()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameSessionId = Guid.NewGuid();
        var liveId = Guid.NewGuid();
        var trackingId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        db.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = liveId,
            CorrelatedGameSessionId = gameSessionId,
            TrackingSessionId = trackingId,
        });
        db.SessionPlayers.AddRange(
            new SessionPlayerEntity { Id = p1, LiveGameSessionId = liveId, DisplayName = "Alice", Color = "Red", Role = "Player" },
            new SessionPlayerEntity { Id = p2, LiveGameSessionId = liveId, DisplayName = "Bob", Color = "Blue", Role = "Player" });
        db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = trackingId,
            ScoringType = "Points",
            ScoreData = $"{{\"scores\":[{{\"playerId\":\"{p1}\",\"points\":10}},{{\"playerId\":\"{p2}\",\"points\":8}}]}}",
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new HistorySessionScoreProvider(db);
        var result = await sut.GetScoreboardAsync(gameSessionId, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.ScoringType.Should().Be("Points");
        result.Value.Players.Should().HaveCount(2);
        result.Value.Players.Should().ContainSingle(p => p.Id == p1 && p.DisplayName == "Alice" && p.Color == "Red");
    }

    [Fact]
    public async Task GetScoreboardAsync_NoCorrelatedLive_ReturnsNull()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new HistorySessionScoreProvider(db);
        var result = await sut.GetScoreboardAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }
}
```

> Seed note: `SessionEntity`/`LiveGameSessionEntity`/`SessionPlayerEntity` may have additional required scalar columns (e.g. status/timestamps). If EF InMemory rejects a save, set the minimal required fields the compiler/EF reports — do NOT add unrelated data. Verify field names against the entity classes before running.

- [ ] **Step 3: Run test — verify it fails**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~HistorySessionScoreProviderScoreboardTests"`
Expected: compile error (`GetScoreboardAsync` not implemented).

- [ ] **Step 4: Implement `GetScoreboardAsync`**

Append to `HistorySessionScoreProvider`:

```csharp
    public async Task<SessionScoreboard?> GetScoreboardAsync(
        Guid gameSessionId,
        CancellationToken cancellationToken)
    {
        var scoreRow = await (
            from live in _dbContext.LiveGameSessions.AsNoTracking()
            where live.CorrelatedGameSessionId == gameSessionId
            join track in _dbContext.SessionTrackingSessions.AsNoTracking()
                on live.TrackingSessionId equals (Guid?)track.Id
            orderby (track.UpdatedAt ?? track.CreatedAt) descending
            select new { LiveId = live.Id, track.ScoringType, track.ScoreData }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (scoreRow is null)
            return null;

        var players = await _dbContext.SessionPlayers.AsNoTracking()
            .Where(p => p.LiveGameSessionId == scoreRow.LiveId)
            .Select(p => new ScorePlayerReadModel(p.Id, p.DisplayName, p.Color))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new SessionScoreboard(scoreRow.ScoringType, scoreRow.ScoreData, players);
    }
```

- [ ] **Step 5: Run test — verify it passes**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~HistorySessionScoreProviderScoreboardTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/IHistorySessionScoreProvider.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/HistorySessionScoreProvider.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/Persistence/HistorySessionScoreProviderScoreboardTests.cs
git commit -m "feat(session-summary): #3022 BE — GetScoreboardAsync (score + aligned players)"
```

---

## Task 2: BE — enrich `GameSessionDto` on the single-session GET

**Files:**
- Modify: `GameSessionDto.cs`
- Modify: `GetGameSessionByIdQueryHandler.cs`
- Test: `.../Handlers/GetGameSessionByIdQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IGameCoreDataProvider.GetCoreDataAsync`, `GameRef.Shared`, `Slugifier.Slugify`, `IHistorySessionScoreProvider.GetScoreboardAsync` (Task 1).
- Produces: `GameSessionDto` with `GameSlug?`, `GameName?`, `IReadOnlyList<ScorePlayerDto>? ScorePlayers`; `internal record ScorePlayerDto(Guid Id, string DisplayName, string? Color)`.

- [ ] **Step 1: Add DTO fields + `ScorePlayerDto`**

In `GameSessionDto.cs`, extend the record and add the sub-DTO:

```csharp
internal record GameSessionDto(
    Guid Id,
    Guid GameId,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int PlayerCount,
    IReadOnlyList<SessionPlayerDto> Players,
    string? WinnerName,
    string? Notes,
    int DurationMinutes,
    string? ScoringType = null,
    string? ScoreData = null,
    // #3022: identity + score-aligned players for the summary flavor. Populated ONLY on
    // GET /api/v1/sessions/{id}; null on list/history paths (GameSessionMapper.ToDto).
    string? GameSlug = null,
    string? GameName = null,
    IReadOnlyList<ScorePlayerDto>? ScorePlayers = null
);

/// <summary>Player aligned to scoreData.playerId (LiveGameSession player). #3022.</summary>
internal record ScorePlayerDto(Guid Id, string DisplayName, string? Color);
```

- [ ] **Step 2: Write the failing handler tests**

Update the test ctor to inject all three mocks (fixes ALL existing tests), then add coverage. Add imports `using Api.SharedKernel.Application;`, `using Api.SharedKernel.Domain.ValueObjects;`.

```csharp
private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
private readonly Mock<IGameCoreDataProvider> _gameCoreDataMock;
private readonly Mock<IHistorySessionScoreProvider> _scoreProviderMock;
private readonly GetGameSessionByIdQueryHandler _handler;

public GetGameSessionByIdQueryHandlerTests()
{
    _sessionRepositoryMock = new Mock<IGameSessionRepository>();
    _gameCoreDataMock = new Mock<IGameCoreDataProvider>();
    _scoreProviderMock = new Mock<IHistorySessionScoreProvider>();
    _handler = new GetGameSessionByIdQueryHandler(
        _sessionRepositoryMock.Object, _gameCoreDataMock.Object, _scoreProviderMock.Object);
}

private static GameCoreData MakeCoreData(string title = "Catan") =>
    GameCoreData.Create(title, 1995, 3, 4, 90, 10);
```

New tests:

```csharp
[Fact]
public async Task Handle_PopulatesSlugNameAndScoreboard()
{
    var gameId = Guid.NewGuid();
    var session = CreateSession(gameId);
    var pid = Guid.NewGuid();
    _sessionRepositoryMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
    _gameCoreDataMock.Setup(p => p.GetCoreDataAsync(GameRef.Shared(gameId), It.IsAny<CancellationToken>())).ReturnsAsync(MakeCoreData("Catan"));
    _scoreProviderMock.Setup(p => p.GetScoreboardAsync(session.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new SessionScoreboard("Points", "{\"scores\":[]}",
            new List<ScorePlayerReadModel> { new(pid, "Alice", "Red") }));

    var result = await _handler.Handle(new GetGameSessionByIdQuery(session.Id), TestContext.Current.CancellationToken);

    result!.GameSlug.Should().Be("catan");
    result.GameName.Should().Be("Catan");
    result.ScoringType.Should().Be("Points");
    result.ScorePlayers.Should().ContainSingle(sp => sp.Id == pid && sp.DisplayName == "Alice" && sp.Color == "Red");
}

[Fact]
public async Task Handle_NoScoreboard_LeavesScoreFieldsNull()
{
    var gameId = Guid.NewGuid();
    var session = CreateSession(gameId);
    _sessionRepositoryMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
    _gameCoreDataMock.Setup(p => p.GetCoreDataAsync(It.IsAny<GameRef>(), It.IsAny<CancellationToken>())).ReturnsAsync((GameCoreData?)null);
    _scoreProviderMock.Setup(p => p.GetScoreboardAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((SessionScoreboard?)null);

    var result = await _handler.Handle(new GetGameSessionByIdQuery(session.Id), TestContext.Current.CancellationToken);

    result!.GameSlug.Should().BeNull();
    result.ScoringType.Should().BeNull();
    result.ScorePlayers.Should().BeNull();
}
```

Also add the mapper guard test in the existing `GameSessionMapper` test file (or here if none):

```csharp
[Fact]
public void ToDto_LeavesSummaryOnlyFieldsNull()
{
    var session = CreateSession(Guid.NewGuid());
    var dto = session.ToDto();   // GameSessionMapper.ToDto
    dto.GameSlug.Should().BeNull();
    dto.GameName.Should().BeNull();
    dto.ScorePlayers.Should().BeNull();
}
```

- [ ] **Step 3: Run tests — verify they fail**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~GetGameSessionByIdQueryHandlerTests"`
Expected: compile error (ctor arity).

- [ ] **Step 4: Implement the handler**

```csharp
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

internal class GetGameSessionByIdQueryHandler : IQueryHandler<GetGameSessionByIdQuery, GameSessionDto?>
{
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IGameCoreDataProvider _gameCoreData;
    private readonly IHistorySessionScoreProvider _scoreProvider;

    public GetGameSessionByIdQueryHandler(
        IGameSessionRepository sessionRepository,
        IGameCoreDataProvider gameCoreData,
        IHistorySessionScoreProvider scoreProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _gameCoreData = gameCoreData ?? throw new ArgumentNullException(nameof(gameCoreData));
        _scoreProvider = scoreProvider ?? throw new ArgumentNullException(nameof(scoreProvider));
    }

    public async Task<GameSessionDto?> Handle(GetGameSessionByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null) return null;

        var coreData = await _gameCoreData
            .GetCoreDataAsync(GameRef.Shared(session.GameId), cancellationToken).ConfigureAwait(false);
        var gameName = coreData?.Title;
        var gameSlug = gameName is null ? null : Slugifier.Slugify(gameName);

        var scoreboard = await _scoreProvider
            .GetScoreboardAsync(session.Id, cancellationToken).ConfigureAwait(false);

        return MapToDto(session, gameSlug, gameName, scoreboard);
    }

    private static GameSessionDto MapToDto(
        GameSession session, string? gameSlug, string? gameName, SessionScoreboard? scoreboard)
    {
        var playerDtos = session.Players.Select(p => new SessionPlayerDto(
            PlayerName: p.PlayerName, PlayerOrder: p.PlayerOrder, Color: p.Color)).ToList();

        var scorePlayers = scoreboard?.Players
            .Select(sp => new ScorePlayerDto(sp.Id, sp.DisplayName, sp.Color))
            .ToList();

        return new GameSessionDto(
            Id: session.Id,
            GameId: session.GameId,
            Status: session.Status.Value,
            StartedAt: session.StartedAt,
            CompletedAt: session.CompletedAt,
            PlayerCount: session.PlayerCount,
            Players: playerDtos,
            WinnerName: session.WinnerName,
            Notes: session.Notes,
            DurationMinutes: (int)session.Duration.TotalMinutes,
            ScoringType: scoreboard?.ScoringType,
            ScoreData: scoreboard?.ScoreData,
            GameSlug: gameSlug,
            GameName: gameName,
            ScorePlayers: scorePlayers
        );
    }
}
```

- [ ] **Step 5: Run tests — verify they pass**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~GetGameSessionByIdQueryHandlerTests"` then `--filter "BoundedContext=GameManagement"`.
Expected: PASS (existing 6 + new). Fix any other `new GetGameSessionByIdQueryHandler(` call sites the compiler flags (integration tests); the DI container resolves the 3 deps automatically since all are registered.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameSessionDto.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetGameSessionByIdQueryHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/GetGameSessionByIdQueryHandlerTests.cs
git commit -m "feat(session-summary): #3022 BE — enrich GameSessionDto (slug/name/scoreboard) on single GET"
```

---

## Task 3: FE — Zod schema (`gameSlug`/`gameName`/`scorePlayers`)

**Files:**
- Modify: `apps/web/src/lib/api/schemas/games.schemas.ts`
- Test: `apps/web/src/lib/api/schemas/__tests__/games.schemas.test.ts`

**Interfaces:**
- Produces: `ScorePlayerDtoSchema` / `ScorePlayerDto = { id: string; displayName: string; color: string | null }`; `GameSessionDto` gains `gameSlug?`, `gameName?`, `scorePlayers?`.

- [ ] **Step 1: Write the failing test**

```ts
import { describe, it, expect } from 'vitest';
import { GameSessionDtoSchema } from '../games.schemas';

const base = {
  id: '00000000-0000-4000-8000-000000000001',
  gameId: '00000000-0000-4000-8000-0000000000aa',
  status: 'Completed', startedAt: '2026-01-01T00:00:00Z', completedAt: '2026-01-01T01:00:00Z',
  playerCount: 1, players: [{ playerName: 'Alice', playerOrder: 1, color: 'Red' }],
  winnerName: 'Alice', notes: null, durationMinutes: 47,
};

describe('GameSessionDtoSchema #3022', () => {
  it('parses gameSlug/gameName/scorePlayers', () => {
    const p = GameSessionDtoSchema.parse({
      ...base, gameSlug: 'catan', gameName: 'Catan',
      scorePlayers: [{ id: 'x', displayName: 'Alice', color: 'Red' }],
    });
    expect(p.gameSlug).toBe('catan');
    expect(p.scorePlayers?.[0]).toEqual({ id: 'x', displayName: 'Alice', color: 'Red' });
  });
  it('accepts absent/null new fields (back-compat)', () => {
    expect(GameSessionDtoSchema.parse(base).scorePlayers).toBeUndefined();
    expect(GameSessionDtoSchema.parse({ ...base, scorePlayers: null }).scorePlayers).toBeNull();
  });
});
```

- [ ] **Step 2: Run — verify fail**

Run: `cd apps/web && pnpm vitest run games.schemas` — FAIL (fields stripped).

- [ ] **Step 3: Add to schema**

In `games.schemas.ts`, before `GameSessionDtoSchema`:

```ts
export const ScorePlayerDtoSchema = z.object({
  id: z.string(),
  displayName: z.string(),
  color: z.string().nullable(),
});
export type ScorePlayerDto = z.infer<typeof ScorePlayerDtoSchema>;
```

Inside `GameSessionDtoSchema` (after `turnOrderType`):

```ts
  // #3022: summary flavor identity + score-aligned players (single-session GET only).
  gameSlug: z.string().nullable().optional(),
  gameName: z.string().nullable().optional(),
  scorePlayers: z.array(ScorePlayerDtoSchema).nullable().optional(),
```

- [ ] **Step 4: Run — verify pass**

Run: `cd apps/web && pnpm vitest run games.schemas` — PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/schemas/games.schemas.ts apps/web/src/lib/api/schemas/__tests__/games.schemas.test.ts
git commit -m "feat(session-summary): #3022 FE — gameSlug/gameName/scorePlayers on schema"
```

---

## Task 4: FE — pure standings builder (uses `scorePlayers`)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/catan-summary-standings.ts`
- Test: `.../catan/__tests__/catan-summary-standings.test.ts`

**Interfaces:**
- Consumes: `mapScoreDataToEndgameSummary`; `ScorePlayerDto`; `ScoreType`/`ScoreDataByType`.
- Produces: `CatanSummaryRow = { playerName: string; score: number; isWinner: boolean; color: string | null }`; `buildCatanSummaryStandings(scoringType: string|null|undefined, scoreDataJson: string|null|undefined, scorePlayers: readonly ScorePlayerDto[] | null | undefined) => CatanSummaryRow[]`.

- [ ] **Step 1: Write the failing test**

```ts
import { describe, it, expect, vi } from 'vitest';
import { buildCatanSummaryStandings } from '../catan-summary-standings';
import type { ScorePlayerDto } from '@/lib/api/schemas/games.schemas';

const players: ScorePlayerDto[] = [
  { id: 'p1', displayName: 'Alice', color: 'Red' },
  { id: 'p2', displayName: 'Bob', color: 'Blue' },
  { id: 'p3', displayName: 'Carol', color: 'Orange' },
];
const pointsJson = JSON.stringify({ scores: [
  { playerId: 'p1', points: 10 }, { playerId: 'p2', points: 8 }, { playerId: 'p3', points: 6 } ] });

describe('buildCatanSummaryStandings', () => {
  it('joins by id, orders winner-first then score DESC, zips color', () => {
    const rows = buildCatanSummaryStandings('Points', pointsJson, players);
    expect(rows.map(r => r.playerName)).toEqual(['Alice', 'Bob', 'Carol']);
    expect(rows[0]).toMatchObject({ playerName: 'Alice', score: 10, isWinner: true, color: 'Red' });
  });
  it('returns [] for null score / null-or-empty scorePlayers / unknown type', () => {
    expect(buildCatanSummaryStandings(null, null, players)).toEqual([]);
    expect(buildCatanSummaryStandings('Points', pointsJson, null)).toEqual([]);
    expect(buildCatanSummaryStandings('Points', pointsJson, [])).toEqual([]);
    expect(buildCatanSummaryStandings('Nope', pointsJson, players)).toEqual([]);
  });
  it('returns [] and warns on malformed JSON', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    expect(buildCatanSummaryStandings('Points', '{bad', players)).toEqual([]);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });
});
```

- [ ] **Step 2: Run — verify fail** (`pnpm vitest run catan-summary-standings`, module missing).

- [ ] **Step 3: Implement**

```ts
/**
 * buildCatanSummaryStandings (#3022) — pure adapter: raw GameSessionDto score fields
 * + score-aligned players (scorePlayers[].id === scoreData.scores[].playerId) → ordered
 * rows with color. mapScoreDataToEndgameSummary preserves player order, so the output is
 * index-parallel to scorePlayers → zip color by index before sorting.
 */
import { mapScoreDataToEndgameSummary } from '@/lib/session-live/score-data-to-endgame-summary';
import type { ScorePlayerDto } from '@/lib/api/schemas/games.schemas';
import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';

export interface CatanSummaryRow {
  readonly playerName: string;
  readonly score: number;
  readonly isWinner: boolean;
  readonly color: string | null;
}

const SCORE_TYPES: readonly ScoreType[] = ['Points', 'BinaryWin', 'Objectives', 'Ranking'];

export function buildCatanSummaryStandings(
  scoringType: string | null | undefined,
  scoreDataJson: string | null | undefined,
  scorePlayers: readonly ScorePlayerDto[] | null | undefined
): CatanSummaryRow[] {
  if (scoringType == null || scoreDataJson == null || scorePlayers == null || scorePlayers.length === 0) {
    return [];
  }
  if (!SCORE_TYPES.includes(scoringType as ScoreType)) return [];

  let parsed: ScoreDataByType[ScoreType];
  try {
    parsed = JSON.parse(scoreDataJson) as ScoreDataByType[ScoreType];
  } catch {
    console.warn(`buildCatanSummaryStandings: malformed scoreData JSON for "${scoringType}"`);
    return [];
  }

  const adapterPlayers = scorePlayers.map(p => ({ id: p.id, name: p.displayName }));
  const entries = mapScoreDataToEndgameSummary(scoringType as ScoreType, parsed, adapterPlayers);
  if (entries.length === 0) return [];

  const withColor: CatanSummaryRow[] = entries.map((e, i) => ({
    playerName: e.playerName,
    score: e.score,
    isWinner: e.isWinner,
    color: scorePlayers[i]?.color ?? null,
  }));

  return withColor.sort((a, b) => Number(b.isWinner) - Number(a.isWinner) || b.score - a.score);
}
```

- [ ] **Step 4: Run — verify pass**.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/session-live/flavors/catan/catan-summary-standings.ts \
        apps/web/src/components/features/session-live/flavors/catan/__tests__/catan-summary-standings.test.ts
git commit -m "feat(session-summary): #3022 pure Catan standings builder (join by id + color)"
```

---

## Task 5: FE — `CatanSummaryFlavor` (winnerName-preferred hero, no auto-crown)

**Files:**
- Create: `.../catan/CatanSummaryFlavor.tsx`
- Test: `.../catan/__tests__/CatanSummaryFlavor.test.tsx`

**Interfaces:**
- Consumes: `buildCatanSummaryStandings` (Task 4); `catanPieceColor` from `./catan-palette`; `GameSessionDto`, `useTranslation`.
- Produces: named export `CatanSummaryFlavor` (local props `{ session: GameSessionDto; className?: string }`).

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import type { ReactElement } from 'react';
import { CatanSummaryFlavor } from '../CatanSummaryFlavor';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

const MESSAGES = {
  'pages.sessionSummary.flavor.catan.winnerTemplate': '{name} vince!',
  'pages.sessionSummary.flavor.catan.vpUnit': 'PV',
  'pages.sessionSummary.flavor.catan.durationTemplate': '{minutes} min',
  'pages.sessionSummary.flavor.catan.standingsTitle': 'Classifica finale',
  'pages.sessionSummary.flavor.catan.empty': 'Riepilogo non disponibile',
};
const renderWithIntl = (ui: ReactElement) =>
  render(<IntlProvider locale="it" messages={MESSAGES} onError={() => {}}>{ui}</IntlProvider>);

const base: GameSessionDto = {
  id: '00000000-0000-4000-8000-000000000001', gameId: '00000000-0000-4000-8000-0000000000aa',
  status: 'Completed', startedAt: '2026-01-01T00:00:00Z', completedAt: '2026-01-01T00:47:00Z',
  playerCount: 2, players: [], winnerName: 'Alice', notes: null, durationMinutes: 47,
  scoringType: 'Points',
  scoreData: JSON.stringify({ scores: [{ playerId: 'p1', points: 10 }, { playerId: 'p2', points: 8 }] }),
  gameSlug: 'catan', gameName: 'Catan',
  scorePlayers: [{ id: 'p1', displayName: 'Alice', color: 'Red' }, { id: 'p2', displayName: 'Bob', color: 'Blue' }],
};

describe('CatanSummaryFlavor', () => {
  it('renders winner hero (winnerName) + ordered standings', () => {
    renderWithIntl(<CatanSummaryFlavor session={base} />);
    expect(screen.getByText('Alice vince!')).toBeInTheDocument();
    expect(screen.getAllByTestId('catan-summary-row-name').map(n => n.textContent)).toEqual(['Alice', 'Bob']);
  });
  it('no auto-crown when no isWinner and winnerName null', () => {
    const noWinner = { ...base, winnerName: null,
      scoreData: JSON.stringify({ scores: [{ playerId: 'p1', points: 0 }, { playerId: 'p2', points: 0 }] }) };
    renderWithIntl(<CatanSummaryFlavor session={noWinner} />);
    expect(screen.queryByText(/vince!/)).toBeNull();
    expect(screen.getAllByTestId('catan-summary-row-name').length).toBe(2);
  });
  it('empty state when no scorePlayers', () => {
    renderWithIntl(<CatanSummaryFlavor session={{ ...base, scorePlayers: null, scoreData: null }} />);
    expect(screen.getByText('Riepilogo non disponibile')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run — verify fail**.

- [ ] **Step 3: Implement**

```tsx
'use client';

import { useTranslation } from '@/hooks/useTranslation';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';
import { buildCatanSummaryStandings } from './catan-summary-standings';
import { catanPieceColor } from './catan-palette';

interface CatanSummaryFlavorProps {
  readonly session: GameSessionDto;
  readonly className?: string;
}

export function CatanSummaryFlavor({ session, className }: CatanSummaryFlavorProps): React.JSX.Element {
  const { t } = useTranslation();
  const rows = buildCatanSummaryStandings(session.scoringType, session.scoreData, session.scorePlayers);

  if (rows.length === 0) {
    return (
      <section
        data-slot="catan-summary-flavor"
        className={`rounded-2xl border border-border bg-card p-4 text-center text-[13px] text-muted-foreground ${className ?? ''}`}
      >
        {t('pages.sessionSummary.flavor.catan.empty')}
      </section>
    );
  }

  const standingsTitle = t('pages.sessionSummary.flavor.catan.standingsTitle');
  const vpUnit = t('pages.sessionSummary.flavor.catan.vpUnit');

  // Winner precedence: BE-authoritative winnerName (matched to a row) → scoreData isWinner → none.
  const heroRow =
    (session.winnerName != null ? rows.find(r => r.playerName === session.winnerName) : undefined) ??
    rows.find(r => r.isWinner) ??
    null;
  const heroName = heroRow?.playerName ?? (session.winnerName ?? null);
  const maxScore = rows.reduce((m, r) => Math.max(m, r.score), 0);

  return (
    <section
      data-slot="catan-summary-flavor"
      aria-label={standingsTitle}
      className={`flex flex-col gap-4 rounded-2xl border border-border bg-card p-4 ${className ?? ''}`}
    >
      {heroName != null && (
        <header data-slot="catan-summary-hero" className="flex items-center gap-3">
          <span
            aria-hidden="true"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full border border-border-strong text-lg"
            style={{ backgroundColor: catanPieceColor(heroRow?.color ?? '') }}
          >
            👑
          </span>
          <div className="flex flex-col">
            <span className="text-base font-semibold text-foreground">
              {t('pages.sessionSummary.flavor.catan.winnerTemplate', { name: heroName })}
            </span>
            <span className="text-[13px] text-muted-foreground">
              {heroRow != null ? `${heroRow.score} ${vpUnit} · ` : ''}
              {t('pages.sessionSummary.flavor.catan.durationTemplate', { minutes: session.durationMinutes })}
            </span>
          </div>
        </header>
      )}

      <ol data-slot="catan-summary-standings" className="flex flex-col gap-1.5">
        {rows.map((row, i) => (
          <li key={`${row.playerName}-${i}`} data-slot="catan-summary-row" className="flex items-center gap-2 text-[13px]">
            <span className="w-5 tabular-nums text-muted-foreground">{i + 1}°</span>
            <span
              aria-hidden="true"
              className="h-3.5 w-3.5 shrink-0 rounded-full border border-border-strong"
              style={{ backgroundColor: catanPieceColor(row.color ?? '') }}
            />
            <span data-testid="catan-summary-row-name" className="flex-1 truncate text-foreground">{row.playerName}</span>
            <span className="h-1.5 w-24 overflow-hidden rounded-full bg-muted" aria-hidden="true">
              <span className="block h-full rounded-full bg-entity-session"
                style={{ width: `${maxScore > 0 ? (row.score / maxScore) * 100 : 0}%` }} />
            </span>
            <span className="w-12 text-right tabular-nums text-foreground">{row.score} {vpUnit}</span>
          </li>
        ))}
      </ol>
    </section>
  );
}
```

> Verify at implementation: the exact return type used by sibling flavors (`React.JSX.Element` vs `JSX.Element`) — match `CatanLiveFlavor.tsx`. And that `useTranslation().t` supports `t(key, values)` ICU interpolation (`useTranslation.ts:122-132`).

- [ ] **Step 4: Run — verify pass**.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/session-live/flavors/catan/CatanSummaryFlavor.tsx \
        apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanSummaryFlavor.test.tsx
git commit -m "feat(session-summary): #3022 CatanSummaryFlavor (winnerName-preferred hero + standings)"
```

---

## Task 6: FE — `SummaryFlavorRenderer` dispatcher

**Files:**
- Create: `apps/web/src/components/features/session-live/SummaryFlavorRenderer.tsx`
- Test: `.../session-live/__tests__/SummaryFlavorRenderer.test.tsx`

**Interfaces:**
- Produces: `SummaryFlavorProps = { session: GameSessionDto; className?: string }`; `hasSummaryFlavor(gameSlug)`; `SummaryFlavorRenderer(props & { gameSlug })`.

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, it, expect } from 'vitest';
import { hasSummaryFlavor } from '../SummaryFlavorRenderer';

describe('hasSummaryFlavor', () => {
  it('true for catan', () => expect(hasSummaryFlavor('catan')).toBe(true));
  it('false for unknown/null/undefined', () => {
    expect(hasSummaryFlavor('wingspan')).toBe(false);
    expect(hasSummaryFlavor(null)).toBe(false);
    expect(hasSummaryFlavor(undefined)).toBe(false);
  });
});
```

- [ ] **Step 2: Run — verify fail**.

- [ ] **Step 3: Implement**

```tsx
'use client';

import dynamic from 'next/dynamic';
import type { ComponentType } from 'react';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';
import { FlavorLoadingSkeleton } from './FlavorLoadingSkeleton';

export interface SummaryFlavorProps {
  readonly session: GameSessionDto;
  readonly className?: string;
}

type SummaryFlavorComponent = ComponentType<SummaryFlavorProps>;

const CatanSummaryFlavorLazy: SummaryFlavorComponent = dynamic(
  () => import('./flavors/catan/CatanSummaryFlavor').then(m => ({ default: m.CatanSummaryFlavor })),
  { ssr: false, loading: () => <FlavorLoadingSkeleton /> }
);

const SUMMARY_FLAVOR_MAP: Record<string, SummaryFlavorComponent> = {
  catan: CatanSummaryFlavorLazy,
};

export function hasSummaryFlavor(gameSlug: string | null | undefined): boolean {
  return gameSlug != null && SUMMARY_FLAVOR_MAP[gameSlug] != null;
}

interface SummaryFlavorRendererProps extends SummaryFlavorProps {
  readonly gameSlug: string | null | undefined;
}

export function SummaryFlavorRenderer({
  gameSlug, session, className,
}: SummaryFlavorRendererProps): React.JSX.Element | null {
  const LazyFlavor = gameSlug != null ? SUMMARY_FLAVOR_MAP[gameSlug] : undefined;
  if (LazyFlavor == null) return null;
  return <LazyFlavor session={session} className={className} />;
}
```

- [ ] **Step 4: Run — verify pass**.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/session-live/SummaryFlavorRenderer.tsx \
        apps/web/src/components/features/session-live/__tests__/SummaryFlavorRenderer.test.tsx
git commit -m "feat(session-summary): #3022 SummaryFlavorRenderer twin dispatcher"
```

---

## Task 7: FE — i18n keys + parity guard

**Files:**
- Modify: `apps/web/src/locales/it.json`, `en.json`
- Test: `.../catan/__tests__/i18n-catan-summary-keys.test.ts`

- [ ] **Step 1: Write the failing guard test**

```ts
import { describe, it, expect } from 'vitest';
import enMessages from '@/locales/en.json';
import itMessages from '@/locales/it.json';

const KEYS = ['winnerTemplate', 'vpUnit', 'durationTemplate', 'standingsTitle', 'empty'];
type Catalog = { pages: { sessionSummary: { flavor: { catan: Record<string, string> } } } };
const it_ = (itMessages as Catalog).pages.sessionSummary?.flavor?.catan ?? {};
const en_ = (enMessages as Catalog).pages.sessionSummary?.flavor?.catan ?? {};

describe('Catan summary i18n keys (#3022)', () => {
  it.each(KEYS)('IT has %s', k => expect(it_[k]).toBeTruthy());
  it.each(KEYS)('EN has %s', k => expect(en_[k]).toBeTruthy());
  it('parity', () => expect(Object.keys(it_).sort()).toEqual(Object.keys(en_).sort()));
});
```

- [ ] **Step 2: Run — verify fail**.

- [ ] **Step 3: Add the subtree to both catalogs**

First check: `node -e "console.log(!!require('./src/locales/it.json').pages.sessionSummary)"` (run in `apps/web`). Merge into existing `pages.sessionSummary` if present; else create it.

IT (`it.json`), under `pages.sessionSummary`:
```json
"flavor": { "catan": {
  "winnerTemplate": "{name} vince!",
  "vpUnit": "PV",
  "durationTemplate": "{minutes} min",
  "standingsTitle": "Classifica finale",
  "empty": "Riepilogo non disponibile"
} }
```

EN (`en.json`), same path:
```json
"flavor": { "catan": {
  "winnerTemplate": "{name} wins!",
  "vpUnit": "VP",
  "durationTemplate": "{minutes} min",
  "standingsTitle": "Final standings",
  "empty": "Summary not available"
} }
```

- [ ] **Step 4: Run — verify pass**.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/locales/it.json apps/web/src/locales/en.json \
        apps/web/src/components/features/session-live/flavors/catan/__tests__/i18n-catan-summary-keys.test.ts
git commit -m "feat(session-summary): #3022 i18n Catan summary keys + parity guard"
```

---

## Task 8: FE — wire into `SessionSummaryView` (status-gated)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx`
- Test: `.../__tests__/SessionSummaryView.test.tsx`

**Interfaces:**
- Consumes: `SummaryFlavorRenderer`, `hasSummaryFlavor`; the raw `sessionQuery.data: GameSessionDto | null`.

- [ ] **Step 1: Write positive + negative wiring tests**

Reuse this file's existing real-data harness for `useSessionDetail`. Positive: completed Catan DTO (`gameSlug:'catan'`, `status:'Completed'`, scoreData + scorePlayers) → `catan-summary-row-name` present. Negatives: `gameSlug:'wingspan'` → absent; `status:'InProgress'` (Catan) → absent. If the file lacks a real-data harness, add `vi.mock('@/hooks/queries/useSessionDetail', () => ({ useSessionDetail: () => ({ data: dto, isLoading: false, isError: false, isSuccess: true }) }))` and drive the DTO per case.

```tsx
it('mounts Catan flavor for completed Catan (#3022)', async () => {
  // arrange: useSessionDetail → completed catan dto with scoreData+scorePlayers
  expect(await screen.findByTestId('catan-summary-row-name')).toBeInTheDocument();
});
it('does NOT mount for non-catan', async () => {
  // arrange: gameSlug 'wingspan'
  expect(screen.queryByTestId('catan-summary-row-name')).toBeNull();
});
it('does NOT mount for non-completed status', async () => {
  // arrange: catan but status 'InProgress'
  expect(screen.queryByTestId('catan-summary-row-name')).toBeNull();
});
```

- [ ] **Step 2: Run — verify fail**.

- [ ] **Step 3: Wire the mount (inside the default+partial return block only)**

Import:
```tsx
import { SummaryFlavorRenderer, hasSummaryFlavor } from '@/components/features/session-live/SummaryFlavorRenderer';
```

As the FIRST child of the `<div data-slot="session-summary-view">` in the **default+partial full-render block** (the one containing `<SessionSummaryHero>`, ~line 895 — NOT the loading/error/not-found/not-completed shells):

```tsx
{sessionQuery.data != null &&
  sessionQuery.data.status === 'Completed' &&
  hasSummaryFlavor(sessionQuery.data.gameSlug) && (
    <div className="px-4 pt-4 sm:px-6">
      <SummaryFlavorRenderer gameSlug={sessionQuery.data.gameSlug} session={sessionQuery.data} />
    </div>
  )}
```

- [ ] **Step 4: Run — verify pass + no regression**

Run: `cd apps/web && pnpm vitest run SessionSummaryView` — new tests pass, existing green.

- [ ] **Step 5: Full FE gate**

Run: `cd apps/web && pnpm typecheck && pnpm vitest run SummaryFlavorRenderer CatanSummaryFlavor catan-summary-standings i18n-catan-summary-keys SessionSummaryView games.schemas`
Expected: all green, no type errors.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx \
        apps/web/src/app/(authenticated)/sessions/[id]/_components/__tests__/SessionSummaryView.test.tsx
git commit -m "feat(session-summary): #3022 wire CatanSummaryFlavor (status-gated) into SessionSummaryView"
```

---

## Final verification (before PR)

- [ ] `cd apps/api && dotnet test --filter "BoundedContext=GameManagement"` — green.
- [ ] `cd apps/web && pnpm typecheck && pnpm lint && pnpm test` — green, no new lint violations.
- [ ] Manual (optional): a completed Catan session with recorded polymorphic scores → hero + standings with real VP/colors; a non-Catan or non-completed session → generic layout unchanged.

## Out of scope (documented)

Board snapshot, DiceChart, TradeBars, robber-move counter, longest-production-run, biggest-hand, 5-category breakdown, longest-road/largest-army badges — require persisted end-game `gameState`, absent on the summary DTO.
