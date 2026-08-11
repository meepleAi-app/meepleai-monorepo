---
status: Draft (post-Phase F audit consolidation)
parent_issue: "#2055"
date: 2026-06-20
author: Claude (research subagent)
depends_on:
  - "#1823 (CLOSED 2026-06-12) — original L2 enrichment epic; Phase B-E shipped"
  - "#1903 + #1821 cluster (CLOSED) — SPARQL HTTP client + DB schema + L2 cascade"
  - "PR #2300 (MERGED 2026-06-14) — Phase F bundle (acknowledge mutator + TriggeredByAdminUserId column)"
  - "ADR adr-2026-06-09-wikidata-enrichment-architecture.md (Accepted) — DEC-3a..3j"
parent_spec_panel: "sess.46h synthesis (Wiegers + Fowler + Newman + Nygard + Crispin + Hightower) + 2026-06-20 follow-up review (this spec)"
---

# Wikidata L2 cover enrichment — design (gap-closure spec)

> **Status note**: questa spec consolida la design surface effettivamente shippata (Phase B/C/D/E/F) e formalizza il residuo Phase G (M14 FE attribution + M15 quarterly re-verification + IT-publisher fallback follow-up spike). Non duplica `adr-2026-06-09-wikidata-enrichment-architecture.md`: lo estende con i sei follow-up del spec-panel review 2026-06-20.

---

## 1. Contesto

Issue [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) (closed 2026-06-12) ha materializzato una pipeline E2E che enrica `shared_games.wikidata_cover_*` con cover images legal-clean (PD / CC0 / CC-BY / CC-BY-SA) da Wikidata `wdt:P18` + Wikimedia Commons API + R2 upload.

Issue [#2055](https://github.com/meepleAi-app/meepleai-monorepo/issues/2055) ("Plan harden") è OPEN e codifica:
- 4 DEC architetturali post P228 discovery (DEC-3a/b/c/d)
- ~3-4gg di lavoro residuo

**Stato as-shipped 2026-06-20** (verificato source-code-side da questa research session):

| Layer | Component | Path | Stato |
|---|---|---|---|
| Domain | `WikidataCoverEnrichmentAttempt` aggregate | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/WikidataCoverEnrichmentAttempt.cs` | ✅ Shipped — 4 outcomes (Success/Skipped/Failed/DeadLetter) + record-of-fact pattern + `Acknowledge()` mutator (Phase F) + `TriggeredByAdminUserId` (Phase F) |
| Domain | EF column family `wikidata_cover_*` on `shared_games` | migrations `20260610094431_AddWikidataQidColumnsToSharedGames`, `20260610110434_AddWikidataCoverEnrichmentAttemptsTable`, `20260613175905_AddAcknowledgeAndTriggerSourceToWikidataAttempts` | ✅ Shipped |
| Application | `IWikidataCoverEnrichmentRunner` orchestrator | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IWikidataCoverEnrichmentRunner.cs` | ✅ Shipped — single source of truth for M9 scheduler + M12 admin trigger |
| Application | `EnrichCatalogCoverCommand` + handler | `Application/Commands/EnrichCatalogCover/*` | ✅ Shipped |
| Application | `WikidataCoverEnrichmentJob` Quartz singleton | `Application/Jobs/WikidataCoverEnrichmentJob.cs` | ✅ Shipped — `[DisallowConcurrentExecution]`, 30/tick batch, 1s throttle |
| Application | `WikidataCoverDeadLetterRetentionJob` | `Application/Jobs/WikidataCoverDeadLetterRetentionJob.cs` | ✅ Shipped — 7-day sweep |
| Application | `WikidataQuarterlyReVerificationJob` | `Application/Jobs/WikidataQuarterlyReVerificationJob.cs` | ✅ Shipped — DEC-3i quarterly QID re-check (file present; behavior to audit Phase G) |
| Infrastructure | `WikidataCatalogProvider.FetchCoverImageAsync(qid)` | `Infrastructure/Providers/WikidataCatalogProvider.cs:116-209` | ✅ Shipped — DEC-3a extend + P18 SPARQL builder + circuit-breaker rethrow + latency metric |
| Infrastructure | `IWikimediaCommonsClient` + impl | `Infrastructure/Services/WikimediaCommonsClient.cs` | ✅ Shipped — DEC-3b separate client + `FetchLicenseAsync` + `FetchImageBytesAsync` |
| Infrastructure | `LicenseValidator` regex | `Infrastructure/Services/LicenseValidator.cs` | ✅ Shipped — DEC-3c whitelist regex + `Normalize()` |
| Infrastructure | `IWebpVariantGenerator` ImageSharp | `Infrastructure/Services/WebpVariantGenerator.cs` | ✅ Shipped — DEC-3d 200×300 webp |
| Infrastructure | `ICoverR2UploadPipeline` | `Infrastructure/Services/CoverR2UploadPipeline.cs` | ✅ Shipped |
| Infrastructure | `IWikimediaRateLimiter` token bucket | `Infrastructure/Services/InMemoryWikimediaRateLimiter.cs` | ✅ Shipped — DEC-3e shared 5 RPS |
| Infrastructure | Polly `WikimediaCircuitBreakerHandler` | `Infrastructure/Resilience/WikimediaCircuitBreakerHandler.cs` | ✅ Shipped — DEC-3f |
| Routing | `AdminWikidataCoverEnrichmentEndpoints` | `Routing/Admin/AdminWikidataCoverEnrichmentEndpoints.cs` | ✅ Shipped — `POST /{gameId}` trigger, `GET /dead-letters`, `POST /bulk-retry`, `POST /bulk-acknowledge`, `GET /games/{id}/attempts`, `GET /events` (SSE) |
| Observability | `MeepleAiMetrics.WikidataEnrichment` family | `Observability/Metrics/MeepleAiMetrics.WikidataEnrichment.cs` | ✅ Shipped — 5 metrics (attempts counter, sparql_latency histogram, qid_hit_rate gauge, queue_depth gauge, batch_duration histogram, dead_letter_count gauge) |
| Audit input | M0 spike output | `docs/spikes/1823/spike-summary.md` | ✅ Shipped — Gate decision GO (60% QID hit-rate, 100% license whitelist) |

**Residuo Phase G** (oggetto di questa spec + del plan companion):

1. **Phase F (Phase B M14)** — `<MeepleCard>` attribution footer FE component (license + source attribution link).
2. **Phase G1** — M15 audit + harden: verificare che `WikidataQuarterlyReVerificationJob` esista e abbia coverage test minima; se non shipped, completarlo.
3. **Phase G2** — IT-publisher fallback spike (Newman SN-001 follow-up): 50% IT hit-rate gap vs 73% top BGG osservato in M0 spike.
4. **Phase G3** — Gate 0 spike protocol formalization come **template riusabile** per port futuri (audio enrichment, publisher API, Italian Wikipedia fallback).
5. **Phase G4** — ports/adapters formalization via [[ADR-082]] (questa spec è il design input).

---

## 2. Decisioni locked

> ✅ **DECISION BUNDLE LOCKED 2026-06-20** — 7 DEC accepted in user review session 2026-06-20:
> - DEC-3k template repo-wide → estratto in [`external-api-spike-protocol.md`](../../for-developers/operations/external-api-spike-protocol.md)
> - DEC-3l / DEC-3l-1 / DEC-3m / DEC-3n / DEC-G6-1 accepted-as-default (bundle "Accept all defaults")
> - **DEC-3d-1 LOCKED su Magick.NET migration** (NOT ImageSharp 2.x downgrade) — cross-cutting refactor
> - **DEC-3h Cache-Control DESCOPED** — audit immediato 2026-06-20 ha confermato shipped in `CoverR2UploadPipeline.cs:18,67`
> - Audit trail: spec-panel review session 2026-06-20, sub-agent B output, user AskUserQuestion 2026-06-20

### 2.1 Confermate da ADR `adr-2026-06-09-wikidata-enrichment-architecture.md` (immutate)

| ID | Decisione | Status |
|---|---|---|
| DEC-3a | Extend `WikidataCatalogProvider` con `FetchCoverImageAsync(qid)` | ✅ Shipped |
| DEC-3b | Separate `IWikimediaCommonsClient` con shared rate-limiter | ✅ Shipped |
| DEC-3c | Hardcoded license whitelist regex (PD / CC0 / CC-BY / CC-BY-SA) | ✅ Shipped |
| DEC-3d | ImageSharp managed C# 200×300 webp | ✅ Shipped (verifica licenza § 11) |
| DEC-3e | Single-pod HPA=1 + in-process 5 RPS token bucket | ✅ Shipped |
| DEC-3f | Polly circuit breaker 3 fail / 60s → open 5min | ✅ Shipped |
| DEC-3g | 3 metrics minimum (attempts_total, sparql_latency, qid_hit_rate) — esteso a 5 in pratica | ✅ Shipped (5 metrics) |
| DEC-3h | R2 covers/* `Cache-Control: public, max-age=31536000, immutable` | ✅ **Confirmed shipped 2026-06-20** (`CoverR2UploadPipeline.cs:18,67`) — Phase 4 DESCOPED |
| DEC-3i | Quarterly QID re-verification cron + `WikidataQidLastVerifiedAt` column | ⚠️ Job presente, copertura test da audit Phase G |
| DEC-3j | Retry 3× backoff 1m/5m/15m + DL 7gg + 4xx/5xx/timeout/license-mismatch matrix | ✅ Shipped |

### 2.2 Nuove decisioni (questa spec, 2026-06-20)

#### DEC-3k — Gate 0 spike protocol come template riusabile

**Decisione**: il protocollo M0 spike (`docs/spikes/1823/spike-summary.md`) diventa template **canonico** per qualsiasi futuro port di enrichment esterno. Decision branches:

```yaml
gate_0_protocol_template:
  required_artifacts:
    - file: docs/spikes/{issue}/spike-summary.md
    - data:
        sample_size: ">=30 (4-bucket stratification: top/mid/local/niche)"
        primary_hit_rate: "measured"  # QID lookup, search match, etc.
        downstream_validation_rate: "measured"  # license, schema, format
        latency_p95: "measured"
  decision_branches:
    - primary_below_15pct: "ABORT — propose alternative source or L1 placeholder only"
    - primary_15_to_25pct: "DESCOPE — ship BE only, defer FE attribution UI"
    - primary_above_25pct: "GREEN — proceed full plan"
  audit_trail:
    - raw_input: docs/spikes/{issue}/sample-list.json
    - per_item_output: docs/spikes/{issue}/spike-results.json
    - runner_script: docs/spikes/{issue}/spike-runner.{sh,py}
```

**Rationale (Wiegers)**: il M0 spike originale ha già validato GO (60% hit-rate vs 25% threshold). Formalizzare il pattern previene future regression-by-assumption ("speculiamo 30%, costa la verifica") in port enrichment N (#2). Tracciabile in [[ADR-082]].

✅ **LOCKED 2026-06-20**: DEC-3k template estratto in canonical doc → [`docs/for-developers/operations/external-api-spike-protocol.md`](../../for-developers/operations/external-api-spike-protocol.md). Future external API integration **MUST** reference questo doc come `applies_to` field nel kickoff brief.

#### DEC-3l ✅ **LOCKED 2026-06-20** — DLQ riusa table esistente Phase F (no nuova table)

**Decisione**: il tavolo `wikidata_cover_enrichment_attempts` (shipped Phase B M9, Phase F M14) è **single source of truth** per:
- Outcome log (Success / Skipped / Failed / DeadLetter)
- Retry scheduling (`NextRetryAt`, `RetryCount`)
- Acknowledgement audit (`AcknowledgedAt`, `AcknowledgedBy`, Phase F)
- Admin trigger attribution (`TriggeredByAdminUserId`, Phase F)

**Verifica gap fields** (da auditare Phase G1):

| Campo richiesto da Nygard | Presente nell'entity? | Azione |
|---|---|---|
| `NextRetryAt` (UTC) | ✅ Sì | — |
| `RetryCount` | ✅ Sì | — |
| `DeadLetteredAt` | ✅ Sì | — |
| `Reason` machine-readable | ✅ Sì (max 64 char) | — |
| `Details` human-readable | ✅ Sì (max 1024 char) | — |
| `ScheduledAttemptCount` distinto da `RetryCount` | ❌ NO | Verifica in Phase G se serve (probabilmente `RetryCount` è sufficiente perché ogni row è un fact-record indipendente) |
| `LastError` distinto da `Details` | ❌ NO | Verifica in Phase G se serve (idem) |
| `CircuitBreakerOpenedAt` row a livello system (non per-game) | ❌ NO | Decisione: tracciare via Prometheus, NON in tabella (DEC-3l-1) |

**Sotto-decisione DEC-3l-1 ✅ LOCKED 2026-06-20**: lo stato circuit breaker NON entra in tabella. È runtime state del Polly handler; ops view via Prometheus metric (`meepleai.wikidata.sparql.latency_seconds` p99 spike + `WikidataEnrichmentAttempts{outcome="failure", reason="circuit-open"}` rate). **No admin reset endpoint** nel scope #2055 — Polly auto-recovery dopo break duration. Re-evaluation se metric breach observed.

#### DEC-3m ✅ **LOCKED 2026-06-20** — Orchestrator pattern (Fowler service composition)

**Decisione**: **Option B (facade orchestrator)** — `IWikidataCoverEnrichmentRunner` (shipped) È l'orchestrator. Il composer "mega-handler" è stato evitato. Pattern is:

```
WikidataCoverEnrichmentJob (Quartz)  ─┐
                                       ├──► IWikidataCoverEnrichmentRunner.EnrichAndRecordAsync(gameId, forceRefresh, triggeredByAdminUserId)
AdminWikidataCoverEnrichmentEndpoints ─┘                │
                                                        ├──► IMediator.Send(EnrichCatalogCoverCommand)
                                                        │       │
                                                        │       └──► EnrichCatalogCoverCommandHandler
                                                        │              ├──► WikidataCatalogProvider.FetchCoverImageAsync (DEC-3a)
                                                        │              ├──► IWikimediaCommonsClient.FetchLicenseAsync (DEC-3b)
                                                        │              ├──► LicenseValidator.IsWhitelisted (DEC-3c)
                                                        │              ├──► IWikimediaCommonsClient.FetchImageBytesAsync (DEC-3b)
                                                        │              ├──► IWebpVariantGenerator.GenerateAsync (DEC-3d)
                                                        │              └──► ICoverR2UploadPipeline.UploadAsync
                                                        │
                                                        ├──► IWikidataCoverEnrichmentRetryPolicy.Classify (DEC-3j)
                                                        │
                                                        ├──► IWikidataCoverEnrichmentAttemptRepository.AddAsync
                                                        ├──► IUnitOfWork.SaveChangesAsync
                                                        ├──► MeepleAiMetrics.IncrementWikidataDeadLetterCount (post-save)
                                                        └──► IWikidataEnrichmentEventBroadcaster.Publish (post-save SSE)
```

**Rationale (Fowler)**:
- Single composer, due caller (cron + admin) condividono `IWikidataCoverEnrichmentRunner` → audit trail uniforme.
- Handler restituisce solo terminal outcome; il runner applica retry policy + persiste attempt + emette metric/SSE (post-save garantito).
- Testabile in isolamento via `Runner.EnrichAndRecordAsync` con mediator mock.

#### DEC-3n ✅ **LOCKED 2026-06-20** — Ports/adapters layout (Newman BC boundary)

**Decisione**: ports vivono nel **BC `SharedGameCatalog` Application layer** (consumer-owned); adapters vivono in `Infrastructure/Services` (catalog-internal) o `Infrastructure/Providers` (shared con catalog seed).

Riferimento [[ADR-082]] per pattern formale.

```
apps/api/src/Api/BoundedContexts/SharedGameCatalog/
├── Application/Services/
│   └── IWikidataCoverEnrichmentRunner.cs       ← orchestrator port (consumer-owned)
└── Infrastructure/
    ├── Providers/
    │   └── WikidataCatalogProvider.cs           ← shared adapter (P1+P2 catalog seed + P18 cover)
    └── Services/
        ├── IWikimediaCommonsClient.cs           ← port (BC-internal)
        ├── WikimediaCommonsClient.cs            ← adapter
        ├── IWikimediaRateLimiter.cs             ← shared port (consumed by BOTH adapters)
        ├── InMemoryWikimediaRateLimiter.cs      ← adapter
        ├── LicenseValidator.cs                  ← pure domain helper (non-port)
        ├── IWebpVariantGenerator.cs             ← port
        ├── WebpVariantGenerator.cs              ← adapter (ImageSharp)
        ├── ICoverR2UploadPipeline.cs            ← port
        └── CoverR2UploadPipeline.cs             ← adapter (calls IBlobStorageService)
```

**Future BC consumer** (es. PdfDocument cover, Player avatar) registrerebbe:
- Proprio port `IPdfCoverEnrichmentRunner` nel proprio Application layer.
- Riusa adapter `IWikimediaCommonsClient` se rilevante.

NO mega-shared `MediaEnrichment` BC.

---

## 3. Architecture

### 3.1 Sequence diagram (as-shipped + Phase G FE attribution)

```mermaid
sequenceDiagram
    autonumber
    participant Q as Quartz Trigger<br/>(1 min cadence)
    participant J as WikidataCoverEnrichmentJob
    participant R as IWikidataCoverEnrichmentRunner
    participant M as IMediator
    participant H as EnrichCatalogCoverCommandHandler
    participant W as WikidataCatalogProvider
    participant C as IWikimediaCommonsClient
    participant L as LicenseValidator
    participant V as IWebpVariantGenerator (ImageSharp)
    participant U as ICoverR2UploadPipeline
    participant DB as MeepleAiDbContext
    participant Metric as MeepleAiMetrics
    participant SSE as IWikidataEnrichmentEventBroadcaster
    participant FE as &lt;MeepleCard&gt; (Phase G)

    Q->>J: Execute(tick)
    J->>J: queryDueGameIds(BatchSize=30)
    J->>Metric: SetWikidataQueueDepth(N)
    loop per ogni gameId
        J->>R: EnrichAndRecordAsync(gameId, forceRefresh=false, null)
        R->>M: Send(EnrichCatalogCoverCommand)
        M->>H: Handle
        H->>W: FetchCoverImageAsync(qid)
        Note over W: 1. IWikimediaRateLimiter.AcquireAsync (DEC-3e)<br/>2. SPARQL P18 query<br/>3. Polly circuit breaker (DEC-3f)<br/>4. Record sparql_latency histogram
        W-->>H: WikidataCoverImageResult.Found(filename, sourceUrl)
        H->>C: FetchLicenseAsync(filename)
        C-->>H: CommonsLicenseResult.Found(license, attribution)
        H->>L: IsWhitelisted(license)
        L-->>H: true
        H->>C: FetchImageBytesAsync(filename)
        C-->>H: byte[] (raw image)
        H->>V: GenerateAsync(bytes) → 200×300 webp
        V-->>H: byte[] webp
        H->>U: UploadAsync(gameId, webp)
        U-->>H: r2Key
        H->>DB: SharedGame.UpdateWikidataCover(r2Key, sourceUrl, license, attribution)
        H->>DB: SaveChangesAsync
        H-->>R: EnrichCatalogCoverResult.Success
        R->>R: Policy.Classify → Terminal
        R->>DB: AddAsync(WikidataCoverEnrichmentAttempt.RecordSuccess(...))
        R->>DB: SaveChangesAsync
        Note over R,DB: POST-save side-effects (DEC-3l-2 ordering)
        R->>Metric: WikidataEnrichmentAttempts++
        R->>SSE: Publish(WikidataEnrichmentEvent)
        J->>J: delay(1000ms) inter-item throttle
    end
    J->>Metric: WikidataBatchDuration.Record(elapsed)

    Note over FE: Phase G — read CoverR2Key + CoverLicense + CoverAttribution<br/>via existing SharedGameDto.CoverUrl + new attribution footer
```

**Pattern DEC-3l-2 (post-save side-effects)**: metric + SSE broadcast vivono **dopo** `SaveChangesAsync` per evitare phantom events su rollback. Mirror del pattern `LedgerEntryRepository.AddAndCommitAsync` (#1938) + `INotificationRepository.AddAndCommitAsync` (PR #2391).

### 3.2 Layered architecture

```
┌───────────────────────────────────────────────────────────────┐
│ Routing                                                        │
│ ├── AdminWikidataCoverEnrichmentEndpoints                      │
│ │   ├── POST /api/v1/admin/wikidata/enrichment/{gameId}        │
│ │   ├── GET  /api/v1/admin/wikidata/enrichment/dead-letters    │
│ │   ├── POST /api/v1/admin/wikidata/enrichment/bulk-retry      │
│ │   ├── POST /api/v1/admin/wikidata/enrichment/bulk-acknowledge│
│ │   ├── GET  /api/v1/admin/wikidata/enrichment/games/{id}/attempts │
│ │   └── GET  /api/v1/admin/wikidata/enrichment/events (SSE)    │
└───────────────────────────────────────────────────────────────┘
              │ MediatR.Send / runner orchestrator
              ▼
┌───────────────────────────────────────────────────────────────┐
│ Application (BC SharedGameCatalog)                             │
│ ├── Services/IWikidataCoverEnrichmentRunner ◄── orchestrator   │
│ ├── Commands/EnrichCatalogCover/                               │
│ │   ├── EnrichCatalogCoverCommand                              │
│ │   ├── EnrichCatalogCoverCommandHandler ◄── service composer  │
│ │   └── EnrichCatalogCoverResult (Success / Skipped / Failed)  │
│ ├── Commands/AdminEnrichWikidataCover/                         │
│ ├── Commands/AdminBulkRetryWikidataCover/                      │
│ ├── Commands/AdminBulkAcknowledgeWikidataCover/                │
│ ├── Queries/GetWikidataDeadLetters                             │
│ ├── Queries/GetWikidataAttemptTimeline                         │
│ ├── Jobs/WikidataCoverEnrichmentJob (Quartz)                   │
│ ├── Jobs/WikidataCoverDeadLetterRetentionJob (sweep 7d)        │
│ └── Jobs/WikidataQuarterlyReVerificationJob (DEC-3i, audit G1) │
└───────────────────────────────────────────────────────────────┘
              │ ports
              ▼
┌───────────────────────────────────────────────────────────────┐
│ Infrastructure (BC SharedGameCatalog)                          │
│ ├── Providers/WikidataCatalogProvider                          │
│ │   ├── FetchAsync (catalog seed, shipped #1903)               │
│ │   └── FetchCoverImageAsync (P18, DEC-3a)                     │
│ ├── Services/                                                  │
│ │   ├── IWikimediaCommonsClient + WikimediaCommonsClient       │
│ │   ├── IWikimediaRateLimiter + InMemoryWikimediaRateLimiter   │
│ │   ├── LicenseValidator (regex DEC-3c)                        │
│ │   ├── IWebpVariantGenerator + WebpVariantGenerator (ImageSharp) │
│ │   └── ICoverR2UploadPipeline + CoverR2UploadPipeline         │
│ └── Resilience/                                                │
│     ├── WikimediaCircuitBreakerHandler (Polly DEC-3f)          │
│     └── CircuitBreakerExceptionDetector                        │
└───────────────────────────────────────────────────────────────┘
              │ IBlobStorageService (shared)
              ▼
┌───────────────────────────────────────────────────────────────┐
│ Shared Infrastructure                                          │
│ ├── Services/Pdf/IBlobStorageService                           │
│ ├── Services/Pdf/BlobStorageServiceFactory (STORAGE_PROVIDER)  │
│ │   ├── BlobStorageService (local)                             │
│ │   └── S3BlobStorageService (R2 / AWS / MinIO)                │
│ └── Observability/Metrics/MeepleAiMetrics.WikidataEnrichment   │
└───────────────────────────────────────────────────────────────┘
```

### 3.3 Data model — `WikidataCoverEnrichmentAttempt` (as-shipped Phase F)

```csharp
public sealed class WikidataCoverEnrichmentAttempt : AggregateRoot<Guid>
{
    public Guid          SharedGameId            { get; private set; }  // FK shared_games.id
    public DateTime      AttemptedAt             { get; private set; }
    public WikidataCoverEnrichmentOutcome Outcome { get; private set; } // Success | Skipped | Failed | DeadLetter
    public string        Reason                  { get; private set; }  // machine-readable, max 64
    public string?       Details                 { get; private set; }  // human-readable, max 1024
    public int           RetryCount              { get; private set; }  // 0 = first, N = (N+1)-th retry
    public DateTime?     NextRetryAt             { get; private set; }  // null = terminal
    public DateTime?     DeadLetteredAt          { get; private set; }  // non-null when Outcome=DeadLetter
    public DateTime?     AcknowledgedAt          { get; private set; }  // Phase F F5
    public Guid?         AcknowledgedBy          { get; private set; }  // Phase F F5
    public Guid?         TriggeredByAdminUserId  { get; private set; }  // Phase F F6, null = M9 cron
}
```

**Record-of-fact pattern**: ogni attempt è una row immutable post-creation. Retry produce nuova row con `RetryCount=N+1`. Solo `Acknowledge()` muta (deroga documentata sull'aggregate doc-comment).

---

## 4. Bounded Context boundary

Riferimento [[ADR-082]] (ports/adapters layout for external media enrichment).

**SharedGameCatalog BC owns**:
- `IWikidataCoverEnrichmentRunner` (orchestrator port)
- `IWikimediaCommonsClient` (catalog-internal port — non shared)
- `IWebpVariantGenerator` / `ICoverR2UploadPipeline` (catalog-internal ports)

**Shared cross-BC infra**:
- `IBlobStorageService` (storage abstraction, riusato da PdfDocument BC)
- `IWikimediaRateLimiter` (rate-limit token bucket — sarebbe shared se future BC consumer chiamasse Wikimedia, ma per ora 1 consumer)

**Future BC enrichment** (es. PdfDocument cover via Wikimedia):
1. Aggiungere proprio port `IPdfCoverEnrichmentRunner` nel proprio BC Application.
2. Riusare adapter `IWikimediaCommonsClient` se rilevante (in tal caso promuovere a shared infra).
3. NO mega-shared `MediaEnrichment` BC inflation.

---

## 5. Domain model

### 5.1 Aggregati esistenti coinvolti

| Aggregato | BC | Modifiche `2055` |
|---|---|---|
| `SharedGame` | SharedGameCatalog | Già esteso con `WikidataCoverR2Key`, `WikidataCoverSourceUrl`, `WikidataCoverLicense`, `WikidataCoverAttribution`, `WikidataQid`, `WikidataQidLastVerifiedAt` (shipped) |
| `WikidataCoverEnrichmentAttempt` | SharedGameCatalog | Già shipped + Phase F mutator + TriggeredByAdminUserId |

### 5.2 NO nuovi aggregati

`#2055` non introduce nuovi aggregati. La pipeline è completamente reificata su `WikidataCoverEnrichmentAttempt` + `SharedGame`.

### 5.3 Domain events emitted

| Evento | Source | Consumer |
|---|---|---|
| `WikidataEnrichmentEvent` (broadcast non domain-event-outbox) | `WikidataCoverEnrichmentRunner` post-save | SSE subscribers (`GET /api/v1/admin/wikidata/enrichment/events`) |

**NOT a domain event** in `domain_event_outbox` sense — è pure SSE fan-out con `Channel<T>` per-subscriber + `DropOldest` policy. Non sopravvive a pod restart (DEC-3e single-pod consente).

---

## 6. Application layer

### 6.1 Orchestrator port (shipped)

```csharp
internal interface IWikidataCoverEnrichmentRunner
{
    Task<EnrichCatalogCoverResult> EnrichAndRecordAsync(
        Guid gameId,
        bool forceRefresh,
        Guid? triggeredByAdminUserId = null,
        CancellationToken cancellationToken = default);
}
```

### 6.2 Commands esistenti (shipped)

- `EnrichCatalogCoverCommand` — single-game enrichment, returns `EnrichCatalogCoverResult` (Success/Skipped/Failed discriminated union)
- `EnrichCatalogCoverBatchCommand` — batch wrapper
- `AdminEnrichWikidataCoverCommand` — admin trigger wrapper con audit
- `AdminBulkRetryWikidataCoverCommand` — Phase E F2 (max 50/batch)
- `AdminBulkAcknowledgeWikidataCoverCommand` — Phase F F5 (max 50/batch)

### 6.3 Quartz jobs

| Job | Cadenza | DEC | Stato |
|---|---|---|---|
| `WikidataCoverEnrichmentJob` | 1 min (30 games/tick) | DEC-3e | ✅ Shipped |
| `WikidataCoverDeadLetterRetentionJob` | 03:00 UTC daily | DEC-3j (7d window) | ✅ Shipped |
| `WikidataQuarterlyReVerificationJob` | 90gg | DEC-3i | ⚠️ File shipped, audit copertura Phase G |

---

## 7. Infrastructure layer

### 7.1 Adapter `WikidataCatalogProvider.FetchCoverImageAsync` (shipped, DEC-3a)

```csharp
public async Task<WikidataCoverImageResult> FetchCoverImageAsync(
    string qid,
    CancellationToken ct)
{
    // 1. QID regex validation (anti SPARQL injection)
    // 2. IWikimediaRateLimiter.AcquireAsync (shared 5 RPS)
    // 3. SPARQL P18 query
    // 4. Polly circuit breaker via WikimediaCircuitBreakerHandler
    // 5. JSON parse + extract Commons filename
    // 6. Record meepleai.wikidata.sparql.latency_seconds
}
```

### 7.2 Adapter `WikimediaCommonsClient` (shipped, DEC-3b)

```csharp
internal interface IWikimediaCommonsClient
{
    Task<CommonsLicenseResult> FetchLicenseAsync(string filename, CancellationToken ct);
    Task<byte[]?> FetchImageBytesAsync(string filename, CancellationToken ct);
}
```

Riferimento: `Infrastructure/Services/IWikimediaCommonsClient.cs:26-83`.

### 7.3 Adapter `LicenseValidator` (shipped, DEC-3c)

Regex: `^(?:public domain|PD|CC0|CC[ -]BY(?:[ -][0-9.]+)?|CC[ -]BY[ -]SA(?:[ -][0-9.]+)?)$` con `IgnoreCase | CultureInvariant | Compiled | ExplicitCapture` + timeout 50ms (DoS-safe).

### 7.4 Adapter `WebpVariantGenerator` (shipped, DEC-3d)

ImageSharp 3.x managed C# → 200×300 webp.

**Verifica licenza ImageSharp 3.x** (§ 11): pin major version, audit Six Labors Split License vs Apache 2.0.

> **✅ RESOLVED / MOOT (DEC-3d-1, issue #2055 Phase G)** — l'audit licenza Six Labors è superato: `SixLabors.ImageSharp` è stato **rimosso dal backend** e sostituito da **`Magick.NET-Q8-AnyCPU` 14.x (Apache 2.0)** (`WebpVariantGenerator`, `VisionOcrAdapter`, `GamebookPhotoStorageService`). Nessun package/uso ImageSharp residuo; regressione presidiata dal guard CI `infra/scripts/lint-deps-imagesharp.sh` (dev-fast.yml). Chiude il gate go-live "audit licenza image-lib (Six Labors)" del tracker #3373 / ADR-087.

### 7.5 R2 upload pipeline

`ICoverR2UploadPipeline` riusa `IBlobStorageService` (`BlobStorageServiceFactory.Create()` factory `STORAGE_PROVIDER=s3` → R2 endpoint). Vedi `Services/Pdf/BlobStorageServiceFactory.cs:32-95`.

**Verifica Cache-Control header** (Phase G audit, DEC-3h): conferma che upload setta `Cache-Control: public, max-age=31536000, immutable` su R2 PUT (header `S3PutObjectRequest.Headers["Cache-Control"]`).

---

## 8. Failure modes + DLQ

### 8.1 Matrice failure (DEC-3j, shipped)

| Failure type | Detect via | Retry policy | Backoff | Terminal? |
|---|---|---|---|---|
| 4xx 404 (QID missing) | `WikidataCoverImageResult.NotFound` | NO | — | ✅ Skipped (no retry) |
| 4xx 403 (forbidden) | HTTP status | NO | — | ✅ DeadLetter (no retry) |
| 5xx server error | HTTP status | YES | 1m / 5m / 15m | After 3 retries → DeadLetter |
| Timeout (`TaskCanceledException` no caller-cancel) | DEC-3a guard pattern | YES | 1m / 5m / 15m | After 3 retries → DeadLetter |
| Circuit breaker open | `CircuitBreakerExceptionDetector.IsBrokenCircuit` rethrow | YES | 1m / 5m / 15m | `RetryCount` NOT incremented (DEC-3l infrastructure failure) |
| License mismatch (whitelist fail) | `LicenseValidator.IsWhitelisted == false` | NO | — | ✅ Skipped (will never succeed) |
| Image bytes empty | `FetchImageBytesAsync == null` | YES (transient) | 1m / 5m / 15m | After 3 retries → DeadLetter |
| Image processing error (ImageSharp throw) | `ImageProcessingException` | YES | 1m / 5m / 15m | After 3 retries → DeadLetter |
| R2 upload error (network) | `S3Exception` | YES | 1m / 5m / 15m | After 3 retries → DeadLetter |

### 8.2 Circuit breaker (DEC-3f, shipped)

`WikimediaCircuitBreakerHandler` (Polly) wrappa `HttpClient` per `WikidataCatalogProvider` + `IWikimediaCommonsClient`. State machine:

- **Closed** (default): pass-through
- **Open** dopo 3 consecutive 5xx in 60s: rifiuta richieste per 5min
- **Half-open** dopo 5min: 1 prova; success → closed, fail → open di nuovo

`CircuitBreakerExceptionDetector.IsBrokenCircuit(ex)` identifica eccezioni Polly v7/v8 con FQN match (workaround namespace shift Polly v7→v8). Rethrown in `WikidataCatalogProvider.FetchCoverImageAsync` per evitare di confondere "upstream unavailable" con "no P18 claim".

**Manual override**: NESSUN endpoint admin per reset breaker (out-of-scope; ops via redeploy o aspetto naturale 5min half-open). Future improvement tracking gap: P1 admin button `POST /api/v1/admin/wikidata/circuit-breaker/reset`.

### 8.3 DLQ retention (DEC-3j shipped)

`WikidataCoverDeadLetterRetentionJob` daily 03:00 UTC sweep:
- Cancella attempts con `Outcome == DeadLetter && DeadLetteredAt < now - 7d && AcknowledgedAt != null` (acked rows safe per audit).
- Cancella attempts con `Outcome == DeadLetter && DeadLetteredAt < now - 30d` (forza eviction anche non-acked).
- Re-anchora `WikidataDeadLetterCount` gauge a ground-truth count.

### 8.4 Admin DLQ visibility (Phase E + F, shipped)

| Endpoint | Phase | Purpose |
|---|---|---|
| `GET /admin/wikidata/enrichment/dead-letters` | E F3 | Paginated list con filter `reason`, `includeAcknowledged` |
| `GET /admin/wikidata/enrichment/games/{id}/attempts` | E F3 | Timeline drawer per-game |
| `POST /admin/wikidata/enrichment/bulk-retry` | E F2 | Max 50/batch retry |
| `POST /admin/wikidata/enrichment/bulk-acknowledge` | F F5 | Max 50/batch acknowledge + note ≤500 char (log-only) |
| `GET /admin/wikidata/enrichment/events` (SSE) | E F4 | Real-time stream `WikidataEnrichmentEvent` con 15s heartbeat |

---

## 9. Observability

### 9.1 Prometheus metrics (DEC-3g, shipped — 5 metrics)

| Metric | Type | Cardinality | Suggested alert |
|---|---|---|---|
| `meepleai.wikidata.enrichment.attempts.total` | Counter | tag `outcome=success\|failure\|dead_letter` | `dead_letter rate > 5%` sustained 1 batch → license drift |
| `meepleai.wikidata.sparql.latency_seconds` | Histogram | — | p99 > 10s sustained 5min → endpoint degraded, pre-trip breaker |
| `meepleai.wikidata.qid_hit_rate` | Gauge | — | drop > 10pp below 30d avg → schema drift |
| `meepleai.wikidata.queue_depth` | Gauge | — | depth > 5000 sustained 1h → scheduler under-provisioned |
| `meepleai.wikidata.batch_duration_seconds` | Histogram | — | p95 > 90s → overrunning 60s trigger interval |
| `meepleai.wikidata.dead_letter_count` | Gauge | — | count > 100 sustained 1h → operator triage backlog |

Riferimento: `Observability/Metrics/MeepleAiMetrics.WikidataEnrichment.cs`.

### 9.2 Grafana dashboard

**Verifica Phase G** (audit): esiste panel dedicato in `infra/monitoring/grafana/dashboards/`? Se NO, aggiungere come parte di [[Phase G/Plan]].

Panel suggeriti (3 minimum):
1. **Enrichment rate per night** — `rate(meepleai_wikidata_enrichment_attempts_total[24h])` stacked by `outcome`.
2. **Failure breakdown** — top-5 `reason` labels su `WikidataEnrichmentAttempts{outcome="failure"}`.
3. **Circuit-breaker state estimator** — `histogram_quantile(0.99, rate(meepleai_wikidata_sparql_latency_seconds_bucket[5m]))` vs `rate(WikidataEnrichmentAttempts{outcome="failure", reason="circuit-open"}[5m])`.

### 9.3 SSE event stream

`GET /admin/wikidata/enrichment/events` emette `event: attempt-recorded\ndata: {WikidataEnrichmentEvent}\n\n`. Per-subscriber `Channel<T>` con `BoundedChannelOptions(128, FullMode.DropOldest)` — slow subscriber droppato, NON back-pressure scheduler tick.

---

## 10. Gate 0 spike protocol

### 10.1 Storico — M0 spike #1823 (eseguito 2026-06-09, sess.46h)

| Threshold | Target | Misurato | Status |
|---|---|---|---|
| QID hit-rate | ≥ 25% (GO) | **60%** | ✅ +35pp |
| License machine-readable | ≥ 80% (GO) | **93%** | ✅ +13pp |
| License whitelist match | informativo | **100%** (13/13) | ✅ |

**Catalog-wide forecast** (weighted): ~59.6% (0.30 × 0.73 + 0.40 × 0.63 + 0.25 × 0.50 + 0.05 × 0.00).

**Decisione**: ✅ GO (proceed full plan).

### 10.2 Gate 0 protocol come template riusabile (DEC-3k)

Per qualsiasi futuro port enrichment (IT-publisher fallback, audio enrichment, publisher API):

```yaml
gate_0_protocol:
  step_1_sample_assembly:
    - sample_size: ">=30 stratified"
    - stratification: "4-bucket (top / mid / local / niche)"
    - output: docs/spikes/{issue}/sample-list.json

  step_2_probe_execution:
    - script: docs/spikes/{issue}/spike-runner.{sh,py}
    - measures:
        - primary_hit_rate    # QID lookup / search match / API resolve
        - downstream_validation_rate  # license / schema / format
        - latency_p50_p95     # endpoint health snapshot
    - output: docs/spikes/{issue}/spike-results.json

  step_3_decision:
    branches:
      primary_below_15pct:
        action: ABORT
        deliverable: alternative source proposal OR L1 placeholder-only ADR

      primary_15_to_25pct:
        action: DESCOPE
        deliverable: ship BE only, defer FE attribution UI to follow-up
        rationale: ROI insufficient per attribution surface investment

      primary_above_25pct:
        action: GREEN
        deliverable: full plan execution

  step_4_documentation:
    - mandatory: docs/spikes/{issue}/spike-summary.md
    - contents:
        - decision_gate_table (target vs measured)
        - per_bucket_hit_rate_table
        - validation_of_spec_panel_concerns
        - implementation_implications
        - out_of_scope_followups
        - raw_data_links

  step_5_adr_input:
    - if GREEN: spike-summary feeds the ADR "Decision" + "Consequences" sections
    - if DESCOPE/ABORT: spike-summary feeds the ADR "Alternatives Considered" + "Rejection rationale"
```

### 10.3 Application al residuo Phase G — IT-publisher fallback (out of scope #2055)

L'IT-publisher gap (50% IT vs 73% top BGG, ~23pp delta) è tracciato come **separate spike** per Phase G2. Trigger condition: post-deploy se IT catalog coverage rimane < 60%. Tracking: future issue, NOT #2055.

---

## 11. Security & legal

### 11.1 License whitelist enforcement (DEC-3c)

Whitelist hardcoded in `LicenseValidator.cs`: PD / CC0 / CC-BY / CC-BY-SA. Tutto il resto (CC-BY-NC, CC-BY-ND, "All Rights Reserved", "Fair use") è **rifiutato** → `EnrichCatalogCoverResult.Skipped { Reason = "license-not-whitelisted" }`.

### 11.2 Attribution storage

`SharedGame.WikidataCoverAttribution` salva la stringa raw da `extmetadata.Artist` Commons. Format: HTML + plain text — sanitize prima del render FE (Phase G M14 attribution footer).

#### DEC-G6-1 ✅ **LOCKED 2026-06-20** — FE attribution rendering: plain text

**Decisione**: FE renderizza `WikidataCoverAttribution` come **plain text** (NOT `dangerouslySetInnerHTML` + DOMPurify allowlist).

**Implementation pattern**:
- **BE upstream**: `AttributionTextExtractor.Strip(rawHtml)` invocato durante `EnrichCatalogCoverCommandHandler` PRIMA di persistere su DB. Strip via `HtmlAgilityPack` o equivalente — output puro text.
- **DB column**: `WikidataCoverAttribution` salva già la versione text-only (no HTML).
- **FE component**: `<MeepleCard.AttributionFooter>` renderizza come `<small>{game.wikidataCoverAttribution}</small>` plain — no XSS surface.

**Rationale (Hightower + security)**:
- Plain text elimina XSS attack surface a costo zero (no DOMPurify dep).
- Attribution typicallt è "Author Name + License + Year" — markdown/HTML rare nei Commons.
- Future: se l'utente designer richiede HTML fidelity (es. link cliccabile su `Author Name`), revisit con DOMPurify allowlist v2.

### 11.3 Source URL preservation

`SharedGame.WikidataCoverSourceUrl` salva canonical Wikidata entity URL (`https://www.wikidata.org/wiki/Q{qid}`). FE attribution footer linka qui (Phase G M14).

### 11.4 GDPR posture

Cover images da Wikidata/Commons sono **public domain o CC-licensed** per definizione. Non-PII. No data subject rights triggered. Compliance check: cross-ref [[ADR-059]] (catalog seed legal posture).

### 11.5 BGG ban compliance (#2123)

Wikidata enrichment è **3rd-party legal-clean alternative** al user-side BGG image ban (#2123). FE serve `WikidataCoverR2Key` via R2 CDN (NOT BGG host). Vedi CLAUDE.md § Active Freezes "BGG user-side asset ban".

### 11.6 ImageSharp licenza (DEC-3d follow-up)

**Decisione locked** in ADR Wikidata 2026-06-09: ImageSharp Apache 2.0.

**Audit Phase G richiesto**:
- ImageSharp v1.x (Apache 2.0) — OK
- ImageSharp v2.x (Apache 2.0) — OK
- ImageSharp v3.x (**Six Labors Split License**) — **WARNING**: commercial use richiede licenza commerciale.

**Action item**: il plan Phase G1 audita la version pin (`SixLabors.ImageSharp` PackageReference) vs licenza:
- Se 3.x con commercial-use → **decisione blocking**: downgrade a 2.x OR procurare licenza commerciale OR sostituire con SkiaSharp.
- Se 2.x → no action.

**Tracking**: la decisione finale verrà documentata come `DEC-3d-1` sotto-decisione in ADR Wikidata 2026-06-09 update post-audit.

### 11.7 R2 bucket policy

`covers/*` path: public-read ACL via Cloudflare R2 bucket policy. Cache-Control `public, max-age=31536000, immutable` (DEC-3h). Verifica Phase G audit (vedi § 7.5).

---

## 12. Testing strategy

### 12.1 As-shipped coverage (per audit Phase G)

| Suite | Path | Expected count | Stato |
|---|---|---|---|
| Unit: `WikidataCatalogProvider.FetchCoverImageAsync` | `tests/Api.Tests/Unit/SharedGameCatalog/Infrastructure/Providers/WikidataCatalogProviderTests.cs` | ~15-20 cases (regex / P18 found / P18 missing / 5xx / timeout / circuit-open / cancel) | Audit Phase G |
| Unit: `WikimediaCommonsClient` | `tests/Api.Tests/Unit/SharedGameCatalog/Infrastructure/Services/WikimediaCommonsClientTests.cs` | ~10 cases (license found / not-available / 404 / malformed) | Audit Phase G |
| Unit: `LicenseValidator` | `tests/Api.Tests/Unit/SharedGameCatalog/Infrastructure/Services/LicenseValidatorTests.cs` | ≥ 10 cases per Crispin C-002 (whitelist matrix) | Audit Phase G |
| Unit: `WebpVariantGenerator` | `tests/Api.Tests/Unit/SharedGameCatalog/Infrastructure/Services/WebpVariantGeneratorTests.cs` | ~5 cases (resize + encode + edge cases) | Audit Phase G |
| Unit: `WikidataCoverEnrichmentRunner` | `tests/Api.Tests/Unit/SharedGameCatalog/Application/Services/WikidataCoverEnrichmentRunnerTests.cs` | retry classify + post-save ordering + SSE publish + dead-letter increment | Audit Phase G |
| Integration: `WikidataCoverEnrichmentJob` Testcontainers | `tests/Api.Tests/Integration/SharedGameCatalog/Jobs/WikidataCoverEnrichmentJobIntegrationTests.cs` | full pipeline E2E con mock SPARQL + mock Commons + mock R2 | Audit Phase G |
| Integration: admin endpoints | `tests/Api.Tests/Integration/Admin/AdminWikidataCoverEnrichmentEndpointsIntegrationTests.cs` | 6 endpoints + auth + idempotency | Audit Phase G |

### 12.2 Contract tests con WireMock (Crispin C-002 follow-up)

**Status**: NOT yet shipped (gap identificato dal spec-panel review).

**Phase G plan deliverable**: aggiungere WireMock.Net contract tests con fixture reali da Wikidata + Commons:

```
tests/Api.Tests/Fixtures/Wikidata/
├── P18-catan-q1234.json           # P18 query success per Catan QID
├── P18-no-image.json              # QID valido senza P18 claim
├── P18-503.json                   # Wikidata 503 transient
├── commons-license-pd.json        # Commons imageinfo PD
├── commons-license-cc-by-sa.json  # CC-BY-SA whitelisted
├── commons-license-cc-by-nc.json  # CC-BY-NC RIFIUTATO (whitelist guard)
├── commons-malformed.json         # Malformed JSON body
└── commons-license-missing.json   # extmetadata.LicenseShortName missing
```

Refresh policy: ogni release minor, manual update tramite `curl + diff review`. Tracking: `infra/scripts/wikidata-fixtures/refresh.sh` (Phase G plan task).

### 12.3 Gate 0 smoke test post-deploy

Post-Phase G deploy:
1. Trigger admin endpoint `POST /api/v1/admin/wikidata/enrichment/{catan-id}` con noto-good QID.
2. Verifica response `outcome=success`.
3. Verifica R2 object esistente: `r2://covers/{catan-id}/cover.webp`.
4. Verifica DB row: `shared_games.wikidata_cover_r2_key != NULL`.
5. Verifica metric: `meepleai_wikidata_enrichment_attempts_total{outcome="success"}` incrementato.
6. Verifica SSE: subscribed `GET /events` riceve `event: attempt-recorded` payload.

---

## 13. Acceptance criteria

### 13.1 Già soddisfatti (closure #1823)

- [x] DB migration adds `WikidataCoverR2Key`, `WikidataCoverSourceUrl`, `WikidataCoverLicense`, `WikidataCoverAttribution` columns (shipped via #1903/#1821)
- [x] CQRS `EnrichCatalogCoverCommand` + scheduler + dead-letter (shipped Phase B/C)
- [x] R2 bucket policy: public read on `covers/*` path (assumed, audit Phase G)
- [x] Idempotent: re-run skips games already enriched (force-refresh flag carve-out)
- [x] M0 spike + Gate 0 GO decision (shipped 2026-06-09 sess.46h)
- [x] DEC-3a/3b/3c/3d/3e/3f/3g/3i/3j applied (per source code audit)
- [x] Phase F bulk-acknowledge + TriggeredByAdminUserId (PR #2300 merged 2026-06-14)

### 13.2 Phase G (questa spec)

- [ ] **AC-G1** — Audit + harden `WikidataQuarterlyReVerificationJob` (DEC-3i):
  - [ ] Test coverage ≥ 5 cases (Quarterly trigger / QID still valid / QID reassigned / License changed / R2 key stale)
  - [ ] Metric per re-verification outcomes
- [ ] **AC-G2** — ImageSharp licenza audit (DEC-3d follow-up):
  - [ ] Identificare version pin in `apps/api/src/Api/Api.csproj`
  - [ ] Documentare DEC-3d-1 (downgrade / commercial / replace) in ADR Wikidata 2026-06-09 update
- [ ] **AC-G3** — Cache-Control header audit (DEC-3h):
  - [ ] Verifica `CoverR2UploadPipeline.UploadAsync` setta header `public, max-age=31536000, immutable`
  - [ ] Se assente, aggiungere + test
- [ ] **AC-G4** — Grafana dashboard panel (DEC-3g extension):
  - [ ] 3 panel minimum (enrichment rate, failure breakdown, circuit-breaker estimator)
  - [ ] Path: `infra/monitoring/grafana/dashboards/wikidata-enrichment.json`
- [ ] **AC-G5** — Contract tests WireMock (Crispin C-002 follow-up):
  - [ ] ≥ 8 fixtures in `tests/Api.Tests/Fixtures/Wikidata/`
  - [ ] Test suite `WikidataContractTests.cs`
- [ ] **AC-G6** — FE `<MeepleCard>` attribution footer (originale M14):
  - [ ] Read `SharedGameDto.WikidataCoverLicense` + `WikidataCoverAttribution` + `WikidataCoverSourceUrl`
  - [ ] Render footer condizionale solo se license != NULL
  - [ ] Link source URL (`rel="nofollow"`)
  - [ ] Vitest + axe a11y test pass
- [ ] **AC-G7** — [[ADR-082]] (ports/adapters) shipped Status: Accepted
- [ ] **AC-G8** — Staging dry-run smoke 5 noto-good games → all `Success`
- [ ] **AC-G9** — Update #2055 body con closure summary + close issue

---

## 14. Out of scope

- ❌ Audio enrichment (separate epic, Wikidata `wdt:P51` audio property)
- ❌ Multi-source fallback (publisher API, IGDB, Italian Wikipedia) — Phase H separate
- ❌ BGG image fetch (#2123 BAN, user-side hard-block, no exceptions)
- ❌ Multi-pod Wikimedia rate-limiter (DEC-3e single-pod constraint stays — finché HPA=1)
- ❌ Circuit breaker admin reset endpoint (defer to follow-up issue P1)
- ❌ Source-attribution audit table separato da `WikidataCoverEnrichmentAttempt` (rejected — DEC-3l riusa table esistente)
- ❌ Redis pub/sub multi-pod SSE backplane (#2256 deferred until DEC-3e is lifted)

---

## 15. Riferimenti

### 15.1 Issue tracking

- Issue ombrello L1+L2+L3: [#1821](https://github.com/meepleAi-app/meepleai-monorepo/issues/1821) (CLOSED)
- Issue L2 epic: [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) (CLOSED 2026-06-12)
- Issue Plan harden: [#2055](https://github.com/meepleAi-app/meepleai-monorepo/issues/2055) (OPEN, target di questa spec)
- Issue Phase F follow-up: [#2256](https://github.com/meepleAi-app/meepleai-monorepo/issues/2256) (multi-pod backplane, deferred)

### 15.2 ADR

- [`adr-2026-06-09-wikidata-enrichment-architecture.md`](../../for-claude/architecture/adr/adr-2026-06-09-wikidata-enrichment-architecture.md) — Accepted, DEC-3a..3j
- [[ADR-082]] — Proposed, ports/adapters layout (`adr-082-external-media-enrichment-ports.md`)
- [ADR-059](../../for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md) — Catalog seed legal posture (cross-ref BGG ban + Wikidata fallback)

### 15.3 Plan

- [[Plan 2026-06-20 — Wikidata L2]] — companion file `docs/superpowers/plans/2026-06-20-wikidata-l2.md`
- [Plan 2026-06-09 — Large/Medium remaining](../../superpowers/plans/2026-06-09-large-medium-remaining-plan.md) § Phase 3 #1823 (storico)

### 15.4 Spike

- [Spike summary 2026-06-09](../../spikes/1823/spike-summary.md) — GO decision
- [Spike runner](../../spikes/1823/spike-runner.sh)
- [Sample list](../../spikes/1823/sample-list.json)
- [Spike results](../../spikes/1823/spike-results.json)

### 15.5 Phase F bundle (cross-ref)

- PR [#2300](https://github.com/meepleAi-app/meepleai-monorepo/pull/2300) (MERGED 2026-06-14)
- Phase F spec: `docs/superpowers/specs/2026-06-13-issue-2254-2255-phase-f-bundle-design.md`
- Phase F plan: `docs/superpowers/plans/2026-06-13-issue-2254-2255-phase-f-bundle.md`

### 15.6 Spec-panel synthesis

- 2026-06-09 sess.46h: Wiegers + Fowler + Newman + Nygard + Crispin + Hightower (original)
- 2026-06-20 (this spec): post-Phase F audit consolidation + gap closure

---

**Last updated**: 2026-06-20 | **Status**: Draft — pending Gate 0 audit phase confirmation before Phase G execution.
