# Happy Path — Mappa di copertura globale (route → area)

> Guardia anti-buchi. Ogni route `page.tsx` dell'app è assegnata a **esattamente una** macro-area. Task A-FINAL verifica che l'unione delle matrici dei 13 cataloghi copra tutte le route qui elencate. Ultimo inventario: 2026-07-10 (`glob apps/web/src/app/**/page.tsx`).
>
> Colonna **Liv.**: livello atteso (`Flow` transazionale · `Smoke` vista read-only). Indicativo — il catalogo può correggerlo.

## U1 — Accesso, Onboarding & Pubbliche informative (25)

| Route | Liv. |
|-------|------|
| `(auth)/login` | Flow |
| `(auth)/register` | Flow |
| `(auth)/reset-password` | Flow |
| `(auth)/setup-account` | Flow |
| `(auth)/verify-email` | Flow |
| `(auth)/verification-pending` | Smoke |
| `(auth)/verification-success` | Smoke |
| `(auth)/welcome` | Smoke |
| `(auth)/oauth-callback` | Smoke |
| `(auth)/invitation-expired` | Smoke |
| `(public)/` (landing) | Smoke |
| `(public)/accept-invite` | Flow |
| `(public)/invites/[token]` | Flow |
| `(public)/about` | Smoke |
| `(public)/contact` | Smoke |
| `(public)/pricing` | Smoke |
| `(public)/legal` | Smoke |
| `(public)/faq` | Smoke |
| `(public)/how-it-works` | Smoke |
| `(public)/privacy` | Smoke |
| `(public)/terms` | Smoke |
| `(public)/cookies` | Smoke |
| `(public)/cookie-settings` | Flow |
| `(authenticated)/onboarding` | Flow |
| `(authenticated)/setup` | Flow |

## U2 — Catalogo & Discover (11)

| Route | Liv. |
|-------|------|
| `(authenticated)/games` | Flow |
| `(authenticated)/games/[id]` | Flow |
| `(authenticated)/games/[id]/faqs` | Smoke |
| `(authenticated)/games/[id]/rules` | Smoke |
| `(authenticated)/games/[id]/sessions` | Smoke |
| `(authenticated)/discover` | Flow |
| `(authenticated)/hub` | Smoke |
| `(authenticated)/hub/games/[id]` | Smoke |
| `(public)/shared-games` | Smoke |
| `(public)/shared-games/[id]` | Flow |
| `(public)/library-public` | Smoke |

## U3 — Library & Knowledge Base (18)

| Route | Liv. |
|-------|------|
| `(authenticated)/library` | Flow |
| `(authenticated)/library/wishlist` | Flow |
| `(authenticated)/library/private` | Smoke |
| `(authenticated)/library/private/add` | Flow |
| `(authenticated)/library/private/[id]` | Smoke |
| `(authenticated)/library/private/[id]/toolkit/configure` | Flow |
| `(authenticated)/library/[gameId]` | Smoke |
| `(authenticated)/library/[gameId]/kb` | Smoke |
| `(authenticated)/private-games/[id]` | Smoke |
| `(authenticated)/upload` | Flow |
| `(authenticated)/knowledge-base` | Smoke |
| `(authenticated)/knowledge-base/global` | Smoke |
| `(authenticated)/knowledge-base/[id]` | Smoke |
| `(authenticated)/knowledge-base/[id]/pdf` | Flow |
| `(authenticated)/kb/[id]` | Smoke |
| `(public)/library/shared/[token]` | Smoke |
| `(authenticated)/gamebook` | Smoke |
| `(authenticated)/gamebook/upload` | Flow |

> Nota: `gamebook` e `gamebook/upload` sono qui per prossimità KB/upload; la *riproduzione* gamebook (`library/[gameId]/play/**`) sta in U7.

## U4 — Chat RAG & Agenti (14)

| Route | Liv. |
|-------|------|
| `(chat)/chat` | Flow |
| `(chat)/chat/[threadId]` | Flow |
| `(chat)/chat/new` | Flow |
| `(chat)/chat/agents/create` | Flow |
| `(authenticated)/library/[gameId]/agent` | Flow |
| `(authenticated)/agents` | Smoke |
| `(authenticated)/agents/[id]` | Smoke |
| `(authenticated)/editor` | Smoke |
| `(authenticated)/editor/agent-proposals` | Smoke |
| `(authenticated)/editor/agent-proposals/create` | Flow |
| `(authenticated)/editor/agent-proposals/[id]/edit` | Flow |
| `(authenticated)/editor/agent-proposals/[id]/test` | Flow |
| `(authenticated)/pipeline-builder` | Smoke |
| `(authenticated)/hub/agents` | Smoke |

## U5 — Game Night (6)

| Route | Liv. |
|-------|------|
| `(authenticated)/game-nights` | Smoke |
| `(authenticated)/game-nights/new` | Flow |
| `(authenticated)/game-nights/[id]` | Flow |
| `(authenticated)/game-nights/[id]/live` | Flow |
| `(authenticated)/game-nights/[id]/summary` | Smoke |
| `(public)/game-nights/shared/[token]` | Smoke |

> `(public)/join/event/[code]` è in U5 concettualmente ma catalogato in U6 con gli altri `join/*` (vedi nota U6).

## U6 — Sessioni & Scoring (23)

| Route | Liv. |
|-------|------|
| `(authenticated)/sessions` | Smoke |
| `(authenticated)/sessions/new` | Flow |
| `(authenticated)/sessions/join` | Flow |
| `(authenticated)/sessions/[id]` | Smoke |
| `(authenticated)/sessions/[id]/live` | Flow |
| `(authenticated)/sessions/[id]/notes` | Flow |
| `(authenticated)/sessions/[id]/scoreboard` | Smoke |
| `(authenticated)/sessions/[id]/join` | Flow |
| `(authenticated)/play-records` | Smoke |
| `(authenticated)/play-records/new` | Flow |
| `(authenticated)/play-records/[id]` | Smoke |
| `(authenticated)/play-records/[id]/edit` | Flow |
| `(authenticated)/play-records/stats` | Smoke |
| `(authenticated)/players` | Smoke |
| `(authenticated)/players/[id]` | Smoke |
| `(authenticated)/players/[id]/achievements` | Smoke |
| `(authenticated)/players/[id]/games` | Smoke |
| `(authenticated)/players/[id]/sessions` | Smoke |
| `(authenticated)/players/[id]/stats` | Smoke |
| `(public)/join` | Smoke |
| `(public)/join/event/[code]` | Flow |
| `(public)/join/session/[code]` | Flow |
| `(public)/play-records/shared/[token]` | Smoke |

## U7 — Toolkit & Gamebook play (22)

| Route | Liv. |
|-------|------|
| `(authenticated)/toolkit` | Smoke |
| `(authenticated)/toolkit/play` | Flow |
| `(authenticated)/toolkit/history` | Smoke |
| `(authenticated)/toolkit/stats` | Smoke |
| `(authenticated)/toolkit/templates` | Smoke |
| `(authenticated)/toolkit/[sessionId]` | Flow |
| `(authenticated)/toolkits` | Smoke |
| `(authenticated)/toolkits/[id]` | Smoke |
| `(authenticated)/hub/toolkits` | Smoke |
| `(authenticated)/library/[gameId]/toolbox` | Smoke |
| `(authenticated)/library/[gameId]/toolkit` | Smoke |
| `(authenticated)/library/[gameId]/toolkit/[sessionId]` | Flow |
| `(authenticated)/library/[gameId]/play` | Smoke |
| `(authenticated)/library/[gameId]/play/[campaignId]` | Flow |
| `(authenticated)/library/[gameId]/play/[campaignId]/encounter` | Flow |
| `(authenticated)/library/[gameId]/play/[campaignId]/translate` | Flow |

> Conteggio 16 righe reali; il briefing del piano stimava ~22 includendo varianti. Il catalogo U7 verifica via glob.

## U8 — Profilo & Notifiche (7)

| Route | Liv. |
|-------|------|
| `(authenticated)/dashboard` | Smoke |
| `(authenticated)/profile` | Flow |
| `(authenticated)/profile/achievements` | Smoke |
| `(authenticated)/notifications` | Flow |
| `(authenticated)/notifications/preferences` | Flow |
| `(authenticated)/versions` | Smoke |
| `(authenticated)/n8n` | skip (n8n in rimozione) |

## A1 — Agenti AI admin (24)

| Route | Liv. |
|-------|------|
| `admin/(dashboard)/agents` | Smoke |
| `admin/(dashboard)/agents/builder` | Smoke |
| `admin/(dashboard)/agents/config` | Flow |
| `admin/(dashboard)/agents/playground` | Flow |
| `admin/(dashboard)/agents/ab-testing/new` | Flow |
| `admin/(dashboard)/agents/ab-testing/[id]` | Smoke |
| `admin/(dashboard)/agents/ab-testing/results` | Smoke |
| `admin/(dashboard)/agents/chat-history` | Smoke |
| `admin/(dashboard)/agents/chat-limits` | Flow |
| `admin/(dashboard)/agents/debug` | Smoke |
| `admin/(dashboard)/agents/debug-chat` | Flow |
| `admin/(dashboard)/agents/definitions` | Smoke |
| `admin/(dashboard)/agents/definitions/create` | Flow |
| `admin/(dashboard)/agents/definitions/[id]` | Smoke |
| `admin/(dashboard)/agents/definitions/[id]/edit` | Flow |
| `admin/(dashboard)/agents/definitions/playground` | Flow |
| `admin/(dashboard)/agents/models` | Smoke |
| `admin/(dashboard)/agents/pipeline` | Smoke |
| `admin/(dashboard)/agents/sandbox` | Smoke |
| `admin/(dashboard)/agents/strategy` | Smoke |
| `admin/(dashboard)/agents/templates` | Smoke |
| `admin/(dashboard)/agents/inspector` | Smoke |
| `admin/(dashboard)/agents/analytics` | Smoke |
| `admin/(dashboard)/agents/usage` | Smoke |
| `admin/(dashboard)/agents/infrastructure` | Smoke |

> 25 righe (una in più del titolo: infrastructure). Il catalogo A1 conferma via glob.

## A2 — Knowledge Base admin (19)

| Route | Liv. |
|-------|------|
| `admin/(dashboard)/knowledge-base` | Smoke |
| `admin/(dashboard)/knowledge-base/documents` | Smoke |
| `admin/(dashboard)/knowledge-base/embedding` | Smoke |
| `admin/(dashboard)/knowledge-base/queue` | Smoke |
| `admin/(dashboard)/knowledge-base/rag-pipeline` | Smoke |
| `admin/(dashboard)/knowledge-base/pipeline` | Smoke |
| `admin/(dashboard)/knowledge-base/feedback` | Smoke |
| `admin/(dashboard)/knowledge-base/games` | Smoke |
| `admin/(dashboard)/knowledge-base/processing` | Smoke |
| `admin/(dashboard)/knowledge-base/settings` | Flow |
| `admin/(dashboard)/knowledge-base/snapshots` | Smoke |
| `admin/(dashboard)/knowledge-base/upload` | Flow |
| `admin/(dashboard)/knowledge-base/vectors` | Smoke |
| `admin/(dashboard)/knowledge-base/mechanic-extractor` | Smoke |
| `admin/(dashboard)/knowledge-base/mechanic-extractor/dashboard` | Smoke |
| `admin/(dashboard)/knowledge-base/mechanic-extractor/analyses` | Smoke |
| `admin/(dashboard)/knowledge-base/mechanic-extractor/review` | Flow |
| `admin/(dashboard)/knowledge-base/mechanic-extractor/golden` | Smoke |
| `admin/(dashboard)/knowledge-base/mechanic-extractor/golden/[gameId]` | Smoke |
| `admin/(dashboard)/rag-quality` | Smoke |

## A3 — Catalogo condiviso admin (13)

| Route | Liv. |
|-------|------|
| `admin/(dashboard)/shared-games` | Smoke |
| `admin/(dashboard)/shared-games/all` | Smoke |
| `admin/(dashboard)/shared-games/new` | Flow |
| `admin/(dashboard)/shared-games/import` | Flow |
| `admin/(dashboard)/shared-games/wizard` | Flow |
| `admin/(dashboard)/shared-games/seeding` | Flow |
| `admin/(dashboard)/shared-games/categories` | Smoke |
| `admin/(dashboard)/shared-games/[id]` | Smoke |
| `admin/(dashboard)/shared-games/[id]/rag-setup` | Flow |
| `admin/(dashboard)/shared-games/[id]/knowledge-base` | Smoke |
| `admin/(dashboard)/games/new` | Flow |
| `admin/(dashboard)/games/[gameId]/phases` | Flow |
| `admin/(dashboard)/games/[gameId]/agent/test` | Flow |
| `admin/(dashboard)/games/[gameId]/processing` | Smoke |
| `admin/(dashboard)/catalog-ingestion` | Smoke |
| `admin/(dashboard)/catalog/seed-queue` | Smoke |

> 16 righe. Titolo indicativo; glob conferma.

## A4 — Config & Sistema admin (10)

| Route | Liv. |
|-------|------|
| `admin/(dashboard)/config` | Smoke |
| `admin/(dashboard)/config/tiers` | Flow |
| `admin/(dashboard)/config/n8n` | skip (n8n in rimozione) |
| `admin/(dashboard)/content` | Smoke |
| `admin/(dashboard)/content/email-templates` | Flow |
| `admin/(dashboard)/ai` | Smoke |
| `admin/(dashboard)/providers` | Smoke |
| `admin/(dashboard)/providers/[name]` | Flow |
| `admin/(dashboard)/staging-access` | Flow |
| `admin/(dashboard)/business` | Smoke |
| `admin/database-sync` | Flow |

> 11 righe. `database-sync` è fuori dal gruppo `(dashboard)` ma resta A4.

## A5 — Monitoraggio & Utenti admin (23)

| Route | Liv. |
|-------|------|
| `admin/page.tsx` (redirect/landing) | Smoke |
| `admin/(dashboard)/overview` | Smoke |
| `admin/(dashboard)/overview/activity` | Smoke |
| `admin/(dashboard)/overview/system` | Smoke |
| `admin/(dashboard)/monitor` | Smoke |
| `admin/(dashboard)/monitor/grafana` | Smoke |
| `admin/(dashboard)/monitor/mau` | Smoke |
| `admin/(dashboard)/monitor/logs` | Smoke |
| `admin/(dashboard)/monitor/services` | Smoke |
| `admin/(dashboard)/monitor/service-calls` | Smoke |
| `admin/(dashboard)/monitor/operations` | Smoke |
| `admin/(dashboard)/monitor/containers` | Smoke |
| `admin/(dashboard)/monitor/wikidata-dead-letters` | Smoke |
| `admin/(dashboard)/analytics` | Smoke |
| `admin/(dashboard)/business` → vedi A4 | — |
| `admin/(dashboard)/users` | Smoke |
| `admin/(dashboard)/users/[id]` | Smoke |
| `admin/(dashboard)/users/activity` | Smoke |
| `admin/(dashboard)/users/access-requests` | Flow |
| `admin/(dashboard)/users/invitations` | Flow |
| `admin/(dashboard)/users/roles` | Flow |
| `admin/(dashboard)/notifications/compose` | Flow |
| `admin/(dashboard)/ui-library` | Smoke |
| `admin/(dashboard)/ui-library/[id]` | Smoke |
| `admin/(dashboard)/ui-library/compositions` | Smoke |
| `admin/(dashboard)/ui-library/compositions/[id]` | Smoke |

## Skip — non user-facing / dev fixtures

| Route | Motivo |
|-------|--------|
| `(public)/dev/meeple-card` | Dev fixture (showcase componente), non superficie utente |

## Riepilogo conteggi

| Area | Route |
|------|-------|
| U1 | 25 |
| U2 | 11 |
| U3 | 18 |
| U4 | 14 |
| U5 | 6 (+1 `join/event` catalogato in U6) |
| U6 | 23 |
| U7 | 16 |
| U8 | 7 |
| A1 | 25 |
| A2 | 20 |
| A3 | 16 |
| A4 | 11 |
| A5 | 25 |
| Skip | 1 |
| **Totale** | **~218 mappate + 1 skip** (glob riferimento: 220-221; A-FINAL riconcilia eventuali delta) |

> Se A-FINAL trova una route non presente qui, la aggiunge all'area competente e nota il delta. Nessuna route deve restare fuori sia da questa mappa sia dalle matrici dei cataloghi.

## Riconciliazione A-FINAL (2026-07-10)

Verifica anti-buchi eseguita: **220** `page.tsx` reali (glob), ripartite `(auth) 10 · (authenticated) 84 · (chat) 4 · (public) 23 · admin 97 · **top-level 2**`. Le sezioni U/A sopra coprono i primi 5 gruppi al 100% (confermato per-area dai subagenti). La guardia ha rilevato **2 route top-level fuori dai route-group**, assenti nei briefing per-area, ora riconciliate:

| Route | Assegnata a | Scenario | Note |
|-------|-------------|----------|------|
| `app/join/[token]` (Guest Landing) | **U6** | U6-27 | Join ospite a sessione live via token (Game Night Improvvisata) |
| `app/offline` (PWA fallback) | **U8** | U8-13 | Pagina offline; `history.back()` se online → Smoke solo in stato offline |

**Copertura finale: 220/220** (218 in sezioni U/A + 2 top-level riconciliate; `dev/meeple-card` resta l'unico `skip` documentato, dentro le 23 `(public)`). Totale scenari corpus: **248**.
