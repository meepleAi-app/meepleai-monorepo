# MeepleAI — User-Facing Pages Specification

**Data**: 2026-04-11  
**Stato**: Approvata  
**Fonte**: mockup `admin-mockups/meeple-card-drawer-tabs-mockup.html`, `mobile-card-layout-mockup.html`, `mobile-card-entity-types.html`, `meeple-card-summary-render.html`  
**Criteri di accettazione**: UI identica ai mockup sopra

---

## 1. Bottom Action Bar (Mobile — `MobileBottomBar`)

### Normal Mode

5 tab con icona + label, `md:hidden`:

| # | Label | Lucide Icon | Route | Active color |
|---|---|---|---|---|
| 1 | Home | `Home` | `/dashboard` | `hsl(25,95%,45%)` |
| 2 | Collection | `Library` | `/library` | `hsl(25,95%,45%)` |
| 3 | Agent Chat | `MessageCircle` | `/agents` | `hsl(25,95%,45%)` |
| 4 | Sessions | `Dices` | `/sessions` | `hsl(25,95%,45%)` |
| 5 | Toolkit | `Wrench` | `/toolkit` | `hsl(25,95%,45%)` |

> **Profilo**: spostato nell'avatar del TopBar (non nel bottom nav)  
> **Cerca**: integrata in ogni sezione (nessuna pagina `/discover` separata)

### Session Mode (invariato, già implementato)

```
◀ Back | 📊 Classifica | 🧰 Toolkit | 💬 AI (disabled) | ⋯ Altro
border-top: 2px solid hsl(240,60%,55%)
```

---

## 2. Layout Globale

```
┌─ TopBar (56px) ─────────────────────────────────────────┐
│  🎲 MeepleAI    [spacer]    🔍   🔔   [Avatar → profilo]│
└─────────────────────────────────────────────────────────┘
┌─ MiniNav (40px) — solo entity detail pages ─────────────┐
│  › Entity / Title  [Tab1] [Tab2] [Tab3]  ···  [⊙][⊙][⊙]│
│                    (entity tabs)          (recents pills) │
└─────────────────────────────────────────────────────────┘
┌─ [SessionBanner — md: only, session mode only] ─────────┐
│  ● Sessione: Serata Azul · T3/5  [📊][🧰][💬]  [✕]     │
└─────────────────────────────────────────────────────────┘
┌─ Content Area (flex:1, overflow-y:auto) ────────────────┐
│  [page-specific content]                                 │
└─────────────────────────────────────────────────────────┘
┌─ MobileBottomBar (56px, fixed) — md:hidden ─────────────┐
│  🏠 Home  📚 Collection  💬 Agents  🎯 Sessions  🔧 Toolkit│
└─────────────────────────────────────────────────────────┘
```

---

## 3. Dashboard Page (`/dashboard`)

### Ricerca
- Campo integrato nella pagina (non route separata)
- Scope: **globale** — tutte le entity types (game, session, agent, kb, chatSession, event, toolkit, player)
- Placeholder: "Cerca giochi, sessioni, agenti…"

### Layout Mobile
- **Hand-stack** a sinistra (36px) con le card recenti
- **Focused card** centrale (entità selezionata)
- Ogni sezione mantiene il proprio state di view mode

### 4 Sezioni

| # | Titolo sezione | Entity type | Ordinamento | Max | View modes |
|---|---|---|---|---|---|
| 1 | 🎲 I tuoi giochi | `game` (libreria) | `addedToLibraryAt DESC` | 8 | grid, list, carousel |
| 2 | 🎯 Sessioni | `session` | `createdAt DESC` | 6 | grid, list, carousel |
| 3 | 🌐 Giochi condivisi | `sharedGame` | `lastViewedAt DESC` | 8 | grid, list, carousel |
| 4 | 💬 Chat | `chatSession` | `createdAt DESC` | 6 | list, carousel |

**Visibilità sezione**: nascosta se `count === 0` (stato vuoto non mostrato).

**View mode toggle**: icone per ogni sezione, stato persistito in `localStorage` per sezione.

**Card variant per sezione**:
- Desktop: `variant=featured` (16:9) o `variant=grid` (7:10) a seconda della larghezza
- Mobile (hand-stack): focused card con `variant=grid` + card laterali in formato compatto

---

## 4. Collection Page (`/library`)

### Ricerca
- Scope: **libreria personale + shared games catalog**
- Non ricerca in sessioni, agenti, chat

### Sub-navigazione (tabs inline)
- Tutti | Posseduti | Wishlist | Shared Games | Playlist

### Card
- `MeepleCard entity=game` con status badge (`owned`, `wishlisted`, `borrowed`, `for-trade`)
- View modes: grid, list

---

## 5. Agent Chat Page (`/agents`)

### Struttura
- Lista agenti disponibili (card `entity=agent`)
- Click su agente → `/agents/[id]` (entity detail page) con drawer tab `Overview` + footer `💬 Chat`
- Chat avviene tramite apertura drawer o navigazione a chatSession

### Ricerca
- Scope: nome agente, gioco associato, contenuto chat recenti

### Card
- `MeepleCard entity=agent` — colore `hsl(38,92%,50%)`

---

## 6. Sessions Page (`/sessions`)

### Struttura
- Lista sessioni per stato: `in corso`, `in pausa`, `completate`
- Filtro per stato (tabs o chip)

### Ricerca
- Scope: nome sessione, gioco, giocatori

### Card
- `MeepleCard entity=session` — colore `hsl(240,60%,55%)`
- Badge stato: `In Corso` (verde), `In Pausa` (ambra), `Completata` (grigio)
- Action contestuale: `▶️ Riprendi` / `📊 Risultati`

---

## 7. Toolkit Page (`/toolkit`)

### Struttura
- Lista toolkit pubblicati + propri
- Sub-tab: Tutti | Miei | Preferiti

### Ricerca
- Scope: nome toolkit, gioco, tool contenuti

### Card
- `MeepleCard entity=toolkit` — colore `hsl(142,70%,45%)`

---

## 8. Entity Detail Pages (template condiviso)

### Route map completa

| Route | Entity | Colore HSL |
|---|---|---|
| `/games/[id]` | game | `hsl(25,95%,45%)` |
| `/sessions/[id]` | session | `hsl(240,60%,55%)` |
| `/agents/[id]` | agent | `hsl(38,92%,50%)` |
| `/knowledge-base/[id]` | kb | `hsl(174,60%,40%)` |
| `/chat/[id]` | chatSession | `hsl(220,80%,55%)` |
| `/toolkit/[id]` | toolkit | `hsl(142,70%,45%)` |
| `/players/[id]` | player | `hsl(262,83%,58%)` |
| `/game-nights/[id]` | event | `hsl(350,89%,60%)` |
| `/toolkit/tools/[id]` | tool | `hsl(195,80%,50%)` |

### Layout Hero (sopra il fold)

```
┌─ Hero (cover gradient + entity-badge + rating?) ────────┐
│  [cover gradient entity-colored]                         │
│  [ENTITY BADGE top-left]          [⭐ rating top-right]  │
└─────────────────────────────────────────────────────────┘
┌─ Info (titolo + subtitle + meta chips) ─────────────────┐
│  Titolo entità                                           │
│  Subtitle (publisher · anno o equivalente)               │
│  [chip 1] [chip 2] [chip 3]                              │
└─────────────────────────────────────────────────────────┘
┌─ ConnectionBar (pips entity correlate) ─────────────────┐
│  Collegati:  🤖×1  📄×3  💬×5  🎯×23                    │
└─────────────────────────────────────────────────────────┘
┌─ QuickActions (CTA primari) ────────────────────────────┐
│  [▶️ Gioca]  [🤖 Chiedi AI]                              │
└─────────────────────────────────────────────────────────┘
```

### MiniNav tabs per entity

| Entity | Tab 1 | Tab 2 | Tab 3 | Badge |
|---|---|---|---|---|
| game | ℹ️ Info | 📊 Stats* | 📜 Storico | Stats = `totalPlays` |
| player | 👤 Profilo | 🏆 Stats | 📜 Storico | Stats = `totalWins`, Storico = `totalSessions` |
| session | 📋 Live | 🔧 Toolkit** | 🕐 Timeline | Toolkit = `toolCount` |
| agent | 🤖 Overview | 📜 Storico | ⚙️ Config | Storico = `invocationCount` |
| kb | 📄 Overview | 👁️ Anteprima | 💬 Citazioni | — |
| chat | 💬 Messaggi | 📄 Fonti | — | Messaggi = `msgCount` |
| event | 📅 Overview | 🎲 Programma | — | Programma = `gameCount` |
| toolkit | 🧰 Overview | 📐 Template | 📜 Storico | Storico = `useCount` |
| tool | 🔧 Dettaglio | ▶️ Preview | — | — |

*Stats game: **nascosto** se `totalPlays === 0`  
**Toolkit session: **nascosto** se `toolCount === 0`

### Drawer Footer actions per entity

| Entity | Azione primaria | Azioni secondarie |
|---|---|---|
| game | ▶️ Gioca (sempre) · 🤖 Chiedi AI (se agent attivo) | ↗️ Apri |
| player | — | 📊 Confronta (se match comuni) · ↗️ Apri |
| session | ▶️ Riprendi (in corso/pausa) · 📊 Risultati (completata) | ↗️ Apri |
| agent | 💬 Chat (se attivo+KB) · 🔄 Riavvia (se errore/idle) | ↗️ Apri |
| kb | 🔄 Reindex · ⬇️ Download | ↗️ Apri |
| chat | 💬 Continua (attiva) · 📦 Archivia (attiva) · 🔓 Riapri (archiviata) | ↗️ Apri |
| event | ✅ Conferma / ❌ Declina · 📤 Invita (se organizzatore) | ↗️ Apri |
| toolkit | ▶️ Usa (se pubblicato) · ✏️ Modifica (se owner) | ↗️ Apri |
| tool | ▶️ Usa (se in sessione attiva) · ✏️ Modifica (se owner) | ↗️ Apri |

---

## 9. Sistema Colori Entità (Design Tokens)

```css
/* Da meeple-card-summary-render.html */
--e-game:    hsl(25,  95%, 45%);   /* arancione */
--e-player:  hsl(262, 83%, 58%);  /* viola */
--e-session: hsl(240, 60%, 55%);  /* blu indaco */
--e-agent:   hsl(38,  92%, 50%);  /* ambra */
--e-doc:     hsl(210, 40%, 55%);  /* grigio-blu (document) */
--e-chat:    hsl(220, 80%, 55%);  /* blu brillante (chatSession) */
--e-event:   hsl(350, 89%, 60%);  /* rosa/rosso */
--e-toolkit: hsl(142, 70%, 45%);  /* verde */
--e-kb:      hsl(174, 60%, 40%);  /* teal (knowledge base) */
--e-tool:    hsl(195, 80%, 50%);  /* ciano */
--e-custom:  hsl(220, 70%, 50%);  /* blu (custom) */
```

Usati per: border-accent, entity-badge, glow ring on hover, gradient overlay, drawer header, pip ConnectionBar.

---

## 10. MeepleCard Variants per contesto

| Contesto | Variant | Aspect ratio |
|---|---|---|
| Dashboard sections (desktop) | `featured` | 16:9 |
| Dashboard sections (mobile, hand-stack focused) | `grid` | 7:10 |
| Collection, Sessions, Toolkit list (grid view) | `grid` | 7:10 |
| Collection, Sessions list (list view) | `list` | 56px thumb |
| Dashboard carousel, quick preview | `compact` | dot only |
| Entity page hero | `hero` | full-bleed min 280px |

---

## 11. Comportamento Ricerca

| Pagina | Scope ricerca |
|---|---|
| Dashboard `/dashboard` | **Globale** — game, session, agent, kb, chatSession, event, toolkit, player |
| Collection `/library` | libreria personale + shared games catalog |
| Agents `/agents` | nome agente, gioco associato, chat recenti |
| Sessions `/sessions` | nome sessione, gioco, giocatori |
| Toolkit `/toolkit` | nome toolkit, gioco, tool contenuti |
| Entity detail `/[entity]/[id]` | contenuto specifico dell'entità (nessuna search globale) |

Implementazione: search bar **integrata nella pagina** (non route `/search` separata), con debounce 300ms e risultati inline.

---

## 12. Criteri di Accettazione (DoD)

- [ ] Bottom nav mostra esattamente: Home, Collection, Agent Chat, Sessions, Toolkit
- [ ] Profilo accessibile dall'avatar nel TopBar
- [ ] Dashboard mostra le 4 sezioni con card corrette e view mode toggle
- [ ] Dashboard search scope = globale
- [ ] Collection search scope = libreria + shared games
- [ ] Entity detail pages hanno MiniNav con tabs corretti per ogni tipo
- [ ] Drawer per ogni entity ha tabs e footer actions come da tabelle §8
- [ ] Colori entità matching design tokens §9
- [ ] Mobile usa hand-stack layout (non grid puro)
- [ ] Session mode bottom bar = Back/Classifica/Toolkit/AI/Altro con bordo indaco
- [ ] SessionBanner visibile solo `md:` breakpoint in session mode
