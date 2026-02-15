# Game Management - Flussi API

## Panoramica

Il bounded context Game Management gestisce il ciclo di vita dei giochi, delle sessioni di gioco, dello stato di gioco, dei play record e dei conflict FAQ.

---

## 1. Game CRUD

### Lettura (Public)

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/games` | `GetAllGamesQuery` | `search?, page?, pageSize?` | `[P]` |
| GET | `/games/{id}` | `GetGameByIdQuery` | — | `[P]` |
| GET | `/games/{id}/similar` | `GetSimilarGamesQuery` | `limit?, minSimilarity?` | `[P]` |
| GET | `/games/search` | `SearchGamesQuery` | `q` (autocomplete) | `[S]` |

### Lettura (Autenticata)

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/games/{id}/details` | `GetGameDetailsQuery` | — | `[S]` |
| GET | `/games/{id}/rules` | `GetRuleSpecsQuery` | — | `[S]` |
| GET | `/games/{id}/agents` | `GetAllAgentsQuery` | — | `[S]` |
| GET | `/games/{id}/sessions` | `GetGameSessionsQuery` | `pageNumber?, pageSize?` | `[S]` |

### Scrittura (Admin/Editor)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/games` | `CreateGameCommand` | `{ title, publisher, yearPublished, minPlayers, maxPlayers, minPlayTimeMinutes, maxPlayTimeMinutes, iconUrl?, imageUrl?, bggId?, sharedGameId? }` | `[A/E]` |
| PUT | `/games/{id}` | `UpdateGameCommand` | `{ title, publisher, yearPublished, minPlayers, maxPlayers, ... }` | `[A/E]` |
| POST | `/games/upload-image` | `UploadGameImageCommand` | Multipart: `file, gameId, imageType` | `[A/E]` |
| PUT | `/games/{id}/publish` | `PublishGameCommand` | `{ status }` | `[A]` |

---

## 2. Game Session Lifecycle

### Creazione e Gestione

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/sessions` | `StartGameSessionCommand` | `{ gameId, players[] }` | `[S]` |
| POST | `/sessions/{id}/players` | `AddPlayerToSessionCommand` | `{ playerName, playerOrder, color }` | `[S]` |
| POST | `/sessions/{id}/pause` | `PauseGameSessionCommand` | — | `[S]` |
| POST | `/sessions/{id}/resume` | `ResumeGameSessionCommand` | — | `[S]` |
| POST | `/sessions/{id}/complete` | `CompleteGameSessionCommand` | `{ winnerName? }` | `[S]` |
| POST | `/sessions/{id}/abandon` | `AbandonGameSessionCommand` | — | `[S]` |
| POST | `/sessions/{id}/end` | `EndGameSessionCommand` | `{ winnerName? }` | `[S]` |

### Flusso Session Lifecycle

```
                          ┌──────────┐
                          │  START   │
                          │  POST    │
                          └────┬─────┘
                               │
                    ┌──────────▼──────────┐
                    │     ACTIVE          │
                    │  Add Players        │
                    │  Initialize State   │
                    └──┬──────────┬───────┘
                       │          │
              ┌────────▼───┐  ┌──▼────────┐
              │   PAUSE    │  │  ABANDON   │
              │   POST     │  │  POST      │
              └────┬───────┘  └────────────┘
                   │
              ┌────▼───────┐
              │   RESUME   │
              │   POST     │
              └────┬───────┘
                   │
              ┌────▼───────┐
              │  COMPLETE  │
              │  POST      │
              └────────────┘
```

### Query Sessioni

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/sessions/{id}` | `GetGameSessionByIdQuery` | — | `[S]` |
| GET | `/games/{gameId}/sessions/active` | `GetActiveSessionsByGameQuery` | — | `[S]` |
| GET | `/sessions/active` | `GetActiveSessionsQuery` | `limit?, offset?` | `[S]` |
| GET | `/sessions/history` | `GetSessionHistoryQuery` | `gameId?, startDate?, endDate?, limit?, offset?` | `[S]` |
| GET | `/sessions/statistics` | `GetSessionStatsQuery` | `gameId?, startDate?, endDate?, topPlayersLimit?` | `[S]` |

---

## 3. Game Session State

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/sessions/{sessionId}/state/initialize` | `InitializeGameStateCommand` | `{ templateId, initialState }` | `[S]` |
| GET | `/sessions/{sessionId}/state` | `GetGameStateQuery` | — | `[S]` |
| PATCH | `/sessions/{sessionId}/state` | `UpdateGameStateCommand` | `{ newState }` (JSON) | `[S]` |
| POST | `/sessions/{sessionId}/state/snapshots` | `CreateStateSnapshotCommand` | `{ turnNumber, description }` | `[S]` |
| GET | `/sessions/{sessionId}/state/snapshots` | `GetStateSnapshotsQuery` | — | `[S]` |
| POST | `/sessions/{sessionId}/state/restore/{snapshotId}` | `RestoreStateSnapshotCommand` | — | `[S]` |

### Flusso Game State con Snapshots

```
POST /state/initialize { templateId, initialState }
       │
       ▼
PATCH /state { newState }  ◀──────────┐
       │                                │
       ▼                                │
POST /state/snapshots { turnNumber }   │
       │                                │
       ▼                                │
  [Giocatore fa errore]                │
       │                                │
       ▼                                │
POST /state/restore/{snapshotId} ──────┘
```

---

## 4. Move Suggestions (Player Mode)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/sessions/{sessionId}/suggest-move` | `SuggestMoveCommand` | `{ agentId, query }` | `[S]` |
| POST | `/sessions/{sessionId}/apply-suggestion` | `ApplySuggestionCommand` | `{ suggestionId, stateChanges }` | `[S]` |

---

## 5. Play Record (Issue #3888-3890)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/play-records` | `CreatePlayRecordCommand` | `{ gameId?, gameName, sessionDate, visibility, groupId?, scoringDimensions?, dimensionUnits? }` | `[S]` |
| POST | `/play-records/{recordId}/players` | `AddPlayerToRecordCommand` | `{ userId?, displayName }` | `[S]` |
| POST | `/play-records/{recordId}/scores` | `RecordScoreCommand` | `{ playerId, dimension, value, unit? }` | `[S]` |
| POST | `/play-records/{recordId}/start` | `StartPlayRecordCommand` | — | `[S]` |
| POST | `/play-records/{recordId}/complete` | `CompletePlayRecordCommand` | `{ manualDuration? }` | `[S]` |
| PUT | `/play-records/{recordId}` | `UpdatePlayRecordCommand` | `{ sessionDate?, notes?, location? }` | `[S]` |
| GET | `/play-records/{recordId}` | `GetPlayRecordQuery` | — | `[S]` |
| GET | `/play-records/history` | `GetUserPlayHistoryQuery` | `page?, pageSize?, gameId?` | `[S]` |
| GET | `/play-records/statistics` | `GetPlayerStatisticsQuery` | `startDate?, endDate?` | `[S]` |

### Flusso Play Record

```
POST /play-records { gameName, sessionDate }
       │
       ▼ { recordId }
       │
POST /play-records/{id}/players { displayName: "Alice" }
POST /play-records/{id}/players { displayName: "Bob" }
       │
       ▼
POST /play-records/{id}/start
       │
       ▼ (partita in corso...)
       │
POST /play-records/{id}/scores { playerId, dimension: "points", value: 42 }
POST /play-records/{id}/scores { playerId, dimension: "points", value: 38 }
       │
       ▼
POST /play-records/{id}/complete { manualDuration? }
       │
       ▼
GET /play-records/statistics
```

---

## 6. Rule Conflict FAQ

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/games/{gameId}/rule-conflict-faqs` | `GetAllRuleConflictFaqsForGameQuery` | `page?, pageSize?` | `[P]` |
| GET | `/games/{gameId}/rule-conflict-faqs/pattern/{pattern}` | `GetRuleConflictFaqByPatternQuery` | — | `[P]` |
| POST | `/games/{gameId}/rule-conflict-faqs` | `CreateRuleConflictFaqCommand` | `{ conflictType, pattern, resolution, priority }` | `[A/E]` |
| PUT | `/games/{gameId}/rule-conflict-faqs/{id}` | `UpdateRuleConflictFaqResolutionCommand` | `{ resolution }` | `[A/E]` |
| DELETE | `/games/{gameId}/rule-conflict-faqs/{id}` | `DeleteRuleConflictFaqCommand` | — | `[A]` |

---

## 7. Admin Configuration

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/config/game-library-limits` | `GetGameLibraryLimitsQuery` | — | `[A]` |
| PUT | `/admin/config/game-library-limits` | `UpdateGameLibraryLimitsCommand` | `{ freeTierLimit, normalTierLimit, premiumTierLimit }` | `[A]` |

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 1,300 |
| **Passati** | 1,300 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 4s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Game CRUD | `CreateGameCommandHandlerTests.cs`, `UpdateGameTests.cs` | Passato |
| Game Session Lifecycle | `StartGameSessionTests.cs`, `PauseTests.cs`, `ResumeTests.cs`, `CompleteTests.cs`, `AbandonTests.cs` | Passato |
| Game State | `InitializeGameStateTests.cs`, `UpdateGameStateTests.cs`, `CreateStateSnapshotTests.cs`, `RestoreSnapshotTests.cs` | Passato |
| Play Record | `CreatePlayRecordTests.cs`, `AddPlayerToRecordTests.cs`, `RecordScoreTests.cs`, `CompletePlayRecordTests.cs` | Passato |
| Move Suggestions | `SuggestMoveTests.cs`, `ApplySuggestionTests.cs` | Passato |
| Rule Conflict FAQ | `CreateRuleConflictFaqTests.cs`, `UpdateResolutionTests.cs` | Passato |
| Domain Entities | `Game.cs`, `GameSession.cs`, `Player.cs` (19 file) | Passato |
| Validators | 15 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*
