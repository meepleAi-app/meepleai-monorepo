# Shared Game Catalog - Flussi API

## Panoramica

Il bounded context Shared Game Catalog gestisce il catalogo condiviso di giochi da tavolo con workflow di approvazione, integrazione BGG, FAQ, errata, documenti e contribuzioni della community.

---

## 1. Ricerca Pubblica

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/shared-games` | `SearchSharedGamesQuery` | `search?, categoryIds[], mechanicIds[], minPlayers?, maxPlayers?, maxPlayingTime?, pageNumber?, pageSize?, sortBy?, sortDescending?` | `[P]` |
| GET | `/shared-games/{id}` | `GetSharedGameByIdQuery` | — | `[P]` |
| GET | `/shared-games/categories` | `GetGameCategoriesQuery` | — | `[P]` (cache 24h) |
| GET | `/shared-games/mechanics` | `GetGameMechanicsQuery` | — | `[P]` (cache 24h) |

---

## 2. Admin CRUD

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/shared-games` | `GetFilteredSharedGamesQuery` | `status?, search?, sortBy?, submittedBy?, pageNumber?, pageSize?` | `[A/E]` |
| POST | `/admin/shared-games` | `CreateSharedGameCommand` | `{ title, yearPublished, description, minPlayers, maxPlayers, playingTimeMinutes, minAge, complexityRating, averageRating, imageUrl, thumbnailUrl, rules, bggId }` | `[A/E]` |
| PUT | `/admin/shared-games/{id}` | `UpdateSharedGameCommand` | Same as create | `[A/E]` |
| DELETE | `/admin/shared-games/{id}` | `DeleteSharedGameCommand` | — | `[A]` |
| POST | `/admin/shared-games/{id}/archive` | `ArchiveSharedGameCommand` | — | `[A]` |

---

## 3. Workflow di Pubblicazione

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/shared-games/{id}/submit-for-approval` | `SubmitSharedGameForApprovalCommand` | — | `[A/E]` |
| POST | `/admin/shared-games/{id}/approve-publication` | `ApproveSharedGamePublicationCommand` | — | `[A]` |
| POST | `/admin/shared-games/{id}/reject-publication` | `RejectSharedGamePublicationCommand` | `{ reason }` | `[A]` |
| POST | `/admin/shared-games/batch-approve` | `BatchApproveGamesCommand` | `{ gameIds[] }` | `[A]` |
| POST | `/admin/shared-games/batch-reject` | `BatchRejectGamesCommand` | `{ gameIds[], reason }` | `[A]` |
| GET | `/admin/shared-games/pending-approvals` | `GetPendingApprovalGamesQuery` | `pageNumber?, pageSize?` | `[A/E]` |
| GET | `/admin/shared-games/approval-queue` | `GetApprovalQueueQuery` | `urgency?, submitter?, hasPdfs?` | `[A/E]` |

### Flusso Pubblicazione

```
Draft ──▶ Submit for Approval ──▶ Pending Review
                                       │
                              ┌────────┼────────┐
                              ▼        │        ▼
                          Approved     │     Rejected
                              │        │        │
                              ▼        │        ▼
                          Published    │   Back to Draft
                                       │
                                       ▼
                                   Archived
```

### Delete Requests

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/shared-games/pending-deletes` | `GetPendingDeleteRequestsQuery` | `pageNumber?, pageSize?` | `[A]` |
| POST | `/admin/shared-games/approve-delete/{requestId}` | `ApproveDeleteRequestCommand` | — | `[A]` |
| POST | `/admin/shared-games/reject-delete/{requestId}` | `RejectDeleteRequestCommand` | `{ reason }` | `[A]` |

---

## 4. BGG Integration

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| POST | `/admin/shared-games/import-bgg` | `ImportGameFromBggCommand` | `{ bggId }` | `[A/E]` |
| GET | `/admin/shared-games/bgg/search` | `SearchBggGamesQuery` | `query, exact?` | `[A/E]` |
| GET | `/admin/shared-games/bgg/check-duplicate/{bggId}` | `CheckBggDuplicateQuery` | — | `[A/E]` |
| PUT | `/admin/shared-games/{id}/update-from-bgg` | `UpdateSharedGameFromBggCommand` | `{ fieldsToUpdate[] }` | `[A/E]` |
| POST | `/admin/shared-games/bulk-import` | `BulkImportGamesCommand` | `{ games[] }` (max 500) | `[A]` |

### Flusso Import BGG

```
GET /bgg/search?query="Catan"
       │
       ▼ { bggId: 13, title: "Catan", ... }
       │
GET /bgg/check-duplicate/13
       │
       ▼ { isDuplicate: false }
       │
POST /import-bgg { bggId: 13 }
       │
       ▼ Game creato con dati BGG
       │
POST /{id}/submit-for-approval → Workflow pubblicazione
```

---

## 5. FAQ Management

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/games/{gameId}/faqs` | `GetGameFaqsQuery` | `limit?, offset?` | `[P]` |
| POST | `/faqs/{faqId}/upvote` | `UpvoteFaqCommand` | — | `[P]` |
| POST | `/admin/shared-games/{id}/faq` | `AddGameFaqCommand` | `{ question, answer, order }` | `[A/E]` |
| PUT | `/admin/shared-games/{id}/faq/{faqId}` | `UpdateGameFaqCommand` | `{ question, answer, order }` | `[A/E]` |
| DELETE | `/admin/shared-games/{id}/faq/{faqId}` | `DeleteGameFaqCommand` | — | `[A/E]` |

---

## 6. Errata Management

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/shared-games/{id}/errata` | `AddGameErrataCommand` | `{ description, pageReference, publishedDate }` | `[A/E]` |
| PUT | `/admin/shared-games/{id}/errata/{errataId}` | `UpdateGameErrataCommand` | Same as create | `[A/E]` |
| DELETE | `/admin/shared-games/{id}/errata/{errataId}` | `DeleteGameErrataCommand` | — | `[A/E]` |

---

## 7. Document Management

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/shared-games/{id}/documents` | `GetDocumentsBySharedGameQuery` | `type?` | `[A/E]` |
| GET | `/admin/shared-games/{id}/documents/active` | `GetActiveDocumentsQuery` | — | `[A/E]` |
| POST | `/admin/shared-games/{id}/documents` | `AddDocumentToSharedGameCommand` | `{ documentId, documentType }` | `[A/E]` |
| POST | `/admin/shared-games/{id}/documents/{docId}/set-active` | `SetActiveDocumentVersionCommand` | — | `[A/E]` |
| DELETE | `/admin/shared-games/{id}/documents/{docId}` | `RemoveDocumentFromSharedGameCommand` | — | `[A/E]` |
| POST | `/admin/shared-games/{id}/documents/{docId}/approve` | `ApproveDocumentForRagProcessingCommand` | `{ notes? }` | `[A/E]` |

---

## 8. Quick Questions

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/games/{id}/quick-questions` | `GetQuickQuestionsQuery` | — | `[P]` |
| POST | `/admin/shared-games/{id}/quick-questions/generate` | `GenerateQuickQuestionsCommand` | — | `[A/E]` |
| POST | `/admin/shared-games/{id}/quick-questions` | `AddManualQuickQuestionCommand` | `{ text, emoji, category, displayOrder }` | `[A/E]` |
| PUT | `/admin/quick-questions/{questionId}` | `UpdateQuickQuestionCommand` | Same as create | `[A/E]` |
| DELETE | `/admin/quick-questions/{questionId}` | `DeleteQuickQuestionCommand` | — | `[A/E]` |

---

## 9. Agent e State Templates

### Agent Linking

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/shared-games/{id}/link-agent/{agentId}` | `LinkAgentToSharedGameCommand` | — | `[A/E]` |
| DELETE | `/admin/shared-games/{id}/unlink-agent` | `UnlinkAgentFromSharedGameCommand` | — | `[A/E]` |

### Game State Templates

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/shared-games/{id}/state-template` | `GetActiveGameStateTemplateQuery` | — | `[A/E]` |
| GET | `/admin/shared-games/{id}/state-template/versions` | `GetGameStateTemplateVersionsQuery` | — | `[A/E]` |
| POST | `/admin/shared-games/{id}/state-template/generate` | `GenerateGameStateTemplateCommand` | — | `[A/E]` |
| PUT | `/admin/shared-games/{id}/state-template/{templateId}` | `UpdateGameStateTemplateCommand` | `{ jsonSchema }` | `[A/E]` |
| POST | `/admin/shared-games/{id}/state-template/{templateId}/activate` | `ActivateGameStateTemplateCommand` | — | `[A/E]` |

---

## 10. Share Requests (Community Contributions)

### Utente

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/share-requests` | `CreateShareRequestCommand` | `{ privateGameId, notes }` | `[S]` |
| GET | `/share-requests` | `GetUserShareRequestsQuery` | `status?, pageNumber?, pageSize?` | `[S]` |
| GET | `/share-requests/{id}` | `GetShareRequestDetailsQuery` | — | `[S]` |
| PUT | `/share-requests/{id}/documents` | `UpdateShareRequestDocumentsCommand` | `{ documentIds[] }` | `[S]` |
| DELETE | `/share-requests/{id}` | `WithdrawShareRequestCommand` | — | `[S]` |

### Admin Review

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/share-requests` | `GetPendingShareRequestsQuery` | `status?, contributionType?, search?, pageNumber?, pageSize?` | `[A/E]` |
| GET | `/admin/share-requests/{id}` | `GetShareRequestDetailsQuery` | — | `[A/E]` |
| POST | `/admin/share-requests/{id}/start-review` | `StartReviewCommand` | — | `[A/E]` |
| POST | `/admin/share-requests/{id}/approve` | `ApproveShareRequestCommand` | `{ titleOverride?, descriptionOverride?, selectedDocumentIds[] }` | `[A/E]` |
| POST | `/admin/share-requests/{id}/reject` | `RejectShareRequestCommand` | `{ reason }` | `[A/E]` |
| POST | `/admin/share-requests/{id}/request-changes` | `RequestShareRequestChangesCommand` | `{ feedback }` | `[A/E]` |
| POST | `/admin/share-requests/{id}/release` | `ReleaseReviewCommand` | — | `[A/E]` |
| GET | `/admin/share-requests/my-reviews` | `GetMyActiveReviewsQuery` | — | `[A/E]` |
| POST | `/editor/share-requests/bulk-approve` | `BulkApproveShareRequestsCommand` | `{ requestIds[] }` (max 20) | `[E]` |
| POST | `/editor/share-requests/bulk-reject` | `BulkRejectShareRequestsCommand` | `{ requestIds[], reason }` (max 20) | `[E]` |
| POST | `/admin/share-requests/{id}/approve-game-proposal` | `ApproveGameProposalCommand` | `{ action, targetGameId? }` | `[A]` |
| GET | `/admin/private-games/{id}/check-duplicates` | `CheckPrivateGameDuplicatesQuery` | — | `[A]` |

### Flusso Share Request

```
Utente: POST /share-requests { privateGameId }
              │
              ▼ { requestId, status: "Pending" }
              │
Admin:  POST /admin/share-requests/{id}/start-review
              │
              ▼ status: "InReview"
              │
        ┌─────┼──────────┐
        ▼     ▼           ▼
     Approve  Reject   Request Changes
        │                    │
        ▼                    ▼
   Published           User updates → Re-submit
```

---

## 11. Contributors e Badges

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/shared-games/{id}/contributors` | `GetGameContributorsQuery` | — | `[P]` |
| GET | `/users/{id}/contributions` | `GetUserContributionsQuery` | `pageNumber?, pageSize?` | `[P]` |
| GET | `/users/me/contribution-stats` | `GetUserContributionStatsQuery` | — | `[S]` |
| GET | `/badges` | `GetAllBadgesQuery` | — | `[P]` |
| GET | `/users/{id}/badges` | `GetUserBadgesQuery` | — | `[P]` |
| GET | `/users/me/badges` | `GetUserBadgesQuery` | — | `[S]` |
| GET | `/badges/leaderboard` | `GetBadgeLeaderboardQuery` | `period?, pageNumber?, pageSize?` | `[P]` |
| PUT | `/users/me/badges/{id}/display` | `ToggleBadgeDisplayCommand` | `{ isDisplayed }` | `[S]` |

---

## 12. Trending e Events

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/catalog/trending` | `GetCatalogTrendingQuery` | `limit?` | `[P]` |
| POST | `/catalog/events` | `RecordGameEventCommand` | `{ gameId, eventType }` | `[S]` |

---

## 13. PDF Wizard (Issue #4139)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/shared-games/wizard/upload-pdf` | `UploadPdfForGameExtractionCommand` | Multipart: file | `[A/E]` |
| GET | `/admin/shared-games/wizard/pdf/preview` | `GetPdfPreviewForWizardQuery` | `filePath` | `[A/E]` |
| GET | `/admin/shared-games/wizard/bgg/search` | `SearchBggGamesQuery` | `query, exact?` | `[A/E]` |
| GET | `/admin/shared-games/wizard/bgg/{bggId}` | `GetBggGameDetailsQuery` | — | `[A/E]` |
| POST | `/admin/shared-games/wizard/create` | `CreateSharedGameFromPdfCommand` | Wizard data | `[A/E]` |

### Flusso PDF Wizard

```
POST /wizard/upload-pdf { file }
       │
       ▼ { filePath, extractedText }
       │
GET /wizard/pdf/preview?filePath=xxx
       │
       ▼ { title, description, ... } (extracted)
       │
GET /wizard/bgg/search?query="Catan"
       │
       ▼ { matches[] }
       │
POST /wizard/create { mergedData }
       │
       ▼ Game + PDF creati in un colpo
```

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 1,450 |
| **Passati** | 1,450 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 6s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Public Search | `SearchSharedGamesTests.cs`, `GetSharedGameByIdTests.cs` | Passato |
| Admin CRUD | `CreateSharedGameTests.cs`, `UpdateSharedGameTests.cs`, `DeleteTests.cs` | Passato |
| Publish Workflow | `SubmitForApprovalTests.cs`, `ApprovePublicationTests.cs`, `RejectPublicationTests.cs` | Passato |
| BGG Integration | `ImportGameFromBggTests.cs`, `SearchBggGamesTests.cs`, `BulkImportTests.cs` | Passato |
| FAQ Management | `AddGameFaqTests.cs`, `UpdateFaqTests.cs`, `DeleteFaqTests.cs` | Passato |
| Errata Management | `AddGameErrataTests.cs`, `UpdateErrataTests.cs` | Passato |
| Document Management | `AddDocumentTests.cs`, `SetActiveDocumentTests.cs` | Passato |
| Share Requests | `CreateShareRequestTests.cs`, `ApproveShareTests.cs`, `RejectShareTests.cs` (10+ file) | Passato |
| Contributors/Badges | `GetContributorsTests.cs`, `GetBadgesTests.cs` | Passato |
| Quick Questions | `GenerateQuickQuestionsTests.cs` | Passato |
| State Templates | `GenerateGameStateTemplateTests.cs`, `ActivateTemplateTests.cs` | Passato |
| Domain Entities | CatalogGame aggregate, ShareRequest (59 file) | Passato |
| Validators | 8 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*
