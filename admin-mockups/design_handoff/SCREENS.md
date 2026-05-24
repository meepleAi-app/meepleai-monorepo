# SCREENS — Inventario completo

71 schermate nel demo, organizzate per fase. Per ognuna: file mock, route attesa, endpoint backend, priorità implementazione.

**Priorità**:  
P0 = critical path (gold scenario)  
P1 = core feature  
P2 = nice to have / edge case  
P3 = solo reference

---

## Fase 01 — Dashboard

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 01 | `sp4-dashboard` | `/dashboard` | `GET /api/dashboard?include=...` | P0 |

Dashboard aggrega contatori cross-entity. Endpoint backend deve restituire:
- recent sessions (3)
- upcoming game nights (3)
- top agents per usage
- KB indexing status
- player group activity

---

## Fase 02 — Game Nights flow ⭐ gold scenario

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 02 | `sp4-game-nights-index` | `/game-nights` | `GET /api/game-nights?from=&to=` | P0 |
| 03 | `sp4-game-nights-index` (drawer) | `/game-nights?day=2026-03-22` | (stessa, filtered per day) | P0 |
| 04 | `sp7-game-night-create` | `/game-nights/new` | `POST /api/game-nights` | P0 |
| 05 | `sp7-game-night-detail-rsvp` (host) | `/game-nights/[id]` | `GET /api/game-nights/{id}` | P0 |
| 06 | `sp7-game-night-detail-rsvp` (invited) | (stessa, ruolo diverso) | `PATCH /api/game-nights/{id}/rsvp` | P0 |
| 07 | `sp7-game-night-transition` | `/game-nights/[id]/transition` | (modal, no nuovo endpoint) | P1 |

---

## Fase 03 — Giocatori

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 08 | `sp4-players-index` | `/players` | `GET /api/players` | P1 |
| 09 | `sp4-player-detail` | `/players/[id]` | `GET /api/players/{id}?include=stats,games` | P1 |

---

## Fase 04 — Library & Games

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 10 | `sp4-library-desktop` | `/library` | `GET /api/library?filters=` | P0 |
| 11 | `sp4-library-desktop` (filtri drawer) | `/library?drawer=filters` | (stessa, drawer client) | P0 |
| 12 | `sp4-games-index` | `/games` | `GET /api/games` | P1 |
| 13 | `sp4-hub-games` | `/hub/games` | `GET /api/hub/games` | P2 |
| 14 | `sp4-game-detail` | `/games/[id]` | `GET /api/games/{id}?include=...` | P0 |
| 15 | `sp4-add-game-bgg-step` | `/games/new` | `POST /api/games` | P1 |

---

## Fase 05 — Knowledge Base

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 16 | `sp4-kb-detail` | `/games/[id]/kb` | `GET /api/games/{id}/kb` | P0 |
| 17 | `sp4-kb-hub` | `/kb/hub` | `GET /api/kb/hub` | P2 |
| 18 | `sp4-kb-globale` | `/kb` | `POST /api/kb/search` (SSE) | P1 |
| 19 | `sp4-upload-wizard-extended` | `/games/[id]/kb/upload` | `POST /api/games/{id}/kb/upload` | P1 |
| 20 | `sp4-add-game-pdf-dedup` | (modal) | `POST /api/kb/dedup-check` | P2 |
| 21 | `sp4-citation-pdf-viewer` | `/kb/docs/[id]?p=` | `GET /api/kb/docs/{id}.pdf` | P1 |

---

## Fase 06 — AI Agents

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 22 | `sp7-library-game-agent` | `/library/games/[id]/agent` | `POST /api/agents/{id}/query` (SSE) | P0 |
| 23 | `sp7-library-game-agent` (HR drawer) | `?drawer=house-rule` | `POST /api/games/{id}/house-rules` | P0 |
| 24 | `sp4-game-chat-tab` | `/games/[id]?tab=chat` | (variante, stessa API) | P1 |
| 25 | `chat-fullscreen` | `/chat/[id]` | (stessa, fullscreen route) | P2 |
| 26 | `sp4-agents-index` | `/agents` | `GET /api/agents` | P1 |
| 27 | `sp4-hub-agents` | `/hub/agents` | `GET /api/hub/agents` | P2 |
| 28 | `sp4-agent-detail` | `/agents/[id]` | `GET /api/agents/{id}?include=...` | P1 |
| 29 | `sp4-toolkit-detail` | `/toolkits/[id]` | `GET /api/toolkits/{id}` | P2 |
| 30 | `sp4-hub-toolkits` | `/hub/toolkits` | `GET /api/hub/toolkits` | P2 |

---

## Fase 07 — Sessioni

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 31 | `sp4-sessions-index` | `/sessions` | `GET /api/sessions` | P1 |
| 32 | `sp4-session-live` | `/sessions/[id]/live` | `WebSocket /sessions/{id}/events` | P0 |
| 33 | `sp4-session-summary` | `/sessions/[id]/summary` | `GET /api/sessions/{id}/summary` | P0 |

---

## Fase 08 — Serata Live

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 34 | `sp7-game-night-live` | `/game-nights/[id]/live` | `WebSocket /game-nights/{id}` | P0 |
| 35 | `sp7-game-night-summary` | `/game-nights/[id]/summary` | `GET /api/game-nights/{id}/summary` | P0 |

---

## Fase 09 — Librogame (SP6)

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 36 | `sp6-libro-game-index` | `/librogame` | `GET /api/librogame/library` | P1 |
| 37 | `librogame-runthrough-library-search` | `/librogame/search` | `GET /api/librogame/search` | P1 |
| 38 | `librogame-runthrough-game-detail` | `/librogame/[id]` | `GET /api/librogame/{id}` | P1 |
| 39 | `librogame-runthrough-game-onboarding` | `/librogame/[id]/onboarding` | (state machine FE) | P1 |
| 40 | `librogame-game-night-storyboard` | `/librogame/[id]/storyboard` | `POST /api/librogame/{id}/storyboard` | P2 |
| 41 | `sp6-libro-game-photo-upload` | `/librogame/[id]/photo` | `POST /api/librogame/{id}/photo` | P1 |
| 42 | `librogame-runthrough-setup-wizard` | `/librogame/[id]/setup` | (FE wizard) | P1 |
| 43 | `librogame-runthrough-setup-chat` | (stessa) | `POST /api/agents/setup-chat` | P1 |
| 44 | `librogame-runthrough-resume-picker` | `/librogame/resume` | `GET /api/librogame/states` | P1 |
| 45 | `librogame-runthrough-play-session` | `/librogame/[id]/play` | `WebSocket /librogame/{id}` | P0 |
| 46 | `librogame-runthrough-encounter-cheatsheet` | (overlay) | `POST /api/librogame/{id}/encounter` | P1 |
| 47 | `librogame-runthrough-glossary-editor` | (overlay) | `POST /api/librogame/{id}/glossary` | P1 |
| 48 | `librogame-runthrough-translate-viewer` | (overlay) | `POST /api/translate` | P1 |
| 49 | `librogame-runthrough-quota-credits` | `/credits` | `GET /api/credits/balance` | P1 |
| 50 | `librogame-runthrough-session-end` | `/librogame/[id]/end` | `POST /api/librogame/{id}/finalize` | P1 |
| 51 | `librogame-runthrough-error-states` | (reference) | — | P3 |

---

## Fase 10 — Discover & Account

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 52 | `sp4-discover` | `/discover` | `GET /api/discover` | P2 |
| 53 | `notifications` | `/notifications` | `GET /api/notifications` | P1 |
| 54 | `settings` | `/settings` | `GET /api/user`, `GET /api/groups/{id}` | P0 |
| 55 | `onboarding` | `/onboarding` | `PATCH /api/user` | P1 |

---

## Fase 11 — Pre-auth (vista pubblica)

| # | Mock | Route | Endpoint principale | Priorità |
|---|------|-------|---------------------|----------|
| 56 | `public` | `/` | (static + dynamic featured) | P1 |
| 57 | `sp3-faq-enhanced` | `/faq` | (static) | P1 |
| 58 | `sp3-how-it-works` | `/how-it-works` | (static) | P1 |
| 59 | `sp3-shared-games` | `/games` (public) | `GET /api/public/games` | P2 |
| 60 | `sp3-shared-game-detail` | `/games/[slug]` (public) | `GET /api/public/games/{slug}` | P2 |
| 61 | `sp3-library-public` | `/groups/[slug]/library` | `GET /api/public/groups/{slug}` | P2 |
| 62 | `sp3-join` | `/join?token=` | `POST /api/invites/{token}/accept` | P1 |
| 63 | `sp3-accept-invite` | `/invite?token=` | (stessa) | P1 |
| 64 | `sp3-legal` | `/terms`, `/privacy` | (static) | P1 |

---

## Fase 12 — Design System (reference, NO backend)

| # | Mock | Route | Endpoint | Priorità |
|---|------|-------|----------|----------|
| 65 | `00-hub` | (reference) | — | P3 |
| 66 | `01-screens` | (reference) | — | P3 |
| 67 | `02-desktop-patterns` | (reference) | — | P3 |
| 68 | `03-drawer-variants` | (reference) | — | P3 |
| 69 | `04-design-system` | (reference) | — | P3 |
| 70 | `05-dark-mode` | (reference) | — | P3 |
| 71 | `state-matrix` | (reference) | — | P3 |

Queste sono pure showcase di componenti. Servono solo come riferimento visivo.

---

## Roadmap consigliata di implementazione

### Sprint 1 — Foundation (settimana 1-2)

P0 minimi per avere un'app navigabile:
1. Setup tokens + EntityChip + ConnectionBar
2. `/dashboard`
3. `/library` (senza filtri avanzati)
4. `/games/[id]` (info tab only)
5. Auth flow base

### Sprint 2 — Game Nights gold scenario (settimana 3-4)

6. `/game-nights/new` (wizard)
7. `/game-nights/[id]` (host + invited views)
8. `/game-nights/[id]/live`
9. `/game-nights/[id]/summary`
10. Notifications base

### Sprint 3 — AI Agents (settimana 5-6)

11. `/library/games/[id]/agent` con SSE
12. HouseRuleDrawer
13. `/agents` index + detail
14. KB upload + indexing

### Sprint 4 — Sessions (settimana 7)

15. `/sessions/[id]/live` (WebSocket)
16. `/sessions/[id]/summary`

### Sprint 5 — Librogame (settimana 8-10)

17. Tutto il flow librogame (P0+P1 della Fase 09)

### Sprint 6 — Polish (settimana 11-12)

18. Settings completo
19. Hub pages (P2)
20. Discover
21. Pre-auth marketing pages

### Sprint 7 — Edge cases & Polish (settimana 13)

22. Error states (Fase 09 #51)
23. Tutte le P2 e P3 rimaste

---

## Note finali

- Le route sono **suggerite** — adatta ai tuoi pattern (Next app vs pages, ecc.)
- Gli endpoint sono **forme attese** — adatta a OpenAPI/GraphQL se diverso
- I mock NON sono pixel-perfect su tutto: piccole differenze visive sono normali
- Quando un mock ha più stati side-by-side (es. 3 viste mobile + 2 desktop), implementa **tutti** gli stati ma uno alla volta

Riferimenti operativi: vedi `WIRING_GUIDE.md` per come tradurre un singolo mock.
