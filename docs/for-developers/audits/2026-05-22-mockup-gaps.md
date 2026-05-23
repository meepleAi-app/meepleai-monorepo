# Mockup Gaps Audit — 2026-05-22

**Scope**: user-reachable routes (escluse `admin/(dashboard)/**`) prive di mockup
canonico in `admin-mockups/design_files/`.

**Method**: cross-reference fra le 96 route in `apps/web/src/app/(authenticated)/**` e
`apps/web/src/app/(public)/**`, l'inventario `admin-mockups/MOCKUPS_INDEX.md` e
la sezione *Route → Mockup index* di
[`v2-migration-matrix.md`](../frontend/v2-migration-matrix.md).

**Aggiorna**: [`2026-05-12-mockup-gaps.md`](./2026-05-12-mockup-gaps.md) (stale —
chiusura batch 2026-05-20 di #491/#492 ha invalidato gap #1 e #2, e l'audit
elencava 3 route `/library/playlists/*` che non esistono nel codebase).

**Total gaps confermati**: 9 cluster (di cui 2 da closure false-positive,
5 nuovi, 2 carry-over dall'audit precedente).

## Diff vs audit 2026-05-12

| # | Cluster | Stato 2026-05-12 | Stato 2026-05-22 | Δ |
|---|---|---|---|---|
| 1 | `/dashboard` (#491) | gap OPEN | **closure parziale** — `sp4-dashboard.html` consegnato, chat full-screen NO | ⚠️ ridotto |
| 2 | `/profile/*` (#492) | gap OPEN | **closure false-positive** — `community.jsx + .html` non esistono | ⚠️ riaprire |
| 3 | `/play-records/*` | gap, no issue | invariato — confermo gap, route esistono | = |
| 4 | `/library/playlists/*` | gap, no issue | **route inesistenti** → rimosso dall'audit | ✗ |
| 5 | `/pricing` | gap, no issue | route `(public)/pricing` esiste, gap commerciale invariato | = (fuori scope authenticated) |
| 6 | `/editor/agent-proposals/*` | non auditato | **gap nuovo P1** — 5 route user-facing AI Agent System | + |
| 7 | `/toolkit/{history,stats,templates,play}` | non auditato | **gap nuovo P1** — sub-route coperte solo da mapping generico in MOCKUPS_INDEX | + |
| 8 | `/private-games/[id]` vs `/library/private/[privateGameId]` | non auditato | **routing conflict P2** — co-esistenza non documentata | + |
| 9 | `/library/wishlist` | non auditato | **gap nuovo P2** — impl attiva (`MeepleWishlistCard`) senza mockup | + |
| 10 | `/games/[id]/{reviews,rules,strategies}` | non auditato | **gap nuovo P2** — sub-route oltre tabs `sp4-game-detail` | + |
| 11 | `/sessions/[id]/{scoreboard,notes,join,players}` + `/sessions/live/[sessionId]/{scores,photos,agent,players}` | non auditato | **gap nuovo P2** — 8 sub-route con scope distinto | + |

## Decision policy

> Stessa policy dell'audit 2026-05-12: documentare i gap qui, proporre azioni
> sulle issue esistenti, non aprire automaticamente nuove issue. Il maintainer
> (badsworm@gmail.com) decide il routing.
>
> **Numerazione B-series**: B1-B10 tutti CLOSED. Le nuove issue partono da
> **B11**.

## P0 — Gap critici (chiusura false-positive da correggere)

### #492 — Community / players / friends / achievements

- **Issue**: [#492](https://github.com/meepleAi-app/meepleai-monorepo/issues/492)
  *[Design v1 · B8] Mockup Community (player profile, friends, shared games)*
- **Stato**: CLOSED COMPLETED 2026-05-20
- **AC dichiarati**: `admin-mockups/design_files/community.jsx + .html` + link in
  `00-hub.html` + light/dark mode
- **AC reali**: nessun file `community*`, `friend*`, `achievement*` esiste in
  `design_files/`. Solo `sp4-player-detail.html` (preesistente Wave 3,
  PR #724) e `sp4-players-index.html`.
- **Route impattate** (5):
  - `/profile` — own profile (variant editable mancante)
  - `/profile/achievements` — achievements grid + detail sheets
  - `/players/[id]` — copertura partial via `sp4-player-detail` ma manca friends
    e activity feed
  - `/players/[id]/{achievements,games,sessions,stats}` — sub-tab coperti
    nominalmente, achievement detail sheet NO
- **Azione**: **riaprire #492** con scope ridotto ai 4 screen non consegnati
  (own-editable variant · friend list · shared-games marketplace · achievement
  detail sheet) oppure aprire **B11a follow-up**.

### #491 — Dashboard + Chat full-screen

- **Issue**: [#491](https://github.com/meepleAi-app/meepleai-monorepo/issues/491)
  *[Design v1 · B7] Mockup Dashboard desktop + Chat full-screen*
- **Stato**: CLOSED COMPLETED 2026-05-20
- **AC dichiarati**: `dashboard-chat.jsx + .html` + 5 screen (dashboard 3-col ·
  dashboard empty · chat full-screen desktop · chat mobile · chat empty) +
  light/dark mode
- **AC reali**: `sp4-dashboard.html` esiste (file naming differente, dashboard
  3-col coperto). Chat full-screen desktop NO — solo
  `nanolith-nav-chat-panel.html` che è component-mock (slide-over), non
  full-page.
- **Route impattate** (2):
  - `/dashboard` — coperta da `sp4-dashboard.html` ✓ (verificare dark mode e
    empty state)
  - `/chat/[threadId]` full-screen desktop — coperto solo come slide-over
    (`nanolith-nav-chat-panel`); il design originale prevedeva 3-col full-screen
- **Azione**: **riaprire #491** con scope ridotto al chat full-screen (3 screen:
  desktop 3-col · mobile · empty state) oppure aprire **B11b follow-up**.

## P1 — Gap nuovi bloccanti sprint corrente

### Gap N1 — `/editor/agent-proposals/*`

- **Route impattate** (5):
  - `/editor` — index editor user-facing
  - `/editor/agent-proposals` — index proposals
  - `/editor/agent-proposals/create` — create flow
  - `/editor/agent-proposals/[id]/edit` — edit detail
  - `/editor/agent-proposals/[id]/test` — playground test
- **Impatto**: feature attiva (Epic AI Agent System) senza UX spec → impl
  scollegato dal design system.
- **Existing**: nessuna issue dedicata. Search "mockup editor / agent-proposals"
  → 0 hit.
- **Tier suggerito**: M (proposal index = S, create/edit = M, test = M con stream).
- **Recommendation**: aprire `[Design v1 · B14] Mockup Editor user-facing
  (agent proposals)`.

### Gap N2 — `/play-records/*`

- **Route impattate** (4):
  - `/play-records` (index)
  - `/play-records/new`
  - `/play-records/[id]`
  - `/play-records/[id]/edit`
  - `/play-records/stats`
- **Impatto**: bloccante **US-32 (P1 sprint Core Game Loop)**.
- **Existing**: nessuna issue dedicata (carry-over audit 2026-05-12 #3).
- **Tier suggerito**: S (index + new + stats), M ([id] detail con timeline).
- **Recommendation**: aprire `[Design v1 · B11] Mockup Play Records (index,
  new, detail, edit, stats)`.

### Gap N3 — `/toolkit/{history,stats,templates,play}`

- **Route impattate** (4):
  - `/toolkit/history` — paginated list
  - `/toolkit/stats` — KPI dashboard
  - `/toolkit/templates` — gallery + filter
  - `/toolkit/play` — live session toolkit panel
- **Impatto**: `sp4-toolkit-detail.html` mappa generico "+ sub-routes" ma
  ognuna ha dati e interazioni distinte. Mancano esempi concreti (Adzic).
- **Existing**: nessuna. `sp4-toolkit-detail` copre `[sessionId]` detail solo.
- **Tier suggerito**: S (per ognuna).
- **Recommendation**: aprire `[Design v1 · B15] Mockup Toolkit sub-pages
  (history, stats, templates, play)`.

## P2 — Gap secondari / IA conflict

### Gap N4 — `/private-games/[id]` vs `/library/private/[privateGameId]`

- **Stato**: **routing conflict** — entrambe le route esistono in
  `(authenticated)/`:
  - `/private-games/[id]/page.tsx` → `PrivateGameDetailClient` (origine
    tracking commento Issue #3664, non in GH)
  - `/library/private/[privateGameId]/page.tsx`
- **Mockup attuale**: `sp4-game-detail.html` mappato a "/games/[id],
  /library/[gameId], /private-games/[id]" — usa la prima route stale.
- **Impatto**: confusione utente + manutenzione doppia. Cockburn: chi è
  l'attore primario per quale route?
- **Recommendation**: **decisione architetturale, non mockup**. Stabilire la
  route canonica (probabilmente `/library/private/[privateGameId]`) e deprecare
  l'altra con `redirect()` Next.js. Aggiornare il mapping
  in `MOCKUPS_INDEX.md` e in `v2-migration-matrix.md`.

### Gap N5 — `/library/wishlist`

- **Stato**: route esistente, codice impl attivo (`MeepleWishlistCard`,
  `AddToWishlistDialog`, `useWishlist`).
- **Mockup attuale**: nessuno dedicato. Riusa `MeepleCard` + Dialog primitives.
- **Impatto**: layout/empty state/add-dialog UX non specificati centralmente.
- **Tier**: S.
- **Recommendation**: estendere `sp4-library-desktop.html` con variant
  wishlist OR aprire `[Design v1 · B16] Mockup Library Wishlist standalone`.

### Gap N6 — `/games/[id]/{reviews,rules,strategies}`

- **Route impattate** (3): tre route fisicamente separate da `/games/[id]` index.
- **Mockup attuale**: `sp4-game-detail.html` ha tabs interne; non chiaro se
  ogni tab fosse pensata come sub-state o sub-route.
- **Impatto**: Fowler — interface design coherence. Decidere pattern
  uniforme tabs-as-state vs tabs-as-route.
- **Recommendation**: decisione architetturale + estensione `sp4-game-detail`
  con 3 screen sub-route OR ridurre fisicamente le route (Next.js routes →
  query params).

### Gap N7 — `/sessions/[id]/*` e `/sessions/live/[sessionId]/*` sub-routes

- **Route impattate** (8):
  - `/sessions/[id]/scoreboard` — vista standalone scoreboard
  - `/sessions/[id]/notes` — post-session notes editor
  - `/sessions/[id]/join` — join flow (sp3-join reuse?)
  - `/sessions/[id]/players` — players list management
  - `/sessions/live/[sessionId]/scores` — live scores entry
  - `/sessions/live/[sessionId]/photos` — photo gallery in-session
  - `/sessions/live/[sessionId]/agent` — agent panel in-session
  - `/sessions/live/[sessionId]/players` — players management live
- **Mockup attuale**: `sp4-session-live.html` (live aggregate) +
  `sp4-session-summary.html` (post-game). Le sub-route specifiche non distinte.
- **Recommendation**: aprire `[Design v1 · B17] Mockup Sessions sub-pages`
  (8 screen) OR consolidare sub-route in tabs interne dei mockup esistenti.

## P3 — Coverage stati (cross-cutting, Adzic)

Il fixture `01-screens.html` documenta 24 mobile screen, ma per le **route
reali** la coverage di stati canonici è disomogenea:

| Stato | Coverage attuale | Gap principali |
|---|---|---|
| **Empty state** | `/library`, `/library/wishlist`, `/discover` ✓ | mancano: `/play-records`, `/editor/agent-proposals`, `/notifications`, `/toolkit/history` |
| **Error state** | `nanolith-runthrough-error-states` (chat/translate/encounter only) | mancano: `/sessions/live` SSE-disconnect, `/upload` PDF-corrupt, `/games/[id]` no-data |
| **Loading skeleton** | Componenti impl (es. `WishlistSkeleton`), non specificati in mockup | discrepanza spec/impl globale — Wiegers traceability |
| **Permission-denied** | `auth-flow.html` (login only) | mancano: tier-locked features, suspended-account, expired-session inline |
| **Network-offline** | `nanolith-runthrough-error-states` parziale | mancano: offline cache per `/library`, `/play-records`, `/notifications` |

**Recommendation**: aprire `[Design v1 · B18] Mockup State Matrix
(empty/error/loading/permission/offline cross-route)` come fixture di tipo
`01-screens.html` ma orientata agli stati.

## Periferia (P3) — verifica scope

| Route | Stato | Azione |
|---|---|---|
| `/n8n` | esiste in `(authenticated)/` | Verificare se user-facing o vestigial admin-tool. Se admin → spostare sotto `admin/(dashboard)/` |
| `/pipeline-builder` | esiste in `(authenticated)/` | Idem — se admin-only, riposizionare |
| `/versions` | esiste in `(authenticated)/` | System info, low-priority, P3 |
| `/setup` | esiste, coperto da `onboarding.html` ✓ | nessuna azione |
| `/agents` | coperta da `sp4-agents-index` ✓ | nessuna azione |

## Sommario azioni per maintainer

### Issue da riaprire / aprire

1. **Riaprire #492** con scope ridotto (4 screen community) → o aprire B11a
2. **Riaprire #491** con scope ridotto (3 screen chat full-screen) → o aprire B11b
3. **Aprire B11** `Mockup Play Records` — bloccante US-32
4. **Aprire B14** `Mockup Editor user-facing (agent proposals)`
5. **Aprire B15** `Mockup Toolkit sub-pages`
6. **Aprire B16** `Mockup Library Wishlist standalone` (estensione)
7. **Aprire B17** `Mockup Sessions sub-pages` (estensione)
8. **Aprire B18** `Mockup State Matrix cross-route`

### Decisioni architetturali (non-mockup)

9. **N4** — decidere route canonica `/library/private/[privateGameId]` vs
   `/private-games/[id]`; deprecare l'altra
10. **N6** — decidere pattern tabs-as-state vs tabs-as-route per `/games/[id]/*`
11. **N7** — decidere consolidamento sub-route sessions in tabs vs route fisiche

### Manutenzione audit

12. **Aggiornare `MOCKUPS_INDEX.md`** dopo merge della riapertura #491/#492
13. **Stabilire CI check** route-vs-mockup (un drift simile al gap #4 dell'audit
    precedente — playlists route inesistente — non deve ripresentarsi).
    Suggerimento: script che fa `grep -r "page.tsx" apps/web/src/app/(authenticated)/`
    e diffa contro `MOCKUPS_INDEX.md`.

## Cross-references

- [`docs/for-developers/audits/2026-05-12-mockup-gaps.md`](./2026-05-12-mockup-gaps.md) — audit precedente (stale)
- [`docs/for-developers/frontend/v2-migration-matrix.md`](../frontend/v2-migration-matrix.md) — Route → Mockup index
- [`admin-mockups/MOCKUPS_INDEX.md`](../../../admin-mockups/MOCKUPS_INDEX.md) — File classification
- Issue umbrella #1023 — Design System De-versioning & Mockup-Faithful Convergence
- Issue #492 (closure false-positive) · #491 (closure parziale)

## Reminder

> Questo audit **non crea automaticamente** nuove issue (policy ereditata
> dall'audit 2026-05-12). Le proposte alle voci 1-8 vanno applicate dal
> maintainer via GH UI / `gh` CLI.
