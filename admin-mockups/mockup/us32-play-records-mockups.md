# US-32 Play Records — Mobile-First Mockup

> Design system: dark gaming theme • MeepleCard • GradientButton • MobileHeader • font-quicksand
> Entry points: Dashboard → "Partite recenti" • Library card → "Storico" tab

---

## PANEL REVIEW (sc:spec-panel discussion)

**ALISTAIR COCKBURN** — Primary actor: utente free che vuole rivedere le partite giocate o registrarne una nuova manualmente.
Goal-chain: vedo cosa ho giocato → filtro per gioco → vedo il dettaglio → vedo le mie stat.
Entry point critico: deve essere raggiungibile dalla Dashboard in 1 tap.

**KARL WIEGERS** — Requisiti mancanti nell'attuale implementazione:
1. Testi in italiano (attualmente: "Play History", "Player Statistics", "New Session")
2. Mobile header con back-nav — attuale: container/p-6 desktop-only
3. CTA "Nuova partita" deve essere bottom sheet (non pagina separata su mobile)
4. Stats page deve avere KPI numerici in evidenza prima dei grafici

**GOJKO ADZIC** — Scenari concreti:
```
Given: utente sulla Dashboard
When: tap su card "Ultima partita" o sezione "Partite"
Then: arriva su /play-records con lista filtrata per recente

Given: utente sulla library card di un gioco
When: tap tab "Storico"
Then: arriva su /play-records?gameId=xxx filtrata per quel gioco

Given: utente su /play-records
When: tap "+" FAB o bottom sheet
Then: apre new-play-record con gioco pre-selezionato (se via library)
```

---

## PAGE 1 — /play-records (Lista)

```
┌─────────────────────────────────────┐
│ ← [back]     Partite Giocate    [⚙] │  ← MobileHeader
│─────────────────────────────────────│
│  🔍 [Cerca per gioco o giocatore…]  │
│─────────────────────────────────────│
│  [Tutti ▾] [Stato ▾] [Data ▾]  [×] │  ← filter chips, × se filtri attivi
│─────────────────────────────────────│
│                                     │
│  ┌─────────────────────────────┐    │
│  │ 🎲 Puerto Rico              │    │  ← MeepleCard entity="session" variant="list"
│  │ 3 giocatori • 14 mar 2026   │    │
│  │ ✅ Completata • 2h 15min    │    │
│  │ 🏆 Aaron (42 pts)           │    │  ← winner badge
│  └─────────────────────────────┘    │
│                                     │
│  ┌─────────────────────────────┐    │
│  │ 🎲 Wingspan                 │    │
│  │ 2 giocatori • 10 mar 2026   │    │
│  │ 🔄 In corso                 │    │  ← status badge verde-pulsante
│  └─────────────────────────────┘    │
│                                     │
│  ┌─────────────────────────────┐    │
│  │ 🎲 Catan                    │    │
│  │ 4 giocatori • 5 mar 2026    │    │
│  │ 📅 Pianificata              │    │
│  └─────────────────────────────┘    │
│                                     │
│          [Carica altro…]            │
│                                     │
│─────────────────────────────────────│
│  [🏠] [📚] [🎮] [📊] [👤]         │  ← MobileBottomBar (app nav, NON SessionBottomNav)
│─────────────────────────────────────│
│       [+ Registra partita]          │  ← GradientButton sticky above nav
└─────────────────────────────────────┘

EMPTY STATE (nessuna partita):
┌─────────────────────────────────────┐
│         🎲                          │
│  Non hai ancora registrato          │
│  nessuna partita                    │
│                                     │
│  [+ Registra la prima partita]      │  ← GradientButton
└─────────────────────────────────────┘
```

**Componenti da aggiornare:**
- `play-records/page.tsx` → remove container/p-6, add `MobileHeader` + sticky GradientButton
- `PlayHistory.tsx` → refactor card list con `MeepleCard variant="list"`, testi IT
- Filter chips → nuovo componente `PlayRecordFilters.tsx` (horizontal scroll chips)
- Aggiungere `data-testid` su ogni elemento interattivo

---

## PAGE 2 — /play-records/new (Form multi-step → Bottom Sheet su mobile)

> **Decisione architetturale**: su mobile la creazione avviene tramite BottomSheet a 3 step
> (analoga a StartSessionSheet). Su desktop rimane la pagina full.

```
STEP 1 — Gioco
┌─────────────────────────────────────┐
│ ╳  [1] Gioco  [2] Giocatori  [3] → │  ← step indicator
│─────────────────────────────────────│
│  Quale gioco hai giocato?           │
│                                     │
│  [🔍 Cerca nella tua libreria…]     │
│                                     │
│  Recenti:                           │
│  ┌──────────────────────────────┐   │
│  │ 🎲 Puerto Rico              │   │  ← tappable chip / mini card
│  │ 🎲 Wingspan                 │   │
│  │ 🎲 Catan                    │   │
│  └──────────────────────────────┘   │
│                                     │
│  📅 Data: [14 aprile 2026 ▾]        │
│  👁 Visibilità: [Privata ▾]         │
│                                     │
│  [Continua →]                       │  ← GradientButton
└─────────────────────────────────────┘

STEP 2 — Giocatori & Punteggi
┌─────────────────────────────────────┐
│ ← [1✓] Gioc.  [2] Giocatori  [3] → │
│─────────────────────────────────────│
│  Puerto Rico • 14 apr 2026          │
│─────────────────────────────────────│
│  ┌─────────────────────────────┐   │
│  │ 🔴 Aaron          [42 pts] │   │  ← player row con inline score
│  │ 🔵 Marco          [38 pts] │   │
│  │ 🟢 Giulia         [35 pts] │   │
│  └─────────────────────────────┘   │
│                                     │
│  [+ Aggiungi giocatore]             │
│                                     │
│  🏆 Vincitore: Aaron (auto)         │  ← derivato dal punteggio più alto
│                                     │
│  [Continua →]                       │
└─────────────────────────────────────┘

STEP 3 — Riepilogo
┌─────────────────────────────────────┐
│ ← [1✓] [2✓] Gioc.  [3] Riepilogo  │
│─────────────────────────────────────│
│  Puerto Rico                        │
│  14 aprile 2026 • 3 giocatori       │
│                                     │
│  🏆 Aaron          42 pts           │
│     Marco          38 pts           │
│     Giulia         35 pts           │
│                                     │
│  📝 Note (opzionale):               │
│  [Aggiungi una nota…        ]       │
│                                     │
│  ⏱ Durata (opzionale):             │
│  [2h 15min                  ]       │
│                                     │
│  [▶ Salva Partita]                  │  ← GradientButton
└─────────────────────────────────────┘
```

**Componenti da aggiornare:**
- `play-records/new/page.tsx` → wrapper che su mobile renderizza `NewPlayRecordSheet`
- `SessionCreateForm.tsx` → refactor in 3 step con step-indicator (pattern StartSessionSheet)
- `GameCombobox.tsx` → aggiungere lista "Recenti" dalla libreria
- `PlayerManager.tsx` → inline score input per step 2

---

## PAGE 3 — /play-records/[id] (Dettaglio)

```
┌─────────────────────────────────────┐
│ ←          Puerto Rico          [✎] │  ← MobileHeader + edit icon (solo se owner)
│─────────────────────────────────────│
│  ┌─────────────────────────────┐    │
│  │ 🎲                         │    │  ← MeepleCard entity="session" variant="hero"
│  │ Puerto Rico                 │    │
│  │ 14 aprile 2026              │    │
│  │ ✅ Completata • 2h 15min   │    │
│  └─────────────────────────────┘    │
│                                     │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│  CLASSIFICA                         │
│                                     │
│  🥇  Aaron          42 pts  +4 ★    │  ← rank + pts + delta (se disponibile)
│  🥈  Marco          38 pts          │
│  🥉  Giulia         35 pts          │
│                                     │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│  DETTAGLI PUNTEGGIO                 │  ← collapsible section
│                                     │
│  [Tab: Shipping | Trading | …]      │  ← se il gioco ha score dimensions
│                                     │
│  Aaron   🚢12  💱8  🌾10  🏗12      │
│  Marco   🚢10  💱9  🌾8   🏗11      │
│  Giulia  🚢8   💱7  🌾12  🏗8       │
│                                     │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│  📝 NOTE                            │
│  "Prima partita di Aaron. Ottimo    │
│   risultato per un principiante!"   │
│                                     │
│─────────────────────────────────────│
│  [🗑 Elimina]   [↗ Condividi]       │  ← bottom action row (owner only)
└─────────────────────────────────────┘
```

**Componenti da aggiornare:**
- `play-records/[id]/page.tsx` → add `MobileHeader`, rimuovi `Card` wrapper, layout dark
- `ScoringInterface.tsx` → refactor come tabella collapsible con score dimensions
- `PlayerManager.tsx` → refactor come classifica (rank icons + pts)
- Aggiungere section "Note" con textarea collapsed

---

## PAGE 4 — /play-records/stats (Statistiche)

```
┌─────────────────────────────────────┐
│ ←         Le mie statistiche       │  ← MobileHeader
│─────────────────────────────────────│
│                                     │
│  KPI STRIP (scroll orizzontale)     │
│  ┌────┐ ┌────┐ ┌────┐ ┌────┐       │
│  │ 47 │ │ 12 │ │68% │ │ 8  │       │
│  │Part│ │Gioc│ │Win%│ │Fav │       │
│  │ite │ │ati │ │    │ │Gco │       │
│  └────┘ └────┘ └────┘ └────┘       │
│                                     │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│  GIOCHI PIÙ GIOCATI                 │
│                                     │
│  1. Puerto Rico      12x  ████████ │
│  2. Wingspan          8x  █████    │
│  3. Catan             6x  ████     │
│  4. Azul              5x  ███      │
│  5. Scythe            4x  ███      │
│                                     │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│  ANDAMENTO ULTIME 12 SETTIMANE      │
│                                     │
│  [mini bar chart — partite/sett.]   │
│  ▂▃▅▂▄▇▃▂▅▆▄▃                       │
│                                     │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│  WIN RATE PER GIOCO                 │
│                                     │
│  Puerto Rico   🏆 58%  ████████     │
│  Wingspan      🏆 50%  ███████      │
│  Catan         🏆 33%  █████        │
│                                     │
│─────────────────────────────────────│
│  [🏠] [📚] [🎮] [📊] [👤]         │
└─────────────────────────────────────┘
```

**Componenti da aggiornare:**
- `play-records/stats/page.tsx` → add `MobileHeader`, remove container/p-6
- `PlayerStatistics.tsx` → refactor: KPI strip in cima, barchart semplice, win rate list
- KPI Strip → nuovo componente `StatsKpiStrip.tsx` (horizontal scroll, stile KpiStrip dashboard)

---

## ENTRY POINTS DA AGGIORNARE

### Dashboard → Sezione "Partite"
```
┌─────────────────────────────────────┐
│  PARTITE RECENTI              [→]   │  ← link a /play-records
│                                     │
│  ┌──────────────┐ ┌──────────────┐  │
│  │ Puerto Rico  │ │ Wingspan     │  │  ← MeepleCard compact
│  │ 14 mar • 🥇  │ │ 10 mar • 🔄 │  │
│  └──────────────┘ └──────────────┘  │
└─────────────────────────────────────┘
```

### Library Game Card → Tab "Storico"
```
Aggiungere tab "Storico" in GameDetailsDrawer:
→ renderizza <PlayHistory gameId={gameId} limit={5} />
→ CTA "Tutte le partite →" link a /play-records?gameId=xxx
→ CTA "+ Registra partita" → apre NewPlayRecordSheet con gioco pre-selezionato
```

---

## DECISION LOG (spec-panel consensus)

| Decisione | Motivazione |
|-----------|-------------|
| BottomSheet per /new su mobile | Coerenza con StartSessionSheet; meno navigazione |
| Step 3 con note+durata opzionali | Riduce attrito per quick-entry; dati non critici |
| Winner derivato automaticamente da max score | UX: evita scelta manuale; override possibile in edit |
| KPI Strip prima dei grafici | Mobile: il dato numerico è più leggibile di un grafico piccolo |
| Testi in italiano ovunque | Coerenza con il resto dell'app (già in IT) |
| `MeepleCard entity="session"` | Standardizzazione; evita componenti custom per card |

---

## SCOPE IMPLEMENTAZIONE

### File da modificare (refactor)
1. `apps/web/src/app/(authenticated)/play-records/page.tsx`
2. `apps/web/src/app/(authenticated)/play-records/new/page.tsx`
3. `apps/web/src/app/(authenticated)/play-records/[id]/page.tsx`
4. `apps/web/src/app/(authenticated)/play-records/stats/page.tsx`
5. `apps/web/src/components/play-records/PlayHistory.tsx`
6. `apps/web/src/components/play-records/SessionCreateForm.tsx`
7. `apps/web/src/components/play-records/PlayerStatistics.tsx`

### File da creare (nuovi)
1. `apps/web/src/components/play-records/NewPlayRecordSheet.tsx` (BottomSheet 3-step)
2. `apps/web/src/components/play-records/PlayRecordFilters.tsx` (filter chips)
3. `apps/web/src/components/play-records/StatsKpiStrip.tsx`

### Entry points da toccare
4. `apps/web/src/components/game-detail/mobile/GameDetailsDrawer.tsx` (+ tab Storico)
5. Dashboard → `ContinueCarousel` o sezione dedicata

---

*Mockup creato: 2026-04-12 | Pronto per implementazione*
