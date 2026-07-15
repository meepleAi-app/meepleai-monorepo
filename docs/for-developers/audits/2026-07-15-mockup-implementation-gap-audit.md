# Audit gap: mockup `design_files` → implementazione

> **Scopo**: incrociare ogni mockup di `admin-mockups/design_files/` con l'implementazione reale (route + componenti), usando la [site map verificata](../frontend/site-map.md) (224 route) come sorgente di verità. Trova **pagine/componenti mancanti** e **gap di fidelity** (route esiste ma il rendering diverge dal mockup).
>
> **Metodo**: workflow di classificazione a 2 fasi (classify → **verify avversariale**) su 43 mockup non-fixture (page-mock + component-mock). Ogni "gap" è stato ri-verificato attivamente (lezione: non asserire assenza senza prova). Data: **2026-07-15**.
>
> **Companion**: confronto visivo mockup↔pagina fianco a fianco (screenshot in chat + istruzioni self-serve §5). Gap report Claude Design (prototipi handoff) restano in `2026-06-04`/`2026-06-30-sp6`/`2026-07-15-...-mobile.md` — questo audit copre i mockup `design_files/`, complementare.

## Riepilogo

| Stato | Conteggio | Significato |
|---|---:|---|
| 🔴 **gap-missing** | 10 | Nessuna route/componente implementato per il mockup |
| 🟡 **partial-fidelity** | 17 | Route/componente esiste, ma il mockup mostra feature/stati/rendering non implementati |
| 🟢 **implemented** | 16 | Route/componente esiste e rende contenuto corrispondente |
| **Totale** | **43** | mockup non-fixture classificati |

> ⚠️ **Nota chiave dal confronto visivo**: alcune route marcate `implemented`/`partial-fidelity` divergono **molto** dal mockup a runtime (vedi §5). Lo stato "implemented" significa "la route esiste e rende contenuto reale", NON "fedele al mockup".

---

## 1. 🔴 Gap-missing — pagine/componenti mancanti (10)

| Mockup | Target (route/componente) | Issue | Note |
|---|---|---|---|
| `sp4-game-detail-tab-reviews.html` | `/games/[id]/reviews` | #2148 | Variante M1 commentary friend-first citata nell'audit 2026-06-10-mockup-coverage-gap-report.md. Placeholder AI-generated forward-refactor. Gap REALE (non falso gap): la feature non |
| `sp4-game-detail-tab-strategies.html` | `/games/[id]/strategies` | #2148 | Placeholder AI-generated forward-refactor, designer review richiesta. Gap REALE (non falso gap). |
| `sp4-game-detail-tab-chat.html` | `/games/[id]/chat` | #2148 | Il commento del mockup ammette 'previously partial via sp4-game-chat-tab.html composite; this stub gives /games/[id]/chat its own canonical mockup' — la route dedicata e' aspirazio |
| `sp4-session-play.html` | `/sessions/[id]/play` | #1492 (epic #1475) | Page-mock for a route deliberately not implemented (out of scope per ADR 2026-05-31). Constituent components exist in the game-night live flow; the /play page surface is the real g |
| `sp4-session-catan-summary.html` | `/sessions/[id]/summary` | #2234 | Superficie piu vicina IMPLEMENTATA = la summary generica game-agnostica /sessions/[id] (SessionSummaryView.tsx + 11 componenti features/session-summary): una sessione Catan complet |
| `sp4-session-codenames-summary.html` | `/sessions/[id]/summary` | #2234 | Nearest impl = summary generica /sessions/[id] (game-agnostica). Stub Storybook skeleton-first (#2231/DS-17-15); flavor Codenames deferred a Phase C-3; tracking mockup #2234. |
| `sp4-session-paleo-summary.html` | `/sessions/[id]/summary` | #2234 | Nearest impl = summary generica /sessions/[id] (game-agnostica). Stub Storybook skeleton-first (#2231); flavor per-gioco deferred Phase C-3; tracking mockup #2234. |
| `sp4-session-power-grid-summary.html` | `/sessions/[id]/summary` | #2234 | Nearest impl = summary generica /sessions/[id] (game-agnostica). Stub Storybook skeleton-first (#2231); flavor per-gioco deferred Phase C-3; tracking mockup #2234. |
| `sp4-session-puerto-rico-summary.html` | `/sessions/[id]/summary` | #2234 | Nearest impl = summary generica /sessions/[id] (game-agnostica). Stub Storybook skeleton-first (#2231); flavor per-gioco deferred Phase C-3; tracking mockup #2234. |
| `sp4-session-zombicide-summary.html` | `/sessions/[id]/summary` | #2234 | Nearest impl = summary generica /sessions/[id] (game-agnostica). Stub Storybook skeleton-first (#2231); flavor per-gioco deferred Phase C-3; tracking mockup #2234. |

---

## 2. 🟡 Partial-fidelity — route esiste, rendering diverge (17)

| Mockup | Live | Gap di fidelity | Issue |
|---|---|---|---|
| `sp4-editor-index.html` | `/editor` | Mockup pattern = split-view 'atom-list editor 60% + PDF preview 40%'; live is a rich-text/JSON RuleSpec editor with a RuleSpecPreview panel that renders atoms as a table — no PDF source-preview pane.; Mockup editing surf | #1489 |
| `sp4-editor-proposals-new.html` | `/editor/agent-proposals/create` | Mockup = full-page 5-section accordion create form (default-empty/partial-filled/all-valid/validation-errors/saving/draft-saved/submitting/submit-success).; Live create/page.tsx renders a deliberate 'Feature Removed' stu | #1489 |
| `sp4-editor-proposals-edit.html` | `/editor/agent-proposals/[id]/edit` | Mockup = full edit form (5 sections) + status-variant header/banner (Draft/Pending/Approved/Rejected) + read-only mode + Revisions diff (section 6) + Audit trail (section 7).; Live [id]/edit/page.tsx renders a 'Feature R | #1489 |
| `sp4-editor-proposals-test.html` | `/editor/agent-proposals/[id]/test` | Mockup = asymmetric split-view (config sidebar sx 320px + chat body dx) + optional trace drawer (380px) + streaming FSM 4-states (idle/streaming/completed/error, blinking cursor) + compare-mode.; Live TestSandboxClient i | #1489 |
| `sp4-toolkit-templates.html` | `/toolkit/templates` | Gallery + category filter (All/Strategy/Party/CardGames/Cooperative) + tool-count badges are implemented.; Clone CTA is the primary mockup action but live 'Use This Template' button is disabled with title 'Coming soon' ( | #1490 |
| `sp4-dashboard.html` | `/dashboard` | Mockup is the Pre-Stage-3 forward-design with 5 entity sections (Games/Players/Agents/Sessions/Events) — flagged design_intent 'forward-refactor-obsolete', obsolete_tracking_issue #2114.; Live DashboardClient is the Asse | #2114 |
| `sp4-session-catan-live.html` | `/sessions/[id]/live` | Game-specific Catan board NOT implemented: HexBoard (19-hex), RobberOverlay, DiceDisplay (2D6+roll history), ResourceHandBar, TradePanel, DevCardsPanel, BuildPanel — only a placeholder stub apps/web/src/app/(authenticate | #2234 (fidelity deferral tracker; #2231 DS-17-15 skeleton-first phase) |
| `sp4-session-codenames-live.html` | `/sessions/[id]/live` | Game-specific Codenames board NOT implemented: 5x5 WordGrid, spymaster KeyCell mini-grid, team-colored tiles, RoleAvatar/RoleTag (spymaster/operative), ClueChip 'WORD:N', GuessPips, TimerChip, ClueOutcome — only placehol | #2234 (fidelity deferral tracker; #2231 skeleton-first phase) |
| `sp4-session-paleo-live.html` | `/sessions/[id]/live` | Game-specific Paleo co-op flavor NOT implemented (deferred Phase C-3): only placeholder stub apps/web/src/app/(authenticated)/sessions/_sp4-stubs/paleo.stories.tsx.; Paleo exercises BinaryWin (collective goal/fail meters | #2234 (fidelity deferral tracker; #2231 skeleton-first phase) |
| `sp4-session-power-grid-live.html` | `/sessions/[id]/live` | Game-specific Power Grid flavor NOT implemented (deferred Phase C-3): only placeholder stub apps/web/src/app/(authenticated)/sessions/_sp4-stubs/power-grid.stories.tsx.; Power Grid exercises Ranking/Points scoring + phas | #2234 (fidelity deferral tracker; #2231 skeleton-first phase) |
| `sp4-session-puerto-rico-live.html` | `/sessions/[id]/live` | Game-specific Puerto Rico flavor NOT implemented (deferred Phase C-3): only placeholder stub apps/web/src/app/(authenticated)/sessions/_sp4-stubs/puerto-rico.stories.tsx.; Puerto Rico exercises role-selection/Points scor | #2234 (fidelity deferral tracker; #2231 skeleton-first phase) |
| `sp4-session-wingspan-live.html` | `/sessions/[id]/live` | 'sessions consolidation' demo: mockup shows 3-col layout (PlayerRosterLive 300 · scoring+log · RightColumnTabs 380) with 7 tabs scores/players/agent/chat/photos/tools/notes. SHIPPED layout is the later G1 #2374 2-col 60/ | #2234 (fidelity deferral tracker; #2231 skeleton-first phase; #2374 G1 layout) |
| `sp4-session-zombicide-live.html` | `/sessions/[id]/live` | Game-specific Zombicide co-op flavor NOT implemented (deferred Phase C-3): only placeholder stub apps/web/src/app/(authenticated)/sessions/_sp4-stubs/zombicide.stories.tsx.; Zombicide exercises BinaryWin/Objectives scori | #2234 (fidelity deferral tracker; #2231 skeleton-first phase) |
| `sp4-session-wingspan-summary.html` | `/sessions/[id]` | Il mockup e' la versione EXTENDED che AGGIUNGE un RightColumnTabs (rail review sticky con 3 tab: Scoreboard \| Note \| Player) che consolida le sub-route scoreboard/notes/players — NON implementato: la live SessionSummar | #2234 |
| `librogame-runthrough-error-states.html` | Librogame error-banner system (10 error states: OCR/AI stream/network/quota) | No unified extracted error-banner component exists; the 10-state banner pattern is only a Storybook presentational mock (ErrorStatesMock.tsx, Template K); Error conditions ARE handled but scattered across the play shell: | #2174 |
| `librogame-runthrough-glossary-editor.html` | `GlossaryEditorModal.tsx` | Base states implemented (verified by reading source): edit/idle (default), saving, error (save-error), collision (#1312) + entry.contexts read-only provenance list (#2638/SI-7) + desktop collision side-panel (#1312 AC-6) | #952 |
| `primitive-nav-chat-panel.html` | `ChatSlideOverPanel.tsx` | Slide-over primitive itself implemented: ChatSlideOverPanel.tsx + ChatPanelHeader.tsx + hooks/useChatPanel.ts + lib/stores/chat-panel-store.ts (the PR #344 base the mockup references); Mockup's D7 global thread-picker st | #2321 |

---

## 3. 🟢 Implemented (16)

| Mockup | Live | Componente/nota |
|---|---|---|
| `sp4-editor-proposals-index.html` | `/editor/agent-proposals` | ProposalsList.tsx |
| `sp4-toolkit-history.html` | `/toolkit/history` | client.tsx |
| `sp4-toolkit-play.html` | `/toolkit/play` | page.tsx |
| `sp4-toolkit-stats.html` | `/toolkit/stats` | client.tsx |
| `sp4-library-wishlist.html` | `/library/wishlist` | page.tsx |
| `sp4-game-detail-tab-rules.html` | `/games/[id]/rules` | page.tsx |
| `sp4-game-detail-tab-faqs.html` | `/games/[id]/faqs` | page.tsx |
| `sp4-add-game-drawer.html` | `AddGameDrawer.tsx` | AddGameDrawer.tsx |
| `sp4-citation-pdf-viewer.html` | `CitationPdfTab.tsx` | CitationPdfTab.tsx |
| `sp4-game-chat-tab.html` | `GameChatTab.tsx` | GameChatTab.tsx |
| `librogame-runthrough-library-search.html` | `/gamebook` | GamebookIndexView.tsx |
| `librogame-runthrough-quota-credits.html` | `CheckoutModal.tsx` | CheckoutModal.tsx |
| `primitive-nav-topbar.html` | `AppTopBar.tsx` | AppTopBar.tsx |
| `primitive-nav-bottom-mobile.html` | `MobileBottomBar.tsx` | MobileBottomBar.tsx |
| `sp7-game-night-transition.html` | `GameTransitionDialog.tsx` | GameTransitionDialog.tsx |
| `notifications.html` | `/notifications` | page.tsx |

---

## 4. Indice di confronto self-serve (mockup ↔ live)

> Sei loggato su `localhost:3000`. Servi i mockup con `node` su `localhost:8799` (statici, con companion JS/CSS):
>
> ```bash
> cd admin-mockups/design_files && python -m http.server 8799   # o node static server
> ```
>
> Poi apri le due colonne affiancate. `[id]`/`[gameId]` → sostituisci con un id reale (es. da `/library` o `/games`).

| Stato | Mockup (`:8799/...`) | Live (`:3000/...`) |
|---|---|---|
| 🔴 | `sp4-game-detail-tab-chat.html` | — |
| 🔴 | `sp4-game-detail-tab-reviews.html` | — |
| 🔴 | `sp4-game-detail-tab-strategies.html` | — |
| 🔴 | `sp4-session-catan-summary.html` | — |
| 🔴 | `sp4-session-codenames-summary.html` | — |
| 🔴 | `sp4-session-paleo-summary.html` | — |
| 🔴 | `sp4-session-play.html` | — |
| 🔴 | `sp4-session-power-grid-summary.html` | — |
| 🔴 | `sp4-session-puerto-rico-summary.html` | — |
| 🔴 | `sp4-session-zombicide-summary.html` | — |
| 🟡 | `librogame-runthrough-error-states.html` | (componente: Librogame error-banner system (10 error ) |
| 🟡 | `librogame-runthrough-glossary-editor.html` | (componente: GlossaryEditorModal (librogame inline gl) |
| 🟡 | `primitive-nav-chat-panel.html` | (componente: ChatSlideOverPanel (global chat slide-ov) |
| 🟡 | `sp4-dashboard.html` | `/dashboard` |
| 🟡 | `sp4-editor-index.html` | `/editor` |
| 🟡 | `sp4-editor-proposals-edit.html` | `/editor/agent-proposals/[id]/edit` |
| 🟡 | `sp4-editor-proposals-new.html` | `/editor/agent-proposals/create` |
| 🟡 | `sp4-editor-proposals-test.html` | `/editor/agent-proposals/[id]/test` |
| 🟡 | `sp4-session-catan-live.html` | `/sessions/[id]/live` |
| 🟡 | `sp4-session-codenames-live.html` | `/sessions/[id]/live` |
| 🟡 | `sp4-session-paleo-live.html` | `/sessions/[id]/live` |
| 🟡 | `sp4-session-power-grid-live.html` | `/sessions/[id]/live` |
| 🟡 | `sp4-session-puerto-rico-live.html` | `/sessions/[id]/live` |
| 🟡 | `sp4-session-wingspan-live.html` | `/sessions/[id]/live` |
| 🟡 | `sp4-session-wingspan-summary.html` | `/sessions/[id]` |
| 🟡 | `sp4-session-zombicide-live.html` | `/sessions/[id]/live` |
| 🟡 | `sp4-toolkit-templates.html` | `/toolkit/templates` |
| 🟢 | `librogame-runthrough-library-search.html` | `/gamebook` |
| 🟢 | `librogame-runthrough-quota-credits.html` | (componente: CheckoutModal (gamebook quota-credits mu) |
| 🟢 | `notifications.html` | `/notifications` |
| 🟢 | `primitive-nav-bottom-mobile.html` | (componente: MobileBottomBar (mobile bottom nav, 3+1 ) |
| 🟢 | `primitive-nav-topbar.html` | (componente: AppTopBar (desktop/tablet persistent top) |
| 🟢 | `sp4-add-game-drawer.html` | (componente: AddGameDrawer (Sheet /library?action=add) |
| 🟢 | `sp4-citation-pdf-viewer.html` | (componente: CitationModal / CitationPdfTab (Citation) |
| 🟢 | `sp4-editor-proposals-index.html` | `/editor/agent-proposals` |
| 🟢 | `sp4-game-chat-tab.html` | (componente: GameChatTab (tab aiChat: /library/[gameI) |
| 🟢 | `sp4-game-detail-tab-faqs.html` | `/games/[id]/faqs` |
| 🟢 | `sp4-game-detail-tab-rules.html` | `/games/[id]/rules` |
| 🟢 | `sp4-library-wishlist.html` | `/library/wishlist` |
| 🟢 | `sp4-toolkit-history.html` | `/toolkit/history` |
| 🟢 | `sp4-toolkit-play.html` | `/toolkit/play` |
| 🟢 | `sp4-toolkit-stats.html` | `/toolkit/stats` |
| 🟢 | `sp7-game-night-transition.html` | (componente: GameTransitionDialog (game-night game-tr) |

---

## 5. Confronto visivo — findings chiave (mockup vs live)

Screenshot affiancati catturati a runtime (`localhost:3000` autenticato vs mockup `localhost:8799`). Rivelano gap che il check route-level NON vede.

### `/toolkit/history` — 🟡 grosso gap di fidelity
- **Mockup**: pagina ricca "Storico sessioni" — tab Stats/History/Templates/Play, tabella 156 sessioni (gioco · giocatori · vincitore · score · note · azioni), paginazione, export CSV, variante mobile cards.
- **Live**: pagina base "Session History" (etichette in **INGLESE**), solo card Filtri (Game/Start/End/Reset) + empty-state "No sessions found" + "Start Your First Session". Nessuna tabella, nessun tab, nessun export.
- → Fidelity gap sostanziale **+ probabile gap i18n** (label EN su app IT).

### `/library/wishlist` — 🟡 gap di esperienza (redirect)
- **Mockup**: pagina wishlist dedicata — card gioco con priorità (Alta/Media/Bassa), target prezzo, note, Modifica/Rimuovi, filtri per priorità, spesa stimata.
- **Live**: `/library/wishlist` **redirige a `/library?tab=wishlist`** → mostra l'hub Libreria (tab Tutti/Giochi/Agenti/KB/Sessioni/Chat) **senza** un tab/vista Wishlist. L'esperienza wishlist del mockup non è presente.

### `/games/[id]/reviews` — 🔴 pagina mancante
- **Mockup**: "Recensioni" — recensioni "Dai tuoi amici" (rating a stelle + partite giocate) e "Dalla community". Header: "AI-generated placeholder (#2148) · Designer review required".
- **Live**: route inesistente. Esiste solo lo stub `GameCommunityTab` ("Reviews & Ratings Coming Soon") **escluso dall'alpha (M5)**. Idem `/games/[id]/strategies`; `/games/[id]/chat` esiste solo come tab (`agents`), non come route standalone.

### `/dashboard` — 🟡 mockup superato per design
- **Mockup**: "PRE-STAGE-3 · DASHBOARD (FORWARD-DESIGN)" — 5 sezioni entity (Games/Players/Agents/Sessions/Events) + hero 4 KPI. Il mockup stesso dichiara "Diverge da DashboardClient.tsx PR #309".
- **Live**: design **Asse C priority-driven** (#1898) — sezioni Prossimi / Cosa fanno i tuoi amici. Mockup intenzionalmente obsoleto (tracking **#2114**).



---

## 6. Metodologia & limiti

- **Classificazione**: 5 cluster × (classify + verify avversariale) = 10 agenti (1 verify caduto per errore di connessione — `editor-toolkit-library`, classify valido). 27 gap/partial tutti ri-verificati contro `routes.json` + Grep codebase.
- **Fidelity**: lo stato route-level non cattura le divergenze di rendering. Il confronto visivo (§5) rivela che diverse route "implemented" sono lontane dal mockup (es. `/toolkit/history`, `/library/wishlist`).
- **Non catturabili live**: le 14 demo-partita (`/sessions/[id]/live` + `/sessions/[id]`) richiedono una sessione reale con dati (0 sessioni presenti); il rendering game-specific (HexBoard, WordGrid…) esiste solo come stub Storybook (deferred **#2234**, Phase C-3).
- **Sorgente**: mockup `admin-mockups/design_files/` + codebase `apps/web/src` @ 2026-07-15.

_Generato via /sc:spec-panel — workflow classify+verify (43 mockup). Companion della [site map](../frontend/site-map.md)._
