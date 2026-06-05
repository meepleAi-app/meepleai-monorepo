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
