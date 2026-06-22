# Issue #2123 — Catalog covers: remove BGG-hosted URLs from seed manifests + frontend whitelist

**Date**: 2026-06-10
**Issue**: [#2123](https://github.com/meepleAi-app/meepleai-monorepo/issues/2123)
**Author**: Claude (paired with @DegrassiAaron)
**Status**: Locked (spec-panel review 2026-06-10)
**Branch**: `feature/issue-2123-bgg-tos-compliance`
**Related ADR**: [ADR-059 — Catalog Seed Legal Posture](../../for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md) (will be amended)
**Companion plan**: `docs/superpowers/plans/2026-06-10-issue-2123-bgg-tos-compliance.md`

---

## 1. Context

### 1.1 Legal constraint

ADR-059 (issue #1903) made user-side BGG asset traffic **forbidden**. Server-to-server BGG XML API calls remain legitimate (whitelist-filtered facts only); any browser request to `cf.geekdo-images.com` or `**.boardgamegeek.com` from a non-admin surface is a ToS violation regardless of authentication state.

### 1.2 Pipeline status (as of 2026-06-10)

Issue #1823 Wave 1 (M3+M4+M6+M7) and Wave 2 (M8) shipped the **backend infrastructure** to replace BGG-hosted covers with self-hosted R2 variants generated from Wikidata + Wikimedia Commons:

| Milestone | PR | Capability |
|---|---|---|
| M3 | #2104 | `WikidataCatalogProvider.FetchCoverImageAsync` (SPARQL `wdt:P18`) |
| M4 | #2107 | `WikimediaCommonsClient.FetchLicenseAndAttribution` (CC0/CC-BY/CC-BY-SA only) |
| M6 | #2109 | `WebpVariantGenerator` (ImageSharp 3.1.12, 512×384, quality 85) |
| M7 | #2111 | `CoverR2UploadPipeline` (R2 deterministic key `covers/{gameId}/cover.webp`) |
| M8 | #2121 | `EnrichCatalogCoverCommand` orchestrator (M3→M4→M6→M7 sequential) |

### 1.3 The gap

Despite shipped infrastructure:

1. **Seed manifests untouched** — `dev/staging/prod.yml` still contain **568 BGG URL occurrences** across **142 `bggEnhanced: true` entries** × 4 fields (`imageUrl`, `thumbnailUrl`, `fallbackImageUrl`, `fallbackThumbnailUrl`).
2. **Next.js whitelist still open** — `apps/web/next.config.js` lines 131, 136 explicitly allow BGG hosts. Line 143 contains a catch-all `**` that nullifies any host-specific restriction.
3. **`WikidataQid` columns NULL on all 159 prod entries** — the M8 orchestrator cannot enrich anything until QID assignment runs. No seeder or admin tool populates `WikidataQid` today.
4. **`SharedGameDto` dual-source** — exposes both `ImageUrl` (raw seed value) and `CoverUrl` (R2 presigned, nullable). FE consumers don't agree on a single source of truth.
5. **`cover-utils.ts` `shouldUsePlaceholder()` opt-in** — already blocks BGG hosts in 5 files (HeroCard, Cover.tsx, 2 tests, 1 step) but the other 30+ `next/image` consumers bypass it.

The end-to-end flow today:

```
seed prod.yml → SharedGameEntity.ImageUrl = 'https://cf.geekdo-images.com/...'
  → /api/v1/shared-games response { imageUrl: 'https://cf.geekdo-images.com/...', coverUrl: null }
  → FE <Image src={imageUrl} /> on /shared-games, /hub/games, /discover, /library/*
  → Browser GET cf.geekdo-images.com → ToS violation
```

### 1.4 Audit numbers (verified `main-dev` @ 2026-06-10)

| File | Lines | Games | `bggEnhanced:true` | BGG URLs |
|---|---|---|---|---|
| `Manifests/ci.yml` | 50 | 3 | 0 | **0** ✅ |
| `Manifests/dev.yml` | 12,200 | 160 | 142 | **568** |
| `Manifests/staging.yml` | 12,172 | 159 | 142 | **568** |
| `Manifests/prod.yml` | 12,172 | 159 | 142 | **568** |
| `next.config.js` `remotePatterns` | — | — | — | 2 BGG entries + 1 catch-all `**` |

---

## 2. Goal

Make user-side BGG asset traffic **structurally impossible** by closing the three layers identified by the spec-panel architecture review:

1. **Data plane** — strip BGG URLs from manifests + nullify DB columns
2. **Resolution plane** — make `CoverUrl` (R2 presigned) the single source of truth + delete `<Image>` raw consumption of BGG URLs
3. **Network plane** — Next.js allowlist explicit (no catch-all) + custom Image loader + runtime metric

Maximize cover coverage at cutover via opportunistic Wikidata QID bootstrap + M8 batch run.

## 3. Non-goals

- Admin-only `BggCatalogProvider` server-to-server flow stays (legal per ADR-059 §2).
- `admin-mockups/**` static HTML (design review only, not runtime-reachable).
- `apps/web/src/components/**/*.stories.tsx` Storybook fixtures with hard-coded BGG URLs (non-runtime, follow-up cleanup).
- `SharedGameEntity.BggRawData` JSONB column (server-side BGG metadata cache, not surfaced to FE).
- Replacement of `description` text harvested from BGG (potential thin-copyright vector tracked in separate issue, out-of-scope here per ADR-059 §3 Feist doctrine evaluation).

---

## 4. Locked decisions (spec-panel review 2026-06-10)

| # | Decision | Value | Rationale |
|---|---|---|---|
| **D1** | Fallback strategy for entries without R2/Wikimedia cover | **Placeholder deterministico via `cover-utils.ts`** | Already implemented (`shouldUsePlaceholder`, `hashToHue`, `extractInitials`); zero network requests; WCAG AA verified; ready immediately |
| **D2** | `WikidataQid` bootstrap | **In-scope #2123**: SPARQL `wdt:P2339` (BGG ID property on Wikidata) batch lookup + populate `shared_games.wikidata_qid` for the 159 prod entries + run M8 batch | Maximum cover coverage at cutover; reuses existing M8 orchestrator |
| **D3** | Manifest cleanup tooling | **Codemod Python script** `scripts/scrub-bgg-manifest.py` | 142 × 3 manifests × 4 fields = 1,704 mutations; manual is not scalable |
| **D4** | Next.js `remotePatterns` catch-all `**` | **Removed** + replaced with explicit allowlist: `picsum.photos`, `**.r2.cloudflarestorage.com`, `*.r2.dev`, `commons.wikimedia.org`, `upload.wikimedia.org`, `placehold.co`, `meepleai.app` | True fail-closed; any future hostname requires explicit PR |
| **D5** | Custom Image loader / wrapper | **Mandatory `<Cover>` for SharedGame consumers** — ESLint rule `local/no-raw-next-image-for-shared-game` forbids raw `next/image` on `SharedGameDto` fields | Runtime safety net + lint catches at PR time |
| **D6** | CI gating | **Triple gate**: (a) ESLint custom rule `local/no-bgg-host` on source + (b) `pnpm lint:bgg` grep script on `apps/web/next.config.js`, `apps/web/src/**`, `apps/api/src/Api/Infrastructure/Seeders/**` + (c) xUnit IT seeding `prod.yml` into Testcontainers Postgres → SQL assert `0 rows` matching `geekdo\|boardgamegeek` | Defense in depth: static + grep + runtime |
| **D7** | Documentation home | **Triplet**: ADR-059 §5 amendment (legal) + `docs/for-developers/operations/operations-manual.md` runbook (ops) + `CLAUDE.md` § Active Freezes one-line pointer (devx) | Hightower triplet |
| **D8** | Rollout | **Atomic single PR** — manifest scrub + DB migration + next.config + ESLint + lint:bgg + xUnit IT + Playwright E2E + ADR amendment | Fail-closed integrity requires consistency |
| **D9** | Observability | Prometheus metric `meepleai_cover_resolution_total{source}` (labels: `placeholder`, `r2_pdf`, `r2_bgg`, `r2_wikidata`, `r2_user`) + alert metric `meepleai_bgg_url_attempted_render_total` (gauge, SLO = 0, alert on first increment) | Nygard reliability |
| **D10** | DB schema | **Migration**: `shared_games.image_url` + `thumbnail_url` from `varchar NOT NULL` to `varchar NULL`; nullify all rows where current value matches BGG host pattern; `[Obsolete]` marker on DTO in **follow-up PR** (not this one) | Wiegers null-safety; deprecation marker deferred to avoid bloating atomic PR |

---

## 5. Acceptance criteria

### 5.1 Data plane

- [ ] **AC-1**: `grep -cE 'cf\.geekdo\|boardgamegeek' apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/staging.yml apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/prod.yml apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/ci.yml` returns **`0`** for every file.
- [ ] **AC-2**: `grep -cE 'bggEnhanced' apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/*.yml` returns **`0`** for every file (`bggEnhanced` flag deprecated; semantic replaced by presence/absence of `description`).
- [ ] **AC-3**: `SeedManifestGame.BggEnhanced`, `ImageUrl`, `ThumbnailUrl`, `FallbackImageUrl`, `FallbackThumbnailUrl` properties removed from C# model (compile-time hard ban).
- [ ] **AC-4**: DB migration `20260610_NullifyBggImageColumns` applied: `shared_games.image_url` + `thumbnail_url` set to `nullable`; rows matching `LIKE '%geekdo%' OR LIKE '%boardgamegeek%'` nullified.
- [ ] **AC-5**: WikidataQid bootstrap script `scripts/bootstrap-wikidata-qid.py` populated `wikidata_qid` for at least **120/159** prod entries (75% coverage target via `wdt:P2339` BGG ID property). Failure to reach target documented in spec §9 (known gap).
- [ ] **AC-6**: M8 batch executed via admin endpoint `POST /api/v1/admin/catalog/covers/enrich-batch` for all entries with `WikidataQid IS NOT NULL`. R2 keys populated where Wikidata + Wikimedia license permit.

### 5.2 Resolution plane

- [ ] **AC-7**: `SharedGameDto.CoverUrl` is the single source of truth for cover rendering. `ImageUrl` + `ThumbnailUrl` removed from `SeedManifestModels.cs` legacy model and removed from `GameSeeder.CreateFromEnhancedData/CreateMinimalGame` write paths.
- [ ] **AC-8**: All 30+ FE files identified in audit consume covers via either (a) `<Cover>` wrapper or (b) explicit call to `shouldUsePlaceholder()` before passing to `<Image>`. New ESLint rule `local/no-raw-next-image-for-shared-game` enforces.
- [ ] **AC-9**: `hybrid-hub.mappers.ts:64` (and equivalent fallback chains) cease consuming `gameImageUrl`/`gameIconUrl` for SharedGame covers. They keep working for badge/user-profile use cases.

### 5.3 Network plane

- [ ] **AC-10**: `apps/web/next.config.js` `remotePatterns` contains:
  - ✅ `picsum.photos` (Storybook)
  - ✅ `**.r2.cloudflarestorage.com` (production R2)
  - ✅ `*.r2.dev` (R2 public dev URLs)
  - ✅ `commons.wikimedia.org` (attribution fallback)
  - ✅ `upload.wikimedia.org` (Wikimedia CDN)
  - ✅ `placehold.co` (legacy minimal seed)
  - ✅ `meepleai.app` (self-hosted assets)
  - ❌ NO `cf.geekdo-images.com`
  - ❌ NO `**.boardgamegeek.com`
  - ❌ NO catch-all `**`
- [ ] **AC-11**: Custom Image loader OR runtime middleware logs + redirects-to-placeholder if hostname ∈ BGG list. Emits Prometheus metric `meepleai_bgg_url_attempted_render_total{path=…}` on each attempt.

### 5.4 Test sweep

- [ ] **AC-12**: xUnit integration test `BggToSComplianceIntegrationTests.NoBggHostsInProdSeed` seeds `prod.yml` into Testcontainers Postgres → asserts `SELECT COUNT(*) FROM shared_games WHERE image_url ILIKE '%geekdo%' OR image_url ILIKE '%boardgamegeek%' OR thumbnail_url ILIKE '%geekdo%' OR thumbnail_url ILIKE '%boardgamegeek%'` returns `0`.
- [ ] **AC-13**: Playwright E2E `bgg-tos-compliance.spec.ts` navigates `/shared-games` (public) + `/hub/games` (auth) + `/discover` (public) → intercepts network → asserts zero requests to `cf.geekdo-images.com` or `**.boardgamegeek.com` for any payload type.
- [ ] **AC-14**: Vitest unit suite passes existing `cover-utils.test.ts` + `Cover.test.tsx` + adds `<Cover>` snapshot tests for the new mandatory-wrapper contract.
- [ ] **AC-15**: ESLint passes with new `local/no-bgg-host` + `local/no-raw-next-image-for-shared-game` rules enabled at `error` level.

### 5.5 CI gating

- [ ] **AC-16**: New CI job `Frontend - BGG Lint` runs `pnpm lint:bgg` (grep manifest + next.config + FE/BE seeders) — blocking, fails on any new BGG host literal.
- [ ] **AC-17**: New CI job `Backend - BGG ToS IT` runs `BggToSComplianceIntegrationTests` — blocking, fails if seed-time URL pollution returns.

### 5.6 Observability

- [ ] **AC-18**: `meepleai_cover_resolution_total{source}` exported via existing `/metrics` endpoint. Labels = `{placeholder, r2_pdf, r2_bgg, r2_wikidata, r2_user}`.
- [ ] **AC-19**: `meepleai_bgg_url_attempted_render_total` exported. SLO documented in operations manual: **gauge MUST be 0**, alert on any nonzero increment within a 5min window.

### 5.7 Documentation

- [ ] **AC-20**: ADR-059 §5 amendment («User-side BGG asset ban enforcement») added with link to this spec.
- [ ] **AC-21**: `docs/for-developers/operations/operations-manual.md` § «Catalog covers» runbook added: «What to do if `bgg_url_attempted_render_total > 0`».
- [ ] **AC-22**: `CLAUDE.md` § «Active Freezes» one-line pointer added: «BGG user-side asset ban — see ADR-059 §5».

---

## 6. Architecture

### 6.1 Data flow before (current state)

```
prod.yml entry { bggEnhanced: true, imageUrl: 'https://cf.geekdo-images.com/...' }
  ↓ SeedManifestLoader.LoadFromFile
  ↓ GameSeeder.CreateFromEnhancedData
SharedGameEntity { ImageUrl = 'https://cf.geekdo-images.com/...', ThumbnailUrl = '...', BggCoverR2Key = null, WikidataCoverR2Key = null, WikidataQid = null }
  ↓ Repository.GetByIdAsync
  ↓ MapToSharedGameDto
SharedGameDto { ImageUrl = 'https://cf.geekdo-images.com/...', CoverUrl = null /* CoverUrlResolver returned null because no R2 key */ }
  ↓ JSON serialization
FE entry.imageUrl = 'https://cf.geekdo-images.com/...'
  ↓ <Image src={entry.imageUrl} />
  ↓ Next.js images.remotePatterns matches '**' (or 'cf.geekdo-images.com' explicitly)
Browser GET https://cf.geekdo-images.com/... ← ToS violation
```

### 6.2 Data flow after

```
prod.yml entry { title: 'Catan', bggId: 13, language: 'en', description: '…' }  /* no imageUrl, no thumbnailUrl, no fallback*, no bggEnhanced */
  ↓ SeedManifestLoader.LoadFromFile (model AC-3: no image fields)
  ↓ GameSeeder.CreateFromEnhancedData
SharedGameEntity { ImageUrl = null, ThumbnailUrl = null, BggCoverR2Key = null, WikidataCoverR2Key = 'covers/<id>/cover.webp' (if M8 ran), WikidataQid = 'Q47533' }
  ↓ Repository.GetByIdAsync
  ↓ MapToSharedGameDto (CoverUrlResolver L2 returns presigned R2 URL)
SharedGameDto { ImageUrl = null, CoverUrl = 'https://abc.r2.cloudflarestorage.com/...presigned...' }
  ↓ JSON serialization
FE entry.coverUrl = 'https://abc.r2.cloudflarestorage.com/...'
  ↓ <Cover coverUrl={entry.coverUrl} title={entry.title} gameId={entry.id} />  /* mandatory wrapper */
  ↓ shouldUsePlaceholder(coverUrl) → false
  ↓ <Image src={coverUrl} ... />
  ↓ Next.js images.remotePatterns matches '**.r2.cloudflarestorage.com'
Browser GET https://abc.r2.cloudflarestorage.com/... ← OK
```

For entries where `WikidataQid` couldn't be resolved (Wikidata has no `wdt:P2339=<bggId>` mapping) or where M8 fails (no `wdt:P18`, no Commons file, license incompatible):

```
SharedGameDto { ImageUrl = null, CoverUrl = null }
  ↓ FE entry.coverUrl = null
  ↓ <Cover coverUrl={null} title='…' gameId='…' />
  ↓ shouldUsePlaceholder(null) → true
  ↓ Renders deterministic placeholder (hashToHue + extractInitials)
Browser: no network request for cover
```

### 6.3 Components

#### Backend

| New / Modified | File | Change |
|---|---|---|
| 🆕 | `scripts/bootstrap-wikidata-qid.py` | SPARQL `wdt:P2339` batch lookup → SQL UPDATE per-bggId |
| 🆕 | `scripts/scrub-bgg-manifest.py` | YAML codemod: strip `imageUrl/thumbnailUrl/fallbackImageUrl/fallbackThumbnailUrl/bggEnhanced` |
| ✏️ | `apps/api/src/Api/Infrastructure/Seeders/SeedManifest.cs` | Remove `BggEnhanced`, `ImageUrl`, `ThumbnailUrl`, `FallbackImageUrl`, `FallbackThumbnailUrl` from `SeedManifestGame` |
| ✏️ | `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedManifestModels.cs` | Remove `ImageUrl`, `ThumbnailUrl` from `GameManifestEntry` (already minimal model) |
| ✏️ | `apps/api/src/Api/Infrastructure/Seeders/Catalog/GameSeeder.cs` | Update `CreateFromEnhancedData`, `CreateMinimalGame`, `CreateFromBggData` to assign `ImageUrl = null` + `ThumbnailUrl = null`; remove `bggEnhanced` branching logic |
| 🆕 | `apps/api/src/Api/Infrastructure/Migrations/20260610_NullifyBggImageColumns.cs` | Alter columns nullable + UPDATE WHERE BGG-pattern |
| 🆕 | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCoverBatch/EnrichCatalogCoverBatchCommand.cs` | Batch wrapper around M8 single-entry orchestrator (admin endpoint) |
| 🆕 | `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs` (extend) | `POST /api/v1/admin/catalog/covers/enrich-batch` (admin-only, force-strict 2FA optional, rate-limited) |
| 🆕 | `apps/api/src/Api/SharedKernel/Observability/MeepleAiMetrics.cs` (extend) | Counter `cover_resolution_total{source}` + gauge `bgg_url_attempted_render_total` |
| ✏️ | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs` | Emit metric on each resolution path (L3/L4/L2.5/L2/null) |
| 🆕 | `tests/Api.Tests/Integration/SharedGameCatalog/BggToSComplianceIntegrationTests.cs` | xUnit IT: seed prod.yml → assert 0 BGG URLs in DB |

#### Frontend

| New / Modified | File | Change |
|---|---|---|
| ✏️ | `apps/web/next.config.js` | Remove BGG hosts + catch-all `**`; add explicit allowlist (D4) |
| 🆕 | `apps/web/src/components/ui/data-display/cover/Cover.tsx` (extend or new) | Mandatory wrapper for SharedGame covers; calls `shouldUsePlaceholder` internally |
| ✏️ | 30+ FE consumers | Replace raw `<Image src={imageUrl} />` with `<Cover coverUrl={…} title={…} gameId={…} />` for SharedGame DTOs |
| 🆕 | `apps/web/eslint-rules/no-bgg-host.js` | Custom ESLint rule, error on `geekdo\|boardgamegeek` literals in `apps/web/src/**/*.{ts,tsx}` |
| 🆕 | `apps/web/eslint-rules/no-raw-next-image-for-shared-game.js` | Error on raw `<Image>` consuming `SharedGameDto.imageUrl` (semantic detection via prop name) |
| 🆕 | `apps/web/scripts/lint-bgg.sh` (or `.mjs`) | Standalone grep guard for CI; covers manifest YAML + FE source + BE seeders |
| 🆕 | `apps/web/e2e/bgg-tos-compliance.spec.ts` | Playwright E2E: navigate public/auth pages + assert no BGG network requests |

#### CI / Documentation

| New / Modified | File | Change |
|---|---|---|
| ✏️ | `.github/workflows/ci.yml` | New blocking job `Frontend - BGG Lint` + new blocking job `Backend - BGG ToS IT` |
| ✏️ | `docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md` | §5 amendment: «User-side BGG asset ban enforcement» |
| ✏️ | `docs/for-developers/operations/operations-manual.md` | New § «Catalog covers — BGG ToS compliance»: runbook for alert `bgg_url_attempted_render_total > 0` |
| ✏️ | `CLAUDE.md` § Active Freezes | One-line pointer to ADR-059 §5 |

---

## 7. Risk register

| ID | Risk | Severity | Mitigation |
|---|---|---|---|
| R1 | QID bootstrap returns < 75% mapping coverage (target AC-5) | **HIGH** | Document residual gap in spec §9 + create follow-up issue for manual QID curation; pipeline degrades gracefully (placeholder fallback) |
| R2 | M8 batch reaches rate-limit on Wikimedia (1 req/sec) | MEDIUM | Sequential execution + retry policy already implemented (M8); 159 entries × 3 req each = ~8min; CI doesn't trigger this (in-prod admin tool only) |
| R3 | Next.js `**` removal breaks legitimate use cases (badge iconUrl, etc.) | MEDIUM | Audit all `<Image>` consumers; explicit allowlist must cover all currently-used hosts; deploy with feature flag if uncertainty |
| R4 | ESLint rule `no-raw-next-image-for-shared-game` produces false positives | MEDIUM | Start at `warn` level for 1 week; promote to `error` after baseline sweep |
| R5 | Migration `20260610_NullifyBggImageColumns` slow on 159 rows | LOW | Single UPDATE statement; staging soak before prod |
| R6 | Some FE consumers fall back to `gameImageUrl`/`gameIconUrl` (hybrid-hub.mappers.ts:64) which may still contain BGG | MEDIUM | Audit + replace those mapper layers in this PR; runtime metric catches residuals |
| R7 | Manifest scrub script breaks YAML formatting/anchors | LOW | YAML round-trip via PyYAML safe_dump + line-diff verification in CI |
| R8 | `[Obsolete]` deprecation on `SharedGameDto.ImageUrl` breaks legacy consumers in follow-up PR | LOW (deferred) | Out of scope this PR — handled in follow-up issue |

---

## 8. Test plan

### 8.1 Unit (xUnit, Vitest)

- `SeedManifestGameTests.cs`: Properties `BggEnhanced/ImageUrl/etc.` no longer exist (compile-time assertion).
- `GameSeederTests.cs`: `CreateFromEnhancedData` assigns `ImageUrl = null` + `ThumbnailUrl = null` for all paths.
- `CoverUrlResolverTests.cs`: Add metric assertion via fake `IMeepleAiMetrics`.
- `EnrichCatalogCoverBatchCommandTests.cs`: Validates input (NotEmpty list, admin auth), iterates M8 single-entry handler, aggregates result.
- `cover-utils.test.ts` (extend): Confirm `shouldUsePlaceholder` blocks all 7 BGG hostname variants.
- `Cover.test.tsx` (extend): Verify deterministic placeholder render path, ESLint compliance.

### 8.2 Integration (xUnit Testcontainers)

- `BggToSComplianceIntegrationTests.NoBggHostsInProdSeed` (AC-12): seed → SQL grep → assert 0.
- `WikidataQidBootstrapIntegrationTests.PopulatesQidForKnownBggIds`: seed → run bootstrap script (subprocess) → assert ≥120 rows have non-null `wikidata_qid`.
- `EnrichCatalogCoverBatchIntegrationTests.PopulatesR2KeyForResolvableEntries`: seed + QID + run M8 batch → assert ≥80% of QID-populated entries have `wikidata_cover_r2_key` non-null.

### 8.3 E2E (Playwright)

- `bgg-tos-compliance.spec.ts` (AC-13):
  - Visit `/shared-games` (public, unauthenticated) → page.waitForLoadState → assert `requests.filter(r => r.url() matches BGG).length === 0`.
  - Visit `/hub/games` (auth) → same assertion.
  - Visit `/discover` → same assertion.
  - Visit `/library` (auth) → same assertion.
  - For each: assert at least N `<img>` or `<Image>` elements render successfully (covers not all-zero).

### 8.4 ESLint

- `no-bgg-host.test.js`: positive cases (literal, template string, concatenation) + false-negative documentation.
- `no-raw-next-image-for-shared-game.test.js`: positive/negative on prop name detection.

### 8.5 Lint scripts

- `pnpm lint:bgg` exits 0 on current state, 1 if any new BGG host appears.

---

## 9. Known limitations & follow-up tasks

- **F1**: `SharedGameDto.ImageUrl/ThumbnailUrl` field deprecation via `[Obsolete]` deferred to follow-up PR (avoids bloating atomic PR; backward-compat window for unknown external consumers).
- **F2**: Storybook fixtures with hard-coded BGG URLs (`apps/web/src/components/**/*.stories.tsx`) NOT cleaned by this PR — tracked in follow-up issue (visual-only, not runtime).
- **F3**: QID curation for the residual ~25% gap (R1) tracked in new issue: «Manual Wikidata QID assignment for long-tail BGG games».
- **F4**: BGG `description` text removal (potential thin-copyright vector) — separate issue tracking, see ADR-059 §3.
- **F5**: M9 BackgroundService scheduler (Wave 3 #1823) will automate ongoing QID + cover re-verification quarterly; this PR's batch run is one-shot.
- **F6**: `admin-mockups/**` static HTML still contains BGG URLs — tracked in follow-up cleanup (design-time only).

---

## 10. References

- Issue: [#2123](https://github.com/meepleAi-app/meepleai-monorepo/issues/2123)
- ADR-059: [Catalog Seed Legal Posture](../../for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md)
- Epic #1823: catalog cover replacement pipeline (Wave 1+2 shipped, Wave 3 pending)
- Phase 1 BGG hiding: `docs/superpowers/specs/2026-05-22-hide-bgg-user-facing-design.md` (UI mentions)
- Phase 2 BGG hiding: `docs/superpowers/specs/2026-05-22-hide-bgg-user-facing-phase-2-design.md` (i18n + admin-gating)
- Wave 1 PRs: #2104 (M3), #2107 (M4), #2109 (M6), #2111 (M7)
- Wave 2 PR: #2121 (M8 orchestrator)
- Spec-panel review notes: 2026-06-10 session (this document §4)
