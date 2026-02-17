# Session Tracking - Flussi API

## Panoramica

Il bounded context Session Tracking gestisce il tracking delle attività utente con timeline, filtri per data e paginazione.

---

## 1. Activity Timeline

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/activity/timeline` | `GetActivityTimelineQuery` | `type?, search?, dateFrom?, dateTo?, page?, pageSize?, order?` | `[S]` |
| GET | `/dashboard/activity-timeline` | `GetActivityTimelineQuery` | `type?, search?, dateFrom?, dateTo?, skip?, take?, order?` | `[S]` |

### Parametri

| Parametro | Tipo | Descrizione |
|-----------|------|-------------|
| `type` | string? | Filtro per tipo attività (login, game_session, chat, pdf_upload, etc.) |
| `search` | string? | Ricerca full-text nelle attività |
| `dateFrom` | DateTime? | Data inizio range |
| `dateTo` | DateTime? | Data fine range |
| `page` | int | Numero pagina (default: 1) |
| `pageSize` | int | Elementi per pagina (default: 20) |
| `order` | string | Ordinamento: "asc" o "desc" (default: "desc") |

### Flusso Query Activity Timeline

```
GET /activity/timeline?type=login&dateFrom=2026-01-01&dateTo=2026-02-15&page=1&pageSize=20
       │
       ▼ Filtro per tipo + range date
       │
       ▼ Paginazione page-based
       │
       ▼
{
  "items": [
    { "type": "login", "timestamp": "...", "details": "..." },
    ...
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 42,
  "totalPages": 3
}
```

---

## 2. Dashboard Stream (SSE)

| Metodo | Path | Command/Query | Response | Auth |
|--------|------|---------------|----------|------|
| GET | `/dashboard/stream` | `GetDashboardStreamQuery` | SSE | `[S]` |

Il dashboard stream fornisce aggiornamenti in tempo reale delle attività utente tramite Server-Sent Events.

---

## Note Architetturali

- Il bounded context SessionTracking opera principalmente come domain logic integrato in altri bounded context
- Le sessioni utente sono tracciate dal middleware `SessionQuotaMiddleware`
- Gli eventi di attività sono generati tramite domain events
- Indice DB: `(UserId, IsActive)` per query efficienti sulle sessioni attive
- Indice DB aggiunto per date range filters (Issue #4315)

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 181 |
| **Passati** | 181 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | <1s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Session Quota | `SessionQuotaMiddlewareTests.cs` | Passato |
| Device Tracking | `DeviceFingerprintTests.cs` | Passato |
| Activity Timeline | `GetActivityTimelineTests.cs` | Passato |
| Domain Entities | Session entity (4 file) | Passato |
| Validators | 6 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*
