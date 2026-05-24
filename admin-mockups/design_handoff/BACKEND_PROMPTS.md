# Backend Prompts — Pronti per Claude Code

Prompt template pronti da incollare in Claude Code, raggruppati per area. Personalizza i percorsi (`src/...`) in base alla struttura del tuo progetto.

---

## 🏗️ Setup iniziale (una tantum)

### 0.1 — Mappa il codebase esistente

```
Esplora il codebase e dimmi:
- Struttura cartelle principali (src/features, src/components, src/api, ecc.)
- Framework e versioni (React, Next/Vite, TanStack Query/SWR, stato globale)
- Componenti UI già esistenti che hanno corrispondenza con il design system MeepleAI:
  ConnectionBar, MeepleCard, EntityChip, Drawer, Tabs, MobileBottomBar, RecentsBar
- API layer: client HTTP, generazione tipi, codice OpenAPI?
- Auth: come si autenticano le richieste? Quale provider?
- Realtime: SSE, WebSocket, Liveblocks, niente?

Non modificare nulla. Scrivi un breve report in design_handoff/CODEBASE_AUDIT.md.
```

### 0.2 — Genera tipi backend dai mock

```
Leggi design/data.js ed estrai i TypeScript types per le 9 entità:
  Game, Player, Session, Agent, KB, Chat, Event, Toolkit, Tool

Scrivili in src/types/entities.ts. Per ogni campo:
- Tipo preciso (string, number, ISO date, etc.)
- Optional vs required basato su quando i mock lo usano
- Discriminated unions dove serve (es. Session.status: 'live' | 'completed' | 'planned')

Aggiungi anche:
- HouseRule
- GameNight (con RSVP[])
- Notification
- Player.preferences
```

### 0.3 — Schema DB / API consistency

```
Confronta i types in src/types/entities.ts con lo schema DB attuale
(controlla prisma/schema.prisma, migrations, o equivalente).

Per ogni discrepanza, dimmi:
- Campo nel mock ma non nel DB → richiede migration
- Campo nel DB ma non nel mock → ok, ignorato dalla UI
- Tipo diverso → adatta il mock o il DB

Scrivi il report in design_handoff/SCHEMA_DIFF.md. Non eseguire migrations.
```

---

## 🎲 SP4 — Library & Games

### 4.1 — Game Detail

```
Implementa la pagina /games/[id] basata su design/sp4-game-detail.jsx.

Requisiti:
- Route: app/games/[id]/page.tsx (o pages/games/[id].tsx per Next pages)
- Loader: GET /api/games/{id}?include=stats,sessions(limit=5),agents,kb,houseRules
- Tabs: Info / Sessioni / Chat / Stats / Documenti (state via URL hash)
- Drawer "Aggiungi sessione" → push su cascadeStore
- Empty: se sessions[] vuoto → "Nessuna partita ancora" + CTA
- Loading: skeleton (no spinner)
- Error: retry button + log Sentry/equivalente

Stati testati:
- Game con full data (Wingspan)
- Game appena aggiunto (no sessions)
- Game errore caricamento

NON includere data-testid se non già richiesto in codebase.
```

### 4.2 — Library Desktop (con Advanced Filters Drawer)

```
Implementa /library basato su design/sp4-library-desktop.jsx.

Componenti:
- LibraryFiltersBar (chip orizzontali: Stato, Gioco, Data, Sort)
- LibraryGrid o LibraryList (toggle view)
- AdvancedFiltersDrawer (7 sezioni accordion)
- ConnectionBar in alto

API:
- GET /api/library?status=&games=&from=&to=&tags=&rating=&sort=
- Backend deve restituire { items, pagination, facets }
- facets serve per popolare i count negli accordion del drawer

Stati: default | empty (no items) | empty-search (filtri attivi nulli) | loading | error

Filtri persistenti via URL params (cosi è bookmarkable).
```

### 4.3 — Add Game wizard

```
Implementa il wizard "Aggiungi gioco" basato su design/sp4-add-game-bgg-step.jsx
+ design/sp4-add-game-pdf-dedup.jsx.

Step:
1. Search (libreria community)
2. Verifica metadata (publisher, year, players, weight)
3. Upload regolamento (PDF) — opzionale
4. Conferma + indicizza KB asincrono

API:
- GET /api/community-search?q=
- POST /api/games (creazione)
- POST /api/games/{id}/kb/upload (multipart)
- GET /api/jobs/{id} per polling indexing status

Componenti:
- StepIndicator (nuovo v2)
- UploadZone (drag-drop)
- PdfDedupModal (matching titoli simili)

Validazione: titolo required, anno YYYY, players "min-max".
```

### 4.4 — Games Index + Players Index + Sessions Index

```
Tre liste con stesso pattern. Implementa tre route:
- /games        → design/sp4-games-index.jsx
- /players      → design/sp4-players-index.jsx
- /sessions     → design/sp4-sessions-index.jsx

Astrazione consigliata: <EntityIndexPage entity="games|players|sessions" />
che gestisce filtri/sort/view-toggle. I sub-tipi forniscono il <ItemCard>.

API: GET /api/{entity}?filters=
```

---

## 📚 SP4 — Knowledge Base

### 5.1 — KB Detail per gioco

```
Implementa /games/[id]/kb basato su design/sp4-kb-detail.jsx.

Mostra: lista documenti indicizzati per il gioco, con:
- chunks count
- ultima indicizzazione
- "Reindex" button (POST /api/games/{id}/kb/reindex → async job)
- "Aggiungi documento" → riusa UploadZone

API: GET /api/games/{id}/kb
Stati: empty (no docs) | indexing (progress bar) | ready | error

Citation viewer (design/sp4-citation-pdf-viewer.jsx):
- Apre PDF al paragrafo cited da una chat answer
- Highlight rosso sul chunk
- Implementabile con react-pdf o equivalente
```

### 5.2 — KB Globale + Search SSE

```
Implementa /kb basato su design/sp4-kb-globale.jsx.

Search cross-game in tempo reale:
- POST /api/kb/search (SSE response)
- Risposte stream con citazioni incrementali
- data-testid="sse-stream" + data-testid="citation-card" già nei mock — preservali

Layout split-view:
- Sidebar sx: filtri (gioco, tipo doc, data)
- Main: input search + risultati streaming + answer

Componente nuovo da estrarre: SseStream — wrapper EventSource con auto-reconnect.
```

---

## 🤖 SP7 — AI Agents

### 6.1 — Library Game Agent (chat inline)

```
Implementa /library/games/[id]/agent basato su design/sp7-library-game-agent.jsx.

Layout:
- Mobile: fullscreen chat con header compatto + bottom input + suggested queries
- Desktop: split-view (sidebar 360px game-context | main chat 1fr)

API:
- POST /api/agents/{agentId}/query (SSE)
- GET /api/games/{gameId}/house-rules
- POST /api/games/{gameId}/house-rules (drawer)

Componenti da creare in src/components/ui/v2/:
- LibraryGameAgentShell
- HouseRuleDrawer (riuso anche SP6)
- SuggestedQueriesRow
- ConfidenceBadge (riuso anche SP6)
- ActionChip
- UserBubble + AgentBubble

Stati chat:
- empty (suggested queries + illustrazione)
- default (high-confidence response + citation)
- low-confidence (CTA "Definisci house rule")
- house-rule-applied (badge + footnote)
- network-error (retry banner)

Logica low-confidence → drawer:
- Quando confidence < 0.5, mostra CTA primary "Definisci house rule"
- Click → apre HouseRuleDrawer con originalQuestion + officialRule preset
- Submit → POST house-rule + re-emit query per ottenere new response che la applica
```

### 6.2 — Agents Index + Detail

```
Implementa /agents + /agents/[id] basati su:
- design/sp4-agents-index.jsx
- design/sp4-agent-detail.jsx

Detail page mostra:
- Performance KPI (query count, avg confidence, latency P95)
- Documenti collegati (KbDocList)
- Chat history timeline (ChatHistoryTimeline)
- Prompt editor (advanced)
- A/B test (se 2+ versions)

API:
- GET /api/agents
- GET /api/agents/{id}/performance?since=
- GET /api/agents/{id}/chat-history?page=
- PATCH /api/agents/{id} (prompt)

Componenti nuovi v2:
- ChatHistoryTimeline (cross-session)
- PerformanceKpi (riuso GameStatsKpi)
- KbDocList
```

---

## 🎯 SP4 — Sessions

### 7.1 — Session Live (durante partita)

```
Implementa /sessions/[id]/live basato su design/sp4-session-live.jsx.

Realtime:
- WebSocket: ws://api/sessions/{id}/events
- Eventi: turn, score, custom_event, chat_message, finish
- Backend deve emettere eventi mentre i player annotano

Layout:
- Mobile: tabbed fullscreen (Stats / Eventi / Chat / Timer)
- Desktop: 3-pane (timer 280px | board state 1fr | events 320px)

Componenti:
- SessionTimer (countdown se time-limited, count-up altrimenti)
- EventStream
- ChatPanel inline
- ScoreCard per giocatore

POST per annotare evento: POST /api/sessions/{id}/events
```

### 7.2 — Session Summary (post-partita)

```
Implementa /sessions/[id]/summary basato su design/sp4-session-summary.jsx.

API: GET /api/sessions/{id}?include=events,players,mvp,photos

Mostra:
- Winner banner (entity player color)
- Stats: durata, eventi totali, MVP (per metrica)
- Timeline collassabile degli eventi
- Foto upload (opzionale, multipart)
- "Condividi" → genera link pubblico + immagine OG

POST /api/sessions/{id}/share → restituisce { publicUrl, ogImageUrl }
```

---

## 🌙 SP7 — Game Nights

### 8.1 — Game Night Create wizard

```
Implementa /game-nights/new basato su design/sp7-game-night-create.jsx.

Wizard 4 step:
1. Quando (date+time picker)
2. Dove (location: home / address / venue)
3. Chi (invita player dal gruppo)
4. Cosa (preset giochi proposti)

POST /api/game-nights con tutto il payload alla fine.
Auto-save in localStorage durante navigazione step.

Stati validazione per step:
- Step 1: data nel futuro, max 1 anno
- Step 2: address valido oppure home shortcut
- Step 3: almeno 2 player invitati
- Step 4: opzionale (può essere TBD)

Componenti:
- StepIndicator (riuso v2)
- DateTimePicker (usa libreria esistente o ricrea)
- PlayerInviteList
- GameProposeList
```

### 8.2 — Game Night Detail + RSVP

```
Implementa /game-nights/[id] basato su design/sp7-game-night-detail-rsvp.jsx.

Due viste in base al ruolo dell'utente:
- HOST (Marco): vede RSVP status + può cancellare / modificare / inviare reminder
- INVITED (Davide): vede info evento + bottoni "Ci sarò / Forse / Non posso"

API:
- GET /api/game-nights/{id}
- PATCH /api/game-nights/{id}/rsvp { status: 'yes'|'maybe'|'no' }
- POST /api/game-nights/{id}/remind (host only)

Realtime aggiornamento RSVP via SSE o polling 30s.

Notifica push opzionale via Web Push API o channel esistente.
```

### 8.3 — Game Night Live + Summary

```
Implementa:
- /game-nights/[id]/live → design/sp7-game-night-live.jsx
- /game-nights/[id]/summary → design/sp7-game-night-summary.jsx

Live:
- Gestisce N tavoli paralleli (1 game night = N sessions)
- Stack di SessionLive component
- Quick-jump fra tavoli
- "Concludi serata" → calcola summary

Summary:
- Aggregato di tutte le sessioni della serata
- MVP della serata (across games)
- Photo wall (upload da chiunque ha partecipato)
- Auto-genera report per il prossimo invite
```

---

## 📖 SP6 — Librogame (gamebook AI)

### 9.1 — Librogame Index + Library Search

```
Implementa:
- /librogame → design/sp6-libro-game-index.jsx
- /librogame/search → design/librogame-runthrough-library-search.jsx

API:
- GET /api/librogame/library (la mia)
- GET /api/librogame/search?q=&lang= (community)

Differenze vs games:
- Librogame ha "campaign" invece di "session"
- ogni campaign ha state persistente (paragrafo corrente, inventario, party)
```

### 9.2 — Photo Upload + Setup wizard

```
Implementa:
- design/sp6-libro-game-photo-upload.jsx
- design/librogame-runthrough-setup-wizard.jsx

Photo upload flow:
1. Cattura foto pagina (mobile camera API o file picker)
2. Upload + OCR
3. Conferma testo estratto (editable)
4. Salva paragrafo

API:
- POST /api/librogame/{id}/photo (multipart)
- Backend chiama OCR (Tesseract, Google Vision, GPT-4V)
- Risposta { text, confidence, paragraphRef }

Setup wizard:
1. Lingua originale + traduzione
2. Numero giocatori / personaggi
3. Difficulty mode (rule strictness)
4. Inventario iniziale
```

### 9.3 — Play Session (core gamebook runtime)

```
Implementa /librogame/{id}/play basato su design/librogame-runthrough-play-session.jsx
+ design/sp6-libro-game-play-session.jsx.

Stati:
- Story tab (paragraph + chat)
- Encounter tab (combat stats + dice CTA)
- Glossary tab (inline)
- Chat overlay (low-confidence flow)

API:
- WebSocket: ws://api/librogame/{id}/play
- Eventi server: paragraph_loaded, encounter_started, dice_rolled, ...
- Eventi client: choose_path, roll_dice, ask_agent, save_state

Quota credits visibile sempre top-right (POST /api/credits/consume).

Salvataggio: auto-save ogni 30s + manual save:
POST /api/librogame/{id}/state { paragraph, inventory, characters }
```

### 9.4 — Encounter, Glossary, Translate, Quota

```
Implementa i 4 sub-flow:
- design/librogame-runthrough-encounter-cheatsheet.jsx (combat helper)
- design/librogame-runthrough-glossary-editor.jsx (term definitions)
- design/librogame-runthrough-translate-viewer.jsx (on-the-fly translate)
- design/librogame-runthrough-quota-credits.jsx (credit management)

API:
- POST /api/librogame/{id}/encounter/resolve { rolls, modifiers }
- POST /api/librogame/{id}/glossary { term, definition }
- POST /api/librogame/{id}/translate { paragraph, targetLang } (consume credits)
- GET /api/credits/balance + POST /api/credits/topup
```

### 9.5 — Resume picker + Session end + Error states

```
- design/librogame-runthrough-resume-picker.jsx → /librogame/resume
  Mostra tutte le campaign salvate, ultimo paragrafo, party state

- design/librogame-runthrough-session-end.jsx → fine sessione
  Recap, fork next session, share story

- design/librogame-runthrough-error-states.jsx → catalog di tutti gli errori
  Usalo come reference per implementare ErrorBoundary appropriati
```

---

## 🌐 SP3 — Pre-auth (vista pubblica)

### 10.1 — Public marketing pages

```
Implementa le pagine pre-login basate su:
- design/public.jsx → /
- design/sp3-how-it-works.jsx → /how-it-works
- design/sp3-faq-enhanced.jsx → /faq
- design/sp3-legal.jsx → /terms, /privacy
- design/sp3-shared-games.jsx → /games (public catalog)
- design/sp3-shared-game-detail.jsx → /games/[slug]
- design/sp3-library-public.jsx → /groups/[slug]/library

Niente auth richiesta. SEO matters:
- Server-side render (Next.js app router preferito)
- Meta tags Open Graph
- Sitemap.xml automatico
- robots.txt

API:
- GET /api/public/games (catalog community)
- GET /api/public/groups/{slug}/library
- POST /api/leads (form contatto, se presente)
```

### 10.2 — Auth flow + Invite + Join

```
Implementa:
- design/auth-flow.jsx → /login, /signup, /forgot-password, /reset-password, /verify
- design/sp3-join.jsx → /join?token=
- design/sp3-accept-invite.jsx → /invite?token=

Provider: NextAuth / Auth.js / Clerk / custom (verifica codebase)

Flow invite:
1. Host invia invite tramite UI host (POST /api/invites)
2. Invitee riceve email con link /invite?token=...
3. Pagina valida token, mostra info gruppo
4. Click "Accetta" → se non loggato → /signup poi auto-accept
                  → se loggato → POST /api/invites/{token}/accept
5. Redirect a /dashboard
```

---

## 🛠️ Sezione admin/superadmin (opzionale)

### 11.1 — Onboarding nuovo utente

```
Implementa /onboarding basato su design/onboarding.jsx.

Trigger: primo accesso post-signup (user.onboardingCompletedAt == null).

Step:
1. Welcome + scegli persona (casual/competitive/narrative)
2. Crea gruppo o join esistente
3. Aggiungi primo gioco (search community o BGG)
4. Invita amici (opzionale)
5. Tour 4 hot spot della dashboard

Marca completato con PATCH /api/user { onboardingCompletedAt: now() }.
```

### 11.2 — Settings

```
Implementa /settings basato su design/settings.jsx.

7 sezioni in sidebar:
1. Account (profilo, password, email, 2FA)
2. Gruppo (membri, ruoli, invite settings)
3. AI (default agent, confidence threshold, allowed providers)
4. Privacy (visibilità library, data sharing, retention)
5. Billing (piano corrente, history, upgrade)
6. Notifiche (preferenze email/push)
7. Developers (API keys, webhooks, exports)

API:
- GET/PATCH /api/user
- GET/PATCH /api/groups/{id}
- GET /api/billing/subscription + POST /api/billing/portal (Stripe)
- GET /api/api-keys + POST /api/api-keys + DELETE
```

### 11.3 — Notifications

```
Implementa /notifications basato su design/notifications.jsx.

API:
- GET /api/notifications?since=&category=
- PATCH /api/notifications/{id}/read
- POST /api/notifications/mark-all-read

Realtime: WebSocket o SSE per push live notif.

Componente NotificationToast riusabile (badge nella topbar che vibra)
+ NotificationDrawer (apre lista filtrata).

Categorie: RSVP, indicizzazione, agent update, billing, system.
```

---

## 🎨 Riferimenti

Quando Claude Code chiede chiarimenti su un design specifico:

```
Apri design/{nomefile}.html nel browser per vedere il design renderizzato.
Per il file source: design/{nomefile}.jsx.
Inizia leggendo il commento header del JSX per: route, US, sub-componenti v2.
```

Per il demo navigabile completo:

```
design/demo.html mostra tutti i mock con login simulato e navigazione tour.
Aprilo dal browser per esplorare i 71 step.
```

---

## ⚠️ Cosa NON delegare a Claude Code (richiede umano)

1. **Decisioni di architettura backend** (microservizi vs monolite, scelta DB)
2. **Schema migrations distruttive** (drop column, rename table)
3. **Configurazione produzione** (env vars, secrets, deploy)
4. **Code review delle PR generate** — Claude Code propone, tu approvi
5. **Decisioni di prodotto ambigue** — se il mock non chiarisce, chiedi al PM
