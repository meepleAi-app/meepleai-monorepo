# User Library - Flussi API

## Panoramica

Il bounded context User Library gestisce la libreria personale dei giochi, configurazione agenti, PDF custom, sharing, labels, collections, wishlist e giochi privati.

---

## 1. Libreria Core

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/library` | `GetUserLibraryQuery` | `page?, pageSize?, favoritesOnly?, stateFilter[], sortBy?, sortDescending?` | `[S]` |
| GET | `/library/stats` | `GetLibraryStatsQuery` | — | `[S]` |
| GET | `/library/quota` | `GetLibraryQuotaQuery` | — | `[S]` |
| POST | `/library/games/{gameId}` | `AddGameToLibraryCommand` | `{ notes?, isFavorite? }` | `[S]` |
| DELETE | `/library/games/{gameId}` | `RemoveGameFromLibraryCommand` | — | `[S]` |
| PATCH | `/library/games/{gameId}` | `UpdateLibraryEntryCommand` | `{ notes?, isFavorite? }` | `[S]` |
| GET | `/library/games/{gameId}/status` | `GetGameInLibraryStatusQuery` | — | `[S]` |

### Flusso Aggiunta Gioco

```
GET /shared-games?search="Catan"
       │
       ▼ { id: "abc-123", title: "Catan" }
       │
POST /library/games/abc-123 { isFavorite: true }
       │
       ▼ 201 Created
       │
GET /library?favoritesOnly=true
       │
       ▼ { games: [{ title: "Catan", isFavorite: true }] }
```

---

## 2. Agent Configuration

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/library/games/{gameId}/agent-config` | `GetGameAgentConfigQuery` | — | `[S]` |
| PUT | `/library/games/{gameId}/agent` | `ConfigureGameAgentCommand` | AgentConfigDto | `[S]` |
| DELETE | `/library/games/{gameId}/agent` | `ResetGameAgentCommand` | — | `[S]` |
| POST | `/library/games/{gameId}/agent-config` | `SaveAgentConfigCommand` | Config data | `[S]` |
| POST | `/library/games/{gameId}/agent` | `CreateGameAgentCommand` | Agent definition | `[S]` |

---

## 3. PDF Management

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/library/games/{gameId}/pdf` | `UploadCustomGamePdfCommand` | `{ pdfUrl, fileSizeBytes, originalFileName }` | `[S]` |
| DELETE | `/library/games/{gameId}/pdf` | `ResetGamePdfCommand` | — | `[S]` |
| GET | `/library/games/{gameId}/pdfs` | `GetGamePdfsQuery` | — | `[S]` |
| GET | `/library/{entryId}/pdf/progress` | SSE stream | — | `[S]` SSE |
| DELETE | `/library/entries/{entryId}/private-pdf` | `RemovePrivatePdfCommand` | — | `[S]` |

### Flusso Upload PDF Custom

```
POST /library/games/{gameId}/pdf { pdfUrl, fileSizeBytes }
       │
       ▼ Upload avviato
       │
GET /library/{entryId}/pdf/progress (SSE)
       │
       ▼ events: progress updates
       │
GET /library/games/{gameId}/pdfs
       │
       ▼ Lista PDF associati
```

---

## 4. Game Detail (Epic #2823)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/library/games/{gameId}` | `GetGameDetailQuery` | — | `[S]` |
| GET | `/library/games/{gameId}/checklist` | `GetGameChecklistQuery` | `includeWizard?` | `[S]` |
| PUT | `/library/games/{gameId}/state` | `UpdateGameStateCommand` | `{ newState, stateNotes }` | `[S]` |
| POST | `/library/games/{gameId}/sessions` | `RecordGameSessionCommand` | Session data | `[S]` |
| POST | `/library/games/{gameId}/remind-loan` | `SendLoanReminderCommand` | `{ customMessage? }` | `[S]` |

---

## 5. Library Sharing

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/library/share` | `CreateLibraryShareLinkCommand` | `{ privacyLevel, includeNotes, expiresAt }` | `[S]` |
| GET | `/library/share` | `GetLibraryShareLinkQuery` | — | `[S]` |
| PATCH | `/library/share/{shareToken}` | `UpdateLibraryShareLinkCommand` | `{ privacyLevel?, includeNotes?, expiresAt? }` | `[S]` |
| DELETE | `/library/share/{shareToken}` | `RevokeLibraryShareLinkCommand` | — | `[S]` |
| GET | `/library/shared/{shareToken}` | `GetSharedLibraryQuery` | — | `[P]` |

### Flusso Sharing

```
POST /library/share { privacyLevel: "public", includeNotes: false }
       │
       ▼ { shareToken: "abc123", shareUrl: "..." }
       │
  Condividi link con amici
       │
       ▼
GET /library/shared/abc123 (pubblico, no auth)
       │
       ▼ { games: [...], ownerDisplayName: "..." }
```

---

## 6. Labels (Epic #3511)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/library/labels` | `GetLabelsQuery` | — | `[S]` |
| POST | `/library/labels` | `CreateCustomLabelCommand` | `{ name, color }` | `[S]` |
| DELETE | `/library/labels/{labelId}` | `DeleteCustomLabelCommand` | — | `[S]` |
| GET | `/library/games/{gameId}/labels` | `GetGameLabelsQuery` | — | `[S]` |
| POST | `/library/games/{gameId}/labels/{labelId}` | `AddLabelToGameCommand` | — | `[S]` |
| DELETE | `/library/games/{gameId}/labels/{labelId}` | `RemoveLabelFromGameCommand` | — | `[S]` |

### Flusso Labels

```
POST /library/labels { name: "Da provare", color: "#FF5733" }
       │
       ▼ { labelId }
       │
POST /library/games/{gameId}/labels/{labelId}
       │
       ▼ Label assegnata
       │
GET /library?stateFilter=["Da provare"]
```

---

## 7. Generic Collections (Issue #4263)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/collections/{entityType}/{entityId}/status` | `GetCollectionStatusQuery` | — | `[S]` |
| POST | `/collections/{entityType}/{entityId}` | `AddToCollectionCommand` | `{ notes?, isFavorite? }` | `[S]` |
| DELETE | `/collections/{entityType}/{entityId}` | `RemoveFromCollectionCommand` | — | `[S]` |

### Bulk Operations (Issue #4268)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/collections/{entityType}/bulk-add` | `BulkAddToCollectionCommand` | `{ entityIds[], notes?, isFavorite? }` | `[S]` |
| DELETE | `/collections/{entityType}/bulk-remove` | `BulkRemoveFromCollectionCommand` | `{ entityIds[] }` | `[S]` |
| POST | `/collections/{entityType}/bulk-associated-data` | `GetBulkCollectionAssociatedDataQuery` | `{ entityIds[] }` | `[S]` |

---

## 8. Wishlist

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/wishlist` | `GetWishlistQuery` | — | `[S]` |
| GET | `/wishlist/highlights` | `GetWishlistHighlightsQuery` | — | `[S]` |
| POST | `/wishlist` | `AddToWishlistCommand` | `{ gameId, notes?, priority? }` | `[S]` |
| PUT | `/wishlist/{id}` | `UpdateWishlistItemCommand` | `{ notes?, priority? }` | `[S]` |
| DELETE | `/wishlist/{id}` | `RemoveFromWishlistCommand` | — | `[S]` |

---

## 9. Private Games

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/private-games` | `AddPrivateGameCommand` | Game data | `[S]` |
| GET | `/private-games/{id}` | `GetPrivateGameQuery` | — | `[S]` |
| PUT | `/private-games/{id}` | `UpdatePrivateGameCommand` | Updated data | `[S]` |
| DELETE | `/private-games/{id}` | `DeletePrivateGameCommand` | — | `[S]` |
| POST | `/private-games/{id}/propose-to-catalog` | `ProposePrivateGameCommand` | Proposal data | `[S]` |
| POST | `/private-games/{id}/link-agent/{agentId}` | `LinkAgentToPrivateGameCommand` | — | `[S]` |
| DELETE | `/private-games/{id}/unlink-agent` | `UnlinkAgentFromPrivateGameCommand` | — | `[S]` |

### Flusso Private Game → Catalogo

```
POST /private-games { title: "Il mio gioco", ... }
       │
       ▼ { id }
       │
POST /private-games/{id}/propose-to-catalog
       │
       ▼ Share Request creata
       │
Admin: POST /admin/share-requests/{id}/approve
       │
       ▼ Gioco pubblicato nel catalogo condiviso
```

---

## 10. Proposal Migration

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/migrations/pending` | `GetPendingMigrationsQuery` | — | `[S]` |
| POST | `/migrations/{id}/choose` | `HandleMigrationChoiceCommand` | `{ choice }` (KeepPrivate/MigrateToShared) | `[S]` |

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 842 |
| **Passati** | 842 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 3s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Library Core | `AddGameToLibraryTests.cs`, `RemoveGameTests.cs`, `UpdateEntryTests.cs` | Passato |
| Agent Configuration | `ConfigureGameAgentTests.cs`, `SaveAgentConfigTests.cs` | Passato |
| PDF Management | `UploadCustomGamePdfTests.cs`, `ResetGamePdfTests.cs` | Passato |
| Game Detail | `GetGameDetailTests.cs`, `GetGameChecklistTests.cs` | Passato |
| Library Sharing | `CreateLibraryShareLinkTests.cs`, `GetSharedLibraryTests.cs` | Passato |
| Labels | `CreateCustomLabelTests.cs`, `AddLabelToGameTests.cs` | Passato |
| Collections | `AddToCollectionTests.cs`, `BulkAddTests.cs` | Passato |
| Wishlist | `AddToWishlistTests.cs`, `UpdateWishlistItemTests.cs` | Passato |
| Private Games | `AddPrivateGameTests.cs`, `ProposePrivateGameTests.cs` | Passato |
| Proposal Migration | `HandleMigrationChoiceTests.cs` | Passato |
| Domain Entities | UserGame, Collection, Wishlist (17 file) | Passato |
| Validators | 10 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*
