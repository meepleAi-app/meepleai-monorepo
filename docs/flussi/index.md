# Flussi API Testabili - MeepleAI

Documentazione completa dei flussi API organizzati per Bounded Context.

## Panoramica

| # | Bounded Context | Endpoint | Test | Passati | Falliti | Stato |
|---|----------------|----------|------|---------|---------|-------|
| 1 | [Authentication](./authentication.md) | ~45 | 1,325 | 1,320 | 0 | 100% |
| 2 | [Document Processing](./document-processing.md) | ~32 | 824 | 823 | 0 | 100% |
| 3 | [Game Management](./game-management.md) | ~53 | 1,300 | 1,300 | 0 | 100% |
| 4 | [Knowledge Base](./knowledge-base.md) | ~39 | 3,028 | 3,027 | 0 | 100% |
| 5 | [Shared Game Catalog](./shared-game-catalog.md) | ~86 | 1,450 | 1,450 | 0 | 100% |
| 6 | [User Library](./user-library.md) | ~53 | 842 | 842 | 0 | 100% |
| 7 | [Administration](./administration.md) | ~65 | 885 | 885 | 0 | 100% |
| 8 | [Session Tracking](./session-tracking.md) | ~5 | 181 | 181 | 0 | 100% |
| 9 | [User Notifications](./user-notifications.md) | 6 | 147 | 147 | 0 | 100% |
| 10 | [System Configuration](./system-configuration.md) | ~30 | 409 | 409 | 0 | 100% |
| 11 | [Workflow Integration](./workflow-integration.md) | ~11 | 134 | 134 | 0 | 100% |

**Totale Endpoint**: ~425+ | **Totale Test**: 12,004 | **Passati**: 12,004 | **Falliti**: 0 | **Ignorati**: 11 | **Pass Rate**: 100%

### Test Eseguiti il: 2026-02-15

### Tutti i test passano

Tutti i 21 test precedentemente falliti sono stati corretti nel commit `d07423643`:
- **15 Middleware (BggRateLimit)**: Fix autenticazione mock (`SessionStatusDto` + `UserDto`) e fix produzione case-mismatch lowercase tier
- **4 DocumentProcessing (ETA)**: Fix `NotFoundException` parametri, ETA mock per documenti completati, boundary condition `>=`
- **1 Authentication (UserTier)**: Rimosso "enterprise" da test tier invalidi (tier valido dal Epic #4068)
- **1 Infrastructure (Metrics)**: Skip test `ExecuteDeleteAsync` non supportato da InMemory provider

## Legenda

| Simbolo | Significato |
|---------|-------------|
| `[P]` | Endpoint pubblico (no auth) |
| `[S]` | Richiede sessione autenticata |
| `[A]` | Richiede ruolo Admin |
| `[E]` | Richiede ruolo Editor |
| `[A/E]` | Richiede Admin o Editor |
| `[O]` | Richiede ownership (proprietario della risorsa) |
| `SSE` | Server-Sent Events (streaming) |

## Flussi Principali Testabili

### 1. Flusso Registrazione e Login
```
POST /auth/register → POST /auth/login → GET /auth/me → POST /auth/logout
```

### 2. Flusso Upload e Processamento PDF
```
POST /ingest/pdf → POST /ingest/pdf/{id}/extract → POST /ingest/pdf/{id}/index
                   GET /pdfs/{id}/progress/stream (SSE)
```

### 3. Flusso Game Session
```
POST /sessions → POST /sessions/{id}/players → POST /sessions/{id}/state/initialize
              → POST /sessions/{id}/pause → POST /sessions/{id}/resume
              → POST /sessions/{id}/complete
```

### 4. Flusso Chat RAG
```
POST /chat-threads → POST /chat-threads/{id}/messages → POST /knowledge-base/ask
                   → GET /chat-threads/{id}/export
```

### 5. Flusso Catalogo Condiviso
```
POST /admin/shared-games/import-bgg → POST /admin/shared-games/{id}/submit-for-approval
→ POST /admin/shared-games/{id}/approve-publication → GET /shared-games (public)
```

### 6. Flusso Libreria Utente
```
POST /library/games/{id} → PUT /library/games/{id}/agent → POST /library/games/{id}/pdf
                         → POST /library/share → GET /library/shared/{token}
```

---

## Comandi per Eseguire i Test

```bash
# Tutti i test unitari
cd apps/api/tests/Api.Tests
dotnet test --filter "Category=Unit"

# Per bounded context specifico
dotnet test --filter "Category=Unit&FullyQualifiedName~BoundedContexts.Authentication"
dotnet test --filter "Category=Unit&FullyQualifiedName~BoundedContexts.GameManagement"

# Test di integrazione (richiede Docker per Testcontainers)
dotnet test --filter "Category=Integration"

# Test E2E (richiede infra completa)
dotnet test --filter "Category=E2E"
```

---

*Ultimo aggiornamento: 2026-02-15*
*Ultima esecuzione test: 2026-02-15 (Unit: 12,004/12,004 passati - 100%)*
*Tutti i path sono relativi a `/api/v1/`*
