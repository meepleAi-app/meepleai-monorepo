# ADR-059 — Catalog Seed Legal Posture: Wikidata-primary + BGG-fallback Whitelisted Fetch

**Status**: Accepted (Phase 1: M1-M8 frontend delivered; pre-public-rollout checklist still required)
**Date**: 2026-06-04 (proposed), 2026-06-05 (accepted)
**Deciders**: @badsworm
**Tracking**: Issue [#1903](https://github.com/meepleAi-app/meepleai-monorepo/issues/1903)
**Spec**: [`docs/superpowers/specs/2026-06-04-admin-catalog-seed-design.md`](../../../superpowers/specs/2026-06-04-admin-catalog-seed-design.md)
**Supersedes**: —

## Context

The catalog ingestion pipeline (#1835 admin UI · #1874 backend) lets MeepleAI admins
populate the `SharedGameEntity` catalog from CSV/Excel uploads and BGG IDs. As we
move from "admin assists user upload" to "admin pre-seeds the catalog from
external providers", three legal vectors come into scope simultaneously:

1. **BGG Terms of Service** — `boardgamegeek.com/terms` §"Restrictions" bans
   framing of BGG assets, prohibits using the API for "primary purpose of …
   advertising or subscription revenue", and prohibits "any use that competes
   with or displaces the market for BoardGameGeek". The third clause is
   interpretively ambiguous and is the largest single risk vector.
2. **EU Database Directive 96/9/EC** — Article 7 "sui generis" right gives the
   database maker (BGG) the right to prevent "extraction and/or re-utilization
   of … a substantial part". Repeated extraction of insubstantial parts is also
   covered by Article 7.5 when it "conflicts with normal exploitation".
3. **US Feist Publications v. Rural Telephone Service (1991)** — facts are not
   copyrightable. Database "thin copyright" attaches to selection/arrangement
   only, not individual facts (title, year, designer name).

Wikidata (CC0 1.0 Universal) carries zero copyright/database-right exposure and
makes the obvious primary data source for board game metadata.

The full legal-framework analysis lives in §8.5 of the design spec and is
summarised here so future contributors can find the decision boundary without
reading the entire spec.

## Decision

**1) Wikidata is the primary data provider.**

`IWikidataCatalogProvider` (M2) issues a pre-canned SPARQL query against the
public Wikidata endpoint. CC0 license — no attribution required, no
contractual constraints, no database-right exposure. Every other concern in
this ADR exists because Wikidata coverage is incomplete (long-tail indie titles
absent), not because Wikidata itself is risky.

**2) BGG is a whitelisted fallback, never the primary source.**

`BggImportFieldFilter.ForbiddenFields` (M3) hard-codes the exclusion list:
description text, image/thumbnail, comments/reviews, statistics, ratings.
A unit-test guard in the SharedGameCatalog test suite asserts the whitelist is
respected end-to-end. The whitelist contains only "facts" per the Feist
doctrine: title, year published, designer name, publisher name, mechanic
labels, players, time. Each field has a recorded source URL via
`FieldProvenance` (M2) so GDPR Article 17 erasure requests are tractable.

**3) Kill-switch via runtime feature flag.**

`admin.catalog-seed.enabled` (M7) is a runtime configuration key, default
`false`. When disabled:
- `CatalogSeedFetchJob` early-returns before making provider calls.
- HTTP endpoints under `/api/v1/admin/catalog/seeds` return `503 Service
  Unavailable` (via endpoint filter, see M6.2).
- The FE `CatalogSeedApiError` surfaces 503 in the per-component error UI.

Fail-closed semantics: any error (config not found, DB unreachable,
deserialization failure) yields `false`. Operators must explicitly opt in via
`/admin/config`.

**4) Compliance audit trail.**

- `BggTosWatcherJob` (M5) records BGG ToS hash monthly; alerts on change.
- `domain_event_logs` captures `CatalogSeedFetched` · `CatalogSeedApproved` ·
  `CatalogSeedRejected` · `BggFetchInvoked` events with provider + URL.
- `/admin/catalog/seeds` audit export route is available for legal review.
- User-Agent header on BGG calls includes `abuse@meepleai.app` so the
  rightsholder has an immediate contact path.

**5) Pre-rollout legal checklist (spec §8.5.6).**

Public rollout is BLOCKED until:
- [ ] 1 hour legal consultation completed (validate "competes/displaces" interpretation)
- [ ] MeepleAI ToS updated with "publicly available data sources" clause
- [ ] `abuse@meepleai.app` mailbox active and monitored
- [ ] `BggTosWatcherJob` running in staging with alert routing to on-call
- [ ] `/admin/catalog/seeds/export` (audit log export) functioning

The M1-M8 implementation may be deployed to staging with the flag OFF; it must
remain OFF in production until every box above is checked.

## Consequences

### Positive

- **Wikidata-first** eliminates copyright and database-right exposure for the
  majority of catalog seeds (board games with a Q-page).
- **Whitelist + provenance** makes the Feist doctrine defensible: every stored
  field is a fact with a tracked source URL.
- **Feature flag kill-switch** lets operators park the pipeline immediately
  without a redeploy if BGG sends a takedown notice or the legal posture
  changes mid-rollout.
- **Audit trail** supports GDPR Article 17 erasure requests (designer name
  filter on `Provenance JSON`) and legal review of the import history.

### Negative

- **Long-tail coverage gap** when neither Wikidata nor BGG has a game (indie
  titles). Mitigation: manual entry via `SingleAddForm` mode `searchTermInput`,
  then admin paste of metadata.
- **Interpretive risk** on BGG ToS "competes/displaces" clause is not
  eliminable without a court ruling. Mitigation: legal consultation in the
  pre-rollout checklist + ToS watcher + kill-switch + abuse mailbox.
- **Performance**: every seed costs 1 SPARQL query + (optional) 1 BGG API
  call. At 100 BGG IDs/bulk × 5min rate limit, an admin importing 1000 games
  takes ~50 minutes. Acceptable for an admin tool.

### Neutral

- **GDPR consultation NOT required** for designer/publisher names per Article
  6(1)(f) legitimate-interest + "publicly available professional data"
  exemption (spec §8.5.4). Privacy policy update is OPTIONAL.

## Implementation Status (2026-06-05)

| Milestone | Scope | Status |
|-----------|-------|--------|
| M1 | Domain (`CatalogSeedDraft` aggregate + value objects) | ✅ |
| M2 | Wikidata SPARQL provider + `FieldProvenance` | ✅ |
| M3 | BGG fallback provider + `BggImportFieldFilter` whitelist guard | ✅ |
| M4 | MediatR commands/queries (enqueue, bulk, list, approve, reject) | ✅ |
| M5 | `BggTosWatcherJob` + `CatalogSeedFetchJob` (Quartz) | ✅ |
| M6 | Admin endpoints `/api/v1/admin/catalog/seeds` (REST + SSE) | ✅ |
| M7 | Runtime feature flag `admin.catalog-seed.enabled` | ✅ |
| M8 | Admin UI `/admin/catalog/seed-queue` + Playwright E2E | ✅ |
| — | Pre-rollout legal checklist (§8.5.6) | ⏳ Required before public |

## 5. Amendment 2026-06-10 — User-side BGG asset ban enforcement (issue #2123)

ADR-059 §2 narrowly addressed the **admin pipeline** whitelist filter and left
**user-side BGG asset traffic** (cover images served from `cf.geekdo-images.com`
and `**.boardgamegeek.com`) uncontrolled. The legacy YAML seed manifests carried
568 BGG URL occurrences across 142 `bggEnhanced: true` entries × 4 image fields,
the Next.js `remotePatterns` whitelist contained both BGG hostnames and a
`{hostname: '**'}` catch-all that nullified every host-specific guard, and 30+
FE consumers passed the raw `imageUrl` straight into `<Image>` without runtime
filtering.

Issue #2123 closes this gap with a **three-layer ban**:

### 5.1 Data plane

- `dev/staging/prod.yml` seed manifests scrubbed of `imageUrl`, `thumbnailUrl`,
  `fallbackImageUrl`, `fallbackThumbnailUrl`, and the legacy `bggEnhanced` boolean
  flag (which is semantically replaced by presence/absence of `description`).
- `SeedManifestGame.{BggEnhanced, ImageUrl, ThumbnailUrl, FallbackImageUrl,
  FallbackThumbnailUrl}` C# properties removed — compile-time hard ban prevents
  future regression.
- `GameSeeder` write paths assign `ImageUrl = null` + `ThumbnailUrl = null` on
  every create path; cover URLs are NEVER seeded inline.
- DB migration `20260610152201_NullifyBggImageColumns` makes
  `shared_games.image_url` + `thumbnail_url` nullable and nullifies any
  existing row matching `ILIKE '%geekdo%' OR '%boardgamegeek%'`.

### 5.2 Resolution plane

- `SharedGameDto.CoverUrl` (R2 presigned URL resolved server-side by
  `CoverUrlResolver` via the L3→L4→L2.5→L2 priority chain) is the single
  source of truth for FE rendering.
- `CoverUrlResolver` emits a Prometheus counter
  `meepleai_cover_resolution_total{source}` on every resolution, tagged with
  the winning layer (`r2_user|r2_pdf|r2_bgg|r2_wikidata|placeholder`).
- Fallback when no R2 cover is available: deterministic placeholder rendered by
  `lib/games/cover-utils.ts` (hash-based hue + extracted initials, WCAG AA
  verified). Zero network requests.

### 5.3 Network plane

- `apps/web/next.config.js` `remotePatterns` now an **explicit allowlist** with
  no catch-all `**`. The allowlist contains: `picsum.photos`, `placehold.co`,
  `**.r2.cloudflarestorage.com`, `*.r2.dev`, `commons.wikimedia.org`,
  `upload.wikimedia.org`, `meepleai.app`, `staging.meepleai.app`.
- ESLint custom rule `local/no-bgg-host` errors on any BGG hostname literal in
  `apps/web/src/**`. Path overrides allowlist legitimate sites (admin
  server-to-server BGG path, Storybook fixtures, E2E tests, the cover-utils
  blocklist itself).
- `pnpm lint:bgg` standalone grep gate covers manifest YAML, `next.config.js`,
  FE source, BE seeders. Defense in depth: catches violations ESLint can't
  reach (the manifests, the API source tree).
- Prometheus counter `meepleai_bgg_url_attempted_render_total` (SLO = 0)
  will be incremented from the FE custom Image loader on any browser-side BGG
  asset render attempt; alert fires on the first nonzero increment.

### 5.4 Bootstrap procedure (one-shot, per environment)

The infrastructure pipeline (#1823 M3-M8) is fully wired but `WikidataQid` was
null on every prod entry at the time of issue #2123. To populate covers at
maximum coverage:

1. Run `python scripts/bootstrap_wikidata_qid.py --connection-string $DB_URL`
   to query SPARQL `wdt:P2339` for each `shared_games.bgg_id` and populate
   `shared_games.wikidata_qid` (~120/159 expected hit-rate; the residual gap
   falls back to placeholder).
2. Run the batch admin endpoint to enrich every QID-populated game:
   ```bash
   curl -X POST https://<env>.meepleai.app/api/v1/admin/catalog/covers/enrich-batch \
     -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
     -d "{\"gameIds\":[<id-1>,...,<id-N>]}"
   ```

### 5.5 CI gating

Blocking jobs:
- `Frontend - BGG Lint` runs `pnpm lint:bgg` on every PR.
- `Backend - BGG ToS IT` runs `BggToSComplianceIntegrationTests` against
  Testcontainers Postgres — seeds `dev/staging/prod/ci.yml`, asserts SQL
  `COUNT(*)` matching BGG host pattern equals 0.

References:
- Spec: [`docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md`](../../../superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md)
- Plan: [`docs/superpowers/plans/2026-06-10-issue-2123-bgg-tos-compliance.md`](../../../superpowers/plans/2026-06-10-issue-2123-bgg-tos-compliance.md)
- Operations runbook: [`docs/for-developers/operations/operations-manual.md`](../../../for-developers/operations/operations-manual.md) § Catalog covers — BGG ToS compliance

## 6. Amendment 2026-06-20 — IT seed translations (issue #2339 sub-PR 3/3)

### 6.1 Scope

Sub-PR 3/3 of issue [#2339](https://github.com/meepleAi-app/meepleai-monorepo/issues/2339) introduces 2 IT-locale translation rows for the SP4 seed dataset (`Catan` → `I Coloni di Catan`, `Pandemic` → `Pandemia`). These rows are persisted in the `shared_game_translations` table shipped by sub-PR 1/3 (PR [#2370](https://github.com/meepleAi-app/meepleai-monorepo/pull/2370)) and are surfaced to the FE via the `useGameTitle()` hook shipped by sub-PR 2/3 (PR [#2449](https://github.com/meepleAi-app/meepleai-monorepo/pull/2449)).

### 6.2 Legal posture for translation strings

Game titles are **facts** (per ADR-059 § 1 reasoning). The IT translation of a foreign game title (e.g. `I Coloni di Catan` for `Catan`) is the **official IT trade name** chosen by the IT publisher (`dV Giochi`, `Asmodee Italia`, etc.) for the IT market. As such:

- The IT title is **identifiable factual information** about the game, not creative content.
- Storing the IT title alongside the canonical EN does **not** create derivative work — same legal status as storing publisher names or BGG IDs (already permitted by ADR-059 § 3).
- No attribution is required for trade name re-use; trademarks remain the publishers' property and are referenced descriptively (not used as marks).

### 6.3 Curation process

The 2 IT translations were curated by `@badsworm` (IT native, board game collector) via cross-reference with:

- IT publisher catalog pages (`dV Giochi`, `Asmodee Italia`)
- BGG IT version pages
- IT retail listings (verifying that the IT edition is currently sold under the localized title)

The full audit trail is in [`infra/scripts/seed-sp4/translations-research.md`](../../../../infra/scripts/seed-sp4/translations-research.md), which documents the decision (`translate` vs `retain canonical EN`) for all 13 SP4 games.

### 6.4 Source attribution

Each persisted translation carries a `source` discriminator:

- `manual` — admin-curated (the 2 SP4 seed entries, fully attributable to `@badsworm`'s review)
- `auto-openrouter` — machine-translated via `OpenRouterTranslationService` (DeepSeek V3); NOT used in seed but available for future bulk imports
- `community` — community-sourced (out of scope MVP, moderation workflow TBD)

The `source` field is the audit primitive: any future Wikidata enrichment or community submission for translations would create a NEW row with the appropriate source, preserving the original `manual` row as the curated canonical IT title.

### 6.5 BGG-compliance note

Translation rows are stored entirely in our DB (no upstream sync to BGG). They are not subject to the BGG user-side asset ban (§ 5). The 2 seed translations originate from IT publisher catalogs, not BGG version pages.

---

## References

- Design spec: [`docs/superpowers/specs/2026-06-04-admin-catalog-seed-design.md`](../../../superpowers/specs/2026-06-04-admin-catalog-seed-design.md)
- Issue: [#1903](https://github.com/meepleAi-app/meepleai-monorepo/issues/1903)
- Operations runbook: [`docs/for-developers/operations/operations-manual.md`](../../../for-developers/operations/operations-manual.md) § Catalog seed pipeline (#1903)
- Related ADR: [ADR-025 — Shared Catalog Bounded Context](./adr-025-shared-catalog-bounded-context.md)
- Related ADR: [ADR-046 — Game/SharedGame data ownership](./adr-046-game-sharedgame-data-ownership.md)
- BGG ToS (verified 2026-06-02): `boardgamegeek.com/terms`
- EU Database Directive 96/9/EC, Article 7
- Feist Publications v. Rural Telephone Service, 499 U.S. 340 (1991)
- Wikidata CC0 deed: `creativecommons.org/publicdomain/zero/1.0/`
