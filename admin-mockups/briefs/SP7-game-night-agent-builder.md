# SP7 Game Night + Agent Builder — Brief Claude Design (10 mockup, Sprint N+1 P1 backbone)

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.
> Questo brief introduce **SP7 Game Night & Agent Builder**, scope nuovo per attivare le user story P1 dormant `US-31` (game nights), `US-33` (agent browser/builder), `US-41` (notifications). Riferimento: [`docs/_archive/roadmap/user-stories-tracking.md`](../../.docs-archive/roadmap/user-stories-tracking.md).

## Stato programma

| SP | Stato | Audience | Mockup |
|----|-------|----------|--------|
| SP3 public-secondary | ✅ merged | nuovi visitatori | 8 mockup |
| SP4 entity-desktop wave 1+2+3 | ✅ merged | utenti autenticati | 14 mockup |
| SP5 admin-tools | ⏳ in design | admin/power-user | (separate brief) |
| SP6 libro-game (vision Phase 1 + Iter 1 dogfood) | ✅ 7/8 (F house-rule pending) | casual italian gamer (Sara) + dogfood (Aaron) | 7 mockup |
| **SP7 game-night + agent-builder + notifications** | **(questo brief)** | **utente authenticated power-user** | **10 mockup A-J** |

**SP7 = nuova feature P1 backbone**, NON redesign. Attiva 3 user story con backend già pronto:
- **US-31 game nights** (`GameManagement` BC, `GameNightEvent` + `RSVP` entities)
- **US-33 agent browser/builder** (`KnowledgeBase` BC, `AgentDefinition` + `AgentProposal` flow)
- **US-41 notifications** (`UserNotifications` BC, 24 cmd + 18 query)

Riusa al 100% il design system esistente (token, EntityChip/Pip, MeepleCard, ConnectionBar, Drawer, BottomBar).

## Persona target & contesto d'uso

### Persona primaria: utente authenticated, power-user italiano

**"Marco, 35, software engineer, board game group host"**. Membro fondatore di un gruppo di 6-8 amici che si vede 2-3 volte/mese. Funge da host (sceglie i giochi, organizza, prende le scopate sui play records). Conosce bene 15-20 giochi, vuole strumenti che riducano il tempo di organizzazione e amplifichino il piacere del tavolo.

**Setup tipico**: desktop a casa per pianificazione/agent setup, mobile al tavolo per RSVP/notifiche/chat agente in-game.

### Persona secondaria: Aaron (dogfood superadmin)

Già documentato in [SP6 Capitolo 2](./SP6-libro-game.md#capitolo-2--iter-1-dogfooding-extension-mockup-g--h). Per SP7 Aaron usa:
- US-33 agent builder per creare agenti per i suoi giochi
- US-31 game nights per pianificare le serate Nanolith con gli amici
- US-41 notifications per ricevere alert quando giocatori RSVP / KB indexing complete

**Mapping persona → mockup**:
- Marco è primary actor su tutti i 10 mockup SP7
- Aaron è secondary actor che valida happy case dogfood

### Job-to-be-done emozionali

> **Marco — game-night**: «Voglio organizzare la serata in 5 minuti, sapere chi viene, chi non può, e con cosa giocheremo. Non voglio chat WhatsApp di 50 messaggi per decidere».

> **Marco — agent-builder**: «Voglio creare un assistente personalizzato per Twilight Imperium che conosca le regole sul mio combat, le mie houserules, e che chieda chiarimenti se non è sicuro. Voglio testarlo prima di farlo girare al tavolo».

> **Marco — notifications**: «Voglio sapere quando Davide dice 'ci sarò' o quando l'AI ha finito di indicizzare il manuale che ho appena uploadato — ma non voglio essere bombardato».

## Scope SP7 — esattamente 10 mockup, 3 wave

| # | File | Route | US | Pattern primario |
|---|------|-------|------|------------------|
| **A** | `sp7-game-night-create.{html,jsx}` | `/game-nights/new` | US-31 | Multistep wizard 4 step (mobile) / split-form (desktop) |
| **B** | `sp7-game-night-detail-rsvp.{html,jsx}` | `/game-nights/[id]` | US-31 | Hub detail + RSVP + game voting |
| **C** | `sp7-game-night-edit.{html,jsx}` | `/game-nights/[id]/edit` | US-31 | Form edit + cancel/reschedule states |
| **D** | `sp7-agent-proposals-list.{html,jsx}` | `/editor/agent-proposals` | US-33 | Grid + filters + status badge |
| **E** | `sp7-agent-builder-create.{html,jsx}` | `/editor/agent-proposals/create` | US-33 | Multistep wizard 4 step (KB → prompt → tone → review) |
| **F** | `sp7-agent-builder-test.{html,jsx}` | `/editor/agent-proposals/[id]/test` | US-33 | Playground chat + feedback annotation |
| **G** | `sp7-agent-builder-edit.{html,jsx}` | `/editor/agent-proposals/[id]/edit` | US-33 | Form edit + version diff |
| **H** | `sp7-library-game-agent.{html,jsx}` | `/library/games/[gameId]/agent` | US-13/33 | Chat inline a livello game (full-screen mobile / split desktop) |
| **I** | `sp7-notifications-hub.{html,jsx}` | `/notifications` | US-41 | Timeline + grouping + bulk actions |
| **J** | `sp7-notifications-preferences.{html,jsx}` | `/notifications/preferences` | US-41 | Preferences form + channel settings |

**Sequence di consegna consigliata** (3 wave, generated 1-by-1 in claude.ai/design):

- **Wave 1 — Game Night Lifecycle**: A → B → C (3 mockup, ~2h totale)
- **Wave 2 — Agent Builder Flow**: D → E → F → G (4 mockup, ~3-4h)
- **Wave 3 — Agent Inline + Notifications**: H → I → J (3 mockup, ~2h)

Wave indipendenti, possono essere lavorate in qualsiasi ordine (no dependencies tra wave).

## Componenti già stabili — NON ridisegnare

Sono in produzione post-SP4. Il mockup li **istanzia** (placeholder JSX che indica il componente prod), non li clona:

| Componente | Path codice | Riuso obbligatorio in SP7 |
|------------|-------------|---------------------------|
| `MeepleCard` (grid/list/compact/featured/hero) | `apps/web/src/components/ui/data-display/meeple-card/` | A (game candidate cards), B (player RSVP cards), D (agent proposal cards) |
| `EntityChip` / `EntityPip` | `apps/web/src/components/ui/data-display/entity-chip/` | tutti i mockup, ovunque cross-reference |
| `ConnectionBar` + `ConnectionPip` | `apps/web/src/components/ui/data-display/connection-bar/ConnectionBar.tsx` | B, F, H (sopra hero/header) |
| `Drawer` (cascadeNavigationStore) | `apps/web/src/stores/cascadeNavigationStore.ts` | B (RSVP detail), F (debug panel test playground), I (notification action drawer) |
| `MobileBottomBar` + `RecentsBar` | `apps/web/src/components/ui/navigation/` | tutti i mockup mobile (shell layout) |
| `Tabs` animated underline (wave 1) | wave 1 SP4 | B (RSVP / Voting / Chat tabs), F (Test / History / Debug tabs), I (All / Unread / Mentions filter tabs) |
| `StepIndicator` | emerso da SP6-B | A (4 step game-night creation), E (4 step agent-builder wizard) |
| `useAgentChatStream` + chat-stream UI primitives | esistenti | F (playground chat), H (game agent chat) |
| `ConfirmModal` | emerso da SP6-B | C (cancel game night), G (delete agent proposal) |
| `ConfidenceBadge` | emerso da SP6-C | F (test playground answer feedback), H (chat answer confidence) |

**ConnectionBar reproduction 1:1 prod** (lezione PR #568, #569, #570): `borderRadius: 999` (`var(--r-pill)`), bg pieno = `entityHsl(type, 0.1)` non-empty, dashed + opacity 0.6 + Plus icon empty, aria-label `${label}: ${count}` non-empty / solo `${label}` empty, tabular-nums per count.

### `entityHsl` helper inline (palette 9 entity)

Replica nel mockup come funzione locale (l'impl userà `apps/web/src/lib/theme/entity-hsl.ts`):

```js
const ENTITY_HSL = {
  game:    '25 95% 45%',
  player:  '262 83% 58%',
  session: '240 60% 55%',
  agent:   '38 92% 50%',
  kb:      '174 60% 40%',
  chat:    '220 80% 55%',
  event:   '350 89% 60%',
  toolkit: '142 70% 45%',
  tool:    '195 80% 50%',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(${ENTITY_HSL[entity]} / ${alpha})` : `hsl(${ENTITY_HSL[entity]})`;
```

## Mapping SP7 → entity color

SP7 NON introduce nuovi entity type. Mapping su 9 esistenti:

| Concept SP7 | Entity | Razionale |
|---|---|---|
| Game night event | `event` (🎉 rose `350 89% 60%`) | È un evento sociale schedulato |
| RSVP risposta giocatore | `player` (🎮 violet `262 83% 58%`) | Lato player |
| Game candidato per voting | `game` (🎲 orange `25 95% 45%`) | Reference a un gioco |
| Agent proposal/draft/published | `agent` (🤖 amber `38 92% 50%`) | È un agente |
| KB linked to agent | `kb` (📄 teal `174 60% 40%`) | KB references |
| Test playground answer | `chat` (💬 blue `220 80% 55%`) | Conversazione test |
| In-game agent chat | `chat` (💬 blue) | idem |
| Notification = system event | `event` (🎉 rose) | Tutti gli alerts sono eventi |
| Notification = chat reply | `chat` (💬 blue) | Reply notification ha primary chat tone |
| Toolkit suggestion | `toolkit` (🔧 green `142 70% 45%`) | Riferimento toolkit AI |

**Status badge agent proposals** (D, E):
- 🟢 Published (`agent` amber pieno) — agente attivo, deployable
- 🟡 Draft (`agent` amber 0.4 alpha + dashed border) — in fase di edit/test
- 🔵 Testing (`session` indigo pulsing) — in fase di playground test
- ⚫ Archived (`--text-muted` grayscale) — disattivato

**Status badge game night** (A, B, C):
- 🟠 Pianificata (`event` rose pieno) — futura, RSVP aperta
- 🟢 In corso (`session` indigo pulsing) — serata oggi/ora
- 🔘 Completata (`--text-muted` grayscale + ✓ icon)
- 🔴 Cancellata (`--c-danger` rosso + strikethrough title)

**Notification badge severity** (I):
- 🔴 Critica (`event` rose) — RSVP cancel last-minute, agent training failed
- 🟡 Importante (`agent` amber) — RSVP nuovo, KB embedding complete
- 🟢 Info (`toolkit` green) — agent suggestion, daily summary
- ⚫ Lette (text-muted) — già viste

## Vincolo dati (lezione PR #568, GitGuardian)

❌ **VIETATO**: token UUID-like (`g6h7i8j9-...`, hex string ≥ 32 char), bearer-like (`sk_test_...`, `eyJ...`), api-key pattern.

✅ **OK**: ID short e leggibili (`gn-saturday-3`, `agent-nanolith-tutor`, `notif-rsvp-marco`), GUID solo se ovviamente fittizi (`00000000-...-aaaa`).

### Dati realistici da usare nei mockup SP7

**Game night events** (per A, B, C):
- "Sabato boardgame con i Padovani" · sab 17 maggio 21:00 · Casa Marco · 6/8 confermati
- "Mercoledì rapidi" · mer 14 maggio 20:30 · Casa Davide · 4/4 confermati
- "Festa estiva 24h" · sab 21 giugno 14:00 · Agriturismo Vinzaglio · 5/12 confermati

**Player names italiani**: Marco (host), Giulia, Davide, Luca, Sara, Aaron, Federica, Matteo

**Game candidates** (preferiti per fedeltà persona Marco): Twilight Imperium, Brass Birmingham, Spirit Island, Wingspan, Ark Nova, Terraforming Mars, Puerto Rico, Concordia

**Agent proposals** (per D, E, F, G):
- "Twilight Imperium Tutor" — Draft, 2 KB linked (rules + faqs)
- "Spirit Island Coach" — Published, 3 KB linked
- "Brass Birmingham Strategist" — Testing, 1 KB linked
- "Nanolith Narrator" — Draft (collegamento al SP6)

**Test queries** (per F playground):
- "Quanti dadi tira il barbaro al primo round?"
- "Cosa fa il bonus del Mech Spider?"
- "Posso costruire una fabbrica nel mio territorio nemico?"

**Notification samples** (per I):
- "🎮 Davide ha confermato RSVP per Sabato boardgame" (player)
- "🎲 Indicizzazione 'Twilight Imperium Rules' completata (4823 chunks)" (kb)
- "🤖 Il tuo agente 'Spirit Island Coach' ha ricevuto 12 query oggi" (agent)
- "🎉 Il gruppo ha votato Brass Birmingham per sabato" (event)

## Vincolo FREEZE v2 (issue #807, #808)

NO pattern `hsl(<num>, 89%, 48%)` o `hsla(<num>, 89%, *, 0.10)` hardcoded. Solo `entityHsl(entity, alpha)` helper o token semantici `var(--c-game)`, `var(--c-agent)`, `var(--c-kb)`, `var(--c-warning)`, `var(--c-event)`, `var(--c-success)`, `var(--c-danger)`, `var(--c-info)`.

Verifica grep post-generazione obbligatoria su ogni mockup:

```bash
grep -E "hsl\([0-9]+,?\s*89%,\s*48%\)|hsla\([0-9]+,?\s*89%" \
  admin-mockups/design_files/sp7-*.html
# Expected output: empty (zero match)
```

---

# WAVE 1 — Game Night Lifecycle (US-31)

## A — Game Night Create (`sp7-game-night-create`)

**File**: `sp7-game-night-create.{html,jsx}`
**Route**: `/game-nights/new`
**Persona**: Marco (host) sul desktop a casa, pianifica con calma. Mobile fallback per scheduling al volo.
**Pattern desktop**: split-form 12-col (left form 8-col, right preview RSVP-card live 4-col)
**Pattern mobile**: multistep wizard 4 step (StepIndicator riusato pattern SP6-B)

### Step 1 — Quando? (date + time + duration)

- Calendar picker desktop (inline) / Sheet picker mobile
- Time picker (24h, default 21:00)
- Duration estimate: 2h / 3h / 4h / "tutto il giorno"
- Hint "Sabato sera è l'orario più popolare nel tuo gruppo (8 partite su 12 ultimi 3 mesi)"

### Step 2 — Dove? (location)

- Toggle: 🏠 Casa di [nome host] / 🏠 Casa di [altro membro] / 📍 Custom address / 🌐 Online (Tabletop Simulator)
- Address autocomplete se Custom
- Note opzionale "Piano terra, ascensore non funziona" (max 200 char)

### Step 3 — Chi? (party)

- Player picker autocomplete (filtra dai contatti registrati nel gruppo)
- Email picker per non-registrati ("Invitiamo per email + magic link RSVP")
- Min/max giocatori (default 4-6)
- Toggle "RSVP automatico per i regular" (chi ha confermato negli ultimi 3 inviti = pre-checked)

### Step 4 — Cosa? (game candidates)

- Multi-select dalla library Marco (max 3 candidates)
- Per ogni candidate: durata stimata, n° giocatori supportato, complessità (BGG weight)
- Toggle "Lascia decidere al gruppo" → voting attivato in B detail
- Hint "Twilight Imperium e Brass non si possono giocare nella stessa sessione (>4h ciascuno)"

### Stati richiesti (anchor id `state-NN-...`)

| Stato | Descrizione | Edge case |
|---|---|---|
| `state-01-step1-date` | Step 1 calendar inline desktop / sheet picker mobile | First-time, suggest preferred slot |
| `state-02-step1-conflict` | User picks data che ha già altro evento | Banner amber "Hai già 'Mercoledì rapidi' alle 20:30" + override option |
| `state-03-step2-location` | Step 2 toggle locations | "Casa Aaron" auto-prefilled se host = Aaron |
| `state-04-step3-party` | Step 3 player picker + email | Tag chips entity=player, autocomplete dropdown |
| `state-05-step4-games` | Step 4 game multi-select | Library cards + duration/players matching |
| `state-06-step4-conflict` | 2 game candidates con duration totale > "tutto il giorno" | Banner red "Troppo lungo per una sera" |
| `state-07-review` | Riepilogo finale (read-only summary) | "Crea e invia inviti" CTA primary |
| `state-08-success` | Post-submit: redirect a B con toast | "Invitato 6 giocatori, attendi RSVP" |
| `state-09-mobile-step-flow` | Mobile vista wizard (4 step single column) | Bottom bar fixed con CTA "Avanti" / "Indietro" |

Desktop variants:
- Stato 04-05 split-form: left form + right "Anteprima RSVP card" che simula come apparirà in B per i giocatori

### Componenti / primitive da riusare

- `StepIndicator` (4 step) da SP6-B
- `Calendar` primitive (esistente in design system)
- `MeepleCard` variant `compact` per game candidates
- `EntityChip` entity=player per party tags
- `auth-card` per form sections
- `Tabs` non usato (è un wizard, no tab)
- `entityHsl('event', 0.1)` per accent color sezioni
- `var(--c-warning)` per conflict banner

### Coverage Gherkin

US-31 happy: G31.1 (create + send invites), G31.5 (game candidates max 3), G31.7 (auto-RSVP regulars), G31.10 (mobile single-column wizard).

---

## B — Game Night Detail + RSVP (`sp7-game-night-detail-rsvp`)

**File**: `sp7-game-night-detail-rsvp.{html,jsx}`
**Route**: `/game-nights/[id]`
**Persona**: misto — host (Marco) e player (Davide). Mobile primario (notifica → tap → vede dettagli + RSVP one-tap).
**Pattern**: hero summary + 3 tab (Dettagli / Voting / Chat gruppo)

### Hero summary

- Title evento (es. "Sabato boardgame con i Padovani")
- Status badge entity=event (🟠 Pianificata pulsing fino a -1h, 🟢 In corso da -1h a +duration, 🔘 Completata)
- Date+time relative ("tra 3 giorni · sabato 17 maggio 21:00")
- Location with map pin icon (clickable opens external maps)
- Host avatar + "Organizzato da Marco"

### Tab 1: Dettagli (default)

- Lista RSVP con player avatars + status icon:
  - ✅ "Davide ha confermato" (green)
  - ❓ "Giulia: forse" (amber)
  - ❌ "Federica: non posso" (gray)
  - ⏳ "Luca: in attesa" (dashed border)
- Counter "5/8 confermati" mono kicker
- CTA per il viewer:
  - Se è host: "Modifica serata" + "Cancella serata"
  - Se è invitato + RSVP missing: bottoni grandi [Ci sarò ✅] [Forse 🤔] [Non posso ❌]
  - Se è invitato + RSVP done: status corrente + "Cambia risposta"

### Tab 2: Voting (game candidates)

- 3 game candidates da Step 4 di A (passato come prop)
- Per ogni candidate:
  - MeepleCard variant `compact` con duration + player count + BGG weight
  - Voting bar: "{vote_count} voti" entityHsl(game)
  - Tap pill "+1 voto" / "-1 voto" se viewer è invitato
  - Total bar progress
- Caption "Voti chiudono 1h prima dell'evento"

### Tab 3: Chat gruppo

- Inline chat thread (riusa primitive chat-stream esistenti)
- Messaggi auto-system inseriti:
  - "🎮 Davide ha confermato RSVP"
  - "🎲 Federica ha votato Brass Birmingham"
  - "🎉 Marco ha cambiato la location: ora è Casa Davide"
- Input box bottom per messaggi liberi
- Mention @username highlight

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-host-view-pending` | Host (Marco) vede serata pianificata con 3/8 RSVP attese |
| `state-02-host-view-ready` | 6/8 confermati, mostra "Pronti per giocare!" CTA "Inizia sessione live" (link a US-30) |
| `state-03-invitee-pending-rsvp` | Davide arriva da invito, RSVP missing, hero CTA grandi |
| `state-04-invitee-confirmed` | Davide ha già confermato, mostra "Ci sarò ✅ · Cambia risposta" |
| `state-05-voting-active` | Tab 2 voting in corso, 12h dall'evento |
| `state-06-voting-locked` | Voting chiusa (-1h evento), risultato finale shown |
| `state-07-mobile-rsvp-bottom-sheet` | Mobile: RSVP CTA in bottom sheet sticky |
| `state-08-cancelled` | Serata cancellata, banner red + reason "Marco è ammalato, riprogrammiamo" |
| `state-09-completed-with-link-to-records` | Serata passata, CTA "Vai al play record" link a `/play-records/[id]` |
| `state-10-mobile-tab-chat` | Mobile vista tab 3 chat fullscreen |

Desktop variants: split-view 380px summary sidebar + main area tab content.

### Componenti / primitive da riusare

- `ConnectionBar` con pip [event=1, player={confirmed_count}, game={candidate_count}, kb=0]
- `EntityPip` per RSVP avatars
- `Tabs` 3-way con animated underline (riuso wave 1 pattern)
- `Drawer` per RSVP detail bottom sheet mobile
- chat-stream primitive per Tab 3

### Coverage Gherkin

US-31: G31.2 (RSVP one-tap), G31.3 (voting active/locked), G31.4 (host modifies), G31.6 (chat group).

---

## C — Game Night Edit (`sp7-game-night-edit`)

**File**: `sp7-game-night-edit.{html,jsx}`
**Route**: `/game-nights/[id]/edit`
**Persona**: host only (Marco). Modifica serata già pianificata.
**Pattern**: form edit + section delete/cancel modal + reschedule warning

### Sezione 1: Modifica metadata

- Pre-filled form da hero summary di B (date, time, duration, location, host, party, candidates)
- Tutti i campi editable
- Diff highlight: campi modificati badge "Modificato" entityHsl(warning, 0.1)
- "Salva modifiche" CTA primary + "Annulla" ghost

### Sezione 2: Reschedule warning

- Se l'utente cambia data: banner amber "Stai spostando la serata. I 5 giocatori che hanno già confermato riceveranno una nuova email RSVP. Vuoi continuare?"
- Toggle "Mantieni RSVP precedenti come 'In attesa nuova data'" / "Reset RSVP completo (nuovo invito da zero)"

### Sezione 3: Cancel section

- Bottone red ghost "Cancella serata" → ConfirmModal "Sei sicuro?"
- Modal stati:
  - `state-01-confirm-cancel`: form motivo + "Notifica giocatori"
  - `state-02-cancel-success`: serata = stato Cancellata, redirect a B con banner red

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-edit-pristine` | Form pre-filled, no modifiche |
| `state-02-edit-dirty-no-conflict` | Modifiche salvabili (es. cambio location) |
| `state-03-edit-dirty-reschedule` | Cambio data → reschedule warning amber |
| `state-04-edit-dirty-conflict` | Cambio data conflitto altro evento → red banner |
| `state-05-cancel-modal-pristine` | ConfirmModal aperto, form motivo vuoto |
| `state-06-cancel-modal-with-reason` | ConfirmModal con motivo "Sono malato" |
| `state-07-success-toast` | Salvataggio OK, redirect a B con toast verde |

Desktop variant: split-form (left edit / right preview as in A).

### Componenti / primitive da riusare

- Tutto da A (calendar, picker, ecc.)
- `ConfirmModal` riusato pattern SP6-B
- `var(--c-warning)` reschedule, `var(--c-danger)` cancel/conflict

### Coverage Gherkin

US-31: G31.4 (host edit), G31.8 (cancel + notify), G31.9 (reschedule conflicts).

---

# WAVE 1+ — Estensione issue #487 (game-night runtime)

Tre mockup aggiunti 2026-05-18 per chiudere lo scope residuo della issue [#487](https://github.com/meepleAi-app/meepleai-monorepo/issues/487) (`[Design v1 · B3] Mockup Game Nights`). Gli screen #1-#3 del brief originale sono già coperti da `sp4-game-nights-index` (list desktop+mobile) e `sp7-game-night-create` (planning wizard). I 4 screen residui (#4 Night Live, #5 Transition, #6 Summary, #7 Diary widget) sono distribuiti in 3 file SP7, con il Diary widget embedded come sub-component esportato dentro `sp7-game-night-live.jsx`.

## K — Game Night Live Hub (`sp7-game-night-live`)

**File**: `sp7-game-night-live.{html,jsx}`
**Route**: `/game-nights/[id]/live`
**Persona**: Marco (host) durante la serata, desktop al tavolo o tablet. Mobile per quick-jump tra session.
**Pattern**: 3-pane desktop (sidebar sx games planned 280px | center current game 1fr | sidebar dx diary stream 320px) / Mobile fullscreen swipeable tabs

### Sezioni richieste

- **Top bar live**: title serata + status pulsing `entityHsl('session')` + counter mono kicker "Game 2/3 · 23min elapsed"
- **Toolbar fissa**: [Pause ⏸] [Transition ➡️] [End night 🛑] con confirmation modal per End
- **Center pane — Current game card**: cover game + session live link + quick-jump CTA "Apri sessione live →" (link a `/sessions/[id]/live`)
- **Left pane — Planned games list**: stato per game (✅ completed / 🔴 in-progress pulsing / ⏳ upcoming), tempo stimato vs reale, ordine PlayOrder draggable disabled in live mode
- **Right pane — Cross-game diary**: stream timeline (riusa pattern `SessionDiaryTimeline` da `sp4-session-live-parts.jsx`, aggregato multi-session)
- **Diary inline widget** (componente esportato, copre screen #7 brief originale): versione compatta 320×400 embeddable in altri contesti (es. `/game-nights/[id]` detail o sidebar `/sessions/[id]/live`)

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-game-1-in-progress` | Prima session live, diary 5 eventi, 2 game planned upcoming |
| `state-02-mid-night-game-2` | Game 1 completed (winner banner), Game 2 in-progress, diary 18 eventi |
| `state-03-transition-pending` | Tra Game 1 e Game 2, current pane mostra "In attesa transition" |
| `state-04-paused` | Serata in pausa (overlay `entityHsl('agent', 0.1)` = warning), toolbar pause attiva |
| `state-05-diary-empty` | Inizio serata, diary "Nessun evento ancora · Inizia a giocare!" |
| `state-06-diary-widget-embedded` | Diary widget 320×400 versione compatta isolata (standalone preview per riuso) |
| `state-07-mobile-tab-current` | Mobile tab "Current" attivo |
| `state-08-mobile-tab-planned` | Mobile tab "Planned" attivo |
| `state-09-mobile-tab-diary` | Mobile tab "Diary" attivo |
| `state-10-auto-save-toast` | Toast "✓ Auto-salvato" `entityHsl('toolkit', 0.1)` ogni 60s, posizione bottom-right (acceptance criterion #487) |

Desktop variant: 3-pane fisso 280+1fr+320. Mobile variant: tabs swipeable (Current / Planned / Diary).

### Componenti / primitive da riusare

- `ConnectionBar` con pip `[event=1, game=3, session={current_id}, player={confirmed_count}]`
- `SessionDiaryTimeline` pattern da `sp4-session-live-parts.jsx` (aggregato multi-session)
- `Drawer` per game-jump bottom sheet mobile
- `MeepleCard` variant `compact` per planned games list
- `entityHsl('event')` accent serata, `entityHsl('session')` pulsing per game corrente

### Coverage Gherkin

US-31: G31.11 (live hub navigation), G31.12 (cross-game diary aggregation), G31.13 (auto-save indicator 60s)

---

## L — Game Transition Dialog (`sp7-game-night-transition`)

**File**: `sp7-game-night-transition.{html,jsx}`
**Route**: modal/overlay aperto da K — no standalone route
**Persona**: Marco (host) tra game e game, decisione 30-90 sec.
**Pattern**: Modal centered 600×500 desktop / fullscreen mobile, 2-column split (recap last | preview next)

### Sezioni richieste

- **Header**: "Pronti per il prossimo gioco?" + close X
- **Left col — Ultimo gioco**: cover small + title + winner banner `entityHsl('event')` + durata effettiva + score finale top-3
- **Right col — Prossimo gioco**: cover + title + rules quick-glance da KB (3 bullet) + tempo stimato + setup checklist con icone
- **CTA primary**: "Avvia prossima session →" `entityHsl('session')`
- **Secondary**: "Salta gioco" / "Termina serata qui"

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-transition-default` | Default 2-col split, recap + preview entrambi popolati |
| `state-02-rules-loading-skeleton` | KB quick-glance ancora in loading (skeleton 3 righe) |
| `state-03-rules-load-error` | KB fetch failed, fallback "Rules non disponibili offline" |
| `state-04-skip-confirm` | Submodal "Saltare? Verrà rimosso dai planned" |
| `state-05-end-night-confirm` | Submodal "Terminare serata? Andrai al Summary" |
| `state-06-mobile-fullscreen` | Mobile vertical stack invece di 2-col |

### Componenti / primitive da riusare

- `Drawer` variant `modal` da `03-drawer-variants.html`
- `ConfirmModal` per submodal skip/end (pattern SP6-B)
- `MeepleCard` variant `compact` per game preview entrambi i pannelli
- `entityHsl('event')` winner banner, `entityHsl('session')` CTA primary

### Coverage Gherkin

US-31: G31.14 (transition flow), G31.15 (KB rules preview fallback)

---

## M — Night Summary (`sp7-game-night-summary`)

**File**: `sp7-game-night-summary.{html,jsx}`
**Route**: `/game-nights/[id]/summary` (post-end final state)
**Persona**: tutti i partecipanti, post-serata. Mobile primario per share, desktop per archiviazione/review.
**Pattern**: Full-screen single column scrollable, hero + sections stack

### Sezioni richieste

- **Hero**: title serata + date + total durata + winner banner globale "🏆 MVP della serata: Davide" `entityHsl('event')`
- **Stats grid**: 4 KPI card mono kicker (totale sessioni, durata, n° eventi diary, winner per gioco)
- **Cross-game timeline**: `SessionDiaryTimeline` filtered, collapsed by game (expand per dettaglio)
- **Per-game recap**: 1 row per game con winner + durata + CTA "Vai a session →" link a `/sessions/[id]`
- **Foto gallery** (placeholder): grid 3×N con add-photo CTA `entityHsl('event', 0.1)` placeholder card
- **Footer CTA**: [Condividi riepilogo 📤] [Archivia ✓]

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-summary-full` | 3 games completati, 28 diary events, foto 6 |
| `state-02-summary-no-photos` | Stesso ma foto gallery empty con CTA placeholder "Aggiungi foto" |
| `state-03-summary-single-game` | Serata 1 game (no transition history, no per-game recap multipli) |
| `state-04-share-success-toast` | Post-share toast "Link copiato" `entityHsl('toolkit', 0.1)` |
| `state-05-archived` | Post-archive banner muted `var(--text-muted)` + CTA "Torna alla lista" |
| `state-06-mobile-single-col` | Mobile vertical stack (default già single col, ma con padding ridotto) |

### Componenti / primitive da riusare

- `SessionDiaryTimeline` mode `collapsed` (filtra per game header expandable)
- `MeepleCard` variant `compact` per per-game recap rows
- `entityHsl('event')` hero accent, `entityHsl('toolkit')` per success states (share/archive)

### Coverage Gherkin

US-31: G31.16 (summary aggregation), G31.17 (share riepilogo), G31.18 (archive flow)

---

# WAVE 2 — Agent Builder Flow (US-33)

## D — Agent Proposals List (`sp7-agent-proposals-list`)

**File**: `sp7-agent-proposals-list.{html,jsx}`
**Route**: `/editor/agent-proposals`
**Persona**: Marco (vuole vedere le sue bozze) o Aaron (superadmin vede tutte).
**Pattern**: hero + grid responsive (desktop 12-col → 4-col card grid; mobile 1-col stack)

### Hero compatto

- Title "Agenti AI"
- Subtitle KPI mono kicker "{N_published} attivi · {N_draft} in lavorazione · {N_testing} in test"
- CTA primary "Nuovo agente" → /editor/agent-proposals/create (link a E)

### Filter bar

- Tab segments: [Tutti] [Pubblicati] [Bozze] [In test] [Archiviati]
- Filter chips: per gioco linked, per KB linked, per autore (se superadmin)
- Search box per nome agente

### Grid agent proposal cards

Ogni card:
- MeepleCard variant `featured` con:
  - Avatar entityHsl(agent) icona robot
  - Title nome agente
  - Subtitle "Linked to: Twilight Imperium" (game entity reference)
  - Status badge (Published/Draft/Testing/Archived)
  - KB count chip "📄 2 KB"
  - Last updated relative ("aggiornato 3 giorni fa")
  - 3-dot menu: [Test playground (link F), Modifica (link G), Duplica, Archivia]

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-empty-no-agents` | First-time user, no proposals → empty state illustrato + "Crea il tuo primo agente" CTA |
| `state-02-grid-mixed-status` | 4-6 cards mixed Published/Draft/Testing |
| `state-03-grid-only-published` | Filter "Pubblicati" → solo agenti attivi |
| `state-04-loading-skeleton` | Skeleton 4 cards while fetching |
| `state-05-search-no-results` | Search "xyz123" → empty results card "Nessun agente trovato" |
| `state-06-mobile-stack` | Mobile 1-col stack |

### Componenti / primitive da riusare

- `MeepleCard` variant `featured` o `compact` (mobile)
- `EntityChip` entity=agent per status
- `Tabs` segments per filter
- `EmptyState` primitive per state-01

### Coverage Gherkin

US-33: G33.1 (browse proposals), G33.7 (filter by game).

---

## E — Agent Builder Create (`sp7-agent-builder-create`)

**File**: `sp7-agent-builder-create.{html,jsx}`
**Route**: `/editor/agent-proposals/create`
**Persona**: Aaron (più probabile) o Marco. Desktop primario (form sostanzioso) + mobile fallback.
**Pattern**: multistep wizard 4 step (StepIndicator riusato), desktop split-form con preview live

### Step 1 — Nome + descrizione + linked game

- Input "Nome agente" (required, es. "Twilight Imperium Tutor")
- Textarea "Descrizione" (opzionale, 200 char, "Aiuta con regole avanzate e variant rules")
- Game picker autocomplete da library Marco (1 game required)
- Hint mono kicker "L'agente sarà disponibile dal game detail page"

### Step 2 — Knowledge Bases linked

- Multi-select da KB esistenti (filtered by linked game)
- Per ogni KB: card preview con chunk count + last indexed
- Hint "Più KB = risposte più ricche ma latenza più alta. Consigliato 1-3"
- Banner amber se 0 KB selected: "Senza KB l'agente userà solo i suoi system prompt"
- CTA "Indicizza nuovo KB" → link a /knowledge-base (modale o redirect)

### Step 3 — System prompt + tone

- Tone picker (single-select):
  - "🎓 Tutor (formale, paziente, citation-heavy)"
  - "🎮 Game Master (immersivo, narrativo, evocativo)"
  - "📚 Strategist (analitico, deep-dive)"
  - "💬 Casual (warm, conversazionale)"
  - "🛠️ Custom (advanced)"
- System prompt textarea (~500 char):
  - Pre-filled da tone preset
  - Editable per customization
  - Variable hints: `{game_name}`, `{user_name}`, `{kb_summary}`
- Confidence threshold slider:
  - "Quando rispondere?" 0.5 (lenient) → 0.9 (strict)
  - Visual: pill colorate [bassa][media][alta]

### Step 4 — Review + create

- Riepilogo summary card readonly:
  - Nome + descrizione + game
  - KB linked count + names
  - Tone + system prompt preview (collapsed, expandable)
  - Confidence threshold value
- Toggle "Pubblica subito" (default off → Draft)
- CTA primary "Crea agente" → POST → redirect a F test playground

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-step1-pristine` | Step 1 form vuoto |
| `state-02-step1-filled-with-game` | Step 1 pre-filled "Twilight Imperium Tutor" |
| `state-03-step2-no-kb-warning` | Step 2 no KB selected → banner amber |
| `state-04-step2-3-kbs-selected` | Step 2 con 3 KB selezionati (max consigliato) |
| `state-05-step3-tone-tutor` | Tone "Tutor" selected, prompt pre-filled formal |
| `state-06-step3-tone-custom` | Tone "Custom", prompt textarea editabile completo |
| `state-07-step4-review-draft` | Step 4 con toggle "Pubblica subito" OFF (Draft) |
| `state-08-step4-review-publish` | Step 4 con toggle ON → "Crea e pubblica" CTA |
| `state-09-success-redirect-to-test` | Post-create, toast "Agente creato come Draft. Iniziamo a testarlo?" → redirect F |
| `state-10-mobile-step-flow` | Mobile vista wizard 4 step single column |

Desktop variant: split-form (left wizard 8-col + right "Anteprima agente" preview card 4-col, live update mentre user compila).

### Componenti / primitive da riusare

- `StepIndicator` 4 step
- `MeepleCard` per game picker preview + KB cards
- `EntityChip` entity=kb per KB tags
- `entityHsl('agent', 0.1)` accent
- Slider primitive per confidence threshold

### Coverage Gherkin

US-33: G33.2 (create wizard), G33.3 (multi-KB select), G33.5 (tone preset + custom override).

---

## F — Agent Builder Test Playground (`sp7-agent-builder-test`)

**File**: `sp7-agent-builder-test.{html,jsx}`
**Route**: `/editor/agent-proposals/[id]/test`
**Persona**: Aaron o Marco appena dopo create. Test playground intensivo prima di pubblicare.
**Pattern**: Desktop split-view (left agent context 380px + right chat playground flex). Mobile tabbed.

### Left sidebar (desktop only) — Agent context

- Header con nome agente + status badge
- KB linked list (chips)
- Current settings: tone + confidence threshold
- "Modifica" link a G edit
- Stats live: "Query oggi: 12 · Confidence avg: 0.82"

### Main area — Chat playground

- Tab 1: **Test playground** (default)
  - Chat thread (riusa primitive chat-stream)
  - Suggested test queries chips top: "Setup di base", "Combat advanced", "Edge case houserule"
  - User input box bottom
  - Per ogni risposta agente:
    - Risposta text con citation pagine
    - ConfidenceBadge (alta/media/bassa)
    - Feedback CTA: [👍 OK] [👎 Sbagliata] [❓ Non lo so]
    - "Mostra debug" expandable → mostra retrieval chunks + scoring
- Tab 2: **History** (cronologia query questa sessione + score aggregato)
- Tab 3: **Debug** (advanced: retrieval params, prompt full text, LLM provider)

### Bottom bar — Iteration CTA

- "Modifica system prompt" → open drawer con prompt editor inline (no redirect a G full)
- "Re-deploy con changes" → re-test senza save permanente
- Quando confident: "Pubblica agente" CTA primary entityHsl(agent) → status Draft → Published

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-empty-no-queries` | First time test, suggested queries chips visibili |
| `state-02-active-conversation` | 5 query/response thread |
| `state-03-confidence-high-feedback-thumbs-up` | Risposta confidence 0.92 + user 👍 |
| `state-04-confidence-low-suggestion` | Risposta confidence 0.45 → "Non sono sicuro" + suggerimento "Prova ad aggiungere KB" |
| `state-05-debug-expanded` | Expandable debug panel mostra retrieval chunks |
| `state-06-iteration-prompt-drawer` | Bottom drawer aperto per edit system prompt inline |
| `state-07-publish-success` | Post-publish, toast verde "Agente pubblicato! Ora visibile in /agents" |
| `state-08-history-tab` | Tab 2 history con 12 query precedenti + score aggregati |
| `state-09-mobile-tab-flow` | Mobile single tab visualization, swipe per cambio tab |

Desktop variant: split-view 380px sidebar + flex main.

### Componenti / primitive da riusare

- chat-stream primitive (existing)
- `ConfidenceBadge` (SP6-C)
- `Drawer` per prompt editor bottom (mobile) o side (desktop)
- `Tabs` 3-way per Playground/History/Debug

### Coverage Gherkin

US-33: G33.4 (test playground), G33.6 (iteration without save), G33.8 (publish from test).

---

## G — Agent Builder Edit (`sp7-agent-builder-edit`)

**File**: `sp7-agent-builder-edit.{html,jsx}`
**Route**: `/editor/agent-proposals/[id]/edit`
**Persona**: Marco/Aaron iterano un agente esistente. Quick edit, no full wizard.
**Pattern**: form edit non-wizard + version diff sidebar (desktop)

### Form layout

Sezioni espandibili (accordion):
- Metadata (nome, descrizione, game) — collapsed by default
- KBs linked — collapsed
- System prompt + tone — **espanso by default** (più frequente edit)
- Confidence threshold — collapsed

Ogni sezione ha "Salva sezione" button localizzato per save granular.

### Right sidebar (desktop) — Version diff

- Last 5 versions list
- Selectable per diff viewer:
  - Diff visivo: red strikethrough vecchio + green new
  - "Ripristina questa versione" CTA per ogni
- Annotation: "v3 testato 12 volte, score 4.2/5"

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-edit-pristine` | Form pre-filled, no modifiche |
| `state-02-edit-section-prompt` | Sezione prompt espansa, edit attivo |
| `state-03-section-saved-toast` | Save sezione OK, toast verde "Sezione aggiornata" |
| `state-04-version-diff-side-by-side` | Sidebar version diff aperto v3 vs current |
| `state-05-restore-version-confirm` | Modal "Sei sicuro di ripristinare v3?" |
| `state-06-mobile-no-sidebar` | Mobile: form solo, version history accessible via separate menu |

### Componenti / primitive da riusare

- Same primitives di E (auth-card, slider, etc.)
- `Drawer` per version history mobile
- Diff visualization custom (text decoration)

### Coverage Gherkin

US-33: G33.6 (iterate prompt), G33.9 (rollback to previous version).

---

# WAVE 3 — Agent Inline + Notifications

## H — Library Game Agent Inline (`sp7-library-game-agent`)

**File**: `sp7-library-game-agent.{html,jsx}`
**Route**: `/library/games/[gameId]/agent`
**Persona**: Marco al tavolo (mobile primario). Chat con agente specifico del gioco durante partita.
**Pattern**: Mobile fullscreen chat / Desktop split-view (left game context 380px + right chat flex)

### Hero compatto

- Game cover + title (es. "Twilight Imperium")
- Agent badge "🤖 Twilight Imperium Tutor" + ConnectionPip status (ready/loading/error)
- Quick stats kicker "Hai chiesto 24 query in totale · ultima ieri"

### Chat thread main area

Riuso completo chat-stream primitives di SP4 + SP6:
- Message bubbles user (right, entityHsl(player)) e agent (left, entityHsl(agent))
- Citation chips inline su risposte agente (clickable → opens KB chunk)
- ConfidenceBadge per ogni risposta
- Long-press message → action drawer [Pin, Copy, Share, Report]

### Quick actions footer

- Suggested queries chip strip top: "Setup", "Regola combat", "Variante 4 giocatori", "Edge case [last query]"
- Voice input (mobile) / Text input (desktop) con typing indicator

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-empty-first-chat` | First time, suggested queries visibili, prompt "Cosa vuoi sapere?" |
| `state-02-active-conversation-3-messages` | 3 query/response in thread |
| `state-03-low-confidence-with-suggestion` | Risposta 0.4 confidence → "Non sono certo, controlla pag. X" + suggest "Aggiungi houserule?" |
| `state-04-citation-expanded` | Tap citation chip → side panel desktop / drawer mobile mostra KB chunk full text |
| `state-05-message-action-drawer` | Long-press message → drawer 4 azioni |
| `state-06-typing-indicator` | Agent sta scrivendo, animation 3 puntini |
| `state-07-error-network` | Network down → banner red "Connessione persa, retry tra 5s" |
| `state-08-no-agent-linked` | Game senza agent linked → empty state "Nessun agente per Twilight Imperium" + CTA "Crea agente" link a E |
| `state-09-mobile-fullscreen` | Mobile fullscreen chat con bottom input sticky |
| `state-10-desktop-split-view` | Desktop split-view 380px game context + chat |

### Componenti / primitive da riusare

- chat-stream primitives full (riusate da SP4)
- `ConfidenceBadge`, `CitationChip` (SP6-C)
- `Drawer` per message actions
- `ConnectionPip` per agent status

### Coverage Gherkin

US-13/33: G33.10 (inline chat), G13.1 (citation accuracy), G13.5 (low-confidence disclaimer).

---

## I — Notifications Hub (`sp7-notifications-hub`)

**File**: `sp7-notifications-hub.{html,jsx}`
**Route**: `/notifications`
**Persona**: Marco al risveglio (mobile, mass-check), o desktop occasionale.
**Pattern**: timeline + grouping + bulk actions

### Header

- Title "Notifiche"
- Counter "{N_unread} nuove · {N_total} totali"
- Bulk actions: [Segna tutte come lette] [Archivia tutte] (visible se > 5 unread)
- Filter tabs: [Tutte] [Non lette] [Mention] [Critiche]

### Timeline grouped by date

Sezioni "Oggi", "Ieri", "Questa settimana", "Più vecchie".

Ogni notification card:
- Icon entity (event/player/agent/kb)
- Severity dot color (red critical, amber important, green info, gray read)
- Title + sub (es. "🎮 Davide ha confermato RSVP" / "Sabato boardgame con i Padovani")
- Relative time ("3 min fa" / "2 ore fa" / "ieri 18:32")
- Tap → action (apri evento, apri agente, apri KB, dismiss)
- Long-press / hover → action drawer [Apri, Marca letta, Snooze 1h, Archivia]

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-empty-no-notifications` | First time, illustrato "Nessuna notifica" + suggested actions |
| `state-02-mixed-15-notifications` | 15 notifiche miste, 5 unread, gruppi Oggi/Ieri/Settimana |
| `state-03-only-unread-filter` | Tab "Non lette" filtra a 5 |
| `state-04-bulk-mark-read-confirm` | "Segna tutte come lette" → confirm modal |
| `state-05-action-drawer-on-tap` | Long-press notification → drawer 4 azioni |
| `state-06-snooze-success` | Toast "Notifica posticipata di 1h" |
| `state-07-error-network` | Banner red "Impossibile caricare nuove notifiche" + retry |
| `state-08-mobile-stack` | Mobile vertical stack |

### Componenti / primitive da riusare

- `Tabs` filter
- `Drawer` action menu
- `EntityChip`/`EntityPip` per icon notifications

### Coverage Gherkin

US-41: G41.1 (browse notifications), G41.4 (filter), G41.6 (snooze), G41.8 (bulk actions).

---

## J — Notifications Preferences (`sp7-notifications-preferences`)

**File**: `sp7-notifications-preferences.{html,jsx}`
**Route**: `/notifications/preferences`
**Persona**: Marco one-time setup, poi rare modifiche.
**Pattern**: form preferences settings + 3 channel sections

### Sezione 1: Per evento type

Toggle switches per categoria:
- 🎉 Game night events (RSVP changes, voting, cancellations)
- 🤖 Agent events (training complete, error, daily summary)
- 📄 KB events (indexing complete, error, embedding failure)
- 💬 Chat mentions
- 🎮 Player social (friends RSVP, tournaments)

Per ogni: 3 sub-toggle [Email] [Push] [In-app]

### Sezione 2: Frequency

- "Quando ricevere notifiche?" radio:
  - "Real-time" (immediate push)
  - "Daily digest" (single email morning)
  - "Off" (only critical)
- Time window pickers ("non disturbare 22:00-08:00")

### Sezione 3: Channels

- Email primary + alternate
- Push device list (mobile devices registrati)
- Browser web push toggle

### Stati richiesti

| Stato | Descrizione |
|---|---|
| `state-01-default-all-on` | Default settings: tutti i toggles ON |
| `state-02-customized-realtime-only-critical` | Real-time only critical, daily digest others |
| `state-03-quiet-hours-22-08` | Quiet hours configurati |
| `state-04-no-mobile-device-registered` | Section channel mostra "Nessun device mobile" + CTA "Installa app" |
| `state-05-save-success-toast` | Save → toast "Preferenze aggiornate" |

### Componenti / primitive da riusare

- Form primitives (toggle switch, radio, time picker)
- `auth-card` per section grouping

### Coverage Gherkin

US-41: G41.2 (configure preferences), G41.5 (quiet hours), G41.7 (multi-device).

---

## File da allegare ogni nuova chat Claude Design (tipico set)

```
admin-mockups/briefs/_common.md
admin-mockups/briefs/SP7-game-night-agent-builder.md
admin-mockups/design_files/tokens.css
admin-mockups/design_files/components.css
admin-mockups/design_files/sp4-game-detail.{html,jsx}
admin-mockups/design_files/sp4-agents-index.{html,jsx}
admin-mockups/design_files/sp4-agent-detail.{html,jsx}
admin-mockups/design_files/sp4-game-nights-index.{html,jsx}
admin-mockups/design_files/sp4-session-live.{html,jsx}
admin-mockups/design_files/sp6-libro-game-glossary-editor.{html,jsx}  (per pattern modal H reference)
```

12-13 file totali. Per Wave 1 (game nights) aggiungere `sp4-session-summary.{html,jsx}` come reference. Per Wave 2 (agent builder) aggiungere mockup F SP6-C `sp6-libro-game-play-session.{html,jsx}` per chat panel pattern.

## Risposta attesa nel thread Claude Design

Per ogni mockup SP7:

1. Conferma scope SP7 (10 mockup A-J, 3 wave, persona Marco + Aaron, FREEZE-compliant)
2. Genera **una risposta = un mockup** (no batch)
3. File completo (HTML + JSX entrambi per A-J, no solo HTML)
4. Path salvataggio esplicito: `admin-mockups/design_files/sp7-<name>.{html,jsx}`
5. Anchor id `state-NN-...` su tutti i wrapper per review meeting
6. ConnectionPip[] inline nel JSX dove richiesto (replicato dalle sezioni B, F, H)
7. Note finali per ogni mockup:
   - Deviazioni flaggate (con motivazione vs vincolo brief)
   - Nuovi componenti v2 emersi (per impl in `apps/web/src/components/ui/v2/`)
   - Stati Gherkin coperti (mappare ogni state UI → US-31/US-33/US-41)

Quando SP7 completo, tabella handoff finale + lista master componenti emersi.

## Note finali per Claude Design

**Tono UI**: warm, accogliente, pari level — Marco è esperto ma non vuole gergo enterprise. Linguaggio paritario ("tu", "il tuo gruppo", "la tua serata"). Italiano informale-formale (Marco è un adulto, no slang).

**Microcopy hint**:
- Game night creation: "Pianifica la tua serata" (non "Schedule a game night session")
- RSVP CTA: "Ci sarò" / "Forse" / "Non posso" (no "Confirm/Maybe/Decline")
- Agent builder: "Crea il tuo agente" (non "Generate AI agent definition")
- Notifications: "Posticipa di 1h" (non "Snooze for 60 minutes")
- Confidence: "Sono abbastanza sicuro / Non sono certo / Non lo so" (non "High/Medium/Low confidence")

**Prioritizza percepito velocità**: ogni mockup deve mostrare almeno 1 stato di feedback immediato sotto 1 sec (skeleton, typing, optimistic UI). I happy path target P95 < 5 sec.

**Edge case empathy**: state-04 / state-08 di B (cancelled/completed), state-04 di H (no agent linked), state-01 di I (empty notifications) devono avere copy che NON faccia sentire l'utente "broken" — sono stati validi.

Buon lavoro. SP7 attiva 3 user story P1 dormant — post-merge si passa allo sprint frontend implementation.

## Wave checklist (per tracking generation)

| Wave | Mockup | Stato | Note |
|------|--------|-------|------|
| 1 | A `sp7-game-night-create` | ⏳ pending | First della wave |
| 1 | B `sp7-game-night-detail-rsvp` | ⏳ pending | After A |
| 1 | C `sp7-game-night-edit` | ⏳ pending | After B |
| 1+ | K `sp7-game-night-live` | ⏳ pending | Issue #487 — central hub durante serata, embed Diary widget (#7) |
| 1+ | L `sp7-game-night-transition` | ⏳ pending | Issue #487 — modal recap+preview tra game e game |
| 1+ | M `sp7-game-night-summary` | ⏳ pending | Issue #487 — recap full-screen post-end |
| 2 | D `sp7-agent-proposals-list` | ⏳ pending | First della wave 2 |
| 2 | E `sp7-agent-builder-create` | ⏳ pending | After D |
| 2 | F `sp7-agent-builder-test` | ⏳ pending | After E |
| 2 | G `sp7-agent-builder-edit` | ⏳ pending | After F |
| 3 | H `sp7-library-game-agent` | ⏳ pending | First della wave 3 |
| 3 | I `sp7-notifications-hub` | ⏳ pending | After H |
| 3 | J `sp7-notifications-preferences` | ⏳ pending | After I |

Aggiornare questa tabella man mano che i mockup vengono consegnati e committati.
