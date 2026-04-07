# AI Infrastructure Dashboard — Design Spec

**Date**: 2026-04-07
**Status**: Draft
**Author**: Claude + SuperAdmin
**Location**: `/admin/agents/infrastructure` + widget on `/admin/agents` Mission Control

## Overview

Dashboard per superadmin per monitorare e gestire l'infrastruttura AI di MeepleAI. Due componenti: un widget semaforo compatto sulla Mission Control esistente e una pagina dedicata con metriche sintetiche, azioni rapide, test di connettività pipeline, e configurazione runtime.

## Requirements Summary

| Requisito | Decisione |
|-----------|-----------|
| Posizionamento | Ibrido: semaforo su Mission Control + pagina `/admin/agents/infrastructure` |
| Servizi monitorati | 8: Embedding, Unstructured, SmolDocling, Reranker, Orchestrator, Ollama, PostgreSQL, Redis |
| Azioni | Restart (cooldown 5min), health check on-demand, metriche sintetiche, test pipeline + dipendenze, config runtime |
| Metriche | Sintetiche: status, uptime, avg latency, error rate 24h. No grafici |
| Config runtime | Solo parametri già esposti dai servizi Python |
| Architettura | Backend-centric, CQRS via .NET API |
| Sicurezza | Admin = read-only, SuperAdmin = azioni mutative |
| Alpha mode | Widget semaforo sempre visibile, pagina Infrastructure nascosta in alpha |

## Servizi Monitorati

| # | Servizio | Tipo | Container | Porta | Health Endpoint |
|---|----------|------|-----------|-------|-----------------|
| 1 | Embedding (multilingual-e5) | AI | meepleai-embedding | 8000 | `GET /health` |
| 2 | Unstructured | AI | meepleai-unstructured | 8001 | `GET /health` |
| 3 | SmolDocling (VLM) | AI | meepleai-smoldocling | 8002 | `GET /health` |
| 4 | Reranker (BGE) | AI | meepleai-reranker | 8003 | `GET /health` |
| 5 | Orchestrator (LangGraph) | AI | meepleai-orchestrator | 8004 | `GET /health` |
| 6 | Ollama | AI/LLM | meepleai-ollama | 11434 | CLI `ollama list` |
| 7 | PostgreSQL (+pgvector) | Infra | meepleai-postgres | 5432 | TCP connect |
| 8 | Redis | Infra | meepleai-redis | 6379 | `PING` |

## Architecture

### Approach: Backend-Centric (CQRS)

Frontend chiama SOLO endpoint .NET. Il backend fa da proxy/aggregatore verso i servizi AI. Nessuna comunicazione diretta frontend → servizi Python.

```
Browser → Next.js → .NET API (CQRS) → Servizi AI / Docker Socket
                         ↓
              IInfrastructureHealthService
              IDockerProxyService
              IPrometheusClientService
              Health Checks (13+)
```

### Riuso codice esistente

| Componente backend esistente | Utilizzo |
|------------------------------|----------|
| `IInfrastructureHealthService` | Aggregazione status servizi |
| `IDockerProxyService` | Container list, logs, restart |
| `/Infrastructure/Health/Checks/*` | Health check on-demand trigger |
| `IPrometheusClientService` | Metriche latency/error rate |
| `POST /admin/operations/restart-service` | Base per restart con cooldown |

## Backend Design

### Bounded Context

Estensione del **Administration** bounded context esistente. Nessun nuovo BC necessario.

### Nuovi endpoint

Tutti sotto `/api/v1/admin/infrastructure/`, autorizzazione SuperAdmin per mutazioni, Admin per lettura.

#### Queries (GET)

| Endpoint | MediatR Query | Response |
|----------|---------------|----------|
| `GET /services` | `GetAiServicesStatusQuery` | `AiServicesStatusResponse` — lista 8 servizi con: name, displayName, type (ai/infra), status, uptime, avgLatencyMs, errorRate24h, lastCheckedAt |
| `GET /services/{name}/dependencies` | `GetServiceDependenciesQuery` | `ServiceDependenciesResponse` — lista dipendenze dirette con stato e latency |
| `GET /services/{name}/config` | `GetServiceConfigQuery` | `ServiceConfigResponse` — parametri configurabili con valori attuali e tipo |
| `GET /pipeline/test` | `TestPipelineConnectivityQuery` | `PipelineTestResponse` — risultato test catena: lista hop con name, status, latencyMs, error |

#### Commands (POST)

| Endpoint | MediatR Command | Response | Auth |
|----------|-----------------|----------|------|
| `POST /services/{name}/restart` | `RestartServiceCommand` | `RestartResponse` — success, cooldownExpiresAt | SuperAdmin |
| `POST /services/{name}/health-check` | `TriggerHealthCheckCommand` | `HealthCheckResponse` — status, details, latencyMs | SuperAdmin |
| `PUT /services/{name}/config` | `UpdateServiceConfigCommand` | `ConfigUpdateResponse` — updatedParams | SuperAdmin |

### DTOs

```csharp
// Query response
public record AiServiceStatusDto(
    string Name,
    string DisplayName,
    string Type,           // "ai" | "infra"
    ServiceHealth Status,  // Healthy, Degraded, Down, Restarting, Unknown
    string Uptime,         // "14d 3h" formatted
    double AvgLatencyMs,
    double ErrorRate24h,
    DateTime LastCheckedAt,
    bool CanRestart,       // false if in cooldown or not SuperAdmin
    int? CooldownRemainingSeconds
);

public record ServiceDependencyDto(
    string Name,
    ServiceHealth Status,
    double LatencyMs
);

public record PipelineHopDto(
    string ServiceName,
    ServiceHealth Status,
    double LatencyMs,
    string? Error
);

public record ServiceConfigParamDto(
    string Key,
    string DisplayName,
    string Value,
    string Type,          // "string" | "int" | "enum"
    string[]? Options     // for enum type
);

// Command
public record RestartServiceCommand(string ServiceName) : IRequest<RestartResponse>;
public record TriggerHealthCheckCommand(string ServiceName) : IRequest<HealthCheckResponse>;
public record UpdateServiceConfigCommand(
    string ServiceName,
    Dictionary<string, string> Parameters
) : IRequest<ConfigUpdateResponse>;
```

### CooldownRegistry

```csharp
// In-memory, registered as Singleton
public class ServiceCooldownRegistry
{
    private readonly ConcurrentDictionary<string, DateTime> _lastRestarts = new();
    private readonly TimeSpan _cooldownDuration = TimeSpan.FromMinutes(5);

    public bool IsInCooldown(string serviceName, out int remainingSeconds);
    public void RecordRestart(string serviceName);
}
```

Il `RestartServiceCommandHandler` controlla il cooldown prima di eseguire il restart. Se in cooldown → `409 Conflict` con `remainingSeconds`.

### Validators (FluentValidation)

- `RestartServiceCommand`: nome servizio deve essere tra i nomi conosciuti
- `UpdateServiceConfigCommand`: parametri devono esistere per il servizio target
- `TriggerHealthCheckCommand`: nome servizio valido

### Service Configuration Map

Parametri configurabili per servizio (hardcoded nel backend, non nel DB):

| Servizio | Parametro | Tipo | Opzioni/Range |
|----------|-----------|------|---------------|
| Embedding | model | enum | e5-small, e5-base, e5-large |
| Reranker | rate_limit | int | 10-1000 |
| Reranker | batch_size | int | 1-128 |
| Reranker | max_length | int | 128-1024 |
| Unstructured | strategy | enum | fast, hi-res |
| Orchestrator | active_agents | enum[] | tutor, arbitro, decisore |
| Ollama | model | string | — |

La config update avviene tramite nuovi endpoint `PUT /config` da aggiungere ai servizi Python (FastAPI). Ogni servizio espone un endpoint che accetta i parametri modificabili e li applica a runtime (no restart necessario). Per Ollama, si usa l'API nativa `POST /api/pull` per cambiare modello.

### Soglie Health

```csharp
public static class HealthThresholds
{
    // Latency (ms) - oltre questa soglia → Degraded
    public static readonly Dictionary<string, int> LatencyDegraded = new()
    {
        ["embedding"] = 2000,
        ["reranker"] = 2000,
        ["smoldocling"] = 5000,
        ["unstructured"] = 5000,
        ["orchestrator"] = 3000,
        ["ollama"] = 5000,
        ["postgres"] = 500,
        ["redis"] = 100
    };

    // Error rate (%) - oltre questa soglia → Degraded
    public const double ErrorRateDegraded = 5.0;
    public const double ErrorRateCritical = 20.0;
}
```

### Pipeline Connectivity Test

Il `TestPipelineConnectivityQueryHandler`:
1. Chiama `GET /health` su ogni servizio nella catena: API → Embedding → Reranker → Orchestrator
2. Misura latency per ogni hop
3. Si ferma al primo errore e riporta il punto di rottura
4. Include anche check dipendenze infra: PostgreSQL, Redis

## Frontend Design

### Widget Mission Control — `InfraStatusBar`

Riga compatta in cima a `/admin/agents` (Mission Control), sotto il titolo:

```
AI Infrastructure: 🟢🟢🟡🟢🟢🟢🟢🔴  [6/8 Healthy]  → Manage
```

- 8 pallini colorati, uno per servizio
- Tooltip su hover: nome servizio + stato
- Count "X/8 Healthy"
- Link "Manage" → naviga a `/admin/agents/infrastructure`
- Componente: `InfraStatusBar.tsx`
- Data: `useInfraServices()` hook (polling 30s)

### Pagina `/admin/agents/infrastructure`

Layout a 3 sezioni verticali.

#### Sezione 1: Service Grid

Griglia responsive (4 colonne desktop, 2 tablet, 1 mobile) con 8 `ServiceCard`.

Ogni `ServiceCard` mostra:
- Icona servizio + nome
- Pallino stato colorato (🟢/🟡/🔴/🔵/⚪)
- Uptime (es. "14d 3h")
- Avg latency (es. "45ms")
- Error rate 24h (es. "0.2%")
- 3 bottoni azione: Health Check | Restart | Config
- Bottoni Restart e Config disabilitati per non-SuperAdmin con tooltip "Requires SuperAdmin"
- Bottone Restart disabilitato + countdown se in cooldown

Componenti: `ServiceGrid.tsx`, `ServiceCard.tsx`

#### Sezione 2: Pipeline Test

Bottone "Test Pipeline Connectivity" che attiva il test e mostra risultato:

```
API → Embedding → Reranker → Orchestrator
 ✅ 12ms    ✅ 8ms     ✅ 23ms     ✅ 15ms
                                    Total: 58ms
```

Se un anello è rotto:
```
API → Embedding → Reranker → Orchestrator
 ✅ 12ms    ❌ Timeout    —          —
            ↑ Connection refused to embedding:8000
```

Componente: `PipelineTest.tsx`

#### Sezione 3: Service Detail Panel

Pannello che appare sotto la griglia quando si clicca una card. Tre tab:

- **Dependencies**: lista dipendenze dirette del servizio con stato
- **Config**: form con parametri modificabili (solo per SuperAdmin, read-only per Admin)
- **Restart**: bottone restart con modale conferma + indicatore cooldown

Componenti: `ServiceDetailPanel.tsx`, `RestartModal.tsx`, `ServiceConfigForm.tsx`

### React Hooks

| Hook | Endpoint | Strategia |
|------|----------|-----------|
| `useInfraServices()` | `GET /services` | Polling 30s, `staleTime: 30000` |
| `useServiceDependencies(name)` | `GET /services/{name}/dependencies` | On-demand (click) |
| `useServiceConfig(name)` | `GET /services/{name}/config` | On-demand (click tab Config) |
| `usePipelineTest()` | `GET /pipeline/test` | Mutation manuale (click bottone) |
| `useRestartService()` | `POST /services/{name}/restart` | Mutation + invalidate cache |
| `useTriggerHealthCheck()` | `POST /services/{name}/health-check` | Mutation + invalidate cache |
| `useUpdateServiceConfig()` | `PUT /services/{name}/config` | Mutation + invalidate cache |

### Componenti React — Lista completa

| Componente | File | Responsabilità |
|------------|------|---------------|
| `InfraStatusBar` | `components/admin/infrastructure/InfraStatusBar.tsx` | Widget semaforo Mission Control |
| `InfrastructurePage` | `app/admin/(dashboard)/agents/infrastructure/page.tsx` | Layout pagina |
| `ServiceGrid` | `components/admin/infrastructure/ServiceGrid.tsx` | Griglia 8 card |
| `ServiceCard` | `components/admin/infrastructure/ServiceCard.tsx` | Card singolo servizio |
| `PipelineTest` | `components/admin/infrastructure/PipelineTest.tsx` | Test connettività chain |
| `ServiceDetailPanel` | `components/admin/infrastructure/ServiceDetailPanel.tsx` | Pannello tabs espandibile |
| `RestartModal` | `components/admin/infrastructure/RestartModal.tsx` | Modale conferma restart + cooldown |
| `ServiceConfigForm` | `components/admin/infrastructure/ServiceConfigForm.tsx` | Form parametri runtime |

## Navigation Integration

### Sidebar entry

In `admin-dashboard-navigation.ts`, aggiungere sotto "Mission Control":

```typescript
{
  title: 'Infrastructure',
  href: '/admin/agents/infrastructure',
  icon: Server, // lucide-react
  alpha: false, // nascosta in alpha mode
}
```

### Alpha Mode

- Widget `InfraStatusBar`: **sempre visibile** (anche in alpha) — leggero e informativo
- Pagina `/admin/agents/infrastructure`: **nascosta in alpha** — feature avanzata

### Autorizzazione UI

| Elemento | Admin | SuperAdmin |
|----------|-------|------------|
| Widget semaforo | ✅ Visibile | ✅ Visibile |
| Pagina Infrastructure | ✅ Read-only | ✅ Full access |
| Service Grid (metriche) | ✅ Visibile | ✅ Visibile |
| Pipeline Test | ✅ Eseguibile | ✅ Eseguibile |
| Health Check on-demand | ❌ Disabilitato | ✅ Eseguibile |
| Restart | ❌ Disabilitato | ✅ Eseguibile |
| Config update | ❌ Read-only | ✅ Modificabile |

## Service States

| Stato | Colore | Pallino | Determinazione |
|-------|--------|---------|----------------|
| Healthy | Verde | 🟢 | Health check ok + latency < soglia + error rate < 5% |
| Degraded | Giallo | 🟡 | Health check ok ma latency > soglia O error rate 5-20% |
| Down | Rosso | 🔴 | Health check fallito O container non running |
| Restarting | Blu | 🔵 | Post-restart, fino al primo health check ok |
| Unknown | Grigio | ⚪ | Primo avvio o health check mai eseguito |

## Error Handling

| Scenario | UI Behavior |
|----------|-------------|
| API non raggiungibile | Banner rosso "Cannot reach API" in cima, retry 10s |
| Singolo servizio down | Card bordo rosso, stato 🔴, azioni disponibili |
| Health check on-demand fallisce | Toast errore con dettaglio ("Connection refused", "Timeout") |
| Restart fallisce | Toast errore, servizio resta nello stato precedente |
| Restart in cooldown | `409 Conflict`, UI mostra countdown rimanente |
| Pipeline test parziale | Chain mostra ✅/❌ per hop, stop al primo errore |
| Config update fallisce | Toast errore, form mantiene valori precedenti |

## Cooldown Mechanism

- **Durata**: 5 minuti per servizio
- **Storage**: In-memory `ConcurrentDictionary<string, DateTime>` (singleton)
- **Reset**: Al restart del servizio API (accettabile — raro in produzione)
- **API response se in cooldown**: `409 Conflict` con body `{ remainingSeconds: 234 }`
- **UI**: Bottone restart disabilitato con countdown live ("Disponibile tra 3:42")

## Polling & Caching

| Dato | Strategia | Stale Time |
|------|-----------|------------|
| Lista servizi | Polling 30s | 30s |
| Dipendenze | On-demand (click) | 60s |
| Config servizio | On-demand (click) | 60s |
| Pipeline test | Manuale (click) | No cache |
| Post-azione (restart/check/config) | Invalidate + refetch | — |

- `refetchOnWindowFocus: true` — refetch quando l'utente torna sulla tab
- Pause polling quando tab non attiva (React Query default)

## Testing Strategy

### Backend

- **Unit**: Handler per ogni query/command, CooldownRegistry, validators, soglie health
- **Integration**: Endpoint auth (Admin vs SuperAdmin), risposta 409 cooldown, pipeline test con servizi mock

### Frontend

- **Unit (Vitest)**: Render ServiceCard con vari stati, InfraStatusBar con dati mock, RestartModal con cooldown
- **Component**: ServiceGrid responsive layout, PipelineTest success/failure states
- **E2E (Playwright)**: Flusso completo: naviga a infrastructure → verifica griglia → click health check → click restart → verifica cooldown

## Out of Scope (V1)

- Log viewer inline (già presente in `/admin/monitor/logs`)
- Grafici storici e sparkline
- Alert automatici da questa dashboard (già gestiti altrove)
- Config runtime per SmolDocling (nessun parametro esposto)
- Restart multiplo / bulk actions
- Export metriche
