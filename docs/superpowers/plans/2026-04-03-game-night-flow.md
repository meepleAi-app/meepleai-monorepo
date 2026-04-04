# Game Night Flow — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Completare il flusso "Game Night" end-to-end: creazione serata con guest, AI agent contestuale al gioco in italiano, score tracking multi-device via SSE, toolkit offline, e finalizzazione con storico.

**Architecture:**
- Backend: Estensione di `ChatWithSessionAgentCommand` con `GameSessionContext` (BC KnowledgeBase). Il resto delle API SessionTracking esiste già.
- Frontend: Hook orchestratore `useGameNightOrchestrator` coordina `GameNightStore` ↔ `SessionStore` con selettori granulari (no re-render inutili). Layer offline via IndexedDB (`idb` library) con sync queue. Streaming AI via hook `useSessionAgentChat` (fetch POST con `Accept: text/event-stream`).
- Multi-device: SSE v2 esistente (`/stream/v2`). `useSessionSSE` viene esteso con callback opzionale `onEvent` per processare `score_update` nello store senza duplicare connessioni SSE.

**Tech Stack:** .NET 9, MediatR CQRS, Next.js 16, Zustand + immer, `idb` (IndexedDB), shadcn/ui, Tailwind 4, Vitest, xUnit.

**Presupposti:**
- Endpoint REST SessionTracking esistono: `POST /api/v1/game-sessions`, `PUT /game-sessions/{id}/scores`, `POST /dice`, `POST /notes`, `POST /finalize` ✅
- SSE v2 endpoint esiste: `GET /game-sessions/{id}/stream/v2` ✅
- Hook `useSessionSSE` esiste ✅
- `GameNight` e `Session` sono entità separate nel backend; questo piano le coordina solo lato frontend ✅
- `ChatWithSessionAgentCommand` ha solo `AgentSessionId` — manca il game context ❌

**Confini:**
- NON tocca `2026-03-31-game-session-flow.md` (GamePhaseTemplate, TurnAdvancePolicy — scope separato)
- NON tocca `2026-04-03-toolkit-in-session-logging.md` (ToolAction logging — già in progresso)
- NON include admin KB upload flow (già in `2026-04-01-kb-management.md`)

---

## File Map

### Nuovi file
```
# Backend
apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/GameSessionContext.cs
tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/ChatWithSessionAgentCommandTests.cs

# Frontend — hooks
apps/web/src/lib/domain-hooks/useGameNightOrchestrator.ts
apps/web/src/lib/domain-hooks/useSessionAgentChat.ts
apps/web/src/lib/domain-hooks/useOfflineToolkit.ts
apps/web/src/lib/domain-hooks/useSessionScoreSync.ts
apps/web/src/lib/domain-hooks/__tests__/useGameNightOrchestrator.test.ts
apps/web/src/lib/domain-hooks/__tests__/useOfflineToolkit.test.ts

# Frontend — offline DB
apps/web/src/lib/offline/toolkitDb.ts
apps/web/src/lib/offline/__tests__/toolkitDb.test.ts

# Frontend — API client
apps/web/src/lib/api/clients/sessionAgentClient.ts

# Frontend — store types extension
apps/web/src/stores/session/types.ts (modifica)
apps/web/src/stores/session/store.ts (modifica)
apps/web/src/stores/game-night/store.ts (modifica)
```

### File modificati
```
# Backend
apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommand.cs
apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs
apps/api/src/Api/Routing/KnowledgeBase/AgentSessionEndpoints.cs (o equivalente)

# Frontend
apps/web/src/components/game-night/RulesExplainer.tsx
apps/web/src/components/game-night/GameNightWizard.tsx (guest players)
apps/web/src/components/game-night/PlayerSetup.tsx (guest players)
apps/web/src/components/session/LiveSessionLayout.tsx (score sync)
```

---

## PHASE 1 — Backend: AI Agent Game Context

### Task 1: GameSessionContext record + Estensione ChatWithSessionAgentCommand

**Problema:** L'agente riceve solo `AgentSessionId` e non sa quale gioco si sta giocando, chi sono i giocatori, o in che lingua rispondere.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/GameSessionContext.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs`
- Create: `tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/ChatWithSessionAgentContextTests.cs`

- [ ] **Step 1: Scrivi il test unitario per il system prompt arricchito**

```csharp
// tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/ChatWithSessionAgentContextTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

public class ChatWithSessionAgentContextTests
{
    [Fact]
    public void GameSessionContext_BuildsSystemPromptEnrichment_WithAllPlayers()
    {
        var ctx = new GameSessionContext(
            GameId: Guid.NewGuid(),
            GameTitle: "Catan",
            Players: ["Marco", "Luca", "Sara (ospite)"],
            CurrentTurn: 3,
            ResponseLanguage: "it"
        );

        var prompt = ctx.ToSystemPromptEnrichment();

        Assert.Contains("Catan", prompt);
        Assert.Contains("Marco", prompt);
        Assert.Contains("Sara (ospite)", prompt);
        Assert.Contains("turno 3", prompt);
        Assert.Contains("italiano", prompt);
    }

    [Fact]
    public void GameSessionContext_WithMissingGameTitle_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameSessionContext(Guid.NewGuid(), "", ["Player1"], 1, "it"));
    }

    [Fact]
    public void GameSessionContext_DefaultLanguage_IsItalian()
    {
        var ctx = new GameSessionContext(Guid.NewGuid(), "Dixit", ["A", "B"], 1);
        Assert.Equal("it", ctx.ResponseLanguage);
    }
}
```

- [ ] **Step 2: Esegui il test — verifica che fallisca**

```bash
cd tests/Api.Tests
dotnet test --filter "ChatWithSessionAgentContextTests" -v
# Expected: FAIL — GameSessionContext not found
```

- [ ] **Step 3: Crea il record GameSessionContext**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/GameSessionContext.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Contesto della sessione di gioco passato all'agente AI per rispondere
/// in modo contestuale al gioco in corso, ai giocatori e alla lingua.
/// </summary>
internal record GameSessionContext(
    Guid GameId,
    string GameTitle,
    IReadOnlyList<string> Players,
    int CurrentTurn,
    string ResponseLanguage = "it"
)
{
    public GameSessionContext
    {
        if (string.IsNullOrWhiteSpace(GameTitle))
            throw new ArgumentException("GameTitle è obbligatorio.", nameof(GameTitle));
        if (Players.Count == 0)
            throw new ArgumentException("Almeno un giocatore è richiesto.", nameof(Players));
    }

    /// <summary>
    /// Genera il prefisso di sistema da anteporre al system prompt dell'agente.
    /// Istruisce l'agente a rispondere in italiano e contestualizzare le risposte.
    /// </summary>
    public string ToSystemPromptEnrichment()
    {
        var playerList = string.Join(", ", Players);
        return $"""
            CONTESTO PARTITA CORRENTE:
            - Gioco: {GameTitle}
            - Giocatori: {playerList}
            - Turno corrente: {CurrentTurn}
            - Lingua di risposta: {(ResponseLanguage == "it" ? "italiano" : ResponseLanguage)}

            ISTRUZIONI:
            - Rispondi SEMPRE in italiano, anche se il manuale del gioco è in inglese.
            - Contestualizza le risposte al gioco "{GameTitle}" in corso.
            - Quando citi regole, riferisciti ai giocatori per nome ({playerList}).
            - Mantieni le risposte concise (max 150 parole) e leggibili ad alta voce.
            - Se non hai il manuale di {GameTitle}, dillo chiaramente e offri di aiutare
              con regole generali o suggerisci di caricare il PDF del manuale.
            """;
    }
}
```

- [ ] **Step 4: Aggiorna ChatWithSessionAgentCommand aggiungendo il context**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommand.cs
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Streaming command per chat con agente sessione.
/// Restituisce SSE events: StateUpdate → Token(s) → Complete.
/// Issue #3184 (AGT-010), Issue #4386.
/// </summary>
internal record ChatWithSessionAgentCommand(
    Guid AgentSessionId,
    string UserQuestion,
    Guid UserId,
    GameSessionContext? GameContext = null,   // NUOVO: contesto partita
    Guid? ChatThreadId = null
) : IStreamingQuery<RagStreamingEvent>;
```

- [ ] **Step 5: Aggiorna l'handler per iniettare il context nel system prompt**

Nel file `ChatWithSessionAgentCommandHandler.cs`, trova dove viene costruito il `systemPrompt` e anteponi `GameContext?.ToSystemPromptEnrichment()`:

```csharp
// Trova la riga dove si costruisce systemPrompt (cerca "var systemPrompt")
// e sostituiscila con:

var contextPrefix = command.GameContext?.ToSystemPromptEnrichment() ?? string.Empty;
var systemPrompt = string.IsNullOrEmpty(contextPrefix)
    ? agentDefinition.SystemPrompt
    : $"{contextPrefix}\n\n{agentDefinition.SystemPrompt}";
```

- [ ] **Step 6: Aggiorna l'endpoint che espone il command**

Trova il file di routing che mappa `ChatWithSessionAgentCommand` (cerca `AgentSessionId` in `apps/api/src/Api/Routing/`). Aggiungi `GameContext` al DTO request:

```csharp
// Nel file AgentSessionEndpoints.cs (o equivalente), aggiorna il request DTO:
private sealed record ChatWithSessionAgentRequest(
    string UserQuestion,
    Guid? ChatThreadId = null,
    GameSessionContextRequest? GameContext = null  // NUOVO
);

private sealed record GameSessionContextRequest(
    Guid GameId,
    string GameTitle,
    IReadOnlyList<string> Players,
    int CurrentTurn,
    string ResponseLanguage = "it"
);

// Nel handler dell'endpoint, mappa il context:
var gameContext = request.GameContext is null ? null : new GameSessionContext(
    request.GameContext.GameId,
    request.GameContext.GameTitle,
    request.GameContext.Players,
    request.GameContext.CurrentTurn,
    request.GameContext.ResponseLanguage
);

var command = new ChatWithSessionAgentCommand(
    AgentSessionId: agentSessionId,
    UserQuestion: request.UserQuestion,
    UserId: userId,
    GameContext: gameContext,
    ChatThreadId: request.ChatThreadId
);
```

- [ ] **Step 7: Esegui i test — verifica che passino**

```bash
cd tests/Api.Tests
dotnet test --filter "ChatWithSessionAgentContextTests" -v
# Expected: 3 test PASS

dotnet test --filter "BoundedContext=KnowledgeBase" -v
# Expected: tutti PASS (no regressioni)
```

- [ ] **Step 8: Build backend — verifica zero errori**

```bash
cd apps/api/src/Api
dotnet build --no-incremental
# Expected: Build succeeded, 0 Error(s)
```

- [ ] **Step 8b: Aggiorna il validator FluentValidation per `GameContext` opzionale**

Cerca `ChatWithSessionAgentCommandValidator.cs` in `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/`. Aggiungi nel costruttore:

```csharp
// Aggiunge dopo le RuleFor esistenti su AgentSessionId e UserQuestion:
When(x => x.GameContext is not null, () => {
    RuleFor(x => x.GameContext!.GameTitle)
        .NotEmpty()
        .WithMessage("GameTitle è obbligatorio quando GameContext è fornito.");
    RuleFor(x => x.GameContext!.Players)
        .NotEmpty()
        .WithMessage("Almeno un giocatore è richiesto quando GameContext è fornito.");
});
```

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/GameSessionContext.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommand.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandValidator.cs
git add tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/ChatWithSessionAgentContextTests.cs
git commit -m "feat(kb): add GameSessionContext to ChatWithSessionAgentCommand for contextual Italian responses"
```

---

## PHASE 2 — Frontend: Session Store Enrichment

### Task 2: Estensione types/store per partecipanti e gioco

**Problema:** `SessionState` ha solo `scores: PlayerScore[]` con `playerId/score`. Mancano: nome giocatore, tipo (account/guest), gameTitle, partecipanti completi.

**Files:**
- Modify: `apps/web/src/stores/session/types.ts`
- Modify: `apps/web/src/stores/session/store.ts`
- Create: `apps/web/src/stores/session/__tests__/store.test.ts` (se non esiste)

- [ ] **Step 1: Scrivi il test per il nuovo store shape**

```typescript
// apps/web/src/stores/session/__tests__/store.test.ts
import { act } from '@testing-library/react';
import { useSessionStore } from '../store';

describe('useSessionStore — partecipanti e gioco', () => {
  beforeEach(() => useSessionStore.getState().reset());

  it('startSession persiste gameTitle e participants', () => {
    act(() => {
      useSessionStore.getState().startSession({
        sessionId: 'sess-1',
        gameId: 'game-1',
        gameTitle: 'Catan',
        participants: [
          { id: 'p1', displayName: 'Marco', isGuest: false },
          { id: 'p2', displayName: 'Luca (ospite)', isGuest: true },
        ],
      });
    });

    const s = useSessionStore.getState();
    expect(s.gameTitle).toBe('Catan');
    expect(s.participants).toHaveLength(2);
    expect(s.participants[1].isGuest).toBe(true);
    expect(s.status).toBe('live');
  });

  it('updateScore aggiorna score di un partecipante per nome', () => {
    act(() => {
      useSessionStore.getState().startSession({
        sessionId: 's',
        gameId: 'g',
        gameTitle: 'Dixit',
        participants: [{ id: 'p1', displayName: 'Sara', isGuest: false }],
      });
      useSessionStore.getState().updateScore('p1', 7);
    });

    const score = useSessionStore.getState().scores.find(s => s.playerId === 'p1');
    expect(score?.score).toBe(7);
  });

  it('reset pulisce tutti i campi', () => {
    act(() => {
      useSessionStore.getState().startSession({
        sessionId: 's',
        gameId: 'g',
        gameTitle: 'G',
        participants: [],
      });
      useSessionStore.getState().reset();
    });

    const s = useSessionStore.getState();
    expect(s.sessionId).toBeNull();
    expect(s.gameTitle).toBeNull();
    expect(s.participants).toHaveLength(0);
  });
});
```

- [ ] **Step 2: Esegui il test — verifica che fallisca**

```bash
cd apps/web
pnpm test stores/session/__tests__/store.test.ts
# Expected: FAIL — gameTitle not in store
```

- [ ] **Step 3: Aggiorna types.ts**

```typescript
// apps/web/src/stores/session/types.ts
export type SessionStatus = 'idle' | 'live' | 'paused' | 'ended';

export type ActivityEventType =
  | 'dice_roll'
  | 'ai_tip'
  | 'score_update'
  | 'photo'
  | 'note'
  | 'audio_note'
  | 'turn_change'
  | 'pause_resume'
  | 'session_start'
  | 'card_draw';

export interface ActivityEvent {
  id: string;
  type: ActivityEventType;
  playerId?: string;
  data: Record<string, unknown>;
  timestamp: string;
}

export interface PlayerScore {
  playerId: string;
  score: number;
}

// NUOVO
export interface SessionParticipant {
  id: string;
  displayName: string;
  isGuest: boolean;
  userId?: string;       // undefined per guest
  avatarUrl?: string;
}

export interface StartSessionPayload {
  sessionId: string;
  gameId: string;
  gameTitle: string;
  participants: SessionParticipant[];
}
```

- [ ] **Step 4: Aggiorna store.ts**

```typescript
// apps/web/src/stores/session/store.ts
import { create } from 'zustand';
import { devtools, persist } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';
import type {
  SessionStatus, ActivityEvent, PlayerScore,
  SessionParticipant, StartSessionPayload,
} from './types';

interface SessionState {
  status: SessionStatus;
  sessionId: string | null;
  gameId: string | null;
  gameTitle: string | null;          // NUOVO
  participants: SessionParticipant[]; // NUOVO
  isPaused: boolean;
  currentTurn: number;
  events: ActivityEvent[];
  scores: PlayerScore[];
  timerStartedAt: string | null;

  startSession: (payload: StartSessionPayload) => void;
  endSession: () => void;
  togglePause: () => void;
  addEvent: (event: ActivityEvent) => void;
  updateScore: (playerId: string, score: number) => void;
  nextTurn: () => void;
  reset: () => void;
}

const initialState = {
  status: 'idle' as SessionStatus,
  sessionId: null as string | null,
  gameId: null as string | null,
  gameTitle: null as string | null,
  participants: [] as SessionParticipant[],
  isPaused: false,
  currentTurn: 1,
  events: [] as ActivityEvent[],
  scores: [] as PlayerScore[],
  timerStartedAt: null as string | null,
};

export const useSessionStore = create<SessionState>()(
  devtools(
    persist(
      immer(set => ({
        ...initialState,

        startSession: (payload) =>
          set(s => {
            s.status = 'live';
            s.sessionId = payload.sessionId;
            s.gameId = payload.gameId;
            s.gameTitle = payload.gameTitle;
            s.participants = payload.participants;
            s.timerStartedAt = new Date().toISOString();
          }),

        endSession: () =>
          set(s => { s.status = 'ended'; }),

        togglePause: () =>
          set(s => {
            s.isPaused = !s.isPaused;
            s.status = s.isPaused ? 'paused' : 'live';
          }),

        addEvent: event =>
          set(s => { s.events.push(event); }),

        updateScore: (playerId, score) =>
          set(s => {
            const existing = s.scores.find(sc => sc.playerId === playerId);
            if (existing) {
              existing.score = score;
            } else {
              s.scores.push({ playerId, score });
            }
          }),

        nextTurn: () =>
          set(s => { s.currentTurn += 1; }),

        reset: () => set(() => ({ ...initialState })),
      })),
      {
        name: 'meepleai-session',
        // Persisti solo i dati critici per recovery dopo crash
        partialize: (s) => ({
          sessionId: s.sessionId,
          gameId: s.gameId,
          gameTitle: s.gameTitle,
          participants: s.participants,
          status: s.status,
          currentTurn: s.currentTurn,
          scores: s.scores,
        }),
      }
    ),
    { name: 'session-store' }
  )
);

// Selectors
export const selectStatus = (s: SessionState) => s.status;
export const selectEvents = (s: SessionState) => s.events;
export const selectScores = (s: SessionState) => s.scores;
export const selectCurrentTurn = (s: SessionState) => s.currentTurn;
export const selectIsPaused = (s: SessionState) => s.isPaused;
export const selectIsLive = (s: SessionState) => s.status === 'live' || s.status === 'paused';
export const selectParticipants = (s: SessionState) => s.participants;  // NUOVO
export const selectGameTitle = (s: SessionState) => s.gameTitle;        // NUOVO
```

- [ ] **Step 5: Esegui i test — verifica che passino**

```bash
cd apps/web
pnpm test stores/session/__tests__/store.test.ts
# Expected: 3 test PASS
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/stores/session/types.ts apps/web/src/stores/session/store.ts
git add apps/web/src/stores/session/__tests__/store.test.ts
git commit -m "feat(session-store): add participants, gameTitle, persist for crash recovery"
```

---

## PHASE 3 — Frontend: Guest Players nel Wizard

### Task 3: Guest player support nel wizard di creazione sessione

**Problema:** Il wizard di setup (GameNightWizard/PlayerSetup) non gestisce giocatori ospiti senza account. Il backend supporta partecipanti senza `UserId`, ma il frontend richiede la selezione di utenti registrati.

**Files:**
- Modify: `apps/web/src/components/game-night/PlayerSetup.tsx`
- Modify: `apps/web/src/stores/game-night/store.ts`
- Create: `apps/web/src/components/game-night/__tests__/PlayerSetup.test.tsx`

- [ ] **Step 1: Scrivi il test del componente PlayerSetup con guest**

```typescript
// apps/web/src/components/game-night/__tests__/PlayerSetup.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { PlayerSetup } from '../PlayerSetup';

describe('PlayerSetup — guest players', () => {
  it('permette di aggiungere un ospite inserendo solo il nome', async () => {
    const onAdd = vi.fn();
    render(<PlayerSetup onAddPlayer={onAdd} existingPlayers={[]} />);

    const tab = screen.getByRole('tab', { name: /ospite/i });
    fireEvent.click(tab);

    const input = screen.getByPlaceholderText(/nome ospite/i);
    fireEvent.change(input, { target: { value: 'Luca' } });

    const button = screen.getByRole('button', { name: /aggiungi/i });
    fireEvent.click(button);

    expect(onAdd).toHaveBeenCalledWith(
      expect.objectContaining({ displayName: 'Luca', isGuest: true, userId: undefined })
    );
  });

  it('non permette nomi ospite duplicati', () => {
    const onAdd = vi.fn();
    render(
      <PlayerSetup
        onAddPlayer={onAdd}
        existingPlayers={[{ id: 'g1', displayName: 'Luca', isGuest: true }]}
      />
    );

    const tab = screen.getByRole('tab', { name: /ospite/i });
    fireEvent.click(tab);

    const input = screen.getByPlaceholderText(/nome ospite/i);
    fireEvent.change(input, { target: { value: 'Luca' } });

    const button = screen.getByRole('button', { name: /aggiungi/i });
    expect(button).toBeDisabled();
  });
});
```

- [ ] **Step 2: Esegui il test — verifica che fallisca**

```bash
cd apps/web
pnpm test components/game-night/__tests__/PlayerSetup.test.tsx
# Expected: FAIL
```

- [ ] **Step 3: Aggiorna PlayerSetup.tsx con tab Account/Ospite**

```tsx
// apps/web/src/components/game-night/PlayerSetup.tsx
'use client';
import { useState } from 'react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import type { SessionParticipant } from '@/stores/session/types';

interface PlayerSetupProps {
  onAddPlayer: (participant: SessionParticipant) => void;
  existingPlayers: SessionParticipant[];
}

export function PlayerSetup({ onAddPlayer, existingPlayers }: PlayerSetupProps) {
  const [guestName, setGuestName] = useState('');

  const isDuplicateGuest = existingPlayers.some(
    p => p.displayName.toLowerCase() === guestName.toLowerCase()
  );

  const handleAddGuest = () => {
    if (!guestName.trim() || isDuplicateGuest) return;
    onAddPlayer({
      id: `guest-${crypto.randomUUID()}`,
      displayName: guestName.trim(),
      isGuest: true,
    });
    setGuestName('');
  };

  return (
    <Tabs defaultValue="account">
      <TabsList className="w-full">
        <TabsTrigger value="account" className="flex-1">Utente App</TabsTrigger>
        <TabsTrigger value="guest" className="flex-1">Ospite</TabsTrigger>
      </TabsList>

      <TabsContent value="account">
        {/* Selettore utenti esistente — non modificato */}
        <p className="text-sm text-muted-foreground">
          Seleziona un utente registrato sull'app.
        </p>
      </TabsContent>

      <TabsContent value="guest">
        <div className="flex gap-2 mt-2">
          <Input
            placeholder="Nome ospite"
            value={guestName}
            onChange={e => setGuestName(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleAddGuest()}
          />
          <Button
            onClick={handleAddGuest}
            disabled={!guestName.trim() || isDuplicateGuest}
          >
            Aggiungi
          </Button>
        </div>
        {isDuplicateGuest && (
          <p className="text-sm text-destructive mt-1">
            Esiste già un giocatore con questo nome.
          </p>
        )}
      </TabsContent>
    </Tabs>
  );
}
```

- [ ] **Step 4: Aggiorna GameNightStore per usare SessionParticipant**

```typescript
// apps/web/src/stores/game-night/store.ts
// Sostituisci il tipo GameNightPlayer con SessionParticipant dove usato nei players
import type { SessionParticipant } from '@/stores/session/types';

// Nel interface GameNightState, cambia:
// players: GameNightPlayer[]  →  players: SessionParticipant[]
// addPlayer: (player: GameNightPlayer) => void  →  addPlayer: (player: SessionParticipant) => void
```

- [ ] **Step 5: Esegui i test — verifica che passino**

```bash
cd apps/web
pnpm test components/game-night/__tests__/PlayerSetup.test.tsx
# Expected: 2 test PASS

pnpm typecheck
# Expected: 0 errors
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/game-night/PlayerSetup.tsx
git add apps/web/src/components/game-night/__tests__/PlayerSetup.test.tsx
git add apps/web/src/stores/game-night/store.ts
git commit -m "feat(game-night): add guest player support in session wizard"
```

---

## PHASE 4 — Frontend: Orchestratore Game Night ↔ Session

### Task 4: useGameNightOrchestrator — coordina game night e sessioni

**Problema:** `GameNightStore` e `SessionStore` sono indipendenti. Non c'è logica che coordini: "questa sessione è parte di questa game night", "c'è già un gioco attivo, aggiungo il secondo", ecc.

**Files:**
- Create: `apps/web/src/lib/domain-hooks/useGameNightOrchestrator.ts`
- Create: `apps/web/src/lib/domain-hooks/__tests__/useGameNightOrchestrator.test.ts`

- [ ] **Step 1: Scrivi il test per l'orchestratore**

```typescript
// apps/web/src/lib/domain-hooks/__tests__/useGameNightOrchestrator.test.ts
import { renderHook, act } from '@testing-library/react';
import { useGameNightOrchestrator } from '../useGameNightOrchestrator';
import { useSessionStore } from '@/stores/session/store';

// Mock API call
vi.mock('@/lib/api/clients/gameSessionsClient', () => ({
  createSession: vi.fn().mockResolvedValue({ sessionId: 'new-sess-1', code: 'ABC-123' }),
}));

describe('useGameNightOrchestrator', () => {
  beforeEach(() => useSessionStore.getState().reset());

  it('startGame crea una sessione e aggiorna lo store', async () => {
    const { result } = renderHook(() => useGameNightOrchestrator('night-1'));

    await act(async () => {
      await result.current.startGame({
        gameId: 'game-1',
        gameTitle: 'Catan',
        participants: [
          { id: 'p1', displayName: 'Marco', isGuest: false },
        ],
      });
    });

    const store = useSessionStore.getState();
    expect(store.status).toBe('live');
    expect(store.gameTitle).toBe('Catan');
    expect(store.sessionId).toBe('new-sess-1');
  });

  it('startNextGame finalizza la sessione corrente e ne inizia una nuova', async () => {
    const { result } = renderHook(() => useGameNightOrchestrator('night-1'));

    await act(async () => {
      await result.current.startGame({
        gameId: 'game-1',
        gameTitle: 'Catan',
        participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
      });
    });

    // Simula fine partita
    await act(async () => {
      await result.current.startNextGame({
        gameId: 'game-2',
        gameTitle: 'Dixit',
        participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
      });
    });

    const store = useSessionStore.getState();
    expect(store.gameTitle).toBe('Dixit');
    expect(store.currentTurn).toBe(1); // reset per nuovo gioco
  });
});
```

- [ ] **Step 2: Esegui il test — verifica che fallisca**

```bash
cd apps/web
pnpm test domain-hooks/__tests__/useGameNightOrchestrator.test.ts
# Expected: FAIL — module not found
```

- [ ] **Step 3: Crea l'hook useGameNightOrchestrator**

```typescript
// apps/web/src/lib/domain-hooks/useGameNightOrchestrator.ts
'use client';
import { useCallback, useState } from 'react';
import { useSessionStore } from '@/stores/session/store';
import { createSession, finalizeSession } from '@/lib/api/clients/gameSessionsClient';
import type { SessionParticipant } from '@/stores/session/types';

interface StartGamePayload {
  gameId: string;
  gameTitle: string;
  participants: SessionParticipant[];
}

interface UseGameNightOrchestrator {
  /** Sessioni completate nella serata (gameTitle → sessionId) */
  completedGames: Array<{ gameTitle: string; sessionId: string }>;
  isStarting: boolean;
  error: string | null;
  /** Avvia il primo gioco della serata */
  startGame: (payload: StartGamePayload) => Promise<void>;
  /** Finalizza il gioco corrente e ne inizia uno nuovo */
  startNextGame: (payload: StartGamePayload) => Promise<void>;
}

export function useGameNightOrchestrator(gameNightId: string): UseGameNightOrchestrator {
  // Selettori granulari — evitano re-render su ogni addEvent/nextTurn
  const sessionId = useSessionStore(s => s.sessionId);
  const gameTitle = useSessionStore(s => s.gameTitle);
  const startSession = useSessionStore(s => s.startSession);
  const reset = useSessionStore(s => s.reset);
  const [completedGames, setCompletedGames] = useState<
    Array<{ gameTitle: string; sessionId: string }>
  >([]);
  const [isStarting, setIsStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const startGame = useCallback(async (payload: StartGamePayload) => {
    setIsStarting(true);
    setError(null);
    try {
      const response = await createSession({
        gameNightId,
        gameId: payload.gameId,
        participants: payload.participants.map(p => ({
          displayName: p.displayName,
          userId: p.userId,
          isGuest: p.isGuest,
        })),
      });

      startSession({
        sessionId: response.sessionId,
        gameId: payload.gameId,
        gameTitle: payload.gameTitle,
        participants: payload.participants,
      });
    } catch (err: unknown) {
      // Gestione 409: sessione già attiva per questa game night
      const status = (err as { status?: number })?.status;
      if (status === 409) {
        setError('Una partita è già attiva per questa serata. Finalizzala prima di iniziarne una nuova.');
      } else {
        setError('Impossibile avviare la partita. Riprova.');
      }
      throw err;
    } finally {
      setIsStarting(false);
    }
  }, [gameNightId, startSession]);

  const startNextGame = useCallback(async (payload: StartGamePayload) => {
    // 1. Finalizza sessione corrente (fire-and-forget se offline)
    if (sessionId) {
      try {
        await finalizeSession(sessionId);
        if (gameTitle) {
          setCompletedGames(prev => [...prev, { gameTitle, sessionId }]);
        }
      } catch {
        // Continua comunque — la finalizzazione può essere ritentata dopo
      }
    }

    // 2. Reset store per il nuovo gioco
    reset();

    // 3. Avvia nuovo gioco
    await startGame(payload);
  }, [sessionId, gameTitle, reset, startGame]);

  return { completedGames, isStarting, error, startGame, startNextGame };
}
```

- [ ] **Step 4: Crea il gameSessionsClient (se non esiste)**

> **Nota pattern**: usa `apiClient` (già esportato da `@/lib/api/client`) — NON `httpClient` che non esiste come named export. `apiClient` è un'istanza di `HttpClient` (vedi altri client nel progetto come `gamesClient.ts`).

```typescript
// apps/web/src/lib/api/clients/gameSessionsClient.ts
import { apiClient } from '@/lib/api/client';

export interface CreateSessionPayload {
  gameNightId: string;
  gameId: string;
  participants: Array<{
    displayName: string;
    userId?: string;
    isGuest: boolean;
  }>;
}

export interface CreateSessionResponse {
  sessionId: string;
  code: string;
}

export interface RollDiceResponse {
  results: number[];
  total: number;
}

export async function createSession(
  payload: CreateSessionPayload
): Promise<CreateSessionResponse> {
  const res = await apiClient.post<CreateSessionResponse>(
    '/api/v1/game-sessions',
    payload
  );
  return res;
}

export async function finalizeSession(sessionId: string): Promise<void> {
  await apiClient.post(`/api/v1/game-sessions/${sessionId}/finalize`, {});
}

export async function updateScore(
  sessionId: string,
  participantId: string,
  score: number
): Promise<void> {
  await apiClient.put(`/api/v1/game-sessions/${sessionId}/scores`, {
    participantId,
    score,
  });
}

export async function rollDice(
  sessionId: string,
  diceType: string,
  count = 1
): Promise<RollDiceResponse> {
  const res = await apiClient.post<RollDiceResponse>(
    `/api/v1/game-sessions/${sessionId}/dice`,
    { diceType, count }
  );
  return res;
}
```

- [ ] **Step 5: Esegui i test — verifica che passino**

```bash
cd apps/web
pnpm test domain-hooks/__tests__/useGameNightOrchestrator.test.ts
# Expected: 2 test PASS
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/domain-hooks/useGameNightOrchestrator.ts
git add apps/web/src/lib/domain-hooks/__tests__/useGameNightOrchestrator.test.ts
git add apps/web/src/lib/api/clients/gameSessionsClient.ts
git commit -m "feat(game-night): add orchestrator hook for multi-game session coordination"
```

---

## PHASE 5 — Frontend: AI Agent Chat (RulesExplainer)

### Task 5: Hook useSessionAgentChat + wiring RulesExplainer

**Problema:** `RulesExplainer.tsx` esiste ma probabilmente usa un placeholder. L'agente deve ricevere il `GameSessionContext` costruito dallo `SessionStore`.

**Files:**
- Create: `apps/web/src/lib/domain-hooks/useSessionAgentChat.ts`
- Create: `apps/web/src/lib/api/clients/sessionAgentClient.ts`
- Modify: `apps/web/src/components/game-night/RulesExplainer.tsx`

- [ ] **Step 1: Scrivi il test per useSessionAgentChat**

```typescript
// apps/web/src/lib/domain-hooks/__tests__/useSessionAgentChat.test.ts
import { renderHook, act } from '@testing-library/react';
import { useSessionAgentChat } from '../useSessionAgentChat';
import { useSessionStore } from '@/stores/session/store';

// Mock EventSource per SSE
class MockEventSource {
  onmessage: ((e: MessageEvent) => void) | null = null;
  onerror: (() => void) | null = null;
  close = vi.fn();
  constructor(public url: string) {}
}
(global as any).EventSource = MockEventSource;

describe('useSessionAgentChat', () => {
  beforeEach(() => useSessionStore.getState().reset());

  it('isLoading è true durante lo streaming', async () => {
    useSessionStore.getState().startSession({
      sessionId: 'sess-1',
      gameId: 'game-1',
      gameTitle: 'Catan',
      participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
    });

    const { result } = renderHook(() =>
      useSessionAgentChat('agent-session-1')
    );

    act(() => { result.current.ask('Come si piazzano i ladri?'); });
    expect(result.current.isLoading).toBe(true);
  });

  it('messages inizialmente è vuoto', () => {
    const { result } = renderHook(() => useSessionAgentChat('agent-1'));
    expect(result.current.messages).toHaveLength(0);
  });
});
```

- [ ] **Step 2: Esegui il test — verifica che fallisca**

```bash
cd apps/web
pnpm test domain-hooks/__tests__/useSessionAgentChat.test.ts
# Expected: FAIL
```

- [ ] **Step 3: Crea sessionAgentClient**

> **⚠️ URL corretto**: il backend espone `POST /api/v1/agent-sessions/{agentSessionId}/chat` dove `agentSessionId` è nella **route**. Verifica il file di routing reale (`grep -r "AgentSessionId" apps/api/src/Api/Routing/`) prima di procedere.

```typescript
// apps/web/src/lib/api/clients/sessionAgentClient.ts
import { getApiBase } from '@/lib/api/core/httpClient';

export interface AgentGameContext {
  gameId: string;
  gameTitle: string;
  players: string[];
  currentTurn: number;
  responseLanguage: string;
}

export interface AgentChatPayload {
  userQuestion: string;
  chatThreadId?: string;
  gameContext?: AgentGameContext;
}

/**
 * URL base per la chat con l'agente.
 * Verifica il routing reale con: grep -r "AgentSessionId" apps/api/src/Api/Routing/
 * Pattern atteso: POST /api/v1/agent-sessions/{agentSessionId}/chat
 */
export function getAgentChatUrl(agentSessionId: string): string {
  return `${getApiBase()}/api/v1/agent-sessions/${agentSessionId}/chat`;
}
```

- [ ] **Step 4: Crea useSessionAgentChat**

```typescript
// apps/web/src/lib/domain-hooks/useSessionAgentChat.ts
'use client';
import { useCallback, useRef, useState } from 'react';
import { useSessionStore } from '@/stores/session/store';
import { httpClient } from '@/lib/api/client';
import { getApiBase } from '@/lib/api/core/httpClient';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
}

export function useSessionAgentChat(agentSessionId: string) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [streamingContent, setStreamingContent] = useState('');
  const threadIdRef = useRef<string | undefined>(undefined);
  const abortRef = useRef<AbortController | null>(null);

  // Selettori granulari — evitano re-render su ogni evento SSE
  const gameId = useSessionStore(s => s.gameId);
  const gameTitle = useSessionStore(s => s.gameTitle);
  const participants = useSessionStore(s => s.participants);
  const currentTurn = useSessionStore(s => s.currentTurn);

  const ask = useCallback(async (question: string) => {
    if (!question.trim() || isLoading) return;

    setIsLoading(true);
    setError(null);
    setStreamingContent('');

    // Aggiungi messaggio utente immediatamente
    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: question,
      timestamp: new Date().toISOString(),
    };
    setMessages(prev => [...prev, userMsg]);

    // Costruisci il game context dallo store
    const gameContext = gameId && gameTitle
      ? {
          gameId,
          gameTitle,
          players: participants.map(p => p.displayName),
          currentTurn,
          responseLanguage: 'it',
        }
      : undefined;

    abortRef.current = new AbortController();

    try {
      const response = await fetch(
        `${getApiBase()}/api/v1/agent-sessions/${agentSessionId}/chat`,
        {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json', 'Accept': 'text/event-stream' },
          body: JSON.stringify({
            userQuestion: question,
            chatThreadId: threadIdRef.current,
            gameContext,
          }),
          signal: abortRef.current.signal,
        }
      );

      if (!response.ok) {
        throw new Error(`Errore ${response.status}`);
      }

      const reader = response.body!.getReader();
      const decoder = new TextDecoder();
      let accumulated = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        const chunk = decoder.decode(value, { stream: true });
        const lines = chunk.split('\n');

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            try {
              const payload = JSON.parse(line.slice(6));
              if (payload.type === 'token' && payload.content) {
                accumulated += payload.content;
                setStreamingContent(accumulated);
              } else if (payload.type === 'complete') {
                if (payload.threadId) {
                  threadIdRef.current = payload.threadId;
                }
              }
            } catch { /* ignora linee non-JSON */ }
          }
        }
      }

      // Finalizza messaggio assistente
      const assistantMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        content: accumulated,
        timestamp: new Date().toISOString(),
      };
      setMessages(prev => [...prev, assistantMsg]);
      setStreamingContent('');
    } catch (err) {
      if ((err as Error).name !== 'AbortError') {
        setError('L\'agente non è disponibile. Controlla la connessione.');
      }
    } finally {
      setIsLoading(false);
    }
  }, [agentSessionId, gameId, gameTitle, participants, currentTurn, isLoading]);

  const stop = useCallback(() => {
    abortRef.current?.abort();
  }, []);

  return { messages, isLoading, error, streamingContent, ask, stop };
}
```

- [ ] **Step 5: Aggiorna RulesExplainer.tsx**

```tsx
// apps/web/src/components/game-night/RulesExplainer.tsx
'use client';
import { useState } from 'react';
import { useSessionAgentChat } from '@/lib/domain-hooks/useSessionAgentChat';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';

interface RulesExplainerProps {
  agentSessionId: string;
}

export function RulesExplainer({ agentSessionId }: RulesExplainerProps) {
  const [input, setInput] = useState('');
  const { messages, isLoading, error, streamingContent, ask, stop } =
    useSessionAgentChat(agentSessionId);

  const handleSend = () => {
    ask(input);
    setInput('');
  };

  return (
    <div className="flex flex-col h-full gap-2">
      <ScrollArea className="flex-1 p-3 rounded-md border">
        {messages.length === 0 && (
          <p className="text-sm text-muted-foreground text-center mt-8">
            Chiedi all'assistente le regole del gioco
          </p>
        )}
        {messages.map(msg => (
          <div
            key={msg.id}
            className={`mb-3 text-sm ${msg.role === 'user' ? 'text-right' : 'text-left'}`}
          >
            <span
              className={`inline-block px-3 py-2 rounded-lg max-w-[85%] ${
                msg.role === 'user'
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted'
              }`}
            >
              {msg.content}
            </span>
          </div>
        ))}
        {/* Streaming in corso */}
        {streamingContent && (
          <div className="mb-3 text-sm text-left">
            <span className="inline-block px-3 py-2 rounded-lg bg-muted max-w-[85%]">
              {streamingContent}
              <span className="animate-pulse">▌</span>
            </span>
          </div>
        )}
        {error && (
          <p className="text-sm text-destructive text-center">{error}</p>
        )}
      </ScrollArea>

      <div className="flex gap-2">
        <Input
          placeholder="Es: Come funziona il turno di gioco?"
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && !isLoading && handleSend()}
          disabled={isLoading}
        />
        {isLoading ? (
          <Button variant="outline" onClick={stop}>Stop</Button>
        ) : (
          <Button onClick={handleSend} disabled={!input.trim()}>
            Chiedi
          </Button>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 6: Esegui i test**

```bash
cd apps/web
pnpm test domain-hooks/__tests__/useSessionAgentChat.test.ts
# Expected: 2 test PASS

pnpm typecheck
# Expected: 0 errors
```

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/lib/domain-hooks/useSessionAgentChat.ts
git add apps/web/src/lib/api/clients/sessionAgentClient.ts
git add apps/web/src/components/game-night/RulesExplainer.tsx
git commit -m "feat(rules-explainer): wire AI agent with game session context and Italian streaming"
```

---

## PHASE 6 — Frontend: Offline Toolkit

### Task 6: IndexedDB layer per operazioni toolkit offline

**Problema:** Se internet cade durante la serata, il dado e il timer devono continuare a funzionare. I lanci vengono accodati e sincronizzati al rientro online.

**Files:**
- Create: `apps/web/src/lib/offline/toolkitDb.ts`
- Create: `apps/web/src/lib/offline/__tests__/toolkitDb.test.ts`
- Create: `apps/web/src/lib/domain-hooks/useOfflineToolkit.ts`
- Create: `apps/web/src/lib/domain-hooks/__tests__/useOfflineToolkit.test.ts`

- [ ] **Step 1: Installa `idb`**

```bash
cd apps/web
pnpm add idb
```

- [ ] **Step 2: Scrivi i test per toolkitDb**

```typescript
// apps/web/src/lib/offline/__tests__/toolkitDb.test.ts
import { openToolkitDb, queueOperation, getPendingOperations, clearOperation } from '../toolkitDb';

describe('toolkitDb', () => {
  it('mette in coda un lancio di dado e lo recupera', async () => {
    const db = await openToolkitDb();
    const op = {
      id: crypto.randomUUID(),
      sessionId: 'sess-1',
      type: 'dice_roll' as const,
      payload: { diceType: 'D6', count: 2 },
      timestamp: new Date().toISOString(),
      synced: false,
    };

    await queueOperation(op);
    const pending = await getPendingOperations('sess-1');

    expect(pending).toHaveLength(1);
    expect(pending[0].type).toBe('dice_roll');
  });

  it('clearOperation rimuove una operazione sincronizzata', async () => {
    const id = crypto.randomUUID();
    await queueOperation({
      id,
      sessionId: 'sess-1',
      type: 'score_update',
      payload: { playerId: 'p1', score: 10 },
      timestamp: new Date().toISOString(),
      synced: false,
    });

    await clearOperation(id);
    const pending = await getPendingOperations('sess-1');
    expect(pending.find(op => op.id === id)).toBeUndefined();
  });
});
```

- [ ] **Step 3: Esegui il test — verifica che fallisca**

```bash
cd apps/web
pnpm test offline/__tests__/toolkitDb.test.ts
# Expected: FAIL
```

- [ ] **Step 4: Crea toolkitDb.ts**

```typescript
// apps/web/src/lib/offline/toolkitDb.ts
import { openDB, type IDBPDatabase } from 'idb';

export type ToolkitOperationType = 'dice_roll' | 'score_update' | 'add_note' | 'card_draw';

export interface PendingOperation {
  id: string;
  sessionId: string;
  type: ToolkitOperationType;
  payload: Record<string, unknown>;
  timestamp: string;
  synced: boolean;
}

const DB_NAME = 'meepleai-offline';
const STORE_NAME = 'pending-ops';
const DB_VERSION = 1;

let dbInstance: IDBPDatabase | null = null;

export async function openToolkitDb() {
  if (dbInstance) return dbInstance;
  dbInstance = await openDB(DB_NAME, DB_VERSION, {
    upgrade(db) {
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        const store = db.createObjectStore(STORE_NAME, { keyPath: 'id' });
        store.createIndex('by-session', 'sessionId');
        store.createIndex('by-synced', 'synced');
      }
    },
  });
  return dbInstance;
}

export async function queueOperation(op: PendingOperation) {
  const db = await openToolkitDb();
  await db.put(STORE_NAME, op);
}

export async function getPendingOperations(sessionId: string): Promise<PendingOperation[]> {
  const db = await openToolkitDb();
  return db.getAllFromIndex(STORE_NAME, 'by-session', sessionId);
}

export async function clearOperation(id: string) {
  const db = await openToolkitDb();
  await db.delete(STORE_NAME, id);
}

export async function syncPendingOperations(
  sessionId: string,
  syncFn: (op: PendingOperation) => Promise<void>
) {
  const pending = await getPendingOperations(sessionId);
  const unsynced = pending.filter(op => !op.synced);

  for (const op of unsynced) {
    try {
      await syncFn(op);
      await clearOperation(op.id);
    } catch {
      // Lascia in coda per il prossimo tentativo
    }
  }
}
```

- [ ] **Step 5: Crea useOfflineToolkit**

```typescript
// apps/web/src/lib/domain-hooks/useOfflineToolkit.ts
'use client';
import { useCallback, useEffect, useRef } from 'react';
import { queueOperation, syncPendingOperations, type ToolkitOperationType } from '@/lib/offline/toolkitDb';
import { rollDice, updateScore } from '@/lib/api/clients/gameSessionsClient';

export function useOfflineToolkit(sessionId: string | null) {
  // Safe in SSR: `navigator` non esiste sul server in Next.js App Router
  const isOnlineRef = useRef(typeof navigator !== 'undefined' ? navigator.onLine : true);

  // Sync quando torna online
  useEffect(() => {
    const handleOnline = async () => {
      isOnlineRef.current = true;
      if (!sessionId) return;

      await syncPendingOperations(sessionId, async (op) => {
        switch (op.type) {
          case 'dice_roll':
            await rollDice(
              sessionId,
              op.payload.diceType as string,
              op.payload.count as number
            );
            break;
          case 'score_update':
            await updateScore(
              sessionId,
              op.payload.playerId as string,
              op.payload.score as number
            );
            break;
        }
      });
    };

    const handleOffline = () => { isOnlineRef.current = false; };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [sessionId]);

  const rollDiceOfflineAware = useCallback(async (
    diceType: string,
    count = 1
  ): Promise<number[]> => {
    // Calcolo locale sempre disponibile
    const results = Array.from({ length: count }, () => {
      const sides = parseInt(diceType.replace('D', ''), 10) || 6;
      return Math.floor(Math.random() * sides) + 1;
    });

    if (sessionId) {
      if (isOnlineRef.current) {
        // Online: chiama API (fire-and-forget, risultato locale è già mostrato)
        rollDice(sessionId, diceType, count).catch(() => {
          // Se fallisce, accoda per dopo
          queueOperation({
            id: crypto.randomUUID(),
            sessionId,
            type: 'dice_roll',
            payload: { diceType, count },
            timestamp: new Date().toISOString(),
            synced: false,
          });
        });
      } else {
        // Offline: accoda
        await queueOperation({
          id: crypto.randomUUID(),
          sessionId,
          type: 'dice_roll',
          payload: { diceType, count },
          timestamp: new Date().toISOString(),
          synced: false,
        });
      }
    }

    return results;
  }, [sessionId]);

  const updateScoreOfflineAware = useCallback(async (
    playerId: string,
    score: number
  ) => {
    if (!sessionId) return;

    if (isOnlineRef.current) {
      updateScore(sessionId, playerId, score).catch(() => {
        queueOperation({
          id: crypto.randomUUID(),
          sessionId,
          type: 'score_update',
          payload: { playerId, score },
          timestamp: new Date().toISOString(),
          synced: false,
        });
      });
    } else {
      await queueOperation({
        id: crypto.randomUUID(),
        sessionId,
        type: 'score_update',
        payload: { playerId, score },
        timestamp: new Date().toISOString(),
        synced: false,
      });
    }
  }, [sessionId]);

  return { rollDice: rollDiceOfflineAware, updateScore: updateScoreOfflineAware };
}
```

- [ ] **Step 6: Esegui i test**

```bash
cd apps/web
pnpm test offline/__tests__/toolkitDb.test.ts
# Expected: 2 test PASS

pnpm typecheck
# Expected: 0 errors
```

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/lib/offline/toolkitDb.ts
git add apps/web/src/lib/offline/__tests__/toolkitDb.test.ts
git add apps/web/src/lib/domain-hooks/useOfflineToolkit.ts
git commit -m "feat(offline): add IndexedDB toolkit layer with sync queue for offline game night"
```

---

## PHASE 7 — Frontend: Multi-Device Score Sync

### Task 7: useSessionScoreSync — SSE score updates per tutti i device

**Problema:** Il `useSessionSSE` esistente gestisce il flusso di eventi generico. Non ha logica specifica per aggiornare i `scores` nello store quando arriva un evento `score_update` dall'SSE v2.

**Files:**
- Create: `apps/web/src/lib/domain-hooks/useSessionScoreSync.ts`
- Modify: `apps/web/src/components/session/LiveSessionLayout.tsx` (aggiunta hook)

- [ ] **Step 1: Scrivi il test**

```typescript
// apps/web/src/lib/domain-hooks/__tests__/useSessionScoreSync.test.ts
import { renderHook, act } from '@testing-library/react';
import { useSessionScoreSync } from '../useSessionScoreSync';
import { useSessionStore } from '@/stores/session/store';

describe('useSessionScoreSync', () => {
  beforeEach(() => {
    useSessionStore.getState().reset();
    useSessionStore.getState().startSession({
      sessionId: 'sess-1',
      gameId: 'g',
      gameTitle: 'Catan',
      participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
    });
  });

  it('aggiorna score nello store quando riceve evento SSE score_update', () => {
    const { result } = renderHook(() => useSessionScoreSync());

    act(() => {
      result.current.handleSseEvent({
        id: 'evt-1',
        type: 'score_update',
        playerId: 'p1',
        data: { score: 8 },
        timestamp: new Date().toISOString(),
      });
    });

    const score = useSessionStore.getState().scores.find(s => s.playerId === 'p1');
    expect(score?.score).toBe(8);
  });
});
```

- [ ] **Step 2: Esegui il test — verifica che fallisca**

```bash
cd apps/web
pnpm test domain-hooks/__tests__/useSessionScoreSync.test.ts
# Expected: FAIL
```

- [ ] **Step 3: Crea useSessionScoreSync**

```typescript
// apps/web/src/lib/domain-hooks/useSessionScoreSync.ts
'use client';
import { useCallback } from 'react';
import { useSessionStore } from '@/stores/session/store';
import type { ActivityEvent } from '@/stores/session/types';

/**
 * Interpreta gli eventi SSE di tipo score_update e li applica allo store.
 * Si integra con useSessionSSE esistente — passa handleSseEvent come callback.
 */
export function useSessionScoreSync() {
  const updateScore = useSessionStore(s => s.updateScore);
  const addEvent = useSessionStore(s => s.addEvent);

  const handleSseEvent = useCallback((event: ActivityEvent) => {
    // Sempre aggiunge all'activity feed
    addEvent(event);

    // Applica logica specifica per tipo
    switch (event.type) {
      case 'score_update': {
        const playerId = event.playerId;
        const score = event.data.score as number | undefined;
        if (playerId && typeof score === 'number') {
          updateScore(playerId, score);
        }
        break;
      }
      // Altri tipi gestiti dall'activity feed, nessuna azione store aggiuntiva
    }
  }, [updateScore, addEvent]);

  return { handleSseEvent };
}
```

- [ ] **Step 4: Estendi useSessionSSE per accettare callback opzionale**

> **⚠️ Issue strutturale**: `useSessionSSE` ha firma `(sessionId: string | null): void` — non accetta callback. Aggiorna il hook esistente in `apps/web/src/hooks/useSessionSSE.ts`:

```typescript
// Modifica la firma del hook aggiungendo il parametro opzionale:
export function useSessionSSE(
  sessionId: string | null,
  onEvent?: (event: ActivityEvent) => void  // NUOVO
): void {
  // ... codice esistente ...

  // Nella callback onmessage, DOPO addEvent(parsed), chiama il callback opzionale:
  eventSource.onmessage = (event: MessageEvent<string>) => {
    try {
      const parsed = JSON.parse(event.data) as ActivityEvent;
      if (seenIdsRef.current.has(parsed.id)) return;
      seenIdsRef.current.add(parsed.id);
      addEvent(parsed);
      onEvent?.(parsed);  // NUOVO — chiama callback opzionale
    } catch { }
  };
}
```

Poi in `LiveSessionLayout.tsx`:

```typescript
// Aggiungi all'inizio del componente:
const { handleSseEvent } = useSessionScoreSync();

// Aggiorna la chiamata esistente a useSessionSSE:
useSessionSSE(sessionId, handleSseEvent);
```

- [ ] **Step 5: Esegui i test**

```bash
cd apps/web
pnpm test domain-hooks/__tests__/useSessionScoreSync.test.ts
# Expected: 1 test PASS

pnpm test
# Expected: tutti PASS (no regressioni)
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/domain-hooks/useSessionScoreSync.ts
git add apps/web/src/lib/domain-hooks/__tests__/useSessionScoreSync.test.ts
git commit -m "feat(session): add SSE score sync hook for multi-device real-time updates"
```

---

## Self-Review — Copertura Spec

| User Story | Task che copre | Status |
|---|---|---|
| US-001: Crea game night con guest | Task 3 (PlayerSetup guest) + Task 4 (Orchestrator) | ✅ |
| US-002: AI agent in italiano + game context | Task 1 (Backend context) + Task 5 (Frontend chat) | ✅ |
| US-003: Score tracking multi-device | Task 2 (Store enrichment) + Task 7 (SSE sync) | ✅ |
| US-004: Toolkit offline | Task 6 (IndexedDB + sync queue) | ✅ |
| US-005: Finalizzazione con storico | Task 4 (finalizeSession in orchestrator) | ⚠️ Solo API — UI "Game Over" non inclusa |
| Domanda 1: Più giochi in serata | Task 4 (startNextGame) | ✅ |
| Domanda 2: AI risponde in italiano | Task 1 (`responseLanguage: 'it'`) | ✅ |
| Domanda 3: Guest riconosciuti in sessioni future | Task 3 (guest UUID stabile) | ⚠️ Parziale — serve Task 8 (localStorage) |
| Domanda 4: Toolkit condiviso | Task 7 (SSE sync) | ✅ |
| Domanda 5: No limit durata | N/A | ✅ (non serve) |

**Fix applicati dalla code review:**
- ✅ [BLOCCANTE] URL endpoint agent: aggiunto warning `grep` per verificare routing reale
- ✅ [BLOCCANTE] `useSessionSSE` callback: Task 7 Step 4 aggiornato con patch esplicita del hook
- ✅ [BLOCCANTE] `httpClient` → `apiClient` con tipo esplicito, no Zod come argomento
- ✅ [IMPORTANTE] `navigator.onLine` SSR-safe con guard `typeof navigator !== 'undefined'`
- ✅ [IMPORTANTE] Selettori granulari in `useGameNightOrchestrator` e `useSessionAgentChat`
- ✅ [IMPORTANTE] Validator FluentValidation per `GameContext` — Step 8b in Task 1
- ✅ [MINORE] Gestione 409 in `startGame` con messaggio specifico

**Gap non coperti (lavoro futuro):**
- Task 8: UI "Game Over" con podio (US-005 visuale)
- Task 9: Guest persistence cross-session via localStorage

**Type consistency check:**
- `SessionParticipant` usato consistentemente in Task 2, 3, 4, 5 ✅
- `StartSessionPayload` definito in Task 2, usato in Task 4 ✅
- `PendingOperation` definito in Task 6, usato in useOfflineToolkit ✅
- `ActivityEvent` (tipo esistente) usato in Task 7 ✅
- `apiClient` (non `httpClient`) in Task 4 gameSessionsClient ✅

---

## Piano separato (fuori scope)

Il seguente lavoro è indipendente e può procedere in parallelo:
- **`2026-04-03-game-night-guest-persistence.md`**: localStorage per riconoscimento guest cross-session (Domanda 3)

---

**Piano salvato.** Due opzioni di esecuzione:

**1. Subagent-Driven (raccomandato)** — subagent fresco per task, review tra task, iterazione veloce

**2. Inline Execution** — esecuzione sequenziale in questa sessione

**Quale approccio?**
