# ADR-061: Game Detail Tab Inventory Canonical

**Status**: Accepted
**Date**: 2026-06-12
**Deciders**: Product Owner + Tech Lead (spec-panel facilitated, 7/7 consensus)
**Issue**: #2203 (sub-issue of #2194 umbrella)
**Supersedes**: shipped scaffolds from `0881856df` (reviews/strategies stub) + `54c504920` (#1411 chat scaffold)

## Context

`/games/{id}` shipped a 7-tab UI shell (`Info / Regole / FAQ / Partite / Statistiche / Agenti / Documenti`) but the filesystem routes diverged:

**Wired tabs** (3): `Info → /page.tsx`, `Regole → /rules`, `FAQ → /faqs`. Plus `Partite → /sessions` (UI disabled with 🔒).

**Orphan routes** (3, no tab UI): `/reviews`, `/strategies`, `/chat` — scaffolded by gap-fix commits (#1411 + nav-map sweep), never wired into nav. Backend endpoints `/api/v1/games/{id}/reviews` + `/strategies` return 404 (#2195).

**UI tabs without routes** (2): `Agenti`, `Documenti` — render as tabs but no `/games/{id}/agents` or `/documents` page exists.

Codebase surrogate analytics (no production data this session):
- 0 inbound `<Link>` references to the 3 orphan routes
- 0 E2E specs covering them (5 existing game-detail specs all cover Info/Regole/FAQ/Partite)
- Git history confirms orphan routes scaffolded as gap-fix work, never wired

## Decision

We adopt **Opzione B (UI-driven canonical, remove orphan routes)** as the canonical game detail tab inventory.

**7 canonical tabs**: `Info / Regole / FAQ / Partite / Statistiche / Agenti / Documenti`.

### Sub-decisions

1. **Tab "Agenti"**: UI-only with `<Link href={\`/agents?gameId=\${id}\`}>` — no nested `/games/{id}/agents` route. Preserves single source of truth (existing `/agents` global hub) and passes game context via query filter.
2. **Tab "Documenti"**: UI-only with `<Link>` to existing Knowledge Base surface. Reassess in 3 months if analytics show demand for a dedicated `/games/{id}/documents` bookmark-able sub-route.
3. **5 sub-tab mockups** (#2198, Draft 11): closed as not-planned. Reviews/Strategies/Chat removed from scope; mockup commission no longer needed.
4. **Deprecation phase for orphan routes**: none. Monitor `meepleai_route_removed_404_total{path}` for 7 days post-deploy. If >10 hits/gg per path, reassess via follow-up issue.

## Spec-panel consensus (vote breakdown)

| Expert | Vote | Constraint added |
|---|---|---|
| Cockburn (use cases) | B | Reviews/Strategies/Chat are tertiary goals |
| Fowler (architecture) | B | Orphan routes already dead code, removal is cleanup |
| Newman (service evolution) | B | Distributed debt elimination |
| Adzic (executable spec) | B | Eliminates today's 404 broken E2E scenario |
| Crispin (testing) | B | Reduces test surface ~10 dev-days |
| Nygard (production) | B + 404 metric | Add `meepleai_route_removed_404_total{path}` for 7gg observation |
| Wiegers (requirements) | B + SMART DoD | 4 measurable acceptance criteria locked |

**Outcome**: 7/7 unanimous on Opzione B.

## Consequences

### Positive
- Eliminates 3 shipped dead-code routes that show user-facing errors (404 on BE endpoints, redirect-only `/chat` wrapper)
- Reduces test surface (no NEW endpoint contract tests, no NEW Playwright specs)
- Cleaner API surface for future schema migrations (Newman)
- Lowest delta path: ship in current delivery cycle without designer + backend dependency
- Reversible: future iteration can add Reviews/Strategies/Chat with proper UX + BE work when product priorities allow

### Negative
- Loses placeholder presence of Reviews/Strategies/Chat (negligible: 0 organic traffic, no working backend)
- Direct URL bookmarks to orphan routes return 404 (next.js default page)
- If future product roadmap revives Reviews/Strategies, must re-scaffold routes + commission mockups + implement BE — but no worse than starting from current state where routes exist but BE returns 404

### Neutral
- Tab labels (Agenti, Documenti, Statistiche) remain the same — only routing semantics change for Agenti/Documenti (UI-only link instead of nested route)
- i18n key `pages.gameDetail.tabs.stats` already fixed by #2226 ("Statistiche" / "Stats")

## Implementation acceptance criteria (Wiegers SMART)

DoD for #2204 (implementation sub-issue):

- [ ] 0 `<Link href>` or `router.push()` references to `/games/{id}/reviews`, `/strategies`, `/chat` in `apps/web/src/`
- [ ] 3 files deleted: `apps/web/src/app/(authenticated)/games/[id]/{reviews,strategies,chat}/page.tsx`
- [ ] Tab "Agenti" wired as `<Link href={\`/agents?gameId=\${id}\`}>` (no nested route)
- [ ] Tab "Documenti" wired as `<Link>` to existing KB surface (verify exact path during implementation)
- [ ] ESLint rule `local/no-game-detail-orphan-routes` prevents re-scaffolding of the 3 deleted paths
- [ ] Prometheus metric `meepleai_route_removed_404_total{path}` emitted post-deploy via Next.js middleware logging
- [ ] 7-day observation window post-merge → reassess if any path > 10 hits/gg
- [ ] BE endpoints `/api/v1/games/{id}/reviews` + `/strategies` deleted (closes #2195)
- [ ] #2198 closed as not-planned (5 sub-tab mockups no longer needed)
- [ ] #2197 (back link routing) scope adjusted (no longer covers /reviews/strategies pages)

## Observability commitment (Nygard)

Post-deploy of #2204, watch:
- Grafana panel: `meepleai_route_removed_404_total{path=~"/games/.+/(reviews|strategies|chat)"}`
- SLO: monitor only (no alert in first 7gg)
- Day-7 review: if total hits across 3 paths < 50/week, no further action. If > 50/week, file follow-up to investigate source (spider, stale link in external docs, etc.)

## Related

- Sister: #2204 (implementation — UNBLOCKED by this ADR)
- Sister: #2205 (i18n cleanup — already shipped via #2226)
- Closes by removal: #2195 (BE 404 endpoints no longer needed)
- Closes by removal: #2198 (Draft 11 mockup commission scope cancelled)
- Adjusts scope: #2197 (back link routing for orphan pages no longer in scope)
- Umbrella: #2194 (game detail tab nav orphan routes — closeable after #2204 ships)
- Spec-panel facilitation: this session 2026-06-12
- Decision deliverable: `docs/superpowers/decisions/2026-06-12-2203-game-detail-tab-inventory.md`
- Delivery plan: `docs/superpowers/plans/2026-06-11-p0-delivery-plan.md` Track D

## Refs

- ADR-059: Catalog Seed Legal Posture (similar shape: codebase audit + Wiegers SMART DoD)
- ADR-054: DevOps Multi-Branch Strategy (PR target branch pattern for #2204)
