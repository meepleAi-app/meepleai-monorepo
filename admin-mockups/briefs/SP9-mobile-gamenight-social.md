# SP9 Mobile GameNight Social — Brief Claude Design (3 mockup mobile-first)

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.
> Follow-up di **#2989** (gap headline della demo SP8 #1890): il flow **GameNight/Session social** non ha superficie mobile. Questo brief produce le **varianti mobile 375px** dei 3 schermi wrapper-social, riusando i componenti FE **già implementati** (v2) come content model. NON è greenfield di dominio: route e componenti RSVP esistono, mancano i mockup mobile + 2 gap di prodotto.

## Stato programma

| SP | Superficie | Stato | Audience |
|----|-----------|-------|----------|
| SP7 game-night | wrapper desktop (create/detail/live) | ✅ FE v2 implementato | host/player serate |
| SP8 mobile-parity | library-mobile + companion | ✅ merged #1689/#2983 | mobile-first |
| **SP9 mobile-gamenight-social** | **dashboard-GN + /game-nights + detail-RSVP** | **⏳ questo brief** | **host/player mobile** |

**SP9 = mobile parity + gap-fill del wrapper social.** Le invarianti sociali #15/#16/#17 non hanno rendering mobile; la dashboard non ha "Recenti"; la card pending-RSVP (#17) non ha forma mobile dedicata.

## Persona target & contesto d'uso

**Marco (host)** organizza la serata dal telefono: crea la GameNight, tagga i player, poi con azione esplicita **invia gli inviti**. **Anna (invitata)** riceve la notifica, apre l'app, vede la serata come **card pending "Da confermare"** e conferma l'RSVP con un tap. Contesto: one-hand, in mobilità, notifica-driven.

**Trigger reale**: "ho creato la serata di sabato, la mando ai ragazzi" (Marco) · "Marco mi ha invitato, confermo" (Anna). Senza superficie mobile per questo, il flow social è scoperto su smartphone.

## Scope SP9 — esattamente 3 mockup

| # | File | Route | Pattern | Audience |
|---|------|-------|---------|----------|
| A | `sp9-dashboard-game-night-mobile.{html,jsx}` | `/` dashboard (sezione GN, <768px) | Sezioni "Prossimi" (con card pending-RSVP inline) + "Recenti" (gap-fill) | dashboard mobile |
| B | `sp9-game-nights-index-mobile.{html,jsx}` | `/game-nights` (<768px) | List/calendar toggle + filter chip + FAB "Nuova serata" | index mobile |
| C | `sp9-game-night-detail-rsvp-mobile.{html,jsx}` | `/game-nights/[id]` (<768px) | Hero status + RSVP bar 3-button + roster + "Invia inviti" (host) | detail mobile |

**Naming**: `sp9-*` per la nuova wave mobile-social. Se la roadmap usa una numerazione SP diversa, rinominare mantenendo il suffisso `-mobile`.

## IA mobile (decisioni di design, coerenti con SP8)

Mobile-first 375px. Vincoli già decisi nel socratic SP8 (#1890) — **applicarli, non riaprirli**:

- **Shell**: `MobileBottomBar` 5-tab (dashboard · library · hub · chat · profile) **persistente**; lo slot 3 (chat) swappa a **live** quando una session è in corso (`useLiveSessionStore`). I tab in-page NON sostituiscono la bottom-nav.
- **Drawer/sheet**: bottom-sheet come disclosure primaria (vaul-style); **back-guard 1-livello** (ESC / Back Android / gesture → pop di 1 solo livello via `cascade-navigation-store`, mai close-all né uscita route). Stack drawer max 3 (event → player → stats).
- **Gesture**: long-press con **cancel-on-move ~10px**; touch-target **≥44×44px** (regressioni SP8 da non ripetere: **B-02** CTA `.hy-goto` a 24px = **CRITICO**; **A-11** search/filtri a 40px = MINORE — su "Conferma RSVP"/"Invia inviti" il minimo è 44px).
- **Wizard creazione**: NON in scope SP9 (esiste già `GameNightCreateWizard` 4-step route-driven `/game-nights/new`); il FAB in B ci deep-linka.

## Componenti già stabili — NON ridisegnare (riusare come content model)

Il mockup **istanzia** questi componenti reali, non li clona:

| Componente | Path codice | Adattamento mobile |
|------------|-------------|---------------------|
| `HomeFeed` "Serate di Gioco" | `apps/web/src/components/features/home/HomeFeed.tsx` | sezione "Prossimi" mobile + aggiungere "Recenti" (gap-fill) |
| `GameNightListCard` | `apps/web/src/components/features/game-nights/GameNightListCard.tsx` | card list 1-col mobile (status, date, location, players, CTA contestuale) |
| `GameNightDetailHero` | `apps/web/src/components/features/game-night-detail/GameNightDetailHero.tsx` | hero compatto mobile (status/title/schedule/location/organizer) |
| `GameNightRsvpActionBar` | `apps/web/src/components/features/game-night-detail/GameNightRsvpActionBar.tsx` | bar 3-button (Accepted/Maybe/Declined) sticky-bottom mobile, ≥44px |
| `GameNightRsvpRow` | `apps/web/src/components/features/game-night-detail/GameNightRsvpRow.tsx` | roster entry (Pending dashed / Accepted ✓ / Maybe ? / Declined ×) |
| `GameNightDrawerContent` | `apps/web/src/components/dashboard/GameNightDrawerContent.tsx` | contenuto drawer cascade (event → player) |
| `DayDetailDrawer` | `apps/web/src/components/features/game-nights/DayDetailDrawer.tsx` | bottom-sheet giorno (index calendar mode) |
| `MobileBottomBar` | `apps/web/src/components/layout/AppNav/MobileBottomBar.tsx` | shell 5-tab (NON ridisegnare) |
| `cascade-navigation-store` | `apps/web/src/lib/stores/cascade-navigation-store.ts` | back-guard 1-livello (semantica stack) |

**Greenfield emergenti** (poi in `apps/web/src/components/ui/v2/game-night/`):
- **Card pending-RSVP** mobile (badge "Da confermare" + Conferma/Declina inline) — copre #17
- **Sezione "Recenti"** dashboard (completed DESC) — copre #4, oggi assente in dashboard

### `entityHsl` helper inline (palette 9 entity, coerente coi mockup esistenti)

```js
const ENTITY_HSL = {
  game:    '25 95% 45%',  player:  '262 83% 58%',  session: '240 60% 55%',
  agent:   '38 92% 50%',  kb:      '174 60% 40%',   chat:    '220 80% 55%',
  event:   '350 89% 60%', toolkit: '142 70% 45%',   tool:    '195 80% 50%',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(${ENTITY_HSL[entity]} / ${alpha})` : `hsl(${ENTITY_HSL[entity]})`;
```
GameNight = **`event`** (rosa) · Session = **`session`** (indigo) · Player = **`player`** (viola).

## Vincolo dati (GitGuardian gate)

❌ UUID-like, bearer, hex ≥32 char. ✅ ID short (`gn-sat-marco`, `p-anna`, `p-marco`, `sess-1`).

### Dati realistici
- **GameNight**: "Serata da Marco · Sabato" (planned), "Giovedì Wingspan" (completed)
- **Player**: Marco (host, ✓ User), Anna (invited pending), Giulia (accepted), Davide (guest), Luca (maybe)
- **Giochi**: Azul, Wingspan, Catan, Codenames (da `data.js`)

---

## A — Dashboard GameNight mobile (`sp9-dashboard-game-night-mobile`)

**Route**: `/` dashboard, sezione GameNight (mobile <768px). Riusa `HomeFeed` "Serate di Gioco".

### Layout — Prossimi + Recenti (stack verticale, priorità #4)

```
┌─────────────────────────────────┐
│  ─── Prossimi ───                │  ← planned/published, data ASC (#4)
│  ┌───────────────────────────┐  │
│  │ 🎉 Serata da Marco · Sab  │  │  ← GameNightListCard, entity=event
│  │ 📍 Casa Marco · 4 player  │  │
│  │ [ Apri ]                  │  │
│  └───────────────────────────┘  │
│  ┌───────────────────────────┐  │
│  │ 🟡 Da confermare          │  │  ← CARD PENDING-RSVP (#17)
│  │ 🎉 Giovedì Wingspan       │  │     card semitrasparente
│  │ Marco ti ha invitato       │  │
│  │ [ Conferma ] [ Declina ]  │  │  ← inline, ≥44px
│  └───────────────────────────┘  │
├─────────────────────────────────┤
│  ─── Recenti ───   (gap-fill)   │  ← completed, data DESC (#4)
│  ┌───────────────────────────┐  │
│  │ ✅ Giovedì scorso · Azul  │  │  ← 1 card = 1 GameNight wrapper
│  │ MVP: Giulia · 3 partite   │  │
│  └───────────────────────────┘  │
│  [ Vedi tutte le completate → ] │  → /game-nights?filter=completed
└─────────────────────────────────┘
```

- **Card pending-RSVP** (#17): badge giallo "Da confermare", card semitrasparente, Conferma/Declina inline. Tap Conferma → card diventa normale.
- Priorità sezioni #4: **Prossimi** (ASC) sopra **Recenti** (DESC).
- Tap card → cascade drawer (`GameNightDrawerContent`, event → player).

### Stati richiesti
- **Default** (Prossimi con 1 planned + 1 pending, Recenti con 1 completed)
- **Empty Prossimi** ("Nessuna serata pianificata" + CTA "Crea serata" → `/game-nights/new`)
- **Empty Recenti** ("Nessuna partita ancora")
- **Loading** (skeleton 2 card, no spinner)
- **Error** (banner + retry)
- **Offline** (banner + card read-only, Conferma/Declina disabled con tip)
- **Light + dark** · **375px + 768px**

---

## B — GameNights index mobile (`sp9-game-nights-index-mobile`)

**Route**: `/game-nights` (<768px). Riusa `GameNightListCard` + `DayDetailDrawer`.

### Layout — list/calendar toggle + filter + FAB

```
┌─────────────────────────────────┐
│  Le mie serate      [Lista|📅]  │  ← view toggle
│  [Tutte] [Organizzo] [Invitato] │  ← filter chip (+ Completate)
├─────────────────────────────────┤
│  ┌───────────────────────────┐  │
│  │ 🎉 Serata da Marco · Sab  │  │  ← list 1-col
│  │ IN CORSO / DA CONFERMARE  │  │  ← status badge
│  └───────────────────────────┘  │
│  ...                             │
├─────────────────────────────────┤
│                          [＋ FAB]│  → /game-nights/new (wizard)
└─────────────────────────────────┘
```

- **Calendar mode**: griglia mese; tap giorno → `DayDetailDrawer` bottom-sheet (card di quel giorno + "aggiungi qui").
- **Filter**: Tutte / Organizzo / Invitato / Completate (chip).
- **FAB "＋ Nuova serata"** → deep-link `/game-nights/new` (wizard esistente, non ridisegnare).
- **Status badge** su card: planned / IN CORSO (in-progress #15) / completata / da-confermare (pending #17).

### Stati richiesti
- **Default list** (3-4 card mix status) · **Calendar mode** (mese + drawer giorno)
- **Empty** ("Nessuna serata" + CTA crea) · **Filtered-empty** ("Nessuna serata in questo filtro")
- **Loading** (skeleton) · **Error** · **Offline**
- **Light + dark** · **375px + 768px**

---

## C — GameNight detail + RSVP mobile (`sp9-game-night-detail-rsvp-mobile`)

**Route**: `/game-nights/[id]` (<768px). Riusa `GameNightDetailHero` + `GameNightRsvpActionBar` + `GameNightRsvpRow`. Branch per status (Draft/Published/InProgress/Completed).

### Layout — hero + roster + CTA contestuale

```
┌─────────────────────────────────┐
│  ← 🎉 Serata da Marco           │  ← Hero compatto, entity=event
│  Sab 21:00 · 📍 Casa Marco      │
│  Organizza: Marco               │  ← status badge (Published)
├─────────────────────────────────┤
│  Player (4)      [Invia inviti] │  ← HOST: CTA esplicita #16, ≥44px
│  ✓ Marco (host)                 │  ← GameNightRsvpRow
│  ⏳ Anna · Da confermare         │  ← Pending dashed (#17)
│  ✓ Giulia                       │
│  ? Luca · Forse                 │
├─────────────────────────────────┤
│  🎲 Giochi candidati: Azul...   │
├─────────────────────────────────┤
│ [ Accetto ] [ Forse ] [ No ]    │  ← RSVP bar sticky-bottom (INVITATO)
└─────────────────────────────────┘
```

- **Host view**: CTA "**Invia inviti**" (#16 — tagged→invited esplicito, entity=event rosa). Prima dell'invio, i player sono "tagged" (no notifica). Dopo, "invited" (notifica → Publish).
- **Invitato view**: `GameNightRsvpActionBar` 3-button (Accetto/Forse/No) **sticky-bottom**, ≥44px. Stato disabled se già risposto.
- **Roster** (`GameNightRsvpRow`): Pending dashed / Accepted ✓ / Maybe ? / Declined ×.
- **InProgress** (#15): badge "IN CORSO" quando la prima session è creata; CTA "Vai alla live".

### Stati richiesti
- **Draft (host, tagged)** — "Invia inviti" primario, roster tutti tagged
- **Published (host)** — roster con RSVP mix, counter
- **Published (invitato, pending)** — RSVP bar attiva
- **Published (invitato, risposto)** — RSVP bar con scelta evidenziata, editabile
- **InProgress** (#15) — badge IN CORSO + "Vai alla live"
- **Completed** — read-only + link summary
- **Loading** (skeleton hero+roster) · **Error** · **Offline** (RSVP disabled + tip)
- **Light + dark** · **375px + 768px**

---

## Invarianti da rendere visibili (marker `[INV-n]`)

| # | Dove | Cosa mostrare |
|---|------|---------------|
| #4 | A dashboard | Prossimi (ASC) sopra Recenti (DESC), ordine fisso |
| #15 | B/C | badge "IN CORSO" alla creazione della prima session (planned→in-progress) |
| #16 | C host | "tagged" (no notifica) → CTA "Invia inviti" → "invited" (notifica) |
| #17 | A/C invitato | card/roster **pending "Da confermare"** finché non RSVP; post-Conferma → normale |
| #20 | shell | bottom-nav: `hub` = Games (Discover default) + `library`; GameNight raggiunta via dashboard/hub, non 3ª voce |

Backend mapping (per coerenza, non UI): tagged = `GameNightEvent.PreInvite` · invited = `Publish` · RSVP = `GameNightRsvp.Status {Pending|Accepted|Maybe|Declined}`.

---

## Definition of Done

### Token & visual
- [ ] Solo CSS variables da `tokens.css` (zero hex hardcoded; ricorda gap SP8 A-09/B-04 scrim)
- [ ] `entityHsl` inline 9-entity; GameNight=`event` rosa, Session=`session` indigo
- [ ] Light + dark · Mobile 375px + tablet 768px

### Componenti
- [ ] Riusa `GameNightListCard` / `GameNightRsvpActionBar` / `GameNightRsvpRow` / `GameNightDetailHero` (NON ridisegnare)
- [ ] Card pending-RSVP + sezione Recenti = nuovi (greenfield v2)
- [ ] `MobileBottomBar` 5-tab persistente (slot live dinamico)

### Stati
- [ ] A: default + 2 empty + loading + error + offline
- [ ] B: list + calendar + empty + filtered-empty + loading + error + offline
- [ ] C: 6 status-branch + loading + error + offline

### A11y
- [ ] Touch-target **≥44×44px** su Conferma/Declina/Invia-inviti/RSVP-bar (NON ripetere SP8 B-02 24px)
- [ ] `role="tablist"`/`aria-selected` su view-toggle Lista|Calendar (B); `aria-pressed`/`role="radiogroup"` sui filter chip
- [ ] `role="dialog"` + focus-trap + ESC su drawer/sheet; back-guard 1-livello
- [ ] `aria-pressed` su RSVP bar; live-region su skeleton→content
- [ ] `prefers-reduced-motion` su sheet/long-press

### Dati
- [ ] Testo UI italiano · Marco/Anna/Giulia/Davide/Luca · giochi reali da data.js
- [ ] NO UUID-like, NO bearer

---

## File di riferimento da allegare in chat Claude Design

**Obbligatori (preambolo)**: `_common.md` · questo brief · `tokens.css` · `components.css` · `data.js`
**Base content-model**: `sp4-dashboard.html` (sezione Events/Serate) · `sp7-game-night-transition.html` (pattern game-night visivo)
**Reference mobile SP8** (pattern bottom-sheet/long-press/back-guard/bottom-nav): `primitive-nav-bottom-mobile.html` (MobileBottomBar) · `mobile-app.jsx` (shell). Nota: `sp4-library-mobile.{html,jsx}` è stato migrato a Storybook e **rimosso** da design_files (#2988) — recuperabile via `git show 56668f4b3:admin-mockups/design_files/sp4-library-mobile.html` se serve il pattern completo.

## Risposta attesa nel thread Claude Design

1. Conferma scope SP9 (3 mockup mobile-first, riuso componenti FE v2, gap-fill pending-RSVP + Recenti)
2. Genera i 3 mockup completi (HTML + JSX), uno per volta: A dashboard → B index → C detail-RSVP
3. Path: `admin-mockups/design_files/sp9-*-mobile.{html,jsx}`
4. Note finali: deviazioni + nuovi componenti v2 + invarianti `[INV-n]` rese visibili

## Note finali per Claude Design

**Tono UI**: warm, casual, italiano informale ("Marco ti ha invitato", "Da confermare").
**Microcopy hint**: pending "Da confermare · Marco ti ha invitato" · empty Prossimi "Nessuna serata — creane una!" · invia inviti "Invia inviti a 4 player".
**Mobile-first è canonical**: 375px primario, 768px adaptation. Il flow è **social wrapper** (chi viene, chi conferma), NON la live-session immersiva (quella è la wave successiva).
