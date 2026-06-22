# ADR-082 — External Media Enrichment: Ports/Adapters Layout

**Status**: Accepted
**Date**: 2026-06-20 (proposed) · 2026-06-22 (ratified post-verification, #2055 Phase 1)
**Deciders**: @badsworm
**Tracking**: [#2055](https://github.com/meepleAi-app/meepleai-monorepo/issues/2055) ("Plan harden") gap closure
**Related**:
- [`adr-2026-06-09-wikidata-enrichment-architecture.md`](./adr-2026-06-09-wikidata-enrichment-architecture.md) (Accepted) — DEC-3a..3j Wikidata cover pipeline
- [ADR-059](./adr-059-catalog-seed-legal-posture.md) — catalog seed legal posture
- [`docs/superpowers/specs/2026-06-20-wikidata-l2-design.md`](../../../superpowers/specs/2026-06-20-wikidata-l2-design.md) § 2.1 DEC-3n + § 4 BC boundary
- [`docs/superpowers/plans/2026-06-20-wikidata-l2.md`](../../../superpowers/plans/2026-06-20-wikidata-l2.md) Phase 1
- [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) (CLOSED) — parent epic

---

## Context

The `SharedGameCatalog` bounded context enriches `shared_games.wikidata_cover_*` via external services (Wikidata SPARQL `wdt:P18` + Wikimedia Commons API + Cloudflare R2 storage). The full pipeline was shipped under issue #1823 across Phase B/C/D/E/F (closed 2026-06-12 + PR [#2300](https://github.com/meepleAi-app/meepleai-monorepo/pull/2300) merged 2026-06-14).

The spec-panel review 2026-06-20 on issue [#2055](https://github.com/meepleAi-app/meepleai-monorepo/issues/2055) — specifically Newman SN-001 — identified an architectural ambiguity:

> Where does `IWikimediaCommonsClient` belong? Catalog-internal (consumer-owned) or shared infrastructure (cross-BC reusable)? Future bounded contexts may want to enrich images too (PdfDocument cover thumbnails, Player avatars, GameNight event posters). The current pipeline does not formalize the ports/adapters boundary, so future consumers either duplicate the HTTP wiring or "borrow" the catalog-internal port + accidentally create cross-BC tight coupling.

This ADR formalizes the ports/adapters layout for external media enrichment and codifies the **rejection** of a `MediaEnrichment` shared bounded context (BC inflation anti-pattern).

---

## Problem

Three architectural questions need a locked decision before any future BC tries to enrich media via Wikimedia:

1. **Ownership** — does `IWikimediaCommonsClient` live in `SharedGameCatalog` (catalog-internal) or in a shared `Infrastructure/ExternalServices/Wikimedia/` namespace?
2. **Reuse pattern** — if BC #2 wants to enrich its own images via Wikimedia, does it (a) inject `IWikimediaCommonsClient` directly, (b) define its own port + adapter, (c) consume a shared `IMediaEnricher` facade?
3. **Rate-limit coordination** — Wikimedia publishes a 5 RPS cap. The token-bucket service (`IWikimediaRateLimiter`) must coordinate ALL Wikimedia callers across pods to avoid IP-ban. Where does the rate-limiter live?

The choices affect: BC boundary purity, deployment surface (adapter registration), testability (port mocking granularity), and future extensibility (multi-source enrichment).

---

## Options Considered

### Option A — Catalog-internal ports, shared adapters by promotion

- Port `IWikimediaCommonsClient` lives in `SharedGameCatalog/Infrastructure/Services/` (catalog-internal `internal` visibility).
- Adapter `WikimediaCommonsClient` colocated.
- Shared rate-limiter `IWikimediaRateLimiter` lives in `SharedGameCatalog/Infrastructure/Services/` initially; **promoted** to a shared infrastructure namespace only when a second BC consumer ships.
- Future BC #2 defines its OWN port (e.g. `IPdfCoverEnrichmentRunner`) in its own Application layer + injects the SHARED rate-limiter; may reuse the Commons adapter if applicable (then promote the adapter too).

**Pros**:
- Maintains BC purity — each BC owns its consumption surface.
- No premature shared abstraction. BC boundary is not crossed until justified by a second consumer.
- Catalog-internal `internal` visibility prevents accidental cross-BC import.
- Promotion ladder is explicit + reversible (start internal → promote on demand).

**Cons**:
- Requires discipline at promotion time: where do shared ports/adapters live? (Answer below in Decision § Adapter promotion path.)
- Possible duplication if 2 BCs ship simultaneously and neither promotes first.

### Option B — Shared `MediaEnrichment` bounded context

Create a new BC `MediaEnrichment` owning:
- Aggregates: `MediaEnrichmentAttempt`, `MediaSource`, `LicenseValidator`
- Ports: `IWikimediaCommonsClient`, `IPublisherApiClient`, etc.
- Adapters: all external clients + rate-limiters + circuit breakers

Consumer BCs (SharedGameCatalog, PdfDocument, Player) cross-BC call `MediaEnrichment.EnrichAsync(target, source)`.

**Pros**:
- Cross-BC unified entry point.
- One place to audit license whitelist + rate-limit + circuit breaker.
- One place to add new sources (Italian Wikipedia, IGDB).

**Cons**:
- BC inflation: 18 BCs → 19 BCs for a slice of 2-3 enrichment pipelines.
- Cross-BC mediator calls introduce ambiguous ownership (whose audit log? whose retry policy?).
- Tight coupling: any new source forces ALL consumer BCs to re-deploy.
- Pre-emptive abstraction — no second consumer exists yet.

### Option C — Adapter direct injection (status quo, undocumented)

BC #2 wants to enrich → injects `IWikimediaCommonsClient` directly from SharedGameCatalog catalog-internal namespace. No port re-definition.

**Pros**:
- Zero new code.
- Adapter reuse without ceremony.

**Cons**:
- Cross-BC tight coupling: BC #2 depends on SharedGameCatalog's internal types.
- Breaks BC boundary purity — `internal` visibility forces upgrade to `public` (leak SharedGameCatalog internals).
- Future SharedGameCatalog refactor breaks BC #2 silently.
- Anti-pattern per DDD bounded context rules.

---

## Decision

**Option A** — Catalog-internal ports + shared adapter promotion ladder.

### Layout (as-shipped #1823 + Phase F)

```
apps/api/src/Api/BoundedContexts/SharedGameCatalog/
├── Application/Services/
│   ├── IWikidataCoverEnrichmentRunner.cs         ← orchestrator port (BC-owned, internal)
│   └── WikidataCoverEnrichmentRunner.cs          ← orchestrator adapter
└── Infrastructure/
    ├── Providers/
    │   └── WikidataCatalogProvider.cs            ← shared adapter (used by catalog seed #1903 + cover P18 #1823)
    ├── Services/
    │   ├── IWikimediaCommonsClient.cs            ← port (BC-internal, internal visibility)
    │   ├── WikimediaCommonsClient.cs             ← adapter (BC-internal)
    │   ├── IWikimediaRateLimiter.cs              ← port (BC-internal initially)
    │   ├── InMemoryWikimediaRateLimiter.cs       ← adapter (BC-internal initially)
    │   ├── LicenseValidator.cs                   ← pure domain helper (static, non-port)
    │   ├── IWebpVariantGenerator.cs              ← port (BC-internal)
    │   ├── WebpVariantGenerator.cs               ← adapter (Magick.NET-Q8-AnyCPU 14.x per DEC-3d-1, superseded ImageSharp 3.x rejected for license)
    │   ├── ICoverR2UploadPipeline.cs             ← port (BC-internal)
    │   └── CoverR2UploadPipeline.cs              ← adapter (wraps IBlobStorageService shared)
    └── Resilience/
        ├── WikimediaCircuitBreakerHandler.cs     ← Polly handler (BC-internal)
        └── CircuitBreakerExceptionDetector.cs    ← shared helper (BC-internal currently)
```

### Adapter promotion path (rules for the 2nd BC consumer)

When a second BC needs to enrich via Wikimedia Commons (e.g. `PdfDocumentBC.PdfDocument.CoverThumbnail`):

1. **Step 1** — BC #2 declares its OWN orchestrator port:
   ```csharp
   // apps/api/src/Api/BoundedContexts/PdfDocument/Application/Services/IPdfCoverEnrichmentRunner.cs
   internal interface IPdfCoverEnrichmentRunner
   {
       Task<PdfCoverEnrichmentResult> EnrichAndRecordAsync(Guid pdfDocumentId, CancellationToken ct);
   }
   ```

2. **Step 2** — `IWikimediaCommonsClient` + `IWikimediaRateLimiter` are PROMOTED from `SharedGameCatalog/Infrastructure/Services/` to a shared infrastructure namespace:
   ```
   apps/api/src/Api/Infrastructure/ExternalServices/Wikimedia/
   ├── IWikimediaCommonsClient.cs
   ├── WikimediaCommonsClient.cs
   ├── IWikimediaRateLimiter.cs
   ├── InMemoryWikimediaRateLimiter.cs
   └── WikimediaCircuitBreakerHandler.cs
   ```
   Visibility upgrade: `internal` → `public` (now genuinely shared).

3. **Step 3** — `WikidataCatalogProvider` is PROMOTED to:
   ```
   apps/api/src/Api/Infrastructure/ExternalServices/Wikidata/
   └── WikidataCatalogProvider.cs   (publicly accessible)
   ```

4. **Step 4** — BC #2 injects shared adapters (NOT BC #1 internals).

5. **Step 5** — `LicenseValidator` (currently pure helper) stays in SharedGameCatalog UNTIL a second BC needs license validation; then promote to `Infrastructure/Validation/LicenseValidator.cs`.

### Anti-promotion rules (BC purity guards)

- ❌ BC #2 MUST NOT inject `Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services.IWikimediaCommonsClient` (the BC-internal port) directly. If access is needed, promotion FIRST.
- ❌ NO `MediaEnrichment` shared bounded context — rejected per Option B cons.
- ❌ NO `IMediaEnricher` facade port at the shared infrastructure level — too generic, leaks coupling between unrelated enrichment domains.
- ❌ Promotion is NOT speculative — execute only when second consumer ships, NOT as anticipatory refactor.

### Rationale

| Concern | Resolution |
|---|---|
| **Newman SN-001 BC boundary ambiguity** | Each BC owns its orchestrator port. Shared adapters live in `Infrastructure/ExternalServices/` ONLY when promoted. |
| **Fowler F-001 rate-limit coordination** | `IWikimediaRateLimiter` is the coordination point; when a second consumer arrives, it's promoted to shared infra. Token bucket is process-singleton — DEC-3e single-pod constraint is preserved. |
| **Hightower H-001 cache strategy** | R2 Cache-Control headers handled by `CoverR2UploadPipeline` adapter — adapter, not port, owns the policy. |
| **BC purity** | Catalog-internal `internal` visibility prevents accidental cross-BC leak. Promotion is explicit + reviewable. |
| **Anti-overengineering** | No `MediaEnrichment` BC, no shared facade. The Wikidata pipeline ships without speculative abstraction; second consumer triggers minimal promotion. |

---

## Consequences

### Positive

- **No BC inflation**: 18 BCs stay 18 BCs. New enrichment pipelines slot into existing BCs.
- **Promotion ladder is reversible**: if BC #2 cancels, no shared adapters were prematurely extracted.
- **Each consumer owns retry policy + audit log**: SharedGameCatalog uses `WikidataCoverEnrichmentAttempt` aggregate; PdfDocument BC would use its own `PdfCoverEnrichmentAttempt` aggregate. No cross-BC mediator confusion.
- **Visibility enforcement is automatic** — `internal` keyword forces promotion BEFORE cross-BC use.

### Negative

- **Discipline burden**: BC author #2 must NOT shortcut by upgrading SharedGameCatalog's internals to `public`. Reviewer enforcement required (CODEOWNERS + ADR cross-link in PR template helpful).
- **Promotion PR is unavoidable** when BC #2 ships — adds ~0.5-1 day vs Option C "just inject it" cheap path.
- **Rate-limit token bucket promotion ordering**: if BC #2 ships WITHOUT promoting `IWikimediaRateLimiter`, BC #2 would re-instantiate its own token bucket → 2× rate consumption → 5+5=10 RPS → potential Wikimedia IP ban. **Mitigation**: PR template + CODEOWNERS auto-tag for any `Infrastructure/Services/Wikimedia*` changes; reviewer checks promotion happened.

### Neutral

- `WikidataCatalogProvider` is already shared between catalog seed + cover P18 use cases. The promotion to a public namespace becomes administrative (move file + change `internal` → `public`) — no behavioral change.
- `LicenseValidator` is a stateless static helper. Its eventual promotion is trivial. Keeping it BC-internal until needed is harmless.

---

## Alternatives Considered

### Rejected — Option B (`MediaEnrichment` shared BC)

- **Reason**: BC inflation pre-emptive — no second consumer to justify it.
- **Disqualifier**: cross-BC mediator calls introduce ambiguous audit ownership (whose `EnrichmentAttempt` table records the row? SharedGameCatalog's or `MediaEnrichment`'s?). Splitting audit across BCs defeats DDD bounded context purpose.
- **When to reconsider**: only if 3+ unrelated BCs (Game catalog + Pdf + Player + ...) each need enrichment AND the maintenance burden of 4 promoted adapters per BC exceeds the BC inflation cost.

### Rejected — Option C (direct injection across BC boundary)

- **Reason**: cross-BC tight coupling violates DDD bounded context purity.
- **Disqualifier**: requires `internal` → `public` visibility upgrade, leaking SharedGameCatalog's full surface (not just the Commons client).
- **When to reconsider**: never. Option A solves this cleanly via promotion.

---

## Compliance

### As-shipped verification (2026-06-22 ratification, #2055 Phase 1)

- [x] **No `MediaEnrichment` BC created** — Glob `apps/api/src/Api/BoundedContexts/**/MediaEnrichment*` returns zero matches.
- [x] **`IWikimediaCommonsClient` lives in `Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services`** (catalog-internal) ✅ — declared `internal interface`.
- [x] **`IWikidataCoverEnrichmentRunner` lives in `Api.BoundedContexts.SharedGameCatalog.Application.Services`** (orchestrator port BC-owned) ✅ — declared `internal interface`, DI `AddScoped`.
- [x] **`WikidataCatalogProvider` is shared between catalog seed + cover P18** — in `SharedGameCatalog/Infrastructure/Providers/`, declared `internal sealed class`. Promotion ladder applies when second BC consumer arrives.
- [x] **DEC-3e single-pod rate-limit constraint preserved** — `InMemoryWikimediaRateLimiter` registered `AddSingleton<IWikimediaRateLimiter, InMemoryWikimediaRateLimiter>()` (line 203 of `SharedGameCatalogServiceExtensions.cs`); class is `internal sealed : IWikimediaRateLimiter, IDisposable`.
- [x] **All adapter ports `internal` visibility** — verified 2026-06-22:
  - `IWikidataCoverEnrichmentRunner` → `internal` ✅
  - `IWikimediaCommonsClient` → `internal` ✅
  - `IWikimediaRateLimiter` → `internal` ✅ (tightened in #2055 Phase 1 from initial `public` shipping; no cross-BC consumer existed)
  - `IWebpVariantGenerator` → `internal` ✅ (tightened in #2055 Phase 1 from initial `public` shipping; no cross-BC consumer existed)
  - `ICoverR2UploadPipeline` → `internal` ✅
  - `LicenseValidator` → `internal static` ✅
  - `ImageProcessingException` → `public sealed` ⚠️ (constraint: Sonar S3871 forbids `internal` exception types — catch-across-assembly assumption; documented exception to "no public-leak" rule, surfaces only in tests via `Assert.ThrowsAsync<ImageProcessingException>`)
- [x] **Adapter ImageSharp → Magick.NET supersession recorded** — DEC-3d-1 LOCKED 2026-06-20 (License: Six Labors Split License conflict on ImageSharp 3.x). § Decision Layout updated to reflect Magick.NET-Q8-AnyCPU 14.x.

### Forward action items (post-Accepted)

- [ ] **Phase G CODEOWNERS cross-link**: add `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/Wikimedia*` and `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Resilience/Wikimedia*` entries pointing to architecture owner. Manual one-off.
- [ ] **PR template enhancement**: add "ADR-082 promotion check" checkbox triggered when PR touches `Infrastructure/Services/IWikimedia*` or visibility modifiers thereof.
- [ ] **Promotion PR contract**: when a second consumer BC ships, the promotion PR must (a) move files per § Decision Steps 2-3, (b) upgrade `internal` → `public`, (c) update DI registrations in both BCs, (d) append a `Phase 2 promotion executed YYYY-MM-DD` line to this ADR Status.

---

## Migration plan

This ADR codifies the AS-SHIPPED layout. No migration is required for current `SharedGameCatalog` Wikidata pipeline. Promotion is deferred until second BC consumer ships.

**Trigger condition for promotion PR**: any new BC declares an orchestrator port that needs to inject `IWikimediaCommonsClient` or `IWikimediaRateLimiter`. The promotion PR:

1. Moves files per § Decision steps 2-3.
2. Upgrades visibility `internal` → `public`.
3. Updates `Program.cs` DI registrations.
4. Documents in this ADR's Status: `Accepted` → `Accepted (Phase 2: shared infra promotion executed YYYY-MM-DD)`.

---

## References

- **Parent issue (closed)**: [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) — L2 Wikidata enrichment epic
- **Plan harden issue (open)**: [#2055](https://github.com/meepleAi-app/meepleai-monorepo/issues/2055) — this ADR closes the BC boundary gap
- **ADR Wikidata 2026-06-09**: [`adr-2026-06-09-wikidata-enrichment-architecture.md`](./adr-2026-06-09-wikidata-enrichment-architecture.md) — DEC-3a..3j architectural decisions
- **Spec source**: [`docs/superpowers/specs/2026-06-20-wikidata-l2-design.md`](../../../superpowers/specs/2026-06-20-wikidata-l2-design.md) § 2.2 DEC-3n + § 4 BC boundary
- **Plan**: [`docs/superpowers/plans/2026-06-20-wikidata-l2.md`](../../../superpowers/plans/2026-06-20-wikidata-l2.md) Phase 1
- **Spec-panel synthesis 2026-06-09 (sess.46h)**: Wiegers + Fowler + Newman + Nygard + Crispin + Hightower
- **Spec-panel synthesis 2026-06-20**: this ADR's gap closure source
- **Sibling BC reference**: [ADR-059](./adr-059-catalog-seed-legal-posture.md) — catalog seed legal posture (BGG ban + Wikidata fallback)

---

**Last updated**: 2026-06-22 | **Status**: Accepted (ratified at #2055 Phase 1, verified against as-shipped code; 2 minor `public → internal` visibility tightenings applied in the same PR).
