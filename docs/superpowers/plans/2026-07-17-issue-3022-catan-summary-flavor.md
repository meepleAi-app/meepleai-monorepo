# Catan SUMMARY flavor (#3022) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a presentational Catan SUMMARY flavor (winner hero + final standings from real `scoreData` + player colors) mounted on `/sessions/[id]` via an isolated summary-flavor dispatcher.

**Architecture:** BE enriches `GameSessionDto` with nullable `GameSlug`/`GameName` (only on the single-session GET path, resolved via `IGameCoreDataProvider` + `Slugifier`). FE adds a *twin* dispatcher `SummaryFlavorRenderer` (props `= { session: GameSessionDto }`, disjoint from the live `FlavorRenderer` typed on `LiveSessionDto`), plus `CatanSummaryFlavor` that reuses the pure adapter `mapScoreDataToEndgameSummary` and the `catanPieceColor` palette.

**Tech Stack:** .NET 9 (MediatR/CQRS, xUnit + Moq + FluentAssertions), Next.js 16 / React 19, Zod, react-intl, Vitest.

## Global Constraints

- **Backend test path**: `apps/api/tests/Api.Tests` (NOT `tests/Api.Tests`).
- **CQRS**: no endpoint changes here; only the existing query handler + DTO.
- **i18n**: every new key exists in BOTH `apps/web/src/locales/it.json` AND `en.json`, with key parity. Italian text keeps correct accents (à, è, ù, …).
- **Design tokens**: semantic tokens + entity utilities only; the only allowed raw inline HSL are the Catan piece colors via `catanPieceColor()` (already carry line-level `eslint-disable meepleai/no-inline-hsl-v2`).
- **TDD**: RED → GREEN → REFACTOR, one behavior per test, frequent commits.
- **Branch**: `feature/issue-3022-catan-summary-flavor` (parent `main-dev`).

---

## File Structure

**Backend**
- Modify `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameSessionDto.cs` — add `GameSlug?`, `GameName?`.
- Modify `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetGameSessionByIdQueryHandler.cs` — inject `IGameCoreDataProvider`, resolve slug/name.
- Modify `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/GetGameSessionByIdQueryHandlerTests.cs` — new ctor arg + coverage.

**Frontend**
- Modify `apps/web/src/lib/api/schemas/games.schemas.ts` — Zod `gameSlug`/`gameName`.
- Create `apps/web/src/components/features/session-live/SummaryFlavorRenderer.tsx` — dispatcher + `SummaryFlavorProps` + `SUMMARY_FLAVOR_MAP` + `hasSummaryFlavor`.
- Create `apps/web/src/components/features/session-live/flavors/catan/catan-summary-standings.ts` — pure standings builder (colors + ordering).
- Create `apps/web/src/components/features/session-live/flavors/catan/CatanSummaryFlavor.tsx` — hero + standings render.
- Modify `apps/web/src/app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx` — conditional mount.
- Modify `apps/web/src/locales/it.json` + `en.json` — new subtree.
- Create `apps/web/src/components/features/session-live/flavors/catan/__tests__/i18n-catan-summary-keys.test.ts` — parity guard.

---

## Task 1: BE — `GameSessionDto` gains `GameSlug`/`GameName`, resolved on single-session GET

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameSessionDto.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetGameSessionByIdQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/GetGameSessionByIdQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IGameCoreDataProvider.GetCoreDataAsync(GameRef, CancellationToken) → Task<GameCoreData?>` (`GameCoreData.Title`); `GameRef.Shared(Guid)`; `Slugifier.Slugify(string) → string` (namespace `Api.SharedKernel.Application.Services`, returns `"unknown"` on null/empty).
- Produces: `GameSessionDto` with new trailing fields `string? GameSlug`, `string? GameName` (default `null`).

- [ ] **Step 1: Add the two fields to the DTO record**

In `GameSessionDto.cs`, append two nullable params to the `GameSessionDto` record (after `ScoreData`):

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
    // #3022: game identity for the summary flavor dispatch. Populated ONLY on
    // GET /api/v1/sessions/{id}; null on list/history paths (GameSessionMapper.ToDto).
    string? GameSlug = null,
    string? GameName = null
);
```

- [ ] **Step 2: Write the failing handler test — slug/name populated from the catalog**

In `GetGameSessionByIdQueryHandlerTests.cs`, first update the ctor to inject a mock provider (this is required for ALL existing tests too — do it now):

```csharp
private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
private readonly Mock<IGameCoreDataProvider> _gameCoreDataMock;
private readonly GetGameSessionByIdQueryHandler _handler;

public GetGameSessionByIdQueryHandlerTests()
{
    _sessionRepositoryMock = new Mock<IGameSessionRepository>();
    _gameCoreDataMock = new Mock<IGameCoreDataProvider>();
    _handler = new GetGameSessionByIdQueryHandler(_sessionRepositoryMock.Object, _gameCoreDataMock.Object);
}

private static GameCoreData MakeCoreData(string title = "Catan") =>
    GameCoreData.Create(title, 1995, 3, 4, 90, 10);
```

Add imports at top: `using Api.SharedKernel.Application;` and `using Api.SharedKernel.Domain.ValueObjects;`.

Then add the new test:

```csharp
[Fact]
public async Task Handle_ExistingSession_PopulatesGameSlugAndName_FromCatalog()
{
    var gameId = Guid.NewGuid();
    var session = CreateSession(gameId);
    _sessionRepositoryMock
        .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(session);
    _gameCoreDataMock
        .Setup(p => p.GetCoreDataAsync(GameRef.Shared(gameId), It.IsAny<CancellationToken>()))
        .ReturnsAsync(MakeCoreData("Catan"));

    var result = await _handler.Handle(new GetGameSessionByIdQuery(session.Id), TestContext.Current.CancellationToken);

    result.Should().NotBeNull();
    result!.GameName.Should().Be("Catan");
    result.GameSlug.Should().Be("catan");
}

[Fact]
public async Task Handle_GameNotInCatalog_LeavesSlugAndNameNull()
{
    var gameId = Guid.NewGuid();
    var session = CreateSession(gameId);
    _sessionRepositoryMock
        .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(session);
    _gameCoreDataMock
        .Setup(p => p.GetCoreDataAsync(It.IsAny<GameRef>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((GameCoreData?)null);

    var result = await _handler.Handle(new GetGameSessionByIdQuery(session.Id), TestContext.Current.CancellationToken);

    result!.GameName.Should().BeNull();
    result.GameSlug.Should().BeNull();
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~GetGameSessionByIdQueryHandlerTests"`
Expected: compile error (`GetGameSessionByIdQueryHandler` has no 2-arg ctor) — this is the RED for the whole task, including the ctor migration.

- [ ] **Step 4: Implement the handler**

Rewrite `GetGameSessionByIdQueryHandler.cs`:

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

    public GetGameSessionByIdQueryHandler(
        IGameSessionRepository sessionRepository,
        IGameCoreDataProvider gameCoreData)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _gameCoreData = gameCoreData ?? throw new ArgumentNullException(nameof(gameCoreData));
    }

    public async Task<GameSessionDto?> Handle(GetGameSessionByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null) return null;

        var coreData = await _gameCoreData
            .GetCoreDataAsync(GameRef.Shared(session.GameId), cancellationToken)
            .ConfigureAwait(false);
        var gameName = coreData?.Title;
        var gameSlug = gameName is null ? null : Slugifier.Slugify(gameName);

        return MapToDto(session, gameSlug, gameName);
    }

    private static GameSessionDto MapToDto(GameSession session, string? gameSlug, string? gameName)
    {
        var playerDtos = session.Players.Select(p => new SessionPlayerDto(
            PlayerName: p.PlayerName,
            PlayerOrder: p.PlayerOrder,
            Color: p.Color
        )).ToList();

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
            GameSlug: gameSlug,
            GameName: gameName
        );
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~GetGameSessionByIdQueryHandlerTests"`
Expected: PASS (all 6 pre-existing + 2 new).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameSessionDto.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetGameSessionByIdQueryHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/GetGameSessionByIdQueryHandlerTests.cs
git commit -m "feat(session-summary): #3022 BE — enrich GameSessionDto with GameSlug/GameName"
```

---

## Task 2: FE — Zod schema exposes `gameSlug`/`gameName`

**Files:**
- Modify: `apps/web/src/lib/api/schemas/games.schemas.ts:100-120`
- Test: `apps/web/src/lib/api/schemas/__tests__/games.schemas.test.ts` (create if absent)

**Interfaces:**
- Produces: `GameSessionDto` (inferred) gains `gameSlug?: string | null`, `gameName?: string | null`.

- [ ] **Step 1: Write the failing schema test**

Create/append `apps/web/src/lib/api/schemas/__tests__/games.schemas.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { GameSessionDtoSchema } from '../games.schemas';

const base = {
  id: '00000000-0000-4000-8000-000000000001',
  gameId: '00000000-0000-4000-8000-0000000000aa',
  status: 'Completed',
  startedAt: '2026-01-01T00:00:00Z',
  completedAt: '2026-01-01T01:00:00Z',
  playerCount: 2,
  players: [{ playerName: 'Alice', playerOrder: 1, color: 'Red' }],
  winnerName: 'Alice',
  notes: null,
  durationMinutes: 47,
};

describe('GameSessionDtoSchema #3022', () => {
  it('parses gameSlug/gameName when present', () => {
    const parsed = GameSessionDtoSchema.parse({ ...base, gameSlug: 'catan', gameName: 'Catan' });
    expect(parsed.gameSlug).toBe('catan');
    expect(parsed.gameName).toBe('Catan');
  });

  it('accepts null and absent gameSlug/gameName (back-compat)', () => {
    expect(GameSessionDtoSchema.parse({ ...base, gameSlug: null, gameName: null }).gameSlug).toBeNull();
    expect(GameSessionDtoSchema.parse(base).gameSlug).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run games.schemas`
Expected: FAIL — `parsed.gameSlug` is `undefined` (schema strips unknown keys), first assertion fails.

- [ ] **Step 3: Add the fields to the schema**

In `games.schemas.ts`, inside `GameSessionDtoSchema` (after `turnOrderType`):

```ts
  // #3022: game identity for the summary flavor dispatch. Populated only on the
  // single-session GET; null/absent on list/history responses.
  gameSlug: z.string().nullable().optional(),
  gameName: z.string().nullable().optional(),
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run games.schemas`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/schemas/games.schemas.ts apps/web/src/lib/api/schemas/__tests__/games.schemas.test.ts
git commit -m "feat(session-summary): #3022 FE — gameSlug/gameName on GameSessionDto schema"
```

---

## Task 3: FE — pure standings builder `buildCatanSummaryStandings`

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/catan-summary-standings.ts`
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/catan-summary-standings.test.ts`

**Interfaces:**
- Consumes: `mapScoreDataToEndgameSummary(scoringType, scoreData, players) → readonly FinalScoreEntry[]` from `@/lib/session-live/score-data-to-endgame-summary`; `SessionPlayerDto` (`{ id?, playerName, playerOrder, color }`) from `@/lib/api/schemas/games.schemas`; `ScoreType`, `ScoreDataByType` from `@/components/sessions/score-strategies/types`.
- Produces: `CatanSummaryRow = { playerName: string; score: number; isWinner: boolean; color: string | null }`; `buildCatanSummaryStandings(scoringType: string | null | undefined, scoreDataJson: string | null | undefined, players: readonly SessionPlayerDto[]) → CatanSummaryRow[]` (winner-first, then score DESC; `[]` when unusable).

- [ ] **Step 1: Write the failing test**

Create `catan-summary-standings.test.ts`:

```ts
import { describe, it, expect, vi } from 'vitest';
import { buildCatanSummaryStandings } from '../catan-summary-standings';
import type { SessionPlayerDto } from '@/lib/api/schemas/games.schemas';

const players: SessionPlayerDto[] = [
  { id: 'p1', playerName: 'Alice', playerOrder: 1, color: 'Red' },
  { id: 'p2', playerName: 'Bob', playerOrder: 2, color: 'Blue' },
  { id: 'p3', playerName: 'Carol', playerOrder: 3, color: 'Orange' },
];

const pointsJson = JSON.stringify({
  scores: [
    { playerId: 'p1', points: 10 },
    { playerId: 'p2', points: 8 },
    { playerId: 'p3', points: 6 },
  ],
});

describe('buildCatanSummaryStandings', () => {
  it('orders winner-first then score DESC, zipping the player color', () => {
    const rows = buildCatanSummaryStandings('Points', pointsJson, players);
    expect(rows.map(r => r.playerName)).toEqual(['Alice', 'Bob', 'Carol']);
    expect(rows[0]).toMatchObject({ playerName: 'Alice', score: 10, isWinner: true, color: 'Red' });
    expect(rows[2]).toMatchObject({ playerName: 'Carol', score: 6, isWinner: false, color: 'Orange' });
  });

  it('returns [] when scoringType/scoreData is null', () => {
    expect(buildCatanSummaryStandings(null, null, players)).toEqual([]);
    expect(buildCatanSummaryStandings('Points', null, players)).toEqual([]);
  });

  it('returns [] and warns on malformed JSON', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    expect(buildCatanSummaryStandings('Points', '{not json', players)).toEqual([]);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run catan-summary-standings`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement the builder**

Create `catan-summary-standings.ts`:

```ts
/**
 * buildCatanSummaryStandings — pure adapter from the raw GameSessionDto scoring
 * fields to ordered summary rows enriched with each player's color.
 *
 * #3022. The upstream adapter (mapScoreDataToEndgameSummary) maps players.map()
 * preserving order, so the FinalScoreEntry[] is index-parallel to `players`,
 * which lets us zip the color by index before sorting.
 */

import { mapScoreDataToEndgameSummary } from '@/lib/session-live/score-data-to-endgame-summary';
import type { SessionPlayerDto } from '@/lib/api/schemas/games.schemas';
import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';

export interface CatanSummaryRow {
  readonly playerName: string;
  readonly score: number;
  readonly isWinner: boolean;
  readonly color: string | null;
}

const SCORE_TYPES: readonly ScoreType[] = ['Points', 'BinaryWin', 'Objectives', 'Ranking'];

function parseScoreData(
  scoringType: string | null | undefined,
  scoreDataJson: string | null | undefined
): { scoringType: ScoreType; scoreData: ScoreDataByType[ScoreType] } | null {
  if (scoringType == null || scoreDataJson == null) return null;
  if (!SCORE_TYPES.includes(scoringType as ScoreType)) return null;
  try {
    return {
      scoringType: scoringType as ScoreType,
      scoreData: JSON.parse(scoreDataJson) as ScoreDataByType[ScoreType],
    };
  } catch {
    console.warn(`buildCatanSummaryStandings: malformed scoreData JSON for "${scoringType}"`);
    return null;
  }
}

export function buildCatanSummaryStandings(
  scoringType: string | null | undefined,
  scoreDataJson: string | null | undefined,
  players: readonly SessionPlayerDto[]
): CatanSummaryRow[] {
  const parsed = parseScoreData(scoringType, scoreDataJson);
  if (parsed === null) return [];

  const adapterPlayers = players.map(p => ({ id: p.id ?? p.playerName, name: p.playerName }));
  const entries = mapScoreDataToEndgameSummary(parsed.scoringType, parsed.scoreData, adapterPlayers);
  if (entries.length === 0) return [];

  // entries is index-parallel to `players` (adapter preserves order) → zip color.
  const withColor: CatanSummaryRow[] = entries.map((e, i) => ({
    playerName: e.playerName,
    score: e.score,
    isWinner: e.isWinner,
    color: players[i]?.color ?? null,
  }));

  return withColor.sort(
    (a, b) => Number(b.isWinner) - Number(a.isWinner) || b.score - a.score
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run catan-summary-standings`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/session-live/flavors/catan/catan-summary-standings.ts \
        apps/web/src/components/features/session-live/flavors/catan/__tests__/catan-summary-standings.test.ts
git commit -m "feat(session-summary): #3022 pure Catan standings builder (scoreData → rows + color)"
```

---

## Task 4: FE — `CatanSummaryFlavor` component (hero + standings)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/CatanSummaryFlavor.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanSummaryFlavor.test.tsx`

**Interfaces:**
- Consumes: `buildCatanSummaryStandings` (Task 3); `catanPieceColor(color)` from `./catan-palette`; `SummaryFlavorProps` (Task 5 — `{ session: GameSessionDto; className? }`). To avoid a circular task dependency, this component declares its own local props interface `{ session: GameSessionDto; className?: string }` (Catan flavor precedent: `CatanLiveFlavor` re-declares props locally instead of importing `FlavorProps`).
- Produces: named export `CatanSummaryFlavor`.

- [ ] **Step 1: Write the failing test**

Create `CatanSummaryFlavor.test.tsx`:

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

function renderWithIntl(ui: ReactElement) {
  return render(<IntlProvider locale="it" messages={MESSAGES}>{ui}</IntlProvider>);
}

const baseSession: GameSessionDto = {
  id: '00000000-0000-4000-8000-000000000001',
  gameId: '00000000-0000-4000-8000-0000000000aa',
  status: 'Completed',
  startedAt: '2026-01-01T00:00:00Z',
  completedAt: '2026-01-01T00:47:00Z',
  playerCount: 2,
  players: [
    { id: 'p1', playerName: 'Alice', playerOrder: 1, color: 'Red' },
    { id: 'p2', playerName: 'Bob', playerOrder: 2, color: 'Blue' },
  ],
  winnerName: 'Alice',
  notes: null,
  durationMinutes: 47,
  scoringType: 'Points',
  scoreData: JSON.stringify({ scores: [{ playerId: 'p1', points: 10 }, { playerId: 'p2', points: 8 }] }),
  gameSlug: 'catan',
  gameName: 'Catan',
};

describe('CatanSummaryFlavor', () => {
  it('renders the winner hero and ordered standings', () => {
    renderWithIntl(<CatanSummaryFlavor session={baseSession} />);
    expect(screen.getByText('Alice vince!')).toBeInTheDocument();
    const names = screen.getAllByTestId('catan-summary-row-name').map(n => n.textContent);
    expect(names).toEqual(['Alice', 'Bob']);
  });

  it('renders the empty state when scoreData is null', () => {
    renderWithIntl(<CatanSummaryFlavor session={{ ...baseSession, scoringType: null, scoreData: null }} />);
    expect(screen.getByText('Riepilogo non disponibile')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run CatanSummaryFlavor`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement the component**

Create `CatanSummaryFlavor.tsx`:

```tsx
'use client';

import { useIntl } from 'react-intl';
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
  const intl = useIntl();

  const winnerTemplate =
    (intl.messages['pages.sessionSummary.flavor.catan.winnerTemplate'] as string) ?? '{name} vince!';
  const durationTemplate =
    (intl.messages['pages.sessionSummary.flavor.catan.durationTemplate'] as string) ?? '{minutes} min';
  const vpUnit = t('pages.sessionSummary.flavor.catan.vpUnit');
  const standingsTitle = t('pages.sessionSummary.flavor.catan.standingsTitle');
  const emptyLabel = t('pages.sessionSummary.flavor.catan.empty');

  const rows = buildCatanSummaryStandings(session.scoringType, session.scoreData, session.players);

  if (rows.length === 0) {
    return (
      <section
        data-slot="catan-summary-flavor"
        className={`rounded-2xl border border-border bg-card p-4 text-center text-[13px] text-muted-foreground ${className ?? ''}`}
      >
        {emptyLabel}
      </section>
    );
  }

  const winner = rows.find(r => r.isWinner) ?? rows[0];
  const maxScore = rows.reduce((m, r) => Math.max(m, r.score), 0);

  return (
    <section
      data-slot="catan-summary-flavor"
      aria-label={standingsTitle}
      className={`flex flex-col gap-4 rounded-2xl border border-border bg-card p-4 ${className ?? ''}`}
    >
      <header data-slot="catan-summary-hero" className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full border border-border-strong text-lg"
          style={{ backgroundColor: catanPieceColor(winner.color ?? '') }}
        >
          👑
        </span>
        <div className="flex flex-col">
          <span className="text-base font-semibold text-foreground">
            {winnerTemplate.replace('{name}', winner.playerName)}
          </span>
          <span className="text-[13px] text-muted-foreground">
            {winner.score} {vpUnit} · {durationTemplate.replace('{minutes}', String(session.durationMinutes))}
          </span>
        </div>
      </header>

      <ol data-slot="catan-summary-standings" className="flex flex-col gap-1.5">
        {rows.map((row, i) => (
          <li
            key={row.playerName}
            data-slot="catan-summary-row"
            className="flex items-center gap-2 text-[13px]"
          >
            <span className="w-5 tabular-nums text-muted-foreground">{i + 1}°</span>
            <span
              aria-hidden="true"
              className="h-3.5 w-3.5 shrink-0 rounded-full border border-border-strong"
              style={{ backgroundColor: catanPieceColor(row.color ?? '') }}
            />
            <span data-testid="catan-summary-row-name" className="flex-1 truncate text-foreground">
              {row.playerName}
            </span>
            <span className="h-1.5 w-24 overflow-hidden rounded-full bg-muted" aria-hidden="true">
              <span
                className="block h-full rounded-full bg-entity-session"
                style={{ width: `${maxScore > 0 ? (row.score / maxScore) * 100 : 0}%` }}
              />
            </span>
            <span className="w-12 text-right tabular-nums text-foreground">
              {row.score} {vpUnit}
            </span>
          </li>
        ))}
      </ol>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run CatanSummaryFlavor`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/session-live/flavors/catan/CatanSummaryFlavor.tsx \
        apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanSummaryFlavor.test.tsx
git commit -m "feat(session-summary): #3022 CatanSummaryFlavor (winner hero + standings)"
```

---

## Task 5: FE — `SummaryFlavorRenderer` dispatcher

**Files:**
- Create: `apps/web/src/components/features/session-live/SummaryFlavorRenderer.tsx`
- Test: `apps/web/src/components/features/session-live/__tests__/SummaryFlavorRenderer.test.tsx`

**Interfaces:**
- Consumes: `CatanSummaryFlavor` (Task 4); `FlavorLoadingSkeleton` from `./FlavorLoadingSkeleton`; `GameSessionDto` from `@/lib/api/schemas/games.schemas`.
- Produces: `SummaryFlavorProps = { session: GameSessionDto; className?: string }`; `SummaryFlavorRenderer(props & { gameSlug: string | null | undefined })`; `hasSummaryFlavor(gameSlug: string | null | undefined): boolean`.

- [ ] **Step 1: Write the failing test**

Create `SummaryFlavorRenderer.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { hasSummaryFlavor } from '../SummaryFlavorRenderer';

describe('hasSummaryFlavor', () => {
  it('is true for catan', () => {
    expect(hasSummaryFlavor('catan')).toBe(true);
  });
  it('is false for unknown slug / null / undefined', () => {
    expect(hasSummaryFlavor('wingspan')).toBe(false);
    expect(hasSummaryFlavor(null)).toBe(false);
    expect(hasSummaryFlavor(undefined)).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run SummaryFlavorRenderer`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement the dispatcher**

Create `SummaryFlavorRenderer.tsx`:

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

// Lazy chunks minted at MODULE scope (never inside render) — same rule as FlavorRenderer.
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
  gameSlug,
  session,
  className,
}: SummaryFlavorRendererProps): React.JSX.Element | null {
  const LazyFlavor = gameSlug != null ? SUMMARY_FLAVOR_MAP[gameSlug] : undefined;
  if (LazyFlavor == null) return null;
  return <LazyFlavor session={session} className={className} />;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run SummaryFlavorRenderer`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/session-live/SummaryFlavorRenderer.tsx \
        apps/web/src/components/features/session-live/__tests__/SummaryFlavorRenderer.test.tsx
git commit -m "feat(session-summary): #3022 SummaryFlavorRenderer twin dispatcher"
```

---

## Task 6: FE — i18n keys + parity guard

**Files:**
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/i18n-catan-summary-keys.test.ts`

**Interfaces:**
- Produces: subtree `pages.sessionSummary.flavor.catan.{winnerTemplate,vpUnit,durationTemplate,standingsTitle,empty}` in both catalogs.

- [ ] **Step 1: Write the failing guard test**

Create `i18n-catan-summary-keys.test.ts`:

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
  it('IT/EN parity', () => {
    expect(Object.keys(it_).sort()).toEqual(Object.keys(en_).sort());
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run i18n-catan-summary-keys`
Expected: FAIL — subtree absent.

- [ ] **Step 3: Add the subtree to both catalogs**

In `it.json`, under `pages.sessionSummary` (create the `flavor.catan` path):

```json
"flavor": {
  "catan": {
    "winnerTemplate": "{name} vince!",
    "vpUnit": "PV",
    "durationTemplate": "{minutes} min",
    "standingsTitle": "Classifica finale",
    "empty": "Riepilogo non disponibile"
  }
}
```

In `en.json`, same path:

```json
"flavor": {
  "catan": {
    "winnerTemplate": "{name} wins!",
    "vpUnit": "VP",
    "durationTemplate": "{minutes} min",
    "standingsTitle": "Final standings",
    "empty": "Summary not available"
  }
}
```

> Note: if `pages.sessionSummary` already exists in a catalog, merge `flavor` into it rather than duplicating the parent key. Verify with `node -e "console.log(!!require('./src/locales/it.json').pages.sessionSummary)"` before editing.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run i18n-catan-summary-keys`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/locales/it.json apps/web/src/locales/en.json \
        apps/web/src/components/features/session-live/flavors/catan/__tests__/i18n-catan-summary-keys.test.ts
git commit -m "feat(session-summary): #3022 i18n Catan summary keys + parity guard"
```

---

## Task 7: FE — wire the flavor into `SessionSummaryView`

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx`
- Test: `apps/web/src/app/(authenticated)/sessions/[id]/_components/__tests__/SessionSummaryView.test.tsx` (extend)

**Interfaces:**
- Consumes: `SummaryFlavorRenderer`, `hasSummaryFlavor` (Task 5); the raw `sessionQuery.data: GameSessionDto | null` already present in the component (line ~402).

- [ ] **Step 1: Write the failing wiring test**

Append to `SessionSummaryView.test.tsx` a case asserting the flavor mounts for a completed Catan session (real-data path, not fixture). Use the existing test's render harness/mocks; assert on the flavor slot:

```tsx
it('mounts the Catan summary flavor for a completed Catan session (#3022)', async () => {
  // Arrange: mock useSessionDetail to return a completed GameSessionDto with
  // gameSlug='catan' + Points scoreData (mirror the harness already used in this file).
  // Act: render SessionSummaryView for that session id.
  // Assert:
  expect(await screen.findByTestId('catan-summary-row-name')).toBeInTheDocument();
});
```

> The exact mock wiring mirrors the existing `useSessionDetail`/fixture mocks already in this test file — reuse that harness; only the returned DTO changes (add `gameSlug:'catan'`, `scoringType:'Points'`, `scoreData` JSON). If the file has no real-data harness, add a `vi.mock('@/hooks/queries/useSessionDetail', ...)` returning `{ data: <dto>, isLoading:false, isError:false, isSuccess:true }`.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run SessionSummaryView`
Expected: FAIL — flavor slot not rendered.

- [ ] **Step 3: Wire the mount**

In `SessionSummaryView.tsx`, add the import near the other imports:

```tsx
import { SummaryFlavorRenderer, hasSummaryFlavor } from '@/components/features/session-live/SummaryFlavorRenderer';
```

Then, as the FIRST child of the main container (`<div data-slot="session-summary-view">`, before `<SessionSummaryHero>`):

```tsx
{sessionQuery.data != null && hasSummaryFlavor(sessionQuery.data.gameSlug) && (
  <div className="px-4 pt-4 sm:px-6">
    <SummaryFlavorRenderer gameSlug={sessionQuery.data.gameSlug} session={sessionQuery.data} />
  </div>
)}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run SessionSummaryView`
Expected: PASS, and the pre-existing SessionSummaryView tests stay green.

- [ ] **Step 5: Full FE + typecheck gate**

Run: `cd apps/web && pnpm typecheck && pnpm vitest run SummaryFlavorRenderer CatanSummaryFlavor catan-summary-standings i18n-catan-summary-keys SessionSummaryView games.schemas`
Expected: all green, no type errors.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx \
        apps/web/src/app/(authenticated)/sessions/[id]/_components/__tests__/SessionSummaryView.test.tsx
git commit -m "feat(session-summary): #3022 wire CatanSummaryFlavor into SessionSummaryView"
```

---

## Final verification (before PR)

- [ ] `cd apps/api && dotnet test --filter "BoundedContext=GameManagement"` — BE green.
- [ ] `cd apps/web && pnpm typecheck && pnpm lint && pnpm test` — FE green, no new lint violations (watch `meepleai/no-hardcoded-color-utility`; inline HSL only via `catanPieceColor`).
- [ ] Manual (optional): open a completed Catan session summary → hero + standings render with real scores/colors; a non-Catan completed session → unchanged generic layout.

## Out of scope (documented in the spec)

Board snapshot, DiceChart, TradeBars, robber-move counter, longest-production-run, biggest-hand, 5-category scoreboard breakdown, longest-road/largest-army badges — all require persisted end-game `gameState`, absent on the summary DTO.
