# Game Night Full Experience — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare i 5 gap critici identificati dal spec-panel per la Game Night Experience mobile: KB-ready filter, State Tier config, backend orchestration warm-up, dispute diary entry, e photo attachment al diary.

**Architecture:** I gap sono distribuiti su due layer: backend (.NET CQRS) per GAP-001/GAP-003 e frontend (React/Next.js) per GAP-002/GAP-005/GAP-006. GAP-004 (offline) e GAP-007 (mobile UX) sono deferiti a piani separati per complessità e rischio.

**Tech Stack:** .NET 9 (MediatR, FluentValidation, xUnit, Moq), Next.js 16 (React 19, Zustand, Vitest, @testing-library/react, Tailwind 4, shadcn/ui)

---

## Scope

Questo piano copre 5 task indipendenti in ordine di priorità:

| Task | Gap | Layer | Priorità |
|------|-----|-------|---------|
| 1 | GAP-002: KB-ready filter in InlineGamePicker | Frontend | P0 |
| 2 | GAP-001: StateTier in StartGameNightSessionCommand | Backend | P0 |
| 3 | GAP-003: Auto warm-up toolbox template in StartGameNightSession | Backend | P1 |
| 4 | GAP-006: Dispute diary entry registration | Frontend | P1 |
| 5 | GAP-005: Photo attachment button in LiveSession | Frontend | P1 |

**Deferiti (piani separati):**
- GAP-004: Offline-first queue — complessità SW/IndexedDB, piano dedicato
- GAP-007: Mobile UX thumb zone — refinement visuale, piano dedicato

---

## File Map

### Creati
| File | Responsabilità |
|------|---------------|
| `apps/web/src/components/game-night/__tests__/InlineGamePickerKbFilter.test.tsx` | Test KB filter |
| `apps/web/src/components/game-night/__tests__/DisputeDiaryEntry.test.tsx` | Test dispute entry |
| `apps/web/src/components/game-night/__tests__/PhotoButton.test.tsx` | Test photo button |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/GameNight/StartGameNightSessionStateTierTests.cs` | Test StateTier propagation |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/GameNight/StartGameNightSessionToolboxWarmupTests.cs` | Test toolbox warm-up |

### Modificati
| File | Modifica |
|------|---------|
| `apps/web/src/stores/game-night/types.ts` | Aggiunge `kbStatus` a `GameNightGame` |
| `apps/web/src/components/game-night/planning/InlineGamePicker.tsx` | Filtra per `kbStatus` |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommand.cs` | Aggiunge `StateTier` |
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommand.cs` | Aggiunge `StateTier` |
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandler.cs` | Propaga `StateTier` + toolbox warm-up |
| `apps/web/src/components/game-night/QuickActions.tsx` | Aggiunge pulsante Foto |
| `apps/web/src/components/game-night/__tests__/QuickActions.test.tsx` | Aggiorna test per 5 pulsanti |
| `apps/web/src/components/game-night/LiveSessionView.tsx` | Wire foto + dispute diary |

---

## Task 1 — GAP-002: KB-Ready Filter in InlineGamePicker

### File coinvolti
- Modify: `apps/web/src/stores/game-night/types.ts`
- Modify: `apps/web/src/components/game-night/planning/InlineGamePicker.tsx`
- Create: `apps/web/src/components/game-night/__tests__/InlineGamePickerKbFilter.test.tsx`

- [ ] **Step 1.1: Scrivi il test fallente**

```typescript
// apps/web/src/components/game-night/__tests__/InlineGamePickerKbFilter.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { InlineGamePicker } from '../planning/InlineGamePicker';
import type { GameNightGame } from '@/stores/game-night';

const kbReadyGame: GameNightGame = {
  id: 'g1',
  title: 'Terraforming Mars',
  kbStatus: 'indexed',
};

const noKbGame: GameNightGame = {
  id: 'g2',
  title: 'Dixit',
  kbStatus: 'not_indexed',
};

const unknownKbGame: GameNightGame = {
  id: 'g3',
  title: 'Catan',
  // kbStatus intentionally omitted (undefined)
};

describe('InlineGamePicker — KB filter', () => {
  it('shows only KB-ready games when filterKbReady=true', () => {
    render(
      <InlineGamePicker
        games={[kbReadyGame, noKbGame, unknownKbGame]}
        onSelect={vi.fn()}
        filterKbReady
      />
    );
    expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
    expect(screen.queryByText('Dixit')).not.toBeInTheDocument();
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });

  it('shows all games when filterKbReady=false (default)', () => {
    render(
      <InlineGamePicker
        games={[kbReadyGame, noKbGame, unknownKbGame]}
        onSelect={vi.fn()}
      />
    );
    expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
    expect(screen.getByText('Dixit')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('shows empty state when no KB-ready games exist', () => {
    render(
      <InlineGamePicker
        games={[noKbGame]}
        onSelect={vi.fn()}
        filterKbReady
      />
    );
    expect(screen.getByText(/Nessun gioco/)).toBeInTheDocument();
  });

  it('renders AI badge on KB-ready games', () => {
    render(
      <InlineGamePicker
        games={[kbReadyGame]}
        onSelect={vi.fn()}
      />
    );
    expect(screen.getByTestId('kb-badge-g1')).toBeInTheDocument();
  });
});
```

- [ ] **Step 1.2: Esegui il test per verificare che fallisca**

```bash
cd apps/web
pnpm test InlineGamePickerKbFilter --run
```

Expected output: FAIL — `kbStatus` non esiste su `GameNightGame`, `filterKbReady` prop non esiste.

- [ ] **Step 1.3: Aggiungi `kbStatus` a `GameNightGame` in types.ts**

In `apps/web/src/stores/game-night/types.ts`, modifica l'interfaccia `GameNightGame`:

```typescript
export interface GameNightGame {
  id: string;
  title: string;
  thumbnailUrl?: string;
  minPlayers?: number;
  maxPlayers?: number;
  kbStatus?: 'indexed' | 'not_indexed' | 'unknown';
}
```

- [ ] **Step 1.4: Aggiorna `InlineGamePicker` per supportare il filtro e il badge**

Sostituisci il contenuto di `apps/web/src/components/game-night/planning/InlineGamePicker.tsx`:

```typescript
'use client';

import { useMemo } from 'react';

import { Gamepad2, Sparkles } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { GameNightGame } from '@/stores/game-night';

interface InlineGamePickerProps {
  games: GameNightGame[];
  onSelect: (game: GameNightGame) => void;
  playerCount?: number;
  excludeIds?: string[];
  /** Quando true, mostra solo giochi con kbStatus === 'indexed' */
  filterKbReady?: boolean;
}

export function InlineGamePicker({
  games,
  onSelect,
  playerCount,
  excludeIds = [],
  filterKbReady = false,
}: InlineGamePickerProps) {
  const filtered = useMemo(() => {
    let result = games.filter(g => !excludeIds.includes(g.id));
    if (playerCount) {
      result = result.filter(
        g =>
          (!g.minPlayers || g.minPlayers <= playerCount) &&
          (!g.maxPlayers || g.maxPlayers >= playerCount)
      );
    }
    if (filterKbReady) {
      result = result.filter(g => g.kbStatus === 'indexed');
    }
    return result;
  }, [games, playerCount, excludeIds, filterKbReady]);

  if (filtered.length === 0) {
    return (
      <div className="py-6 text-center text-sm text-muted-foreground">
        Nessun gioco adatto{playerCount ? ` per ${playerCount} giocatori` : ''}
        {filterKbReady && ' con AI disponibile'}
      </div>
    );
  }

  return (
    <div
      className="flex gap-3 overflow-x-auto py-2 px-1 scrollbar-thin"
      data-testid="inline-game-picker"
    >
      {filtered.map(game => (
        <button
          key={game.id}
          onClick={() => onSelect(game)}
          className={cn(
            'relative flex flex-col items-center gap-1.5 p-3 rounded-xl',
            'border border-border bg-card hover:border-primary/30',
            'transition-all duration-200 hover:shadow-sm',
            'shrink-0 w-[120px]'
          )}
        >
          {game.kbStatus === 'indexed' && (
            <span
              data-testid={`kb-badge-${game.id}`}
              className="absolute top-1.5 right-1.5 inline-flex items-center gap-0.5 rounded-full bg-emerald-50 px-1.5 py-0.5 text-[9px] font-medium text-emerald-700 border border-emerald-200"
            >
              <Sparkles className="h-2.5 w-2.5" aria-hidden="true" />
              AI
            </span>
          )}
          <div className="h-16 w-16 rounded-lg bg-muted flex items-center justify-center">
            {game.thumbnailUrl ? (
              <img
                src={game.thumbnailUrl}
                alt={game.title}
                className="h-full w-full rounded-lg object-cover"
              />
            ) : (
              <Gamepad2 className="h-6 w-6 text-muted-foreground" />
            )}
          </div>
          <span className="text-xs font-medium text-foreground text-center line-clamp-2">
            {game.title}
          </span>
          {(game.minPlayers || game.maxPlayers) && (
            <span className="text-[10px] text-muted-foreground">
              {game.minPlayers}–{game.maxPlayers} giocatori
            </span>
          )}
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 1.5: Esegui i test per verificare che passino**

```bash
cd apps/web
pnpm test InlineGamePickerKbFilter --run
```

Expected: 4/4 PASS

- [ ] **Step 1.6: Verifica che i test pre-esistenti di InlineGamePicker passino**

```bash
cd apps/web
pnpm test InlineGamePicker --run
```

Expected: tutti PASS (il filtro è opt-in, comportamento default invariato)

- [ ] **Step 1.7: Verifica typecheck**

```bash
cd apps/web
pnpm typecheck 2>&1 | grep -i "error" | head -10
```

Expected: nessun errore nuovo.

- [ ] **Step 1.8: Commit**

```bash
git add apps/web/src/stores/game-night/types.ts \
        apps/web/src/components/game-night/planning/InlineGamePicker.tsx \
        apps/web/src/components/game-night/__tests__/InlineGamePickerKbFilter.test.tsx
git commit -m "feat(game-night): add kbStatus filter to InlineGamePicker

GAP-002: giochi con kbStatus='indexed' mostrano badge AI e sono
filtrabili via prop filterKbReady=true nel wizard game night."
```

---

## Task 2 — GAP-001: StateTier in StartGameNightSessionCommand

### File coinvolti
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommand.cs` (aggiungi campo opzionale)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/GameNight/StartGameNightSessionStateTierTests.cs`

**Nota**: `CreateSessionCommand` si trova in `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/`. Cerca il file con:
```bash
find apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands -name "CreateSessionCommand*"
```

- [ ] **Step 2.1: Scrivi il test fallente**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/GameNight/StartGameNightSessionStateTierTests.cs
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class StartGameNightSessionStateTierTests
{
    private readonly Mock<IGameNightEventRepository> _mockRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IAutoSaveSchedulerService> _mockAutoSaveScheduler;
    private readonly StartGameNightSessionCommandHandler _handler;

    public StartGameNightSessionStateTierTests()
    {
        _mockRepository = new Mock<IGameNightEventRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockAutoSaveScheduler = new Mock<IAutoSaveSchedulerService>();
        _handler = new StartGameNightSessionCommandHandler(
            _mockRepository.Object,
            _mockMediator.Object,
            _mockUnitOfWork.Object,
            _mockAutoSaveScheduler.Object);
    }

    private static GameNightEvent CreatePublishedEvent(Guid organizerId)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Friday Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        return evt;
    }

    [Fact]
    public async Task Handle_WithStateTierFull_PropagatesStateTierToCreateSessionCommand()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizerId);

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto(organizerId, "org@test.com", "Organizer", null, null, null, null, false, null, null));

        CreateSessionCommand? capturedCommand = null;
        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateSessionResult>, CancellationToken>((cmd, _) => capturedCommand = cmd as CreateSessionCommand)
            .ReturnsAsync(new CreateSessionResult(Guid.NewGuid(), "ABC123"));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameId, "Terraforming Mars", organizerId,
            StateTier: GameStateTier.Full);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(GameStateTier.Full, capturedCommand.StateTier);
    }

    [Fact]
    public async Task Handle_WithoutStateTier_DefaultsToMinimal()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizerId);

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto(organizerId, "org@test.com", "Organizer", null, null, null, null, false, null, null));

        CreateSessionCommand? capturedCommand = null;
        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateSessionResult>, CancellationToken>((cmd, _) => capturedCommand = cmd as CreateSessionCommand)
            .ReturnsAsync(new CreateSessionResult(Guid.NewGuid(), "ABC123"));

        // StateTier omesso → default
        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameId, "Terraforming Mars", organizerId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(GameStateTier.Minimal, capturedCommand.StateTier);
    }
}
```

- [ ] **Step 2.2: Esegui il test per verificare che fallisca**

```bash
cd apps/api/src/Api
dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~StartGameNightSessionStateTierTests" \
  --no-build 2>&1 | tail -20
```

Expected: errore di compilazione — `GameStateTier` non esiste, `StateTier` non è parametro del command.

- [ ] **Step 2.3: Crea l'enum `GameStateTier`**

Crea il file `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/GameStateTier.cs`:

```csharp
namespace Api.BoundedContexts.SessionTracking.Domain.Enums;

/// <summary>
/// Tier di complessità dello stato di gioco tracciato durante una sessione.
/// T1_Minimal: solo turno/fase corrente (default)
/// T2_Score: punteggi + turno
/// T3_Full: risorse giocatore + scheda + campi custom
/// </summary>
public enum GameStateTier
{
    Minimal = 0,
    Score = 1,
    Full = 2,
}
```

- [ ] **Step 2.4: Aggiungi `StateTier` a `StartGameNightSessionCommand`**

In `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommand.cs`, aggiorna il record:

```csharp
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to start a game session within a published game night event.
/// Cross-BC: dispatches CreateSessionCommand to SessionTracking via MediatR.
/// If Participants is null or empty, the handler auto-seeds the organizer as sole owner.
/// StateTier controls the complexity of game state tracking (default: Minimal).
/// </summary>
internal record StartGameNightSessionCommand(
    Guid GameNightId,
    Guid GameId,
    string GameTitle,
    Guid UserId,
    IReadOnlyList<ParticipantDto>? Participants = null,
    GameStateTier StateTier = GameStateTier.Minimal
) : ICommand<StartGameNightSessionResult>;

internal record StartGameNightSessionResult(
    Guid SessionId,
    Guid GameNightSessionId,
    string SessionCode,
    int PlayOrder);

internal sealed class StartGameNightSessionCommandValidator : AbstractValidator<StartGameNightSessionCommand>
{
    public StartGameNightSessionCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty();
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.GameTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UserId).NotEmpty();
    }
}
```

- [ ] **Step 2.5: Controlla la firma di `CreateSessionCommand`**

```bash
cat apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommand.cs 2>/dev/null || \
  grep -r "record CreateSessionCommand" apps/api/src/Api/BoundedContexts/SessionTracking/
```

Prendi nota della firma attuale. Se ha già `StateTier`, salta il passo 2.6. Se no, procedere.

- [ ] **Step 2.6: Aggiungi `StateTier` a `CreateSessionCommand`**

Nel file trovato al passo 2.5, aggiungi `GameStateTier StateTier = GameStateTier.Minimal` come ultimo parametro opzionale del record `CreateSessionCommand`. Esempio (adattare alla firma attuale):

```csharp
// Aggiungere questo using se non presente:
// using Api.BoundedContexts.SessionTracking.Domain.Enums;

// Nel record, aggiungere il parametro opzionale:
// GameStateTier StateTier = GameStateTier.Minimal
```

- [ ] **Step 2.7: Aggiorna `StartGameNightSessionCommandHandler` per propagare `StateTier`**

In `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandler.cs`, modifica la chiamata a `CreateSessionCommand` aggiungendo `StateTier: command.StateTier`:

```csharp
// Riga attuale (circa riga 55):
var createResult = await _mediator.Send(new CreateSessionCommand(
    command.UserId,
    command.GameId,
    "GameSpecific",
    DateTime.UtcNow,
    null,
    participants), cancellationToken).ConfigureAwait(false);

// Sostituire con:
var createResult = await _mediator.Send(new CreateSessionCommand(
    command.UserId,
    command.GameId,
    "GameSpecific",
    DateTime.UtcNow,
    null,
    participants,
    StateTier: command.StateTier), cancellationToken).ConfigureAwait(false);
```

- [ ] **Step 2.8: Aggiungi l'using per `GameStateTier` nell'handler**

All'inizio di `StartGameNightSessionCommandHandler.cs`, aggiungi se non presente:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Enums;
```

- [ ] **Step 2.9: Esegui i nuovi test**

```bash
cd apps/api/src/Api
dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~StartGameNightSessionStateTierTests" 2>&1 | tail -20
```

Expected: 2/2 PASS.

- [ ] **Step 2.10: Esegui i test pre-esistenti di StartGameNightSession**

```bash
cd apps/api/src/Api
dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~StartGameNightSessionCommandHandlerTests" 2>&1 | tail -20
```

Expected: tutti PASS. Se falliscono per la nuova signature di `CreateSessionCommand`, aggiornare i mock nei test esistenti aggiungendo `StateTier: GameStateTier.Minimal` dove necessario.

- [ ] **Step 2.11: Build completo**

```bash
cd apps/api/src/Api
dotnet build 2>&1 | grep -E "error|Error" | head -20
```

Expected: 0 errori.

- [ ] **Step 2.12: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/GameStateTier.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommand.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandler.cs \
        apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommand.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/GameNight/StartGameNightSessionStateTierTests.cs
git commit -m "feat(session): add GameStateTier to StartGameNightSession command

GAP-001: introduce enum GameStateTier (Minimal/Score/Full) propagato
da StartGameNightSessionCommand a CreateSessionCommand. Default Minimal
mantiene backward compat con sessioni esistenti."
```

---

## Task 3 — GAP-003: Auto Toolbox Warm-up in StartGameNightSession

### File coinvolti
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/GameNight/StartGameNightSessionToolboxWarmupTests.cs`

**Obiettivo**: Se esiste un `ToolboxTemplate` per il gioco selezionato, applicarlo automaticamente come toolbox della sessione (fire-and-forget, non blocca se fallisce).

- [ ] **Step 3.1: Verifica la firma di `GetToolboxTemplatesQuery`**

```bash
grep -r "GetToolboxTemplates" apps/api/src/Api/BoundedContexts/GameToolbox/ | head -5
```

Trova come interrogare i template per un gameId specifico.

- [ ] **Step 3.2: Verifica se esiste `ApplyToolboxTemplateCommand`**

```bash
grep -r "ApplyToolboxTemplateCommand" apps/api/src/Api/BoundedContexts/GameToolbox/ | head -5
```

La struttura esiste in `ToolboxCommands.cs`. Prendi nota della firma.

- [ ] **Step 3.3: Scrivi il test fallente**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/GameNight/StartGameNightSessionToolboxWarmupTests.cs
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class StartGameNightSessionToolboxWarmupTests
{
    private readonly Mock<IGameNightEventRepository> _mockRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IAutoSaveSchedulerService> _mockAutoSaveScheduler;
    private readonly StartGameNightSessionCommandHandler _handler;

    public StartGameNightSessionToolboxWarmupTests()
    {
        _mockRepository = new Mock<IGameNightEventRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockAutoSaveScheduler = new Mock<IAutoSaveSchedulerService>();
        _handler = new StartGameNightSessionCommandHandler(
            _mockRepository.Object,
            _mockMediator.Object,
            _mockUnitOfWork.Object,
            _mockAutoSaveScheduler.Object);
    }

    private static GameNightEvent CreatePublishedEvent(Guid organizerId)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Friday Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        return evt;
    }

    [Fact]
    public async Task Handle_WhenToolboxTemplateExists_AppliesTemplateFireAndForget()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizerId);

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto(organizerId, "org@test.com", "Organizer", null, null, null, null, false, null, null));

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(Guid.NewGuid(), "ABC123"));

        // Template exists for this game
        _mockMediator.Setup(m => m.Send(It.IsAny<GetToolboxTemplatesByGameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolboxTemplateDto> {
                new() { Id = templateId, Name = "TM Template", GameId = gameId }
            });

        _mockMediator.Setup(m => m.Send(It.IsAny<ApplyToolboxTemplateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolboxDto { Id = Guid.NewGuid(), Name = "TM Template" });

        var command = new StartGameNightSessionCommand(gameNight.Id, gameId, "Terraforming Mars", organizerId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert: ApplyToolboxTemplateCommand was dispatched with correct templateId and gameId
        _mockMediator.Verify(
            m => m.Send(
                It.Is<ApplyToolboxTemplateCommand>(c => c.TemplateId == templateId && c.GameId == gameId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoToolboxTemplateExists_DoesNotDispatchApplyTemplate()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizerId);

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto(organizerId, "org@test.com", "Organizer", null, null, null, null, false, null, null));

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(Guid.NewGuid(), "ABC123"));

        // No templates for this game
        _mockMediator.Setup(m => m.Send(It.IsAny<GetToolboxTemplatesByGameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolboxTemplateDto>());

        var command = new StartGameNightSessionCommand(gameNight.Id, gameId, "Dixit", organizerId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert: ApplyToolboxTemplateCommand was NOT dispatched
        _mockMediator.Verify(
            m => m.Send(It.IsAny<ApplyToolboxTemplateCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenToolboxWarmupFails_StillReturnsSuccessResult()
    {
        // Arrange: toolbox apply throws — must not propagate
        var organizerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizerId);

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto(organizerId, "org@test.com", "Organizer", null, null, null, null, false, null, null));

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(Guid.NewGuid(), "ABC123"));

        _mockMediator.Setup(m => m.Send(It.IsAny<GetToolboxTemplatesByGameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ToolboxTemplateDto> {
                new() { Id = Guid.NewGuid(), Name = "Broken Template", GameId = gameId }
            });

        _mockMediator.Setup(m => m.Send(It.IsAny<ApplyToolboxTemplateCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Toolbox error"));

        var command = new StartGameNightSessionCommand(gameNight.Id, gameId, "Terraforming Mars", organizerId);

        // Act — must NOT throw
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }
}
```

- [ ] **Step 3.4: Esegui il test per verificare che fallisca**

```bash
cd apps/api/src/Api
dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~StartGameNightSessionToolboxWarmupTests" \
  --no-build 2>&1 | tail -20
```

Expected: errore di compilazione — `GetToolboxTemplatesByGameQuery` potrebbe non esistere.

- [ ] **Step 3.5: Verifica se `GetToolboxTemplatesByGameQuery` esiste già**

```bash
grep -r "GetToolboxTemplates" apps/api/src/Api/BoundedContexts/GameToolbox/ | head -10
```

Se esiste con filtro gameId, usa quella. Se no, crea la query:

```csharp
// Se non esiste, creare in:
// apps/api/src/Api/BoundedContexts/GameToolbox/Application/Queries/GetToolboxTemplatesByGameQuery.cs
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolbox.Application.Queries;

internal record GetToolboxTemplatesByGameQuery(Guid GameId) : IQuery<List<ToolboxTemplateDto>>;

internal sealed class GetToolboxTemplatesByGameQueryHandler
    : IQueryHandler<GetToolboxTemplatesByGameQuery, List<ToolboxTemplateDto>>
{
    private readonly IToolboxTemplateRepository _repository;

    public GetToolboxTemplatesByGameQueryHandler(IToolboxTemplateRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<ToolboxTemplateDto>> Handle(
        GetToolboxTemplatesByGameQuery query, CancellationToken cancellationToken)
    {
        var templates = await _repository.GetByGameIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);
        return templates.Select(ToolboxMapper.ToDto).ToList();
    }
}
```

Poi verificare se `IToolboxTemplateRepository` ha `GetByGameIdAsync`. Se no, aggiungere il metodo nell'interfaccia e implementarlo.

- [ ] **Step 3.6: Aggiorna `StartGameNightSessionCommandHandler` con toolbox warm-up**

In `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandler.cs`, dopo il blocco `try` principale, aggiungi il warm-up (fire-and-forget):

```csharp
// Aggiungere questi using in cima al file se non presenti:
// using Api.BoundedContexts.GameToolbox.Application.Commands;
// using Api.BoundedContexts.GameToolbox.Application.Queries;
// using Microsoft.Extensions.Logging; (opzionale, per log)

// Nel metodo Handle, DOPO await _unitOfWork.SaveChangesAsync(...) e
// DOPO await _autoSaveScheduler.RegisterAsync(...), aggiungere:

// Toolbox warm-up: fire-and-forget (non blocca se fallisce)
await TryApplyToolboxTemplateAsync(command.GameId, cancellationToken).ConfigureAwait(false);
```

Poi aggiungere il metodo privato alla classe:

```csharp
private async Task TryApplyToolboxTemplateAsync(Guid gameId, CancellationToken cancellationToken)
{
    try
    {
        var templates = await _mediator.Send(
            new GetToolboxTemplatesByGameQuery(gameId), cancellationToken)
            .ConfigureAwait(false);

        var template = templates.FirstOrDefault();
        if (template is null) return;

        await _mediator.Send(
            new ApplyToolboxTemplateCommand(template.Id, gameId), cancellationToken)
            .ConfigureAwait(false);
    }
    catch (Exception)
    {
        // Toolbox warm-up is best-effort: never propagate exceptions.
        // Log in production: _logger?.LogWarning(ex, "Toolbox warm-up failed for game {GameId}", gameId);
    }
}
```

Nota: `ApplyToolboxTemplateCommand` è in `Api.BoundedContexts.GameToolbox.Application.Commands`. Controlla la firma esatta nel file `ToolboxCommands.cs` (ha `TemplateId` e `GameId`).

- [ ] **Step 3.7: Esegui i nuovi test**

```bash
cd apps/api/src/Api
dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~StartGameNightSessionToolboxWarmupTests" 2>&1 | tail -20
```

Expected: 3/3 PASS.

- [ ] **Step 3.8: Esegui tutti i test di StartGameNightSession**

```bash
cd apps/api/src/Api
dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~StartGameNightSession" 2>&1 | tail -20
```

Expected: tutti PASS.

- [ ] **Step 3.9: Build completo**

```bash
cd apps/api/src/Api
dotnet build 2>&1 | grep -E "error|Error" | head -20
```

Expected: 0 errori.

- [ ] **Step 3.10: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/GameNight/StartGameNightSessionToolboxWarmupTests.cs
# Aggiungere eventuali file nuovi creati al passo 3.5
git commit -m "feat(game-night): auto-apply toolbox template on session start

GAP-003: quando si avvia una sessione game-night, il handler cerca
automaticamente un ToolboxTemplate per il gioco e lo applica come
toolbox della sessione. Fire-and-forget: non propaga eccezioni."
```

---

## Task 4 — GAP-006: Dispute Diary Entry Registration

### File coinvolti
- Modify: `apps/web/src/components/game-night/LiveSessionView.tsx`
- Create: `apps/web/src/components/game-night/__tests__/DisputeDiaryEntry.test.tsx`

**Obiettivo**: Dopo una risposta AI nel pannello Regole/Arbitro, mostrare un pulsante "Registra disputa" che crea un entry `dispute_resolved` nel diary con la risposta AI come payload.

**Pattern esistente**: Il pannello Regole è uno `Sheet` in `LiveSessionView.tsx`. L'hook `useAgentChatStream` gestisce le risposte. Il diary usa `DiaryEntryType.dispute_resolved` (già definito in types.ts).

- [ ] **Step 4.1: Scrivi il test fallente**

```typescript
// apps/web/src/components/game-night/__tests__/DisputeDiaryEntry.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock dei moduli pesanti
vi.mock('@/lib/domain-hooks/useSessionSync', () => ({
  useSessionSync: () => ({ isConnected: true }),
}));
vi.mock('@/hooks/useAgentChatStream', () => ({
  useAgentChatStream: () => ({
    state: { isStreaming: false, currentAnswer: 'Luigi non può giocare la carta X in fase di produzione (Regola 4.2, pag. 12).' },
    sendMessage: vi.fn(),
  }),
}));
vi.mock('@/hooks/queries/useGameAgents', () => ({
  useGameAgents: () => ({ data: [{ id: 'agent-1' }] }),
}));
vi.mock('@tanstack/react-query', () => ({
  useQuery: () => ({ data: null }),
}));
vi.mock('@/lib/api', () => ({
  api: { liveSessions: { getResumeContext: vi.fn().mockResolvedValue(null) } },
}));

// Mock del createDisputeDiaryEntry — questo è il hook/funzione da creare
const mockCreateDisputeEntry = vi.fn().mockResolvedValue(undefined);
vi.mock('@/lib/domain-hooks/useDisputeDiary', () => ({
  useDisputeDiary: () => ({ createEntry: mockCreateDisputeEntry }),
}));

import { QuickActions } from '../QuickActions';

describe('Dispute diary entry — QuickActions arbiter flow', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call onAskArbiter when Arbiter button is clicked', () => {
    const onAskArbiter = vi.fn();
    render(
      <QuickActions
        isPaused={false}
        onOpenRules={vi.fn()}
        onAskArbiter={onAskArbiter}
        onTogglePause={vi.fn()}
        onOpenScores={vi.fn()}
      />
    );
    fireEvent.click(screen.getByTestId('quick-action-arbiter'));
    expect(onAskArbiter).toHaveBeenCalledTimes(1);
  });
});
```

**Nota**: Il test principale per il flusso completo va nel componente `SessionChatWidget` o in un test di integrazione. Qui testiamo il trigger del pulsante. La creazione della diary entry avviene tramite hook `useDisputeDiary`.

- [ ] **Step 4.2: Crea `useDisputeDiary` hook**

Crea `apps/web/src/lib/domain-hooks/useDisputeDiary.ts`:

```typescript
import { useCallback } from 'react';

import { api } from '@/lib/api';

interface DisputeEntry {
  sessionId: string;
  question: string;
  ruling: string;
  /** ID del chunk RAG citato, se disponibile */
  sourceChunkId?: string;
}

interface UseDisputeDiary {
  createEntry: (entry: DisputeEntry) => Promise<void>;
}

/**
 * Hook per registrare il risultato di una disputa come diary entry.
 * Wrappa la chiamata all'API session notes con type=dispute_resolved.
 */
export function useDisputeDiary(): UseDisputeDiary {
  const createEntry = useCallback(async (entry: DisputeEntry): Promise<void> => {
    await api.sessions.addNote({
      sessionId: entry.sessionId,
      content: `**Disputa**: ${entry.question}\n\n**Decisione Arbitro**: ${entry.ruling}`,
      noteType: 'dispute_resolved',
      metadata: {
        question: entry.question,
        ruling: entry.ruling,
        sourceChunkId: entry.sourceChunkId ?? null,
      },
    });
  }, []);

  return { createEntry };
}
```

**Nota**: se `api.sessions.addNote` non esiste con questa firma, usa il client appropriato trovato in `apps/web/src/lib/api/clients/`. Cerca con:
```bash
grep -r "addNote\|saveNote\|createNote" apps/web/src/lib/api/clients/ | head -5
```

- [ ] **Step 4.3: Aggiungi il pulsante "Registra disputa" nel pannello Regole di `LiveSessionView`**

In `apps/web/src/components/game-night/LiveSessionView.tsx`, nella sezione del Rules Sheet (cerca `SheetTitle>Regole del gioco`), aggiungi after la chat widget:

```typescript
// Aggiungere in cima al file:
import { useDisputeDiary } from '@/lib/domain-hooks/useDisputeDiary';

// Nel componente, aggiungere hook (dopo gli altri hook, prima degli early returns):
const { createEntry: createDisputeEntry } = useDisputeDiary();
const [disputeRegistered, setDisputeRegistered] = useState(false);

// Handler per registrazione disputa:
const handleRegisterDispute = useCallback(async () => {
  if (!rulesAgentState.currentAnswer || !sessionId) return;
  await createDisputeEntry({
    sessionId,
    question: rulesSentMessages.findLast(m => m.role === 'user')?.content ?? '',
    ruling: rulesAgentState.currentAnswer,
  });
  setDisputeRegistered(true);
  // Reset dopo 3s
  setTimeout(() => setDisputeRegistered(false), 3000);
}, [rulesAgentState.currentAnswer, rulesSentMessages, sessionId, createDisputeEntry]);
```

Poi dentro lo `SheetContent` del Rules Sheet, dopo `SessionChatWidget`, aggiungi:

```tsx
{/* Registra disputa — visibile solo se c'è una risposta AI */}
{rulesAgentState.currentAnswer && !isRulesStreaming && (
  <div className="px-1 pt-2 pb-1">
    <button
      type="button"
      data-testid="register-dispute-btn"
      onClick={handleRegisterDispute}
      disabled={disputeRegistered}
      className={cn(
        'w-full text-xs rounded-lg px-3 py-2 border transition-colors',
        disputeRegistered
          ? 'border-emerald-300 bg-emerald-50 text-emerald-700'
          : 'border-amber-300 bg-amber-50 text-amber-700 hover:bg-amber-100'
      )}
    >
      {disputeRegistered ? '✓ Disputa registrata nel diary' : 'Registra disputa nel diary'}
    </button>
  </div>
)}
```

- [ ] **Step 4.4: Esegui il test**

```bash
cd apps/web
pnpm test DisputeDiaryEntry --run
```

Expected: PASS.

- [ ] **Step 4.5: Verifica typecheck**

```bash
cd apps/web
pnpm typecheck 2>&1 | grep -i "error" | head -10
```

- [ ] **Step 4.6: Commit**

```bash
git add apps/web/src/lib/domain-hooks/useDisputeDiary.ts \
        apps/web/src/components/game-night/LiveSessionView.tsx \
        apps/web/src/components/game-night/__tests__/DisputeDiaryEntry.test.tsx
git commit -m "feat(game-night): add dispute diary entry registration

GAP-006: dopo una risposta AI nel pannello Arbitro, un pulsante
'Registra disputa nel diary' crea un entry dispute_resolved con
la domanda e la decisione dell'arbitro AI."
```

---

## Task 5 — GAP-005: Photo Button in LiveSession

### File coinvolti
- Modify: `apps/web/src/components/game-night/QuickActions.tsx`
- Modify: `apps/web/src/components/game-night/__tests__/QuickActions.test.tsx`
- Modify: `apps/web/src/components/game-night/LiveSessionView.tsx`
- Create: `apps/web/src/components/game-night/__tests__/PhotoButton.test.tsx`

**Obiettivo**: Aggiungere un pulsante Foto a `QuickActions` che apre un file picker. Il file selezionato viene caricato tramite endpoint sessione esistente. L'upload crea automaticamente una diary entry `photo`.

**Infrastruttura esistente**: `SessionMedia` entity + `UploadSessionMediaCommand` (backend). Cerca l'endpoint frontend in:
```bash
grep -r "uploadMedia\|upload.*media\|media.*upload" apps/web/src/lib/api/ | head -5
```

- [ ] **Step 5.1: Scrivi il test del pulsante Foto**

```typescript
// apps/web/src/components/game-night/__tests__/PhotoButton.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { QuickActions } from '../QuickActions';

describe('QuickActions — Foto button', () => {
  const defaultProps = {
    isPaused: false,
    onOpenRules: vi.fn(),
    onAskArbiter: vi.fn(),
    onTogglePause: vi.fn(),
    onOpenScores: vi.fn(),
    onOpenPhoto: vi.fn(),
  };

  it('should render the Foto button', () => {
    render(<QuickActions {...defaultProps} />);
    expect(screen.getByTestId('quick-action-photo')).toBeInTheDocument();
  });

  it('should display "Foto" label', () => {
    render(<QuickActions {...defaultProps} />);
    expect(screen.getByText('Foto')).toBeInTheDocument();
  });

  it('should call onOpenPhoto when Foto button is clicked', () => {
    render(<QuickActions {...defaultProps} />);
    fireEvent.click(screen.getByTestId('quick-action-photo'));
    expect(defaultProps.onOpenPhoto).toHaveBeenCalledTimes(1);
  });

  it('should disable Foto button when loading', () => {
    render(<QuickActions {...defaultProps} isLoading={true} />);
    expect(screen.getByTestId('quick-action-photo')).toBeDisabled();
  });
});
```

- [ ] **Step 5.2: Esegui il test per verificare che fallisca**

```bash
cd apps/web
pnpm test PhotoButton --run
```

Expected: FAIL — `onOpenPhoto` non è prop di `QuickActions`, `quick-action-photo` non esiste.

- [ ] **Step 5.3: Aggiorna `QuickActions` con prop `onOpenPhoto` e pulsante Foto**

In `apps/web/src/components/game-night/QuickActions.tsx`:

```typescript
'use client';

import { BookOpen, Scale, Pause, Play, BarChart3, Camera } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface QuickActionsProps {
  isPaused: boolean;
  isLoading?: boolean;
  onOpenRules: () => void;
  onAskArbiter: () => void;
  onTogglePause: () => void;
  onOpenScores: () => void;
  /** Opzionale: apre photo picker. Se undefined, il pulsante non è renderizzato. */
  onOpenPhoto?: () => void;
  className?: string;
}

export function QuickActions({
  isPaused,
  isLoading = false,
  onOpenRules,
  onAskArbiter,
  onTogglePause,
  onOpenScores,
  onOpenPhoto,
  className,
}: QuickActionsProps) {
  const actions = [
    {
      id: 'rules',
      label: 'Regole',
      icon: BookOpen,
      onClick: onOpenRules,
      variant: 'outline' as const,
    },
    {
      id: 'arbiter',
      label: 'Arbitro',
      icon: Scale,
      onClick: onAskArbiter,
      variant: 'outline' as const,
    },
    {
      id: 'pause',
      label: isPaused ? 'Riprendi' : 'Pausa',
      icon: isPaused ? Play : Pause,
      onClick: onTogglePause,
      variant: 'outline' as const,
    },
    {
      id: 'scores',
      label: 'Punteggi',
      icon: BarChart3,
      onClick: onOpenScores,
      variant: 'outline' as const,
    },
    ...(onOpenPhoto
      ? [{
          id: 'photo',
          label: 'Foto',
          icon: Camera,
          onClick: onOpenPhoto,
          variant: 'outline' as const,
        }]
      : []),
  ];

  return (
    <div
      className={cn('grid grid-cols-2 gap-2 sm:grid-cols-4', onOpenPhoto && 'sm:grid-cols-5', className)}
      data-testid="quick-actions"
    >
      {actions.map(action => {
        const Icon = action.icon;
        return (
          <Button
            key={action.id}
            variant={action.variant}
            disabled={isLoading}
            onClick={action.onClick}
            className={cn(
              'h-11 gap-2 text-sm font-semibold',
              'border-2 border-slate-200 dark:border-slate-700',
              'bg-white dark:bg-slate-800',
              'hover:border-amber-600/50 hover:bg-amber-50 dark:hover:bg-amber-950/20',
              'active:scale-95 transition-all'
            )}
            data-testid={`quick-action-${action.id}`}
          >
            <Icon className="h-4 w-4 flex-shrink-0" aria-hidden="true" />
            <span className="truncate">{action.label}</span>
          </Button>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 5.4: Wire il photo button in `LiveSessionView`**

In `apps/web/src/components/game-night/LiveSessionView.tsx`:

1. Aggiungi `useRef` per l'input file (già in import)
2. Aggiungi un `<input type="file" accept="image/*">` hidden
3. Gestisci l'upload tramite client API esistente

```typescript
// Aggiungere nell'import se non presente:
// import { api } from '@/lib/api';

// Nel componente, aggiungere dopo gli altri state:
const photoInputRef = useRef<HTMLInputElement>(null);

// Handler per aprire il file picker:
const handleOpenPhoto = useCallback(() => {
  photoInputRef.current?.click();
}, []);

// Handler per l'upload:
const handlePhotoSelected = useCallback(
  async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file || !activeSession) return;

    // Reset input per permettere di selezionare lo stesso file
    event.target.value = '';

    try {
      // Cerca partecipantId dell'utente corrente nello store
      const participantId = activeSession.players[0]?.id ?? '';
      await api.sessions.uploadMedia({
        sessionId,
        participantId,
        file,
        mediaType: 'Photo',
        caption: `Foto - ${new Date().toLocaleTimeString('it-IT')}`,
      });
    } catch {
      // Upload failed silently — non blocca l'uso
    }
  },
  [activeSession, sessionId]
);
```

Poi aggiungi l'input hidden e il prop `onOpenPhoto` alle `QuickActions` in entrambi i layout (mobile e desktop):

```tsx
{/* Hidden file input per foto */}
<input
  ref={photoInputRef}
  type="file"
  accept="image/*,image/heic"
  capture="environment"
  className="sr-only"
  onChange={handlePhotoSelected}
  aria-hidden="true"
/>
```

E nelle `QuickActions`:
```tsx
<QuickActions
  isPaused={isPaused}
  isLoading={isLoading}
  onOpenRules={() => setRulesOpen(true)}
  onAskArbiter={() => setRulesOpen(true)}
  onTogglePause={handleTogglePause}
  onOpenScores={() => setScoresOpen(true)}
  onOpenPhoto={handleOpenPhoto}
/>
```

**Nota**: verifica che `api.sessions.uploadMedia` esista. Se la firma è diversa, adattare. Usa:
```bash
grep -r "uploadMedia\|upload.*Media\|UploadMedia" apps/web/src/lib/api/ | head -10
```

- [ ] **Step 5.5: Aggiorna i test pre-esistenti di QuickActions**

In `apps/web/src/components/game-night/__tests__/QuickActions.test.tsx`, aggiorna il test `'should render all four action buttons'` e `'should disable buttons when loading'` per riflettere che il pulsante Foto è opzionale:

```typescript
// Il test 'render all four action buttons' rimane valido — senza onOpenPhoto
// Aggiungere il caso con foto:
it('should render five buttons when onOpenPhoto is provided', () => {
  render(<QuickActions {...defaultProps} onOpenPhoto={vi.fn()} />);
  expect(screen.getByTestId('quick-action-photo')).toBeInTheDocument();
  expect(screen.getAllByRole('button')).toHaveLength(5);
});
```

- [ ] **Step 5.6: Esegui tutti i test coinvolti**

```bash
cd apps/web
pnpm test PhotoButton QuickActions --run
```

Expected: tutti PASS.

- [ ] **Step 5.7: Typecheck**

```bash
cd apps/web
pnpm typecheck 2>&1 | grep -i "error" | head -10
```

Expected: 0 nuovi errori.

- [ ] **Step 5.8: Commit**

```bash
git add apps/web/src/components/game-night/QuickActions.tsx \
        apps/web/src/components/game-night/__tests__/QuickActions.test.tsx \
        apps/web/src/components/game-night/__tests__/PhotoButton.test.tsx \
        apps/web/src/components/game-night/LiveSessionView.tsx
git commit -m "feat(game-night): add photo capture button to LiveSession QuickActions

GAP-005: aggiunge pulsante Foto in QuickActions (opzionale via prop
onOpenPhoto). In LiveSessionView usa input file hidden con capture=environment
per accedere alla fotocamera mobile. Upload asincrono via api.sessions.uploadMedia."
```

---

## Verifica Finale

- [ ] **Build backend completo**

```bash
cd apps/api/src/Api
dotnet build 2>&1 | grep -E "^.*error" | head -20
```

- [ ] **Test backend rilevanti**

```bash
cd apps/api/src/Api
dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~GameNight OR FullyQualifiedName~StartGameNight" 2>&1 | tail -20
```

- [ ] **Build frontend**

```bash
cd apps/web
pnpm build 2>&1 | grep -E "error|Error" | head -20
```

- [ ] **Test frontend completi**

```bash
cd apps/web
pnpm test --run 2>&1 | tail -30
```

- [ ] **Lint**

```bash
cd apps/web
pnpm lint 2>&1 | grep -E "error" | head -20
```

---

## Note per Implementazione

### Pattern test backend (Moq)
```csharp
_mockMediator.Setup(m => m.Send(It.IsAny<TCommand>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(expectedResult);
// Per verificare chiamata:
_mockMediator.Verify(m => m.Send(It.Is<TCommand>(c => c.Property == value), It.IsAny<CancellationToken>()), Times.Once);
```

### Pattern test frontend (Vitest + RTL)
```typescript
render(<Component prop={value} />);
expect(screen.getByTestId('data-testid-value')).toBeInTheDocument();
fireEvent.click(screen.getByRole('button', { name: /label/i }));
await waitFor(() => expect(mockFn).toHaveBeenCalledWith(expectedArg));
```

### Accesso API nel frontend
Tutti i client API sono in `apps/web/src/lib/api/clients/`. Il proxy centrale è `apps/web/src/lib/api/index.ts`. Seguire i pattern esistenti prima di creare nuovi client.
