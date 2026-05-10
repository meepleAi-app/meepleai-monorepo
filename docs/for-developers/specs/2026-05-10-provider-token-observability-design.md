# Provider Token & Quota Observability — Design Spec

**Date**: 2026-05-10
**Status**: Approved (decisions Q1-Q5 confirmed 2026-05-10)
**Author**: Claude (spec-panel + brainstorming)
**Decision authority**: User confirmed all 5 default recommendations on 2026-05-10

---

## 1. Goal

Esporre tre capability operative oggi mancanti o frammentate per l'osservabilità dei provider LLM:

1. **Active token validation on demand** — verifica live che un token configurato sia ancora valido presso il provider
2. **Quota / balance visibility** — credito residuo dove il provider espone API
3. **Audit & rate limiting** — tracciabilità e protezione dall'abuso, integrazione con il sistema di alerting esistente

Estende `Administration` BC; **nessun nuovo bounded context**.

## 2. Sintomo che si chiude

| Oggi | Domani |
|------|--------|
| MTTR rotation token scaduto ~30 min (riproduzione errore in chat utente) | ~2 min (probe diretto) |
| Cost surprise: quota esaurita scoperta a posteriori dalla fattura provider | Warning a 20% remaining, critical a 5% |
| Onboarding nuovo provider richiede SSH + curl + cat env | UI self-service in `/admin/providers` |
| Compliance audit "chi ha ruotato i token?" risponde via grep su log dispersi | Audit log strutturato `ProviderProbeAuditEntry`, retention 365gg |

## 3. Endpoint esistente vs nuovi

| Endpoint | Stato | Capability | SLO |
|----------|-------|-----------|-----|
| `GET /api/v1/admin/llm-health` | ✅ esiste (issue #962) | Passive monitoring (circuit breaker, success rate, latency stats) | < 200ms cached |
| `POST /api/v1/admin/providers/{name}/probe` | 🆕 nuovo | Active token validation on demand | P95 < 3000ms, P99 < 5000ms |
| `GET /api/v1/admin/providers/{name}/quota` | 🆕 nuovo | Quota / balance live (cached 5min) | P95 < 500ms cached, < 3000ms miss |

## 4. Use case e attori

| Attore | Ruolo | Trigger | Frequenza | SLO |
|--------|-------|---------|-----------|-----|
| SuperAdmin | Diagnosi provider, rotazione token | Manuale | ~10/giorno | Real-time, < 5s |
| Admin | Read-only consultazione quota/health | Manuale | ~30/giorno | Cached, < 1s |
| Sistema Ops (Prometheus) | Alert su soglie quota | Scheduled 60s | continuo | Cached, < 200ms |
| Sistema CI/CD | Pre-deploy gate raggiungibilità | Pipeline | ~5/giorno | < 10s |
| Sviluppatore (locale) | Debug "perché chat fallisce?" | Manuale | ~5/giorno | < 5s |

## 5. User goal (SMART)

### G1 — Active token validation on demand
**As a** SuperAdmin **I want** verificare on-demand che un token configurato sia valido **so that** posso diagnosticare fallimenti chat in < 2 min.

- Endpoint: `POST /api/v1/admin/providers/{name}/probe`
- Misura: `tokenAuthenticated`, `latencyMs`, `errorCode`
- Cost: zero quota (usa `list-models` endpoint provider, non `chat-completion`)
- Timing: P95 < 3000ms, P99 < 5000ms (timeout hard 5s)

### G2 — Provider quota visibility (where supported)
**As an** Admin **I want** vedere il credito residuo per provider che espongono API balance **so that** abbiamo 5 giorni di preavviso prima dell'esaurimento budget.

- Endpoint: `GET /api/v1/admin/providers/{name}/quota`
- Misura: `usedUsd`, `limitUsd`, `remainingUsd`, `resetAt`
- Provider supportati MVP: **OpenRouter** (`/api/v1/auth/key`), **DeepSeek** (`/user/balance`)
- Provider non supportati: `501 Not Implemented` con `quotaSupported: false`. **No fake data, no scraping.**
- Cache: 5min server-side, invalidata su `ProviderConfigurationChanged` o ricezione 429 in chat reale

### G3 — Probe abuse prevention & auditability
**As a** Security Officer **I want** che ogni probe sia tracciato e rate-limited **so that** un account admin compromesso non possa esaurire la quota o exfiltrare info.

- Audit entry per ogni probe (success o fail): `{actorId, providerName, fingerprint, result, errorCode, timestamp}`
- Retention: 365 giorni configurabile via `appsettings`
- Rate limit: 10/min per user, 60/h globale per provider
- **Token leakage prevention**: response non contiene mai il raw token; solo `tokenFingerprint = SHA256(token)[0..8]`

### G4 — Quota threshold alerting (riuso AlertRule esistente)
**As an** Ops engineer **I want** alert Prometheus automatici sotto soglia credito **so that** carenze di budget sono rilevate prima di qualsiasi fallimento chat.

- Metric `meeple_provider_quota_remaining_usd{provider}` esposta su `/metrics`
- AlertRule `ProviderQuotaLow` (warn < 20%, critical < 5%) — riusa `AlertRule` aggregate dell'Administration BC
- Provider senza quota API: nessuna metric, nessun alert (impossibile by design)

### G5 — Frontend self-service status page
**As an** Admin **I want** una vista UI in `/admin/overview` con drill-down `/admin/providers/{name}` **so that** non serve SSH/curl per check provider.

- Card "Providers" in overview: `N/M healthy` + last-probe-time
- Detail page `/admin/providers/[name]`: latency stats, circuit state, quota (se supportata), audit log table, "Run Probe" button (SuperAdmin only)
- Quota non supportata → messaggio esplicito "Quota tracking not supported by provider", **mai valori finti**

## 6. Architettura

### 6.1 Bounded Context
**Administration BC** — estende `LlmHealth` esistente con due nuove feature: `ProviderProbe` e `ProviderQuota`.

### 6.2 Application layer (CQRS via MediatR)

```
BoundedContexts/Administration/Application/
├── Commands/
│   └── ProbeProviderCommand.cs              (ProviderName) → ProviderProbeResultDto
│   └── ProbeProviderCommandHandler.cs       (audit + execute + rate limit check)
│   └── ProbeProviderCommandValidator.cs     (FluentValidation: provider name allowlist)
├── Queries/
│   └── GetProviderQuotaQuery.cs             (ProviderName) → ProviderQuotaDto
│   └── GetProviderQuotaQueryHandler.cs      (cache lookup → strategy dispatch)
└── DTOs/
    ├── ProviderProbeResultDto.cs
    └── ProviderQuotaDto.cs
```

### 6.3 Domain layer

```
BoundedContexts/Administration/Domain/
├── Aggregates/
│   └── ProviderProbeAudit/
│       ├── ProviderProbeAuditEntry.cs       (entity, factory Create())
│       └── ProbeOutcome.cs                  (enum: Success, Unauthorized, Timeout, Unreachable, NotConfigured)
├── Services/
│   ├── IProviderProbeService.cs             (orchestrazione probe + audit)
│   └── IProviderQuotaService.cs             (cache + strategy dispatch)
└── Events/
    ├── ProviderProbedEvent.cs               (per future telemetria)
    └── ProviderConfigurationChangedEvent.cs (riusato — invalida quota cache)
```

### 6.4 Infrastructure layer — Strategy pattern

```
Services/Providers/Quota/
├── IProviderQuotaProvider.cs                (interface comune)
├── OpenRouterQuotaProvider.cs               (GET /api/v1/auth/key)
├── DeepSeekQuotaProvider.cs                 (GET /user/balance)
├── NotSupportedQuotaProvider.cs             (default fallback → throw QuotaNotSupportedException)
└── ProviderQuotaProviderFactory.cs          (provider name → IProviderQuotaProvider)

Services/Providers/Probe/
├── IProviderProbeExecutor.cs
├── OpenRouterProbeExecutor.cs               (riusa OpenRouterLlmClient + list-models)
├── OpenAiProbeExecutor.cs                   (riusa OpenAiChatModels + list-models)
├── DeepSeekProbeExecutor.cs
├── OllamaProbeExecutor.cs                   (locale, no auth)
└── ProviderProbeExecutorFactory.cs
```

### 6.5 Endpoint routing

```csharp
// Routing/AdminProviderEndpoints.cs (nuovo file)
group.MapPost("/providers/{name}/probe",
        async (string name, IMediator m) => Results.Ok(await m.Send(new ProbeProviderCommand(name))))
    .RequireAuthorization("SuperAdmin")
    .RequireRateLimiting("admin-probe-per-user")
    .WithOpenApi();

group.MapGet("/providers/{name}/quota",
        async (string name, IMediator m) => Results.Ok(await m.Send(new GetProviderQuotaQuery(name))))
    .RequireAuthorization("Admin")
    .WithOpenApi();
```

### 6.6 Persistence

```
EntityConfigurations/ProviderProbeAuditEntryConfiguration.cs
└── Migration: AddProviderProbeAuditEntries
    Table: provider_probe_audit_entries
    Columns: id (uuid), actor_id (uuid FK users), provider_name (varchar 64),
             token_fingerprint (char 8), outcome (varchar 32), error_code (varchar 64 null),
             latency_ms (int), probed_at (timestamptz), created_at (timestamptz)
    Indexes: (provider_name, probed_at DESC), (actor_id, probed_at DESC)
```

### 6.7 Cache & event flow

```
ProviderConfigurationChangedEvent → invalidates HybridCache key "provider-quota:{name}"
Chat receives 429 from provider → publishes ProviderQuotaPossiblyExhaustedEvent
   → handler invalidates same cache key
```

## 7. DTO contracts

```csharp
internal record ProviderProbeResultDto(
    string ProviderName,
    bool TokenConfigured,         // env var presente e non vuota
    bool TokenAuthenticated,      // 200 da list-models entro 5s
    bool ModelAvailable,          // modello configurato presente nella lista
    string TokenFingerprint,      // SHA256(token)[0..8]
    string? ErrorCode,            // "invalid_token" | "unauthorized" | "timeout" | "unreachable" | "not_configured" | null
    string? ErrorMessage,         // human-readable, sanitizzato (mai contiene token)
    int LatencyMs,
    DateTime ProbedAt
);

internal record ProviderQuotaDto(
    string ProviderName,
    bool QuotaSupported,          // false → tutti i campi sotto sono null
    decimal? UsedUsd,
    decimal? LimitUsd,
    decimal? RemainingUsd,
    DateTime? ResetAt,            // null se rolling/no reset
    DateTime FetchedAt,
    int CacheTtlSeconds,
    bool CacheHit
);
```

## 8. Test strategy

### 8.1 Unit tests (xUnit)
- `ProbeProviderCommandHandler_*` — 15 test (validator, audit write, fingerprint, error mapping, rate limit pass-through)
- `GetProviderQuotaQueryHandler_*` — 10 test (cache hit/miss, invalidation, strategy dispatch, 501 mapping)
- `OpenRouterQuotaProvider_*` — 6 test (parsing, error states)
- `DeepSeekQuotaProvider_*` — 6 test
- `ProviderProbeAuditEntry_*` — 5 test (factory, invariants)

### 8.2 Integration tests (Testcontainers)
- `ProbeEndpoint_AllOutcomes` — 7 scenari (G1 Gherkin) con WireMock per provider upstream
- `QuotaEndpoint_AllOutcomes` — 5 scenari (G2 Gherkin)
- `RateLimit_PerUser_PerProvider` — 6 scenari (G3 Gherkin)
- `Audit_RetentionJob` — 1 scenario (delete > 365gg)

### 8.3 E2E (Playwright, post-FE)
- `admin-providers-overview.spec.ts` — card visibility, badge counts
- `admin-providers-detail.spec.ts` — drill-down, run probe, audit table refresh

### 8.4 Coverage target
Backend ≥ 90% (allineato a target progetto). Frontend ≥ 85%.

## 9. Decisioni confermate (2026-05-10)

| # | Domanda | Decisione |
|---|---------|-----------|
| Q1 | Auth scope | ✅ **Split**: `POST /probe` = SuperAdmin; `GET /quota` + `GET /llm-health` = Admin |
| Q2 | Probe model | ✅ **list-models default** (zero quota cost); `?deep=true` opzionale per chat-completion `max_tokens:1` |
| Q3 | Audit storage | ✅ **Nuova entità** `ProviderProbeAuditEntry` con index `(providerName, probedAt DESC)`; interfaccia `IAuditableEvent` per consolidazione futura |
| Q4 | MVP quota providers | ✅ **OpenRouter + DeepSeek**. OpenAI/Anthropic → `501 quotaSupported:false`. Ollama → N/A |
| Q5 | Frontend placement | ✅ **Card riassuntiva in `/admin/overview`** + drill-down `/admin/providers/[name]` |

## 10. Open question secondarie (chiusura proposta)

| Domanda | Decisione proposta |
|---------|-------------------|
| Q6: cache invalidation triggers | (1) `ProviderConfigurationChanged`, (2) ricezione 429 in chat reale → `ProviderQuotaPossiblyExhaustedEvent` |
| Q7: audit retention | 365 giorni default, configurabile via `Administration:ProbeAuditRetentionDays` |
| Q8: token fingerprint | SHA256(token)[0..8] = 32 bit, OK per < 50 token con collision rate ~0 |
| Q9: probe in dev/CI | Real probe sempre, MA env var `PROVIDER_PROBE_DISABLED=true` per CI offline |

## 11. Out of scope (esplicito)

- Forecasting consumo quota (es. "ti finiranno i crediti tra 4.2 giorni") — futuro
- Scraping dashboard provider che non espongono API quota — fragile, ToS-grey, no
- Multi-account/multi-tenant probe (un solo set di token globale) — non necessario per Alpha
- Probe automatico schedulato — passive monitoring esistente (LlmHealth) basta
- Self-rotation dei token — fuori scope, richiede secret manager integration

## 12. Roadmap implementativa (3 PR proposte)

| PR | Scope | Issue | DoD |
|----|-------|-------|-----|
| PR1 | Backend G1 + G3 (probe + audit + rate limit) | child #NEW.1 | Endpoint live, 28 unit + 13 integration test verdi, audit retention job schedulato |
| PR2 | Backend G2 + G4 (quota + alerting metric) | child #NEW.2 | OpenRouter + DeepSeek strategy, AlertRule template `ProviderQuotaLow`, /metrics expose |
| PR3 | Frontend G5 (UI overview card + detail page) | child #NEW.3 | 2 route live, axe-core a11y AA, bundle ≤ +30 KB |

Umbrella issue: #NEW "Provider token & quota observability".

## 13. Riferimenti

- Esistente: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetLlmHealthQuery.cs`
- Esistente: `apps/api/src/Api/Models/LlmHealthDto.cs`
- Esistente: `apps/api/src/Api/Services/LlmClients/{OpenRouterLlmClient,OpenAiChatModels,DeepSeekLlmClient,OllamaLlmClient}.cs`
- Pattern: AlertRule aggregate in `BoundedContexts/Administration/Domain/Aggregates/AlertRule/`
- Provider quota docs:
  - OpenRouter: `https://openrouter.ai/docs/api-reference/get-credits`
  - DeepSeek: `https://api-docs.deepseek.com/quick_start/pricing` (`/user/balance`)

## 14. Gherkin scenari

I 31 scenari Gherkin per i 5 user goal (G1=7, G2=5, G3=6, G4=5, G5=4) sono nel turno di brainstorming precedente — verranno copiati in fase di plan come acceptance test executable.
