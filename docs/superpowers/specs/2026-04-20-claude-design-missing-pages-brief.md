# Brief per Claude Design — Pagine mancanti per completare il Design System v1

> **Scopo:** Documento da fornire a Claude Design nella prossima sessione per generare i mockup delle aree app non ancora coperte dal design system in `admin-mockups/design_files/`.

**Data:** 2026-04-20
**Autore:** Migration planning session
**Stato:** Pronto per Claude Design (settimana prossima)

---

## Contesto

Claude Design ha già prodotto il Design System v1 in `admin-mockups/design_files/` con:
- **tokens.css** — 9 entity colors (game/player/session/agent/kb/chat/event/toolkit/tool), typography Quicksand+Nunito, spacing 4px, dark mode warm palette
- **components.css** — theme toggle, entity chips/dots, cards, buttons, phone frame, hub nav
- **data.js** — dataset demo cross-referenced
- **mobile-app.jsx** — 24 screen mobile interattivi
- 5 HTML demo: `00-hub.html`, `01-screens.html` (mobile), `02-desktop-patterns.html`, `03-drawer-variants.html`, `04-design-system.html`, `05-dark-mode.html`

Questo brief elenca le aree **non** coperte dal sistema attuale. Vanno create mantenendo la stessa estetica (warm palette, entity colors HSL, Quicksand/Nunito, 4px grid, radius xl/2xl, vaul-style bottom sheets).

---

## Principi di coerenza (da rispettare in tutti i nuovi mockup)

1. **Entity-first color coding** — ogni pagina associata a un'entity usa `--c-<entity>` come accento (border, icone, pill); le pagine di sistema (auth, settings) usano `--c-game` (brand) come fallback.
2. **Warm palette** — `--bg: #f7f3ee` (light), `--bg: #14100a` (dark). Niente grigi freddi.
3. **Typography scale** — `--f-display: Quicksand` (h1-h6 + tag+label), `--f-body: Nunito` (testo), `--f-mono: JetBrains Mono` (kicker+metric).
4. **Mobile-first** — phone frame 380×780, drawer bottom sheet come primary disclosure.
5. **Desktop pattern riuso** — scegli tra Split/Sidebar/HeroTabs per ogni pagina (indicati nello scope sotto).
6. **Connection bar** — dove presente un'entity, mostra pip delle relazioni (come in `mobile-app.jsx`).
7. **Dark mode** — ogni nuova pagina deve avere variante dark con entity colors più chiari (vedi `tokens.css:150-158`).

---

## Pagine da creare — Gruppo 1 · Critiche 🔴

### 1.1 Auth flow (`auth-flow.html` + jsx)

**Screen da produrre:**
- **Login** — email+password, OAuth buttons (Google, Discord), link register/forgot
- **Register** — email+password+username, terms checkbox, OAuth buttons
- **Forgot password** — email-only form → stato "email inviata"
- **Reset password** — new-password+confirm, token nascosto
- **Verify email** — stato "controlla la casella" + resend button
- **2FA setup (opzionale)** — QR code + codice manuale + verify input

**Design hint:**
- Full-screen, no nav, phone frame mantenuto
- Logo MeepleAI al top (brand-mark gradient `--c-game → --c-event → --c-player`)
- Card centrato, `--r-2xl`, shadow-lg
- Pulsante primario `.btn.primary` con `--e: var(--c-game)`
- Error state inline con `hsl(var(--c-danger) / 0.1)` bg + border
- Social buttons outline style con icone colorate

**Entità collegate:** nessuna (pre-auth) — usa `--c-game` come accento.

---

### 1.2 Onboarding (`onboarding.html` + jsx)

**Screen da produrre:**
- **Welcome** — hero illustrato, "Inizia il tour" / "Salta"
- **Step 1 — Choose games** — multi-select da catalogo (grid di game cards)
- **Step 2 — Connect agents** — toggle agenti AI disponibili + descrizione
- **Step 3 — First session** — CTA "Crea la prima serata" / "Esplora"
- **Complete** — "Benvenuto!" + next action

**Design hint:**
- Progress bar top (3-4 step)
- Skip link in alto a destra
- Micro-animation su ogni step (framer-motion-style)
- Back/Next pattern bottom

**Entità collegate:** `game` (step 1), `agent` (step 2), `session` (step 3) — cambia accento entity step-by-step.

---

### 1.3 Settings (`settings.html` + jsx)

**Screen da produrre:**
- **Settings hub** — lista categorie con icone
- **Profile** — avatar, username, bio, email (read-only con verify badge)
- **Account** — password change, 2FA toggle, delete account
- **Preferences** — theme (light/dark/auto), language, timezone, density (compact/comfy/spacious)
- **Notifications prefs** — toggle granulari per tipo (session started, agent reply, game night invite...)
- **API keys** — generate, revoke, last-used timestamp (per power users)
- **Connected services** — Google, Discord, BGG account linking

**Design hint:**
- Desktop: **Sidebar+Drawer** pattern — nav a sx, pannello dx
- Mobile: lista che apre drawer per ogni sezione
- Toggle switches custom styling entity-aware
- Destructive actions (delete) con `--c-danger` e confirm modal

**Entità collegate:** — (system) — accento `--c-game`.

---

### 1.4 Notifications (`notifications.html` + jsx)

**Screen da produrre:**
- **Feed list** — raggruppato per giorno, entity-colored left border per tipo
- **Notification detail** — stesso drawer pattern delle entity, con CTA context
- **Empty state** — "Nessuna notifica" con illustrazione
- **Filters bar** — all / sessions / agents / game-nights / system

**Design hint:**
- Badge counter in top nav (se presente sistema badge)
- Read/unread visivo: unread = bold + dot pip `--c-event`
- Swipe-to-dismiss (mobile), hover-actions (desktop)
- Timestamp relative ("2h fa", "ieri")

**Entità collegate:** varia per notifica (mostra chip dell'entity sorgente: session, agent, player).

---

### 1.5 Public pages (`public.html` + jsx)

**Screen da produrre:**
- **Landing** — hero con value prop, stats row, feature grid, CTA register
- **Pricing** — 3 tier cards (Free / Pro / Team), feature matrix
- **About** — team, mission, story
- **Terms / Privacy** — long-form markdown styling
- **Contact** — form + email + social

**Design hint:**
- Desktop-first (nav `.hub-nav`)
- Hero gradient background entity-mix (come `00-hub.html` `.hero h1 .mark`)
- Footer con link legali + social icons
- Pricing: highlight middle card con `--c-game` border

**Entità collegate:** — (marketing) — mix di entity colors per brand.

---

## Pagine da creare — Gruppo 2 · Importanti 🟡

### 2.1 Game Night creation+edit (`game-night.html` + jsx)

**Screen da produrre:**
- **Game Night list** — calendario mensile + lista prossime + past
- **Create** — wizard 3 step: data+luogo → giocatori invitati → giochi pianificati
- **Edit** — stessi campi con stato corrente
- **Detail** — partecipanti, giochi, diary cross-game, foto gallery (stub)
- **Finalize** — summary card riassuntiva

**Design hint:**
- Entità: `event` (colore `--c-event`)
- Multi-step progress bar come onboarding
- Drag-drop per ordinare giochi
- Avatar stack per partecipanti

**Entità collegate:** `event` primario, `player` + `game` + `session` secondarie (connection bar).

---

### 2.2 Play records / history (`play-records.html` + jsx)

**Screen da produrre:**
- **Records list** — filtri per game/player/date range
- **Record detail** — punteggi finali, durata, MVP, note
- **Stats dashboard** — win rate per giocatore, giochi più giocati, trend temporale
- **Export** — CSV/PDF selector

**Design hint:**
- Entità: `session` (colore `--c-session`)
- Table desktop + card list mobile
- Chart mini-inline per stats (sparkline style)

**Entità collegate:** `session` primario, `game` + `player` secondarie.

---

### 2.3 Session creation wizard (`session-wizard.html` + jsx)

**Screen da produrre:**
- **Step 1 — Game select** — grid da library con search (prefill se da detail page)
- **Step 2 — Players** — add from contacts + guest players (name-only)
- **Step 3 — Turn order** — drag-drop reorder, random shuffle button
- **Step 4 — Variant / house rules** — opzionale, select da KB
- **Step 5 — Ready** — summary + "Start session" CTA

**Design hint:**
- Entità: `session` (colore `--c-session`)
- Mobile: stack verticale, step scroll
- Desktop: split-view sx = step / dx = preview live
- Prefill detection: se query string `?game=<id>` → salta step 1

**Entità collegate:** `game` (step 1), `player` (step 2-3), `kb` (step 4).

---

## Pagine da creare — Gruppo 3 · Opzionali 🟢

### 3.1 Editor / Pipeline / n8n (`editor-pipeline.html` + jsx)

Area avanzata per power users. Mantenere coerenza ma priorità bassa.

**Screen indicativi:**
- Workflow canvas (node-based)
- Pipeline status + logs
- n8n webhook config

**Entità collegate:** `tool` (colore `--c-tool`).

---

### 3.2 Calendar view (`calendar.html` + jsx)

Vista mensile/settimanale degli events.

**Screen indicativi:**
- Month grid con event pills
- Week view con time slots
- Day detail

**Entità collegate:** `event`.

---

## Checklist di consegna per Claude Design

Per ogni gruppo di pagine, Claude Design dovrebbe produrre:
- [ ] File HTML demo standalone (come `01-screens.html`) con tutti gli screen
- [ ] Eventuali estensioni a `components.css` (NO modifiche breaking)
- [ ] Eventuali nuovi dati in `data.js` (es. notifications, records)
- [ ] Aggiornamento di `00-hub.html` con link al nuovo gruppo
- [ ] Variante dark mode completa

**Priorità ordine di lavoro consigliato:**
1. Gruppo 1.1 Auth + 1.3 Settings (bloccanti funzionali)
2. Gruppo 1.4 Notifications + 1.5 Public (visibilità utente)
3. Gruppo 1.2 Onboarding (post-auth)
4. Gruppo 2.1-2.3 (feature app-specific)
5. Gruppo 3 (opzionale)

---

## Integrazione nel piano di migrazione

Una volta consegnati i mockup, verranno aggiunti come **M6+M7** al plan `redesign-v2-full-app-migration-m1-m5.md`:
- **M6 Auth + Settings + Notifications + Public** — dipende da Gruppo 1
- **M7 Game nights + Records + Session wizard** — dipende da Gruppo 2
- **M8 (opzionale)** — Editor + Calendar da Gruppo 3

Il branch `redesign/v2` resta aperto fino a M5; M6+ apriranno nuovi plan e branch dipendenti.
