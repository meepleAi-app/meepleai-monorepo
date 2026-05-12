# Mockup Gaps Audit — 2026-05-12

**Scope**: user-reachable routes (excluding `admin/(dashboard)/**`) that lack a
canonical mockup in `admin-mockups/design_files/`.

**Method**: cross-referenced 84 routes from `apps/web/src/app/` against the
mockup inventory (see [`MOCKUPS_INDEX.md`](../../../admin-mockups/MOCKUPS_INDEX.md)
and the *Route → Mockup index* section of
[`v2-migration-matrix.md`](../frontend/v2-migration-matrix.md)).

**Total gaps identified**: 5 user-facing route clusters with **high journey
impact**. Each is mapped below to an existing GitHub issue when one exists, or
flagged as needing remediation otherwise.

## Decision policy (2026-05-12)

> Per maintainer instruction: **do not open new GitHub issues for these gaps**.
> Instead, document the gap here, link to any existing issue, and propose
> updates to existing issues where overlap exists. The Design v1 epic
> (`B*`/`M3.*` series) is the umbrella for new mockup requests.

## Gap → Existing issue mapping

| # | Gap (route cluster) | Existing issue | Status | Action |
|---|---|---|---|---|
| 1 | `/dashboard` | [#491](https://github.com/meepleAi-app/meepleai-monorepo/issues/491) *[Design v1 · B7] Mockup Dashboard desktop + Chat full-screen* | **OPEN** | Already covers gap. **No new issue needed.** Add cross-link to this audit in #491 body. |
| 2 | `/profile` · `/profile/achievements` | [#492](https://github.com/meepleAi-app/meepleai-monorepo/issues/492) *[Design v1 · B8] Mockup Community (player profile, friends, shared games)* | **OPEN** | Already covers gap. **No new issue needed.** Add cross-link to this audit in #492 body. |
| 3 | `/play-records/*` (5 routes: index, new, `[id]`, `[id]/edit`, stats) | — | — | **No existing issue.** Propose: append `[Design v1 · B11] Mockup Play Records` to existing B-series via comment on epic / umbrella issue, OR extend #492 scope to include play-records. Owner decides per current Design v1 capacity. |
| 4 | `/library/playlists/*` (3 routes: index, `[id]`, `shared/[token]`) | — | — | **No existing issue.** Propose: append `[Design v1 · B12] Mockup Library Playlists` OR extend an existing Library-area issue (e.g. SP4 library follow-up). |
| 5 | `/pricing` | — | — | **No existing issue.** Propose: append `[Design v1 · B13] Mockup Pricing / Plans page` OR fold into the public-pages mockup work (#493 closed — secondary public pages). Likely needs a fresh slot in the B-series given commercial sensitivity. |

## Detailed inventory

### Gap 1 — `/dashboard`

- **Routes affected**: `/dashboard`
- **Impact**: hub principale post-login; oggi inferito da `02-desktop-patterns.html` (pattern library, non page-level).
- **Existing**: #491 already specifies the full Dashboard + Chat full-screen design.
- **Recommendation**: tag #491 in the next Claude Design batch (production resumes post 2026-05-10 per project memory).

### Gap 2 — `/profile/*`

- **Routes affected**: `/profile`, `/profile/achievements` (2 routes)
- **Impact**: standard user page mancante. Oggi `settings.html` copre solo le preferenze; manca la "self-view" (display name, avatar, achievements pubblici).
- **Existing**: #492 already specifies Player profile (public + own-editable variants).
- **Recommendation**: confirm #492 scope includes the *own-editable* variant (it does — see issue body screen #2).

### Gap 3 — `/play-records/*`

- **Routes affected** (5):
  - `/play-records` (index)
  - `/play-records/new`
  - `/play-records/[id]`
  - `/play-records/[id]/edit`
  - `/play-records/stats`
- **Impact**: P1 sprint per *Core Game Loop* (US-32). 5 route senza copertura.
- **Existing**: nessuna issue dedicata. Note: #496 *[Design v1 · M3.2] Migrate Sessions pages to v2* tocca `/sessions/*`, ma play-records è un BC separato (`SessionTracking` vs `GameManagement`).
- **Recommendation**: aprire `[Design v1 · B11] Mockup Play Records (index, edit, stats)` quando la design capacity è disponibile. Bloccante per US-32.

### Gap 4 — `/library/playlists/*`

- **Routes affected** (3):
  - `/library/playlists`
  - `/library/playlists/[id]`
  - `/library/playlists/shared/[token]`
- **Impact**: US-attiva senza copertura visuale. Feature di Curation/Sharing.
- **Existing**: nessuna issue dedicata. `/library` ha già il mockup `sp4-library-desktop.html` ma non copre il sotto-flusso playlists.
- **Recommendation**: estendere il mockup library con sezione playlists, OR aprire `[Design v1 · B12] Mockup Library Playlists`.

### Gap 5 — `/pricing`

- **Routes affected**: `/pricing`
- **Impact**: landing commerciale assente. Senza pricing page, il funnel di acquisizione manca di una surface critica per il tier-upgrade flow.
- **Existing**: nessuna issue dedicata. `pricing-card` primitive esiste in `components/ui/`, ma non c'è una pagina di destinazione.
- **Recommendation**: prioritizzare in coordinamento con il team commerciale / monetization. Issue: `[Design v1 · B13] Mockup Pricing page (tiers, FAQ, CTA)`.

## Follow-up actions for maintainer

The maintainer (badsworm@gmail.com) should manually:

1. **Comment on #491** linking to this audit (validates dashboard gap).
2. **Comment on #492** linking to this audit (validates profile gap).
3. **Decide routing** for gaps 3/4/5:
   - Open 3 new B-series issues, OR
   - Extend an existing umbrella (e.g. #1023 Design System De-versioning) with a "page-gap" checklist, OR
   - Defer to next Design v1 cycle (post 2026-05-10 production resumption).

> **Reminder**: this audit explicitly does **not** create new GitHub issues
> per the policy stated at the top. Modifications to existing issues are
> proposals only — the maintainer applies them via GH UI / `gh` CLI.

## Cross-references

- [`docs/for-developers/frontend/v2-migration-matrix.md`](../frontend/v2-migration-matrix.md) — Route → Mockup index (page-level)
- [`admin-mockups/MOCKUPS_INDEX.md`](../../../admin-mockups/MOCKUPS_INDEX.md) — File classification
- Design v1 B-series tracker: search issues with prefix `[Design v1 · B`
- Umbrella #1023 — Design System De-versioning & Mockup-Faithful Convergence
