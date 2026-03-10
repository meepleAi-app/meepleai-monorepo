# Game Night Experience — Piano di Implementazione

**Epic**: #5578 | **Issues**: #5579-#5589 (11 sub-issues)
**Branch Strategy**: `main-dev` → `game-night-dev` → `feature/issue-XXXX-*`
**Date**: 2026-03-09

---

## Dependency Graph

```
#5579 GameSessionContext ─────┬──→ #5580 Session-aware RAG ──┬──→ #5585 Arbiter Mode
                              │                               │
                              │                               └──→ #5588 Expansion boost
                              │
#5581 Auto-snapshot ──────────┘ (independent)

#5580 + #5579 ───→ #5582 Playlist (BE+FE)
                   #5583 Setup Wizard (FE, needs RulebookAnalysis)
                   #5584 Rules Explainer (FE, needs RulebookAnalysis)
                   #5586 Pre-cache (FE, needs #5580)

All above ────────→ #5587 Live Session UI (FE, integration)
All above ────────→ #5589 Tests (full suite)
```

---

## Implementation Batches

### Batch 1: Backend Foundation (Sprint, ~3 giorni)

| Order | Issue | Title | Size | Deps | Parallel? |
|-------|-------|-------|------|------|-----------|
| 1a | #5579 | GameSessionContext orchestrator | L | none | YES |
| 1b | #5581 | Auto-snapshot on Pause | M | none | YES con 1a |
| 2 | #5580 | Session-aware RAG chat | M | #5579 | NO |
| 3a | #5588 | Expansion priority RAG boost | S | #5580 | YES |
| 3b | #5585 | Arbiter Mode | L | #5580 | YES con 3a |

**Strategy**: #5579 e #5581 in parallelo (zero dipendenze), poi #5580, poi #5588 e #5585 in parallelo.

### Batch 2: Frontend Features (Sprint, ~3 giorni)

| Order | Issue | Title | Size | Deps | Parallel? |
|-------|-------|-------|------|------|-----------|
| 4a | #5582 | Game Night Playlist | L | #5579 | YES |
| 4b | #5584 | Rules Explainer | M | RulebookAnalysis | YES con 4a |
| 5a | #5583 | Setup Wizard | L | RulebookAnalysis | YES |
| 5b | #5586 | Pre-cache + degradation | M | #5580 | YES con 5a |

**Strategy**: #5582 e #5584 in parallelo, poi #5583 e #5586 in parallelo.

### Batch 3: Integration & Tests (Sprint, ~2 giorni)

| Order | Issue | Title | Size | Deps |
|-------|-------|-------|------|------|
| 6 | #5587 | Live Game Session UI | L | Batch 1+2 |
| 7 | #5589 | Tests unit + integration + E2E | L | Tutto |

---

## Batch 1: Backend Foundation

### Issue #5579 — GameSessionContext Orchestrator

**Branch**: `feature/issue-5579-game-session-context` from `game-night-dev`
**PR target**: `game-night-dev`

#### Files da CREARE

```
apps/api/src/Api/BoundedContexts/GameManagement/Application/
├── Services/
│   └── GameSessionOrchestratorService.cs          # Application Service
├── DTOs/
│   └── GameSessionContextDto.cs                   # Context DTO
├── Commands/LiveSessions/
│   └── BuildGameSessionContextCommand.cs          # Command + Handler
└── Queries/LiveSessions/
    └── GetGameSessionContextQuery.cs              # Query + Handler
```

#### Implementazione

1. **`GameSessionContextDto`** — DTO con:
   ```csharp
   public record GameSessionContextDto
   {
       public Guid SessionId { get; init; }
       public Guid? GameId { get; init; }
       public string GameName { get; init; }
       public List<Guid> AllGameIds { get; init; }           // base + espansioni
       public List<Guid> ExpansionGameIds { get; init; }     // solo espansioni
       public List<Guid> SharedGameIds { get; init; }        // per filtro RAG
       public string? CurrentPhase { get; init; }
       public int CurrentTurn { get; init; }
       public Dictionary<Guid, string> GameTitles { get; init; }
   }
   ```

2. **`GameSessionOrchestratorService`** — Application Service:
   - Iniettare: `ILiveSessionRepository`, `IEntityLinkRepository`, `ISharedGameRepository`, `IRulebookAnalysisRepository`, `IVectorDocumentRepository`
   - `BuildContextAsync(Guid sessionId)`:
     1. Load `LiveGameSession` → get `GameId`
     2. Query `EntityLink` where `SourceEntityId = GameId AND LinkType = ExpansionOf` → expansion IDs
     3. Per ogni gameId (base + espansioni): query `SharedGame` → get `SharedGameId`
     4. Query `VectorDocument` where `SharedGameId IN allSharedGameIds` → confirm KB exists
     5. Return `GameSessionContextDto`

3. **`BuildGameSessionContextCommand`** + Handler:
   - `public record BuildGameSessionContextCommand(Guid SessionId) : IRequest<GameSessionContextDto>`
   - Handler usa `GameSessionOrchestratorService`

4. **`GetGameSessionContextQuery`** + Handler:
   - Per query read-only (cached)

5. **Endpoint**:
   - `GET /api/v1/live-sessions/{sessionId}/context` → `GetGameSessionContextQuery`
   - Aggiungere in `LiveSessionEndpoints.cs`

6. **DI Registration**:
   - `GameManagementServiceExtensions.cs`: registrare `GameSessionOrchestratorService` come scoped

#### Test

- Unit: `GameSessionOrchestratorServiceTests` — mock repos, verify context building
- Integration: `BuildGameSessionContextCommandTests` — test con DB reale

---

### Issue #5581 — Auto-snapshot on Pause (PARALLELO con #5579)

**Branch**: `feature/issue-5581-auto-snapshot-pause` from `game-night-dev`

#### Files da MODIFICARE

```
apps/api/src/Api/BoundedContexts/GameManagement/
├── Domain/
│   ├── Entities/LiveGameSession.cs                # Aggiungere GameStateSnapshot child entity
│   ├── Entities/GameStateSnapshot.cs              # CREARE
│   └── Events/SessionPausedEvent.cs               # Verificare esistenza
├── Application/
│   ├── EventHandlers/
│   │   └── SessionPausedSnapshotHandler.cs        # CREARE
│   ├── Commands/LiveSessions/
│   │   └── RestoreSnapshotCommand.cs              # CREARE
│   └── Queries/LiveSessions/
│       └── GetSessionSnapshotsQuery.cs            # CREARE
├── Infrastructure/
│   └── Persistence/GameManagementDbContext.cs      # Aggiungere DbSet
└── Routing/
    └── LiveSessionEndpoints.cs                    # 2 nuovi endpoint
```

#### Implementazione

1. **`GameStateSnapshot`** Entity:
   ```csharp
   public class GameStateSnapshot : Entity<Guid>
   {
       public Guid LiveGameSessionId { get; private set; }
       public JsonDocument GameState { get; private set; }
       public int TurnIndex { get; private set; }
       public int PhaseIndex { get; private set; }
       public Dictionary<Guid, decimal> PlayerScores { get; private set; }
       public string Label { get; private set; }          // "Pausa turno 5"
       public SnapshotTrigger TriggerType { get; private set; }
       public DateTime CreatedAt { get; private set; }
   }
   ```

2. **`SessionPausedSnapshotHandler`**:
   - Listens to `LiveSessionPausedEvent`
   - Crea snapshot automatico con label auto-generata
   - Salva tramite repository

3. **Endpoints**:
   - `GET /api/v1/live-sessions/{id}/snapshots` → lista snapshot
   - `POST /api/v1/live-sessions/{id}/snapshots/{snapshotId}/restore` → ripristina

4. **Migration**: `AddGameStateSnapshots`

---

### Issue #5580 — Session-aware RAG Chat

**Branch**: `feature/issue-5580-session-aware-rag` from `game-night-dev`
**Dipendenza**: #5579 (GameSessionContext)

#### Files da MODIFICARE

```
apps/api/src/Api/BoundedContexts/KnowledgeBase/
├── Application/
│   ├── Commands/
│   │   ├── AskAgentQuestionCommand.cs             # Aggiungere SessionId param
│   │   └── ChatWithSessionAgentCommand.cs         # Aggiungere context loading
│   └── Handlers/
│       ├── AskAgentQuestionCommandHandler.cs      # Filtro SharedGameId
│       └── ChatWithSessionAgentCommandHandler.cs  # Context injection
├── Infrastructure/
│   ├── QdrantVectorStoreAdapter.cs                # Filtro SharedGameId IN
│   └── IQdrantVectorStoreAdapter.cs               # Nuova signature
└── Domain/
    └── Services/
        └── IVectorSearchService.cs                # Aggiungere filtro opzionale
```

#### Implementazione

1. **`AskAgentQuestionCommand`** — aggiungere:
   ```csharp
   public Guid? LiveSessionId { get; init; }  // Opzionale, abilita filtro sessione
   ```

2. **Handler** — quando `LiveSessionId` presente:
   - Chiama `IMediator.Send(new GetGameSessionContextQuery(LiveSessionId))`
   - Usa `context.SharedGameIds` per filtrare vector search
   - Inietta nel system prompt: summary meccaniche, fase corrente

3. **`QdrantVectorStoreAdapter`** — aggiungere filtro:
   ```csharp
   if (sharedGameIds?.Any() == true)
   {
       filter.Must.Add(new FieldCondition
       {
           Key = "shared_game_id",
           Match = new MatchAny { Any = sharedGameIds.Select(id => id.ToString()) }
       });
   }
   ```

4. **System Prompt injection**:
   ```
   Sei l'assistente per la sessione di gioco "{GameName}".
   Giochi attivi: {GameTitles}
   Fase corrente: {CurrentPhase}
   Turno: {CurrentTurn}
   RISPONDI SOLO con informazioni dai regolamenti caricati.
   ```

---

### Issue #5588 — Expansion Priority RAG Boost (PARALLELO con #5585)

**Branch**: `feature/issue-5588-expansion-rag-boost` from `game-night-dev`
**Dipendenza**: #5580

#### Files da MODIFICARE

```
apps/api/src/Api/BoundedContexts/KnowledgeBase/
├── Application/Handlers/
│   └── AskAgentQuestionCommandHandler.cs          # Post-search boost
└── Infrastructure/
    └── QdrantVectorStoreAdapter.cs                # Score reranking
```

#### Implementazione

1. **Post-search score boost**:
   ```csharp
   // Dopo vector search, boost documenti espansione
   foreach (var result in searchResults)
   {
       if (expansionSharedGameIds.Contains(result.SharedGameId))
       {
           result.Score *= 1.3;  // 30% boost per espansioni
       }
   }
   // Re-sort by boosted score
   searchResults = searchResults.OrderByDescending(r => r.Score).ToList();
   ```

2. **System prompt addition**:
   ```
   REGOLA PRIORITA: Se una regola dell'espansione contraddice il gioco base,
   l'espansione PREVALE. Segnala esplicitamente quando citi una regola espansione
   che sovrascrive il base.
   ```

---

### Issue #5585 — Arbiter Mode (PARALLELO con #5588)

**Branch**: `feature/issue-5585-arbiter-mode` from `game-night-dev`
**Dipendenza**: #5580

#### Files da CREARE

```
apps/api/src/Api/BoundedContexts/KnowledgeBase/
├── Domain/
│   ├── Enums/SystemAgentType.cs                   # Aggiungere Arbiter
│   └── ValueObjects/ArbiterVerdictDto.cs          # CREARE
├── Application/
│   ├── Commands/
│   │   └── RequestArbiterVerdictCommand.cs        # CREARE
│   └── Handlers/
│       └── RequestArbiterVerdictCommandHandler.cs # CREARE
└── Routing/
    └── KnowledgeBaseEndpoints.cs                  # Nuovo endpoint

apps/web/src/lib/api/
├── clients/arbiterClient.ts                       # CREARE
└── schemas/arbiter.schemas.ts                     # CREARE
```

#### Implementazione

1. **`ArbiterVerdictDto`**:
   ```csharp
   public record ArbiterVerdictDto
   {
       public string Verdict { get; init; }               // Chi ha ragione
       public string Reasoning { get; init; }              // Spiegazione
       public double Confidence { get; init; }             // 0-1
       public bool IsConclusive { get; init; }             // confidence >= 0.85
       public List<CitationDto> Citations { get; init; }   // Fonti
       public List<DisputePositionDto> Positions { get; init; }
   }

   public record CitationDto(string DocumentTitle, string Section, int? PageNumber);
   public record DisputePositionDto(string PlayerName, string Position);
   ```

2. **`RequestArbiterVerdictCommand`**:
   ```csharp
   public record RequestArbiterVerdictCommand : IRequest<ArbiterVerdictDto>
   {
       public Guid LiveSessionId { get; init; }
       public string DisputeDescription { get; init; }
       public List<DisputePositionDto> Positions { get; init; }
   }
   ```

3. **Handler**:
   - Build `GameSessionContext` per la sessione
   - System prompt arbitro specializzato
   - RAG search con filtro sessione + expansion boost
   - Parse risposta LLM in struttura `ArbiterVerdictDto`
   - Se confidence < 0.70 → `IsConclusive = false`

4. **Endpoint**:
   - `POST /api/v1/live-sessions/{id}/arbiter` → `RequestArbiterVerdictCommand`

5. **Frontend client**:
   - `arbiterClient.requestVerdict(sessionId, dispute)` → `ArbiterVerdictDto`

---

## Batch 2: Frontend Features

### Issue #5582 — Game Night Playlist

**Branch**: `feature/issue-5582-game-night-playlist` from `game-night-dev`

#### Files da CREARE

**Backend**:
```
apps/api/src/Api/BoundedContexts/GameManagement/
├── Domain/
│   └── Entities/GameNightPlaylist.cs              # Aggregate
├── Application/
│   ├── Commands/Playlists/
│   │   ├── CreatePlaylistCommand.cs
│   │   ├── AddGameToPlaylistCommand.cs
│   │   ├── RemoveGameFromPlaylistCommand.cs
│   │   └── ReorderPlaylistGamesCommand.cs
│   └── Queries/Playlists/
│       ├── GetPlaylistQuery.cs
│       └── GetUserPlaylistsQuery.cs
├── Infrastructure/
│   └── Repositories/PlaylistRepository.cs
└── Routing/
    └── PlaylistEndpoints.cs
```

**Frontend**:
```
apps/web/src/
├── app/(authenticated)/game-nights/
│   ├── page.tsx                                   # Lista serate
│   ├── layout.tsx
│   ├── NavConfig.tsx                              # MiniNav config
│   ├── new/page.tsx                               # Crea serata
│   └── [id]/
│       ├── page.tsx                               # Dettaglio serata
│       └── play/page.tsx                          # Redirect a sessione
├── components/game-night/
│   ├── PlaylistCard.tsx                           # Card serata (da mockup)
│   ├── PlaylistGameItem.tsx                       # Gioco nella lista
│   ├── CreatePlaylistModal.tsx                    # Modal creazione
│   └── PlaylistStats.tsx                          # Statistiche
├── lib/api/
│   ├── clients/playlistClient.ts
│   └── schemas/playlist.schemas.ts
└── lib/stores/playlistStore.ts                    # Zustand store
```

#### Dettaglio Componenti (da mockup `game-night-playlist.html`)

- **PlaylistCard**: Card glassmorphism con stato (live/pianificata/pausa/completata), avatar giocatori, giochi, timer
- **Sezioni**: In corso (amber pulsante), Prossime, In pausa (stato salvato visibile), Completate
- **Bottom Nav**: Home, Libreria, **Serate** (active), Chat
- **Stats**: Serate totali, vittorie, ore giocate

---

### Issue #5584 — Rules Explainer

**Branch**: `feature/issue-5584-rules-explainer` from `game-night-dev`

#### Files da CREARE

```
apps/web/src/components/game-night/
├── RulesExplainer.tsx                             # Componente principale
├── RulesTabSummary.tsx                            # Tab sommario
├── RulesTabMechanics.tsx                          # Grid meccaniche
├── RulesTabPhases.tsx                             # Timeline fasi
├── RulesTabVictory.tsx                            # Condizioni vittoria
├── RulesTabFaq.tsx                                # FAQ espandibili
├── RulesTabExpansion.tsx                          # Regole espansione
└── RulesSourceBadge.tsx                           # Badge PDF/No PDF

apps/web/src/lib/api/
├── clients/rulebookClient.ts                      # Fetch RulebookAnalysis
└── schemas/rulebook.schemas.ts                    # Zod schemas
```

#### Dati da RulebookAnalysis

Il componente consuma:
- `summary` → Tab Sommario
- `keyMechanics[]` → Tab Meccaniche (grid cards)
- `gamePhases[]` → Tab Fasi (timeline verticale)
- `victoryConditions` → Tab Vittoria
- `commonQuestions[]` / `generatedFaqs[]` → Tab FAQ
- `resources[]` → Sezione Risorse

---

### Issue #5583 — Setup Wizard

**Branch**: `feature/issue-5583-setup-wizard` from `game-night-dev`

#### Files da MODIFICARE/CREARE

```
apps/web/src/components/game-night/
├── SetupWizard.tsx                                # Wizard principale (4 step)
├── SetupStepGames.tsx                             # Step 1: Scegli giochi
├── SetupStepPlayers.tsx                           # Step 2: Giocatori
├── SetupStepChecklist.tsx                         # Step 3: Setup fisico (AI)
├── SetupStepReady.tsx                             # Step 4: Riepilogo
├── SetupChecklistItem.tsx                         # Item checklist
└── ExpansionSelector.tsx                          # Selettore espansioni

apps/web/src/app/(authenticated)/game-nights/
└── [id]/setup/page.tsx                            # Pagina wizard
```

#### Flusso (da mockup `game-night-setup-wizard.html`)

1. **Scegli Giochi**: Cards dalla libreria con badge PDF/No PDF, selettore espansioni con checkbox
2. **Giocatori**: Lista con avatar, colore, ruolo (Host/Player), max giocatori check
3. **Setup Fisico**: Checklist AI-generated da `RulebookAnalysis.GamePhases[Setup]` + espansioni separate
4. **Tutto Pronto**: Riepilogo con features disponibili (Regole AI, Arbitro, Punteggi, Pausa)

---

### Issue #5586 — Pre-cache + Graceful Degradation

**Branch**: `feature/issue-5586-pre-cache-degradation` from `game-night-dev`

#### Files da CREARE

```
apps/web/src/lib/
├── cache/
│   ├── gameNightCache.ts                          # IndexedDB/localStorage cache
│   ├── rulebookCacheService.ts                    # Pre-fetch + store
│   └── offlineFallback.ts                         # Fallback logic
└── hooks/
    └── useRulebookCache.ts                        # React hook
```

#### Implementazione

1. **`gameNightCache.ts`**: Wrapper IndexedDB (idb-keyval) per:
   - `cacheRulebookAnalysis(sharedGameId, data)` → TTL 24h
   - `getCachedRulebook(sharedGameId)` → return cached | null
   - `clearExpiredCache()` → cleanup

2. **`rulebookCacheService.ts`**: Su session start:
   - Fetch `RulebookAnalysis` per tutti i giochi (base + espansioni)
   - Salva in IndexedDB
   - Segnala progresso pre-cache

3. **`useRulebookCache` hook**:
   ```typescript
   function useRulebookCache(sharedGameId: string) {
     // 1. Try cache first (instant)
     // 2. If miss, fetch API
     // 3. If API fails, return stale cache + warning
     // 4. If no cache at all, return degraded FAQ-only mode
   }
   ```

4. **Graceful degradation levels**:
   - **Full**: API live → risposte RAG complete
   - **Cached**: API down → risposte da cache (FAQ, meccaniche, fasi)
   - **Minimal**: Nessun cache → messaggio "Connessione necessaria"

---

## Batch 3: Integration & Tests

### Issue #5587 — Live Game Session UI

**Branch**: `feature/issue-5587-live-session-ui` from `game-night-dev`

#### Files da CREARE/MODIFICARE

```
apps/web/src/app/(authenticated)/sessions/[id]/
├── play/
│   ├── page.tsx                                   # Live session page
│   └── layout.tsx                                 # Con NavigationProvider
│
apps/web/src/components/game-night/
├── ActiveSessionBanner.tsx                        # Banner amber nel TopNav
├── CompactScoreboard.tsx                          # Scoreboard mobile
├── GameActionBar.tsx                              # 4-button bottom bar
├── InGameChatWidget.tsx                           # Chat compatto + espandibile
├── ArbiterCard.tsx                                # Card verdetto indigo
├── SessionActivityFeed.tsx                        # Feed attivita recente
├── PauseConfirmation.tsx                          # Modal pausa con stato salvato
└── ChatPanel.tsx                                  # Panel chat fullscreen

apps/web/src/components/layout/
└── Navbar.tsx                                     # MODIFICARE: aggiungere ActiveSessionBanner
```

#### Layout (da mockup `game-night-live-session.html`)

**Mobile (< md)**:
```
┌─────────────────────────────┐
│ TopNav + ActiveSessionBanner│
│ MiniNav (tabs)              │
├─────────────────────────────┤
│                             │
│  Tab Content (scrollable)   │
│  - Partita (default)        │
│  - Regole                   │
│  - Arbitro                  │
│  - Punteggi                 │
│  - Strumenti                │
│                             │
├─────────────────────────────┤
│ GameActionBar (4 bottoni)   │
└─────────────────────────────┘
```

**Desktop (>= lg)**:
```
┌─────────────────────────────────────────┐
│ TopNav + ActiveSessionBanner            │
├────────┬────────────────────┬───────────┤
│ToolRail│  Main Content      │ Chat      │
│(left)  │  (Scoreboard +     │ Panel     │
│        │   Activity Feed)   │ (right)   │
│        │                    │           │
├────────┴────────────────────┴───────────┤
│ NavActionBar (contextual)               │
└─────────────────────────────────────────┘
```

#### Componenti Chiave

1. **ActiveSessionBanner**: `glass-amber`, pulse indicator, tap = naviga alla sessione
2. **CompactScoreboard**: Crown shimmer leader, score bump animation, trend arrows
3. **GameActionBar**: Grid 2x2 (Regole amber, Arbitro indigo, Punteggi emerald, Altro slate)
4. **InGameChatWidget**: Preview 2 righe ultima risposta + input sempre visibile
5. **ArbiterCard**: `glass-indigo`, verdetto, posizioni disputanti, citazioni con pagina
6. **PauseConfirmation**: Fullscreen overlay con stato salvato, punteggi snapshot

---

### Issue #5589 — Tests

**Branch**: `feature/issue-5589-game-night-tests` from `game-night-dev`

#### Backend Tests

```
apps/api/tests/Api.Tests/BoundedContexts/GameManagement/
├── Unit/
│   ├── GameSessionOrchestratorServiceTests.cs
│   ├── GameStateSnapshotTests.cs
│   ├── GameNightPlaylistTests.cs
│   └── SessionPausedSnapshotHandlerTests.cs
├── Integration/
│   ├── BuildGameSessionContextCommandTests.cs
│   ├── SessionAwareRagTests.cs
│   ├── ArbiterVerdictTests.cs
│   ├── ExpansionBoostTests.cs
│   ├── SnapshotEndpointTests.cs
│   └── PlaylistEndpointTests.cs

apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/
├── Unit/
│   ├── ArbiterVerdictHandlerTests.cs
│   └── ExpansionBoostServiceTests.cs
└── Integration/
    └── SessionAwareVectorSearchTests.cs
```

#### Frontend Tests

```
apps/web/src/components/game-night/__tests__/
├── ActiveSessionBanner.test.tsx
├── CompactScoreboard.test.tsx
├── GameActionBar.test.tsx
├── InGameChatWidget.test.tsx
├── ArbiterCard.test.tsx
├── RulesExplainer.test.tsx
├── SetupWizard.test.tsx
├── PlaylistCard.test.tsx
├── PauseConfirmation.test.tsx
└── SessionActivityFeed.test.tsx

apps/web/src/lib/__tests__/
├── playlistStore.test.ts
├── arbiterClient.test.ts
├── playlistClient.test.ts
├── rulebookClient.test.ts
└── gameNightCache.test.ts
```

#### E2E Tests (Playwright)

```
apps/web/e2e/
├── game-night-playlist.spec.ts                    # CRUD serate
├── game-night-setup.spec.ts                       # Wizard flow
├── game-night-live-session.spec.ts                # Session interactions
└── game-night-arbiter.spec.ts                     # Disputa flow
```

---

## Execution Timeline

```
Giorno 1-2:  [#5579 GameSessionContext] ──────────┐
             [#5581 Auto-snapshot]     ─── PARALLEL│
                                                   ↓
Giorno 2-3:  [#5580 Session-aware RAG] ───────────┐
                                                   ↓
Giorno 3-4:  [#5588 Expansion boost] ─── PARALLEL ┐
             [#5585 Arbiter Mode]    ─── PARALLEL  │
                                                   ↓
Giorno 4-5:  [#5582 Playlist BE+FE] ─── PARALLEL  ┐
             [#5584 Rules Explainer] ─── PARALLEL  │
                                                   ↓
Giorno 5-6:  [#5583 Setup Wizard]   ─── PARALLEL  ┐
             [#5586 Pre-cache]      ─── PARALLEL   │
                                                   ↓
Giorno 6-7:  [#5587 Live Session UI] ─────────────┤
                                                   ↓
Giorno 7-8:  [#5589 Tests]           ─────────────┘
```

**Totale stimato**: ~8 giorni lavorativi (4 sprint da 2 giorni)

---

## Branch Strategy

```
main-dev
  └── game-night-dev  (integration branch — PR a main-dev alla fine)
       ├── feature/issue-5579-game-session-context     → PR a game-night-dev
       ├── feature/issue-5581-auto-snapshot-pause       → PR a game-night-dev
       ├── feature/issue-5580-session-aware-rag         → PR a game-night-dev
       ├── feature/issue-5588-expansion-rag-boost       → PR a game-night-dev
       ├── feature/issue-5585-arbiter-mode              → PR a game-night-dev
       ├── feature/issue-5582-game-night-playlist       → PR a game-night-dev
       ├── feature/issue-5584-rules-explainer           → PR a game-night-dev
       ├── feature/issue-5583-setup-wizard              → PR a game-night-dev
       ├── feature/issue-5586-pre-cache-degradation     → PR a game-night-dev
       ├── feature/issue-5587-live-session-ui           → PR a game-night-dev
       └── feature/issue-5589-game-night-tests          → PR a game-night-dev
```

**Alla fine**: PR `game-night-dev` → `main-dev` (mega PR con tutto l'epic)

---

## Validation Gates per Issue

Ogni issue segue il flusso `/implementa`:

| Gate | Backend | Frontend |
|------|---------|----------|
| Build | `dotnet build` | `pnpm build` |
| Test | `dotnet test` (≥90%) | `pnpm test` (≥85%) |
| Lint | built-in | `pnpm lint` + `pnpm typecheck` |
| Review | `/code-review` score ≥80% | `/code-review` score ≥80% |
| DoD | Issue checkboxes | Issue checkboxes |

---

## Risk Assessment

| Risk | Probabilita | Impatto | Mitigazione |
|------|-------------|---------|-------------|
| RulebookAnalysis 3 handlers mancanti | Alta | Medio | Setup Wizard/Rules Explainer funzionano con dati parziali |
| Qdrant filter SharedGameId non testato | Media | Alto | Test integration con Testcontainers |
| Mobile performance (glassmorphism) | Media | Medio | Fallback CSS senza backdrop-blur |
| Chat SSE in sessione live | Bassa | Alto | Pre-cache FAQ come fallback |
| Conflitti merge su game-night-dev | Media | Basso | PR piccole, merge frequenti |

---

## Primo Step Concreto

Creare branch `game-night-dev` da `main-dev`, poi implementare **#5579** e **#5581** in parallelo:

```bash
git checkout main-dev && git pull
git checkout -b game-night-dev
git push -u origin game-night-dev
git config branch.game-night-dev.parent main-dev

# Parallel: due feature branch
git checkout -b feature/issue-5579-game-session-context
git config branch.feature/issue-5579-game-session-context.parent game-night-dev
```
