# Reskin Verification Findings — 2026-06-07

**Scope**: #1816 sess 45 + #1895 (Asse A/B/C/D) + epic #1475 — verifica manuale visual + functional pagine user-side reskin.

**Setup**:
- Local web + api Docker: `localhost:3000` + `localhost:8080`
- Staging services via SSH tunnel: postgres `:25432`, redis `:26379`, embedding `:18000`, reranker `:18003`, orchestrator `:18004`
- Mockup server: `http://localhost:8765` (Python http.server in `admin-mockups/design_files/`)
- Branch: `feature/issue-1816-p3-7-phase2-kb-indexing` (5 PR opened sessione 45 ancora NOT merged in main-dev)
- Test users: `admin@meepleai.app` (superadmin, 2FA off) + `badsworm@alice.it` (regular user, email NOT verified, registered post test)

## Page-by-page findings

### Pagina 1 — `/login` 2FA flow (PR #1963 P3-4)
- **Status**: SKIP (admin user non ha 2FA attivo, flow specifico richiede user separato con 2FA setup)
- **F1** *(non-bug, intentional)*: registration auto-login senza email verification. User in grace period; backend blocca solo azioni protette. Best practice: aggiungere banner "Verifica la tua email" in UI per chiarezza.

### Pagina 2 — `/library` + AddGameDrawer (PR #1963 P3-5)
- **Status**: drawer aperto, mockup `sp4-library-desktop.jsx` rivisto
- **F2** 🔴 *(CRITICAL — live app viola ADR-059)*: `sp4-library-desktop.jsx` contiene button "↓ Importa BGG". **Live app HA il button "Importa BGG" in `/library` page TOP-RIGHT** anche per user regolari (verified con `badsworm@alice.it` user). Viola **ADR-059** (Catalog Seed Legal Posture, accepted 2026-06-05): BGG admin-only post ToS compliance. **Original assumption SBAGLIATA**: solo `AddGameDrawer.tsx` rimuove BGG, ma library hero buttons ancora lo espongono. **Scope fix**: rimuovere button da `_content.tsx` / `LibraryHero` / `LibraryHub` (verifica componente sorgente). Screenshot: `claudedocs/screenshots-reskin/p02-library-badsworm.png`.
- **F2.1** *(spec-panel critique 8 findings)*: AddGameDrawer + CatalogSearchStep necessitano rebuild grafica + funzionalità. Mockup nuovo `sp4-add-game-drawer.{html,jsx}` in design fase via Claude Design web project "MeepleAI Demo & Gap Audit".
  - C1: `CatalogSearchStep.tsx` ha hardcoded EN ("No image", "In library")
  - M1: empty state catalog mancante
  - M2: error feedback "already in library" debole
  - M3: UX copy choice cards
  - M4: a11y audit drawer
  - N1: toast success post-add
  - N2: loading skeleton
- **F2.2** ✅ *(mockup designed)*: `sp4-add-game-drawer.{html,jsx}` shipped 2026-06-07 19:17 via Claude Design web project "MeepleAI Demo & Gap Audit". 834 righe JSX coverage 8/8 findings + ADR-059 compliance esplicita (mock catalog Wikidata facts-only). Spec-panel review: tutti i fix indirizzati, T object i18n single source pattern grep-friendly, useFocusTrap hook custom. **Implementation plan**: issue umbrella con 8 sub-task (T1-T8) — vedi sintesi nel tracker.

### Pagina 3 — `/library/[gameId]` (PR #1968 P2-2)
- **Status**: gap MASSIVO confermato via screenshot `_gamepage.png`
- **F3** *(critical gap, scope multi-PR)*: layout completo da rebuild. Mockup ref `sp4-game-detail.jsx` (1163+ righe) mostra:
  - Hero illustrazione + breadcrumb game-name
  - Meta strip (designer · anno · durata · players · complessità · rating ★)
  - 6 tab (Agente · Documenti · Toolkit · Partite · Recensioni · Dischi)
  - Sezioni: Descrizione · Specifiche · House rules · Documenti · Chat agent inline · Sessions storiche
- App attuale ha: h1 "Gioco" (PR #1968 fixa) + 4 tab base + body verde lime monolitico
- **Scope estimate**: ~3-4 settimane multi-PR (sub-issue per ogni sezione)

### Pagina 4 — `/library/[gameId]/kb` (PR #1971 P3-7)
Screenshot ref: `_kbpage.png`
- **F8** ✅ *(PR #1971 shipped, funziona)*: badge "⏳ Indicizzazione in corso" + descrizione visibili in entrambe le sezioni (HubDefault banner + KbStatsCard sidebar). 3-state machine OK.
- **F4** 🔴 *(bug)*: game title mostra **UUID** `cc1678e8...` invece di "Catan". `KbHubContent.tsx:132`: `const gameTitle = status?.gameId ?? gameId;` — fallback all'UUID quando il BE non passa il game name. Stesso pattern di F3 (h1 "Gioco" generic) in altro spot. **Fix scope**: leggere il nome dal contesto layout (useLibraryGameDetail già fetcha l'oggetto game in PR #1968) o estendere BE `UserGameKbStatusDto` per includere `gameTitle`.
- **F5** 🟡 *(visual)*: stats strip mockup tag-style mono "4 DOC · 1247 CHUNK · 4891 EMBED · ULTIMA IDX 3 GG FA · COPERTURA: STANDARD". Live: dot-separated text monolitico, meno scannabile.
- **F6** 🟡 *(functional)*: PDF row mockup mostra status badges colorati per indexing state per-PDF (Ready verde · Outdated giallo · Failed rosso). Live: solo nome+size+data+"Apri dettaglio" — niente status per riga. P83 deferred (BE schema doesn't expose per-doc status).
- **F7** 🟡 *(UX redundancy)*: stats section **duplicata**: live renderizza HubDefault stats strip header + KbStatsCard sotto come card separato. Mockup ha solo stats strip nella header (KbStatsCard è deprecato implicitamente). Decisione: rimuovere KbStatsCard o farlo collapse/expand.
- **F9** 🟢 *(polish)*: sparkline "Consumo token · ultimi 7 gg" renderizza anche con 0 data → UI noise. Nascondere se `costHistory == undefined`.
- **F10** 🟢 *(UX)*: mockup ha bottom drop zone CTA "Drop a PDF file or click to upload". Live solo button "+ Carica PDF" in header — meno discovery-friendly per nuovi utenti.

### Pagina 5 — `/chat/[threadId]?gameId=` (PR #1962 + PR #1963 P3-8)
Screenshot ref: `_chatpage.png` — thread vuoto badsworm `1b1d0842-...` su Catan
- **F11** 🟡 *(layout gap, scope nuova issue)*: mockup `chat-fullscreen.html` prevede 3-col desktop (thread list sx 280px + chat center + **agent info sidebar 260px dx** con sources/actions/agent meta). App attuale: 2-col senza agent info pane dx.
- **F12** 🟡 *(UX, scope nuova issue)*: empty state app "Inizia la conversazione" generico. Mockup `/chat/new` ha **4 quick-starter cards** per agent type (Mostra setup · Spiega regola X · Genera scenario · Mostra statistiche).
- **F13** 🟡 *(feature gap)*: citations pill click → `sp4-citation-pdf-viewer.html` overlay (mockup). App: citations inline no overlay.
- **F14** 🟢 *(polish)*: reader mode toggle 16pt→24pt 📖 icon mancante.
- **F15** 🟢 *(polish)*: wake-lock badge 🔆 durante streaming mancante.
- **F16** ⚠️ *(branch mismatch, atteso)*: entity nav bar "Game" vs "Gioco" — branch corrente `feature/issue-1816-p3-7-phase2-kb-indexing` **NON include PR #1963 P3-8** → live mostra ancora EN. Fix arriva con merge PR #1963.
- **F17** ⚠️ *(branch mismatch, atteso)*: testid mobile `data-testid="message-input"` — branch corrente NON include PR #1962 → atteso assente. Fix arriva con merge PR #1962.
- **F1.1** ✅: badsworm (emailVerified=false) HA POTUTO creare thread → conferma grace period esteso a chat creation. Solo azioni privilegiate bloccate dal verify gate.

### Cross-cutting findings (shell-level)

- **F18** ✅ *(SHIPPED — PR [#1978](https://github.com/meepleAi-app/meepleai-monorepo/pull/1978))*: navigation duplicata cross-page fixed. `MainSidebar` mount removed from `DesktopShell.tsx`. Solo topbar `AppTopBar` come nav primaria desktop. Mobile flow `MobileTopBar` + `SideDrawer` + `MobileBottomBar` unchanged. Closes #1977.
  - Verified post-fix via Playwright MCP: `/library`, `/notifications`, `/profile`, `/toolkit`, `/game-nights/new`, `/sessions/new` → solo topbar visibile, no sidebar.
  - Screenshot proof: `claudedocs/screenshots-reskin/p02-library-post-fix-f18.png`

- **F19** 🟡 *(layout cross-page)*: la pagina **non occupa 100% orizzontale** del viewport — whitespace bianco ai lati su viewport ≥1440px. Probabile `max-w-*` su wrapper invece di `flex-1` + `w-full`. Verificare `DesktopShell.tsx` main container.
  - **Impatto**: ogni route `(authenticated)/*` rendered desktop.
  - **Scope fix**: rimuovere `max-w-7xl` (o simile) dal main content wrapper; assumere `flex-1 w-full` con padding interno controllato.

### Pagina 6 — `/dashboard` (#1895 Asse C)
Screenshot: `screenshots-reskin/p06-dashboard-{live-v3,mockup-v2}.png`
- ✅ Hero "Buonasera, {name}", 4 KPI cards, 3 sezioni Asse C (Prossimi/Potresti giocare/Cosa fanno i tuoi)
- ⚠️ **F20**: Recenti completati section non visibile — atteso (BE endpoint non wired per Asse C P2)
- ⚠️ F18 + F19 cross-cutting

### Pagina 7 — `/games` (#1895 Asse D P2)
Screenshot: `screenshots-reskin/p07-games-{live-v2,mockup}.png`
- ✅ Asse D P2 page.tsx renderizza DiscoverHub come default tab
- ⚠️ **F23a**: mini-nav 4 tabs hub (Discover/Catalogo/Trending/Community) NON visibili nel live render
- ⚠️ **F23b**: navigation highlight inconsistente (Library active anche su /games)
- ⚠️ **F23c**: initial navigate redirect a `/library?tab=public` — Turbopack stale chunk (richiede hard refresh)
- 📌 Mockup `sp4-hub-games.html` rappresenta sub-tab "Catalogo" (Coming Soon nel live), non il default Discover

### Pagina 8 — `/onboarding` (#1895 Asse D P3)
Screenshot: `screenshots-reskin/p08-onboarding-{live-v2}.png`
- 🔴 **F25 CRITICAL i18n**: wizard completamente in INGLESE ("What Do You Enjoy?", interest names Strategy/Party/..., "Skip/Cancel/Next", "Step 1 of 3"). Strutturale Asse D P3 + WizardModal Asse B mounted, ma strings NON localizzate
- 🟡 **F26**: "Miniatures" card e button bar (Cancel/Skip/Next) si OVERLAPPANO (z-index/positioning bug)
- 🟡 **F27**: 2 skip buttons confusi (big orange + link gray)
- ⚠️ F18 cross-cutting

### Pagina 9 — `/game-nights` (#1895 Asse A)
Screenshot: `screenshots-reskin/p09-game-nights-{live-v3,mockup}.png`
- ✅ Header IT, calendar/list toggle, filter pills (Tutte/Organizzo/Invitato/Concluse)
- ✅ Empty state "Nessuna serata in programma" + CTA "+ Crea la prima serata"
- ⚠️ F18 cross-cutting

### Pagina 10 — `/discover` (epic #1475)
Screenshot: `screenshots-reskin/p10-discover-{live,mockup}.png`
- ✅ Hero "Scopri la community" + filter pills + 7 sezioni (Trending/Giochi nuovi/Agenti/Toolkit/KB/Top contributor/Eventi)
- ✅ Empty states OK
- ✅ Tutto IT
- 📌 Topbar/sidebar minimal — page in `(authenticated)` ma shell adaptive

### Pagina 11 — `/players` (epic #1475)
Screenshot: `screenshots-reskin/p11-players-live.png`
- 🟡 **F28**: `/players` mostra "Le tue partite" = play-records UI. Routing confusion — `/players` deve essere players list, NON play-records
- ⚠️ Verifica `apps/web/src/app/(authenticated)/players/page.tsx` content

### Pagina 12 — `/agents` (epic #1475)
Screenshot: `screenshots-reskin/p12-agents-live.png`
- ✅ Header "Studio agenti" + 4 KPI (Attivi/In archivio/Articolari/Installazioni totali) + filter pills + search + button "+ Crea agente"
- ✅ Empty state grid placeholders
- ✅ Tutto IT

### Pagina 13 — `/toolkits` (epic #1475)
Screenshot: `screenshots-reskin/p13-toolkits-live.png`
- ✅ Header "Catalogo toolkit community" + filter pills + grid 12 cards skeleton
- ⚠️ **F31**: 12 console errors rilevati (dev mode) — probabile data fetch fail

### Pagina 14 — `/play-records` (epic #1475)
Screenshot: `screenshots-reskin/p14-play-records-live.png`
- ⚠️ **F29**: page mostra solo header + tabs + sticky bottom CTA "Registra partita". Centro area COMPLETAMENTE VUOTO (manca empty state illustration/messaggio)
- ✅ Sticky bottom orange gradient CTA OK

### Pagina 15 — `/library/wishlist` (epic #1475)
Screenshot: `screenshots-reskin/p15-wishlist-live.png`
- ⚠️ Redirect → `/library?tab=wishlist` (per next.config.js)
- 🟡 **F30**: page stuck su "Verifica autorizzazioni..." (auth gate hangs). Same pattern di /onboarding e /game-nights iniziale → hard refresh fixes

### Pagine Round 4 — verifica post-fix F18

#### Pagina 17 — `/notifications` (epic #1475)
- ✅ F18 confirmed fix: solo topbar, no sidebar
- ✅ Hero "Notifiche" + filter pills (Tutte/Non lette/Sessioni/Agenti/Sistema/Visione)
- ✅ Empty state bell icon + "Nessuna notifica"

#### Pagina 18 — `/profile` (epic #1475)
- ✅ F18 confirmed
- ✅ Hero card user + email + role badge + button Modifica
- ✅ Tabs Overview/Achievements/Activity
- ✅ Sections Library Stats (6 KPI) / Ultime partite (empty) / Quick Actions (3 list)

#### Pagina 19 — `/toolkit` (epic #1475)
- ✅ F18 confirmed
- ✅ Empty state "Toolkit in arrivo" + descrizione IT (coming soon)

#### Pagina 20 — `/game-nights/new` (#1895 Asse A wizard)
- ✅ F18 confirmed
- ✅ Wizard "Nuova serata" 4-step stepper
- ✅ Form data e ora picker
- ✅ Right sidebar "Anteprima invito"
- ✅ Tutto IT

#### Pagina 21 — `/sessions/new` (#1895 Asse A)
- ✅ F18 confirmed
- ✅ Hero "Sessioni" + tabs Attive/Storico
- ✅ Card session type selector
- ✅ Stepper 4-dots + "Scegli il gioco" step
- ✅ Tutto IT

#### Pagina 22 — `/library/[gameId]/agent` tab (epic #1475)
- ⚠️ Redirect → `/library/[gameId]?tab=aiChat`
- ⚠️ Catan NOT in admin library → "Gioco non trovato" card. **F4.1**: h1 in topbar mostra ancora "Gioco" generic (PR #1968 fix non gestisce edge case "game-not-in-library" — fallback wrong)
- ✅ F18 confirmed

#### Pagina 23 — `/profile/achievements` (epic #1475)
- ✅ Redirect → `/profile?tab=achievements`
- ✅ Tab system OK, scroll fluido

#### Pagina 24 — `/game-nights/[id]` (#1895 Asse A, seed)
- ✅ F18 confirmed
- ✅ Hero card status "Bozza" + title + date format IT ("martedì 9 giugno 2026 alle 20:32") + location icon
- ✅ Buttons "Modifica / Pubblica"
- ✅ Tabs sub-nav "Sul tavolo (0) / Giocatori (0)"
- ✅ Section "Suggerimenti AI" card con badge "NEW"
- ✅ Tutto IT

#### Pagina 25 — `/sessions/live/[id]/scores` (#1895 Asse D P1 polymorphic editor, seed)
- ✅ F18 confirmed
- 🟡 **F32** *(typo IT)*: h1 "Partito" — dovrebbe essere "Partita" (Partito = "departed")
- 🟡 **F33** *(data sync)*: empty state "Nessun giocatore ancora registrato" anche se BE session ha 2 players (Admin + Bob). Possibile data fetch issue o BE wire incomplete su `/sessions/live/[id]` route
- ✅ Tabs: Partita / Chat AI / Punteggi (active) / Foto / Giocatori
- ✅ Bottom CTA "Nuovo Round"
- 📌 Asse D P1 Polymorphic ScoreEditor non testato direttamente perché session ha scoringType default `Points` e admin non è host → legacy ScoreBoard rendered (backward-compat path)

#### Pagina 26 — `/library/[gameId]/play` libro game (SP6 Phase A/B)
- ✅ F18 confirmed
- ✅ Page title custom "Riprendi campagna · Libro game"
- ⚠️ **F4.1 confirmed**: h1 ancora "Gioco" generic (Catan not in admin's library edge case)
- ✅ Tabs Dettagli/Agente/Toolkit/FAQ + button "Chat con Agente"
- ✅ Hero card "Inizia la tua prima campagna" + descrizione IT + CTA "💾 Riprendi all'ultimo paragrafo"
- ✅ Floating sticky bottom "Torna alla partita"

### Pagina 16 — `/knowledge-base/global` (epic #1475)
Screenshot: `screenshots-reskin/p16-kb-globale-live.png`
- ✅ Search bar + filter pills (Modalità di ricerca / Semantica) + section "Documenti recenti" empty
- ✅ Empty state "Nessun documento ancora / Carica un PDF dalla libreria per iniziare"
- ✅ Tutto IT
- ⚠️ F18 cross-cutting

---

## 🎯 Summary — proposed umbrella issue structure

**Title**: `feat(reskin): user-facing pages SP4 mockup conformance audit follow-ups — 2026-06-07`

**Tracker**: `claudedocs/2026-06-07-reskin-verification.md` (questo file)

**Findings totali**: 30+ (8 PR-related + 18+ page-specific + 2 cross-cutting + 1 cluster drawer rebuild)

### Sub-issues raggruppate per severity + cluster

#### 🔴 CRITICAL (legal/security/i18n)
1. **F2 — ADR-059 violation**: rimuovere "Importa BGG" button da `/library` page (esposto user-side anche per regular users). Componente sorgente: `_content.tsx` / `LibraryHero` / `LibraryHub`.
2. **F25 — `/onboarding` i18n EN**: localizzare wizard interests strings (interest names, copy, navigation buttons "Skip/Cancel/Next", "Step 1 of 3")
3. **F18 — Navigation duplicata cross-page**: rimuovere sidebar OR rimuovere topbar nav links (design source-of-truth da definire)

#### 🟡 MAJOR (UX/layout)
4. **F3 — `/library/[gameId]` full layout rebuild**: hero + meta strip + 6 tab + 5+ sezioni. Mockup `sp4-game-detail.jsx`. Scope multi-PR ~3-4 settimane.
5. **F19 — Layout `max-width` cross-page**: viewport fill 100% orizzontale (rimuovere wrapper max-w-*)
6. **F4 — KB hub UUID instead of game name**: `KbHubContent.tsx:132` fallback `status?.gameId ?? gameId`
7. **F23a — `/games` mini-nav 4 tabs missing**: verificare `useMiniNavConfig` mount
8. **F23b — Navigation highlight inconsistente**: route → active sidebar/topbar voice mapping
9. **F26 — `/onboarding` overlap card+button bar**: z-index/positioning fix
10. **F28 — `/players` shows play-records UI**: routing confusion
11. **F29 — `/play-records` empty center**: aggiungere empty state illustration
12. **F30 — `/library/wishlist` auth gate hangs**: investigate
13. **F5 — KB stats strip style**: mockup tag-style mono
14. **F6 — KB PDF row status badges**: per-PDF Ready/Outdated/Failed (P83 BE-deferred)
15. **F7 — KB stats duplicate**: HubDefault strip + KbStatsCard redundant
16. **F11 — Chat 3-col layout**: agent info sidebar 260px missing
17. **F12 — Chat empty state**: 4 quick-starter cards per agent type

#### 🟢 POLISH
18. **F1 — Banner "Verifica email"**: best practice (registration auto-login + grace period)
19. **F9 — KB sparkline 0 data**: nascondere se costHistory undefined
20. **F10 — KB bottom drop zone**: drag&drop CTA
21. **F13/F14/F15 — Chat citation overlay/reader mode/wake-lock**: P83 features
22. **F20 — Dashboard Recenti section**: BE endpoint wire (Asse C P2 follow-up)
23. **F27 — `/onboarding` skip duplicate**: single skip button
24. **F31 — `/toolkits` console errors**: investigate (dev mode noise OK?)

#### 🔵 DESIGN (mockup-track, separate)
25. **F2.1/F2.2 — AddGameDrawer rebuild**: mockup `sp4-add-game-drawer.{html,jsx}` shipped 2026-06-07. Implementation T1-T8 (i18n CatalogSearchStep, EmptyState, AlreadyInLibrary alert, choice card copy, focus trap, toast success, skeleton, decomposition)

### PR sess45 da merge per chiudere finding correlati
- PR #1962 P2-1 → chiude testid mobile (F17)
- PR #1963 P3-4/5/6/8 → chiude entity nav "Gioco" (F16) + i18n drawer (parte F2.1)
- PR #1968 P2-2 → chiude h1+title `/library/[gameId]` (sotto-finding F3, NON closure totale)
- PR #1969 P2-3 → chiude CSP staging-only (no UX impact su questo audit)
- PR #1971 P3-7 → chiude KB indexing badge (F8 ✅)

---

🤖 Generated 2026-06-07 sess 45 (estensione)

---

## Pending pages (Round 1 sess 45)
- [ ] `/library/[gameId]/kb` — mockup `sp4-kb-hub.html`
- [ ] `/chat/[threadId]` mobile — mockup `chat-fullscreen.html` + `sp4-game-chat-tab.html`

## Pending pages (Round 2 #1895 Asse C/D)
- [ ] `/dashboard` priority — `sp4-dashboard.html`
- [ ] `/games` hub multi-tab — `sp4-hub-games.html`
- [ ] `/sessions/[id]/scores` polymorphic — (Asse D P1)
- [ ] `/onboarding` 3-step wizard — (Asse D P3)
- [ ] `/game-nights` + `[id]` — `sp4-game-nights-index.html`

## Pending pages (Round 3 epic #1475)
- [ ] `/discover` — `sp4-discover.html`
- [ ] `/players` + `/players/[id]` — `sp4-players-index.html` + `sp4-player-detail.html`
- [ ] `/agents` — `sp4-hub-agents.html`
- [ ] `/toolkits` — `sp4-hub-toolkits.html`
- [ ] `/play-records` — `sp4-play-records-*.html`
- [ ] `/library/wishlist` — `sp4-library-wishlist.html`
- [ ] `/knowledge-base/global` + `[id]` — `sp4-kb-globale.html` + `sp4-kb-detail.html`

---

## Final umbrella issue (post round complete)

Title: `feat(reskin): user-facing pages SP4 mockup conformance audit follow-ups — 2026-06-07`

Structure: 1 issue umbrella + N sub-issues raggruppate per cluster impact (visual rebuild · i18n · a11y · UX polish · mockup updates).
