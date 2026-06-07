# Reskin Audit Implementation Plan — 2026-06-08+

**Source**: audit `claudedocs/2026-06-07-reskin-verification.md` + umbrella [#1974](https://github.com/meepleAi-app/meepleai-monorepo/issues/1974)
**Status at start**: F18 ✅ shipped (PR #1978, pending merge) · F2 (#1975) + F25 (#1976) open + ~25 MAJOR/POLISH findings tracked

## Sequencing strategy

7 wave-based PRs/clusters ordered by **value × dependency × scope**. Quick wins first (Wave 1), then i18n + UX polish (Wave 2-4), then heavy rebuilds (Wave 5-7).

Ogni wave = 1 PR singolo (preferito) o cluster di PR atomici. Branch from `main-dev` ogni volta (no stacked).

---

## Wave 1 — Quick wins (~2h total)

| Finding | Issue | Effort | File hint |
|---------|-------|--------|-----------|
| F18 ✅ shipped | #1977 | — | **MERGE PR #1978 first** |
| **F2** BGG button removal user-side | #1975 | ~30min | `apps/web/src/app/(authenticated)/library/_content.tsx` o `LibraryHero` → trovare button "Importa BGG" + role-gate ad admin |
| **F32** typo "Partito" → "Partita" | (new) | ~10min | `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/page.tsx` o sub-component h1 |
| **F4.1** h1 fallback edge "game-not-in-library" | (sub di F4) | ~45min | `apps/web/src/app/(authenticated)/library/[gameId]/layout.tsx` — `LibraryGameHeader` 3-state machine: aggiungere distinzione `data === null + error.notFound` → "Gioco non trovato" |

**PR Wave 1**: 3 PR atomici (no batch — diversi file) o batch in 1 PR se conviene.

## Wave 2 — i18n + UX polish (~3h total)

| Finding | Issue | Effort | File hint |
|---------|-------|--------|-----------|
| **F25** `/onboarding` interests step EN → IT | #1976 | ~1-2h | `apps/web/src/app/(authenticated)/onboarding/_components/InterestsStep.tsx` (memoria sess.37 #132). Estrai strings → `pages.onboarding.interests.*` keys IT+EN. Localizza interest names (Strategy→Strategia, etc.). Aggiorna test fixtures |
| **F19** max-width cross-page | (new) | ~30min | `apps/web/src/components/layout/UserShell/DesktopShell.tsx` o wrapper parent — rimuovere `max-w-7xl` (o simile) → `flex-1 w-full` |
| **F29** `/play-records` empty state | (new) | ~1h | `apps/web/src/app/(authenticated)/play-records/page.tsx` — aggiungere empty illustration + messaggio + CTA "Registra prima partita" |

## Wave 3 — Asse D follow-ups + polish (~4h total)

| Finding | Issue | Effort | File hint |
|---------|-------|--------|-----------|
| **F23a** `/games` mini-nav tabs missing | (new sub di Asse D P2) | ~1-2h | `apps/web/src/app/(authenticated)/games/page.tsx` + `useMiniNavConfig` — verificare mount tabs Discover/Catalogo/Trending/Community |
| **F23b** nav highlight inconsistent | (new) | ~1h | `apps/web/src/components/layout/AppNav/AppTopBar.tsx` o sibling — mapping route→active voice. Check `/games` should highlight "Hub" or "Games", not "Library" |
| **F26** `/onboarding` Miniatures+button overlap | (new sub di F25) | ~30min | `InterestsStep.tsx` z-index/positioning fix |
| **F28** `/players` routing confusion | (new) | ~1h | `apps/web/src/app/(authenticated)/players/page.tsx` — controlla se mostra play-records UI (wrong) o se redirect |
| **F30** `/library/wishlist` auth gate hangs | (new) | ~30min | tracing auth check: probabile `useCurrentUser` + condition wait |

## Wave 4 — KB hub polish (~5h total)

Cluster in 1 PR batched (stesso bounded context):

| Finding | Issue | Effort |
|---------|-------|--------|
| **F4** game title UUID instead of name | (new) | ~1h — `KbHubContent.tsx:132` usa `useLibraryGameDetail` invece di `status.gameId` |
| **F5** stats strip mono tag style | (new) | ~1h — `HubDefault.tsx` riscrivere statsStripItems styled-mono |
| **F6** PDF row status badges per-PDF | P83 BE-deferred | (skip — BE schema enrich first) |
| **F7** stats duplicate KbStatsCard + HubDefault strip | (new) | ~1h — decisione: rimuovere `KbStatsCard` sidebar o collapse |
| **F9** sparkline hide if 0-data | (new) | ~30min — `KbStatsCard.tsx:208` showSparkline condition |
| **F10** bottom drop zone CTA | (new) | ~1.5h — mockup pattern `sp4-kb-hub.html` |

## Wave 5 — Chat layout MAJOR (~6-9h total)

| Finding | Effort | File hint |
|---------|--------|-----------|
| **F11** chat 3-col agent info sidebar 260px | ~4-6h | `apps/web/src/app/(chat)/chat/[threadId]/page.tsx` + new component `ChatAgentInfoPanel` |
| **F12** `/chat/new` 4 quick-starter cards | ~2-3h | `apps/web/src/app/(chat)/chat/new/page.tsx` — extract `QuickStarterCard` x4 |
| F13/F14/F15 chat polish (citation overlay, reader mode, wake-lock) | P83 deferred | — |

## Wave 6 — AddGameDrawer rebuild (~1-2 days, DESIGN track)

Reference mockup `admin-mockups/design_files/sp4-add-game-drawer.{html,jsx}` (834 LOC, 8 findings coverage).

| Task | Effort |
|------|--------|
| **T1** i18n CatalogSearchStep (F2.1 C1 closure) | ~1h |
| **T2** EmptyState component (M1) | ~1-2h |
| **T3** AlreadyInLibrary alert (M2) | ~1h |
| **T4** Choice card copy update (M3) | ~30min |
| **T5** useFocusTrap or verify shadcn Sheet a11y (M4) | ~1h |
| **T6** sonner toast on add success (N1) | ~30min |
| **T7** CardSkeleton shimmer (N2) | ~1h |
| **T8** Component decomposition refactor | ~2-3h |

Sequence: T1 (fastest closure) → T2/T3/T7 (UX) → T4 (text only) → T5 (a11y) → T6 (polish) → T8 (refactor).

## Wave 7 — Game-detail full rebuild MAJOR (~3-4 settimane)

Scope: **nuova issue umbrella separata** (NOT in #1974). F3 finding troppo grande per uno sprint singolo.

Mockup ref: `admin-mockups/design_files/sp4-game-detail.jsx` (1163 LOC) — hero + meta strip + 6 tabs + 5+ sezioni.

Sub-issues (open separately):
- W7.1: hero + breadcrumb component
- W7.2: meta strip (designer, anno, durata, players, complexity, rating)
- W7.3: 6 tabs structure
- W7.4: Descrizione section
- W7.5: Specifiche section (refer #1463 closed pattern)
- W7.6: House rules section
- W7.7: Sessions cards section
- W7.8: Chat agent inline embedded

---

## Cumulative effort estimate

| Wave | Effort | PR count |
|------|--------|----------|
| 1 | ~2h | 1-3 PR |
| 2 | ~3h | 3 PR |
| 3 | ~4h | 5 PR |
| 4 | ~5h | 1 PR (batched KB) |
| 5 | ~6-9h | 2 PR |
| 6 | ~1-2 days | 1-3 PR (T1-T8) |
| 7 | ~3-4 settimane | separate umbrella |

**Total Wave 1-6**: ~3-5 giorni di lavoro concentrato.

---

## Daily workflow recommendation

1. **Mattina**: pick 1 finding del wave corrente, branch from main-dev, implement + test + commit + push + PR
2. **Pomeriggio**: another finding (stessa wave o successiva)
3. **Fine giornata**: verifica audit doc + close finding nel tracker

After each wave completion:
- `gh pr merge` quando CI green
- Update `claudedocs/2026-06-07-reskin-verification.md` con status
- Comment su umbrella #1974

---

## 🚀 Prompt next session — copy/paste ready

Apri Claude Code nella prossima sessione e incolla questo prompt:

```
Riprendo audit reskin 2026-06-07 (umbrella #1974).

Setup quick recall (memory):
- Tracker file: claudedocs/2026-06-07-reskin-verification.md
- Implementation plan: claudedocs/2026-06-08-reskin-audit-implementation-plan.md
- Mockup server target: http://localhost:8765
- Hybrid deploy: `make tunnel && make integration` (Git Bash, ssh key ~/.ssh/meepleai-staging)
- Admin test user: admin@meepleai.app / 5ZwHNfXqTkRfTQG5bFr5MAPh (in admin.secret)
- Regular test user: badsworm@alice.it / MeepleAi1280!! (creato sessione precedente, emailVerified=false)
- RegistrationMode `Registration:PublicEnabled` row Integration env = true (TEMP per testing — restore false a fine session se necessario)

Stato PR audit:
- #1974 umbrella aperto
- #1975 (F2 BGG) #1976 (F25 onboarding) #1977 (F18 nav) sub-issues aperti
- #1978 (F18 fix) PR aperto pending merge — MERGE QUESTO PRIMA DI ALTRO

Step 1: verifica stato umbrella #1974 + PR #1978 status (mergeable?). Se OK merge.

Step 2: parti con Wave 1 del plan:
- F2 #1975 BGG removal user-side (`/library` page, `_content.tsx` o `LibraryHero` — rimuovere button "Importa BGG" o gate ad admin role)
- F32 typo "Partito" → "Partita" (sessions/live/[id] h1)
- F4.1 h1 fallback edge case (LibraryGameHeader 4-state: loading/loaded/404/not-in-user-library)

Per ogni finding:
1. Branch from main-dev
2. Read existing code + identify scope minimal
3. Apply fix
4. Test (pnpm typecheck + vitest unit if applicable)
5. Verify live via Playwright MCP (login admin + navigate target page + screenshot)
6. Commit + push + PR with reference a #1974
7. Update tracker section corrispondente
8. Comment umbrella con sub-issue closed

Avvia dal Wave 1 finding A (BGG removal). Procedi step-by-step e chiedi conferma prima di switch wave.
```

---

🤖 Plan generated 2026-06-07 sess.45 audit close
