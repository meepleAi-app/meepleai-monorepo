---
status: PROPOSED (brief 2h sessione Aaron)
parent: #2342 (Mockup-to-US Coverage execution v2.0) — Phase 0 P0.2
date: 2026-06-15
author: Claude Opus 4.7 (/sc:spec-panel critique follow-up)
duration: 2h
attendees: Aaron (designer) + Maintainer (badsworm)
---

# Designer Review Session Brief — 2026-06-15

> **Obiettivo della sessione**: ottenere designer signoff su umbrella #2342 v2.0 scope + decisione #2344 + acceptance dei 15 route asse-* in matrix #1895 unsigned + 3 forward-refactor pending.

## 🎯 Agenda (2h, 4 blocchi)

| Block | Durata | Argomento | Output atteso |
|-------|--------|-----------|---------------|
| **A** | 20' | Onboarding umbrella v2.0 + 6 DEC lockate | Aaron capisce scope + dependency graph |
| **B** | 15' | Decisione #2344 (sp7-game-night-edit Option A/B/C) | Decisione lockata + comment GH issue |
| **C** | 50' | Review live 15 route asse-* (priority order) | Signoff matrix ✅/⚠/🚫 + note |
| **D** | 35' | 3 forward-refactor pending (#2209/#2216/#2311) + Q&A | Signoff o issue follow-up |

## 📚 Block A — Onboarding umbrella v2.0 (20')

### Cosa è cambiato 2026-06-15
- **Critique 7-esperti** ha risolto 4 CRIT + 5 MAJOR (quality 6.7 → 8.8)
- **DEC-A4 standalone Play Records**: Tier 2 non più bloccato da Tier 3 GameNight
- **DEC-A5 5-stati cross-tier gate**: ogni sub-issue chiusa DEVE soddisfare 5 stati canonical (default/empty/loading/error/sse) con Storybook + CI lint:storybook-states
- **Phase 0 ADR gate**: 13 ADR-N drafts in tracker #2363 prima di Tier 2-6 start
- **CRIT-3 cleanup**: PR #2364 merged (10 file refs + 3 doc spec fix)

### Domande per Aaron
- [ ] Hai obiezioni allo scope v2.0? (parallelism matrix, success metrics)
- [ ] Vuoi essere notificato su ogni ADR draft mergiato (#2363) o solo su quelli che impattano UI?
- [ ] Confermi pattern 5-stati cross-tier come blocking acceptance gate?

## 🎲 Block B — Decisione #2344 sp7-game-night-edit (15')

### Background
Brief SP7 cita `sp7-game-night-edit.{html,jsx}` Wave 1 mockup C ma il file NON esiste sul filesystem (mai shippato dalle sessioni claude.ai/design). Necessaria decisione disposition.

### 3 opzioni proposte

| Opt | Approccio | Pros | Cons |
|-----|-----------|------|------|
| **A** | Dropped scope (consolidate edit con `detail-rsvp` state variant) | Zero nuovo mockup. Realistic — l'edit è raro post-creation | US-INT-3c usa "host-edit-mode" state inline che non è ancora mockup'd |
| **B** | Commission mockup mancante (apertura issue Design v1 BNN) | Brief SP7 invariato. Spec doc allineato | Aggiunge effort design 1gg + delays Tier 3c start |
| **C** | Architectural: edit via drawer overlay (no standalone route) | Coerente con asse-B DrawerStack pattern. Edit lives as drawer | Route `/game-nights/[id]/edit` diventa redirect a `?action=edit` |

### Recommended per Aaron: **Opt C** (compatibile con DrawerStack già shipped sess.33)

### Q
- [ ] Opt A/B/C? Decisione lockata in #2344
- [ ] Se Opt B, vuoi mockup desktop+mobile o solo desktop?

## 🔍 Block C — Review live 15 route asse-* (50')

### Setup
- Browser tab 1: route locale (http://localhost:3000)
- Browser tab 2: mockup HTML (apri da `admin-mockups/design_files/`)
- Side-by-side comparison
- Storybook stories (post DS-17 ready) per 5 stati

### Route da rivedere (prioritized post-merge)

| # | Route | Mockup canonical | Owner asse | Priority |
|---|-------|------------------|------------|----------|
| 1 | `/dashboard` | `sp4-dashboard.html` | C (#1898) | 🔴 P0 (4 priority sections) |
| 2 | `/library` | `sp4-library-desktop.html` | D | 🔴 P0 (US-10 hybrid hub) |
| 3 | `/library` (mobile <768px) | `sp4-library-mobile.html` (forward-refactor) | D | 🟡 P1 |
| 4 | `/games` | `sp4-games-index.html` + Discover default tab | D (#2270) | 🟡 P1 |
| 5 | `/discover` | `sp4-discover.html` | D | 🟡 P1 (US-INT-1 entry point) |
| 6 | `/games/[id]` | `sp4-game-detail.html` + 5 sub-tab | D (#2148 ADR-061) | 🔴 P0 (US-9) |
| 7 | `/players/[id]` | `sp4-player-detail.html` | D | 🟡 P1 |
| 8 | `/sessions` | `sp4-sessions-index.html` | D | 🟡 P1 |
| 9 | `/players` | `sp4-players-index.html` | D | 🟢 P2 |
| 10 | `/game-nights` | `sp4-game-nights-index.html` | D | 🟡 P1 (US-31 landing) |
| 11 | `/profile?tab=settings` | `sp5-profile-settings.html` | (sp5 standalone) | 🟢 P2 |
| 12 | `/settings` + sub-routes | `settings.html` | (auth standalone) | 🟢 P2 |
| 13 | `/notifications/preferences` | `notifications.html` | (auth standalone) | 🟢 P2 |
| 14 | `/login`, `/register`, `/reset-password` | `auth-flow.html` | (auth) | 🟢 P2 |
| 15 | `/join/event/[code]` | `sp7-game-night-join-public.jsx` (component-mock) | B (PR #1397) | 🟢 P2 |

### Checklist per ogni route (5 criteri)

```
Route: ____________________________
Mockup:____________________________

[ ] Tokens canonical: no hardcoded colors, no legacy v1 names (bg-base, gaming-*, nh-*, e-*)
    → Run pnpm lint:tokens su file modificati
[ ] 5 stati canonical implementati: default ✓ | empty ✓ | loading ✓ | error ✓ | sse ✓
    → Storybook stories per ognuno (post DS-17)
[ ] a11y AA: 0 violazioni axe (color-contrast, ARIA, keyboard nav, focus order)
    → Run axe DevTools o pnpm test:e2e --grep axe
[ ] Responsive 4 breakpoint: 375 / 768 / 1024 / 1440
    → Visual check side-by-side mockup
[ ] Match mockup block-level diff ≤5%
    → Side-by-side visual comparison

Verdict: ✅ Approved | ⚠ Approved with minor | 🚫 Rejected
Note: __________________________________________________
Follow-up issue: ________________________________________
```

## 🎨 Block D — 3 Forward-refactor + Q&A (35')

### Pending design review issues

| Issue | Mockup | Status | Cosa rivedere |
|-------|--------|--------|---------------|
| **#2209** | `sp3-library-public.html` | Forward-refactor pending designer review | Layout pubblico vs auth library, share token UX |
| **#2216** | `sp4-library-mobile.html` | Forward-refactor pending designer review | Mobile hybrid hub, swipeable tabs vs filter chips |
| **#2311** | `sp4-kb-detail.html` | Forward-refactor split-view chunks list + preview | Split-view rebuild (MVP placeholder → 6 component) |

### Action per ognuna
- [ ] Aaron review live (Storybook se ready, altrimenti mockup HTML standalone)
- [ ] Signoff sull'issue: ✅/⚠/🚫 + commit/PR follow-up se needed
- [ ] Aggiornamento status su umbrella #2342 Phase 0 P0.2

## 📋 Output atteso fine sessione

1. **Issue #2344 chiusa** con decisione Opt A/B/C lockata
2. **Matrix umbrella #1895 aggiornata** con signoff 15 route (commento Aaron sui PR)
3. **3 issue forward-refactor signoff** o decision-follow-up
4. **Comment su #2342 Phase 0 P0.2** con verdetto sessione (Aaron's stamp)

## 🚀 Setup tecnico pre-sessione

```bash
# 1. Avvia dev (se non già up)
cd infra && make dev-core   # Backend + Frontend + DB + Redis

# 2. Health check
curl http://localhost:8080/health
curl http://localhost:3000/

# 3. Aprire 2 finestre browser:
#    - Browser A: http://localhost:3000 (live app)
#    - Browser B: admin-mockups/design_files/*.html (mockup standalone)
#    Side-by-side visual comparison

# 4. Storybook (se ready, post DS-17)
cd apps/web && pnpm storybook   # http://localhost:6006

# 5. axe DevTools extension installata in browser
```

## 🔗 Refs

- Umbrella #2342 v2.0: https://github.com/meepleAi-app/meepleai-monorepo/issues/2342
- Phase 0 ADR tracker #2363: https://github.com/meepleAi-app/meepleai-monorepo/issues/2363
- Spec doc: `docs/for-developers/specs/2026-06-14-mockup-us-coverage-map.md`
- Umbrella asse-* #1895 (CLOSED, matrix unsigned): https://github.com/meepleAi-app/meepleai-monorepo/issues/1895
- Forward-refactor issue: #2209, #2216, #2311
- DS-17 #2063 (Storybook companion track)

🤖 Brief generated by Claude Code `/sc:spec-panel --mode critique` follow-up 2026-06-15
