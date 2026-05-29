# SP8 Mobile Parity — Brief Claude Design: Library Mobile (1 mockup greenfield)

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.
> Questo brief introduce la **variante mobile-first** del route `/library`. Il mockup desktop `sp4-library-desktop.html` esiste (SP4, PR #574) ma è **desktop-only** (zero `@media`/`phone`/`375px`). SP8 colma il gap mobile.

## Stato programma

| SP | PR | Stato | Audience |
|----|----|-------|----------|
| SP4 entity-desktop (library) | merged #574 | ✅ desktop | utenti autenticati |
| **SP8 mobile-parity (library)** | **(questo brief)** | ⏳ in design | **utente mobile-first** |

**SP8 = mobile parity, NON nuova feature.** `/library` ha già il content model definito (LibraryHero + LibraryTabs + LibraryHybridGrid + BulkSelectionBar + RecentActivityRail). Questo brief produce la **variante mobile 375px-first** con IA semplificata, riusando lo stesso content model.

## Persona target & contesto d'uso

**Utente autenticato su smartphone** (Aaron/Sara family). Apre l'app durante una serata di gioco o in mobilità per consultare/aprire rapidamente un'entità della sua libreria.

**Trigger reale** (dal flow companion): *"con lo smartphone seleziona il gioco dalla libreria"* → la library mobile è lo **step #1** del flow companion libro-game. Senza variante mobile-first, lo step è scoperto.

**Contesto**: one-hand use, schermo piccolo, possibili momenti offline, navigazione veloce.

## Scope SP8 — esattamente 1 mockup

| # | File | Route | Pattern | Audience |
|---|------|-------|---------|----------|
| A | `sp4-library-mobile.{html,jsx}` | `/library` (mobile <768px) | Hero compatto + tab semplificate + grid 1-col + bulk long-press | Library mobile |

**Naming**: `sp4-library-mobile` (resta scope SP4-entity in naming, è la variante mobile del `sp4-library-desktop` esistente — coerente con convenzione `_common.md`).

## IA semplificata (decisione di design)

Mobile NON è il desktop reflowed. IA prioritizzata 80/20:

- **3 tab primarie top**: Games + Sessions + Chat (high-frequency)
- **Overflow "Più"** (kebab/menu): Agents + KB (low-frequency)
- **Hero compatto** (no full-width gradient desktop)
- **Bulk-select via long-press** (no top-sticky checkbox)
- **Recent activity** = sezione "Recente" sotto la griglia (NON rail sticky)

**Trade-off accettato**: 2 tap per Agents/KB invece di 1. Razionale: mobile real-estate + frequency-of-use.

**Out of scope**: ridisegno bottom-nav globale dell'app (decisione IA cross-route separata).

## Componenti già stabili — NON ridisegnare

| Componente | Path codice | Adattamento mobile |
|------------|-------------|---------------------|
| `MeepleCard` | `apps/web/src/components/ui/data-display/meeple-card/` | `variant="list"` 1-col (esistente) |
| `LibraryTabs` | `apps/web/src/components/features/library/LibraryTabs.tsx` | + `overflowItems` prop (Agents/KB) |
| `LibraryHybridGrid` | `apps/web/src/components/features/library/LibraryHybridGrid.tsx` | forza `columns=1` mobile |
| `BulkSelectionBar` | `apps/web/src/components/features/library/BulkSelectionBar.tsx` | variant FAB + bottom-sheet (no top-sticky) |
| `RecentActivityRail` | `apps/web/src/components/features/library/RecentActivityRail.tsx` | render come sezione, drop `position:sticky` |
| `AdvancedFiltersDrawer` | `apps/web/src/components/features/library/AdvancedFiltersDrawer.tsx` | anchor=bottom, 80vh, swipe-down dismiss |
| `MobileBottomBar` | `apps/web/src/components/layout/` | shell mobile (5-tab nav) |

**Greenfield emergenti** (poi in `apps/web/src/components/ui/v2/library/`):
- Hero compatto mobile (sostituto di `LibraryHeroDesktop`, ~64-96px)
- `useLongPress` hook primitive
- Bottom-sheet bulk-actions panel

### `entityHsl` helper inline

```js
const ENTITY_HSL = {
  game:    '25 95% 45%',  player:  '262 83% 58%',  session: '240 60% 55%',
  agent:   '38 92% 50%',  kb:      '174 60% 40%',   chat:    '220 80% 55%',
  event:   '350 89% 60%', toolkit: '142 70% 45%',   tool:    '195 80% 50%',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(${ENTITY_HSL[entity]} / ${alpha})` : `hsl(${ENTITY_HSL[entity]})`;
```

## Vincolo dati (GitGuardian gate)

❌ UUID-like, bearer, hex ≥32 char.
✅ ID short (`g-catan`, `g-wingspan`, `p-marco`, `sess-sat-3`).

### Dati realistici da usare

- **Giochi**: Azul, I Coloni di Catan, Wingspan, Brass: Birmingham, Carcassonne, 7 Wonders (da `data.js`)
- **Player names**: Marco, Giulia, Davide, Luca
- **Recent activity**: "Hai aperto Catan 2h fa", "Sessione Wingspan in corso", "Chat con Maestro Catan"

---

## A — Library Mobile (`sp4-library-mobile`)

**File**: `sp4-library-mobile.{html,jsx}`
**Route**: `/library` (breakpoint mobile <768px)
**Pattern**: Hero compatto + tab semplificate + grid 1-col + sezione recente. Variante mobile-first di `sp4-library-desktop.html`.

### Header (sticky)

```
┌─────────────────────────────────┐
│  [Logo]  La mia libreria   ☰    │  ← hero compatto (no gradient full)
├─────────────────────────────────┤
│ ▸ Games   Sessions   Chat   …   │  ← 3 tab + "…" overflow
├─────────────────────────────────┤
│ 🔍 Cerca…              [filtri] │  ← search (scope=tab) + filter chip
└─────────────────────────────────┘
```

- Tab attiva: count badge (es. "Games 47"), highlight `entityHsl('game')`
- Overflow "…" → menu: Agents · KB (con dot indicator se nuove entries)
- `[filtri]` → apre `AdvancedFiltersDrawer` bottom-sheet 80vh

### Body — grid + sezione recente

```
┌─────────────────────────────────┐
│ Filtri attivi: [×Tutti]         │
├─────────────────────────────────┤
│ ┌────┐ Azul · Plan B · 2017     │  ← MeepleCard variant="list"
│ │img │ ★ 7.8 · 2-4 giocatori    │     entity="game"
│ └────┘                           │
├─────────────────────────────────┤
│ ┌────┐ I Coloni di Catan        │
│ └────┘ ...                       │
├─────────────────────────────────┤
│   [carica altri · 12 di 47]     │  ← infinite scroll
├─────────────────────────────────┤
│ ──── Recente ────                │  ← sezione (non sticky)
│ Hai aperto Catan 2h fa  →        │
│ Sessione Wingspan in corso →     │
└─────────────────────────────────┘
```

- MeepleCard `variant="list"` (image 64-80px left)
- Infinite scroll page-size 12
- Sezione "Recente": max 3-5 cross-entity items (riusa `RecentActivityRail` data hook)
- Filter chips attivi sopra lista, tap rimuove

### Bulk-select (long-press 500ms)

```
┌─────────────────────────────────┐
│  ← Annulla   3 selezionati   ⋮  │  ← top bar sostituisce header
├─────────────────────────────────┤
│ ▣ Azul                          │  ← checkbox visibili
│ ▢ I Coloni di Catan             │
│ ▣ Brass: Birmingham             │
└─────────────────────────────────┘
                              [⋮ FAB] ← bottom-right
                  actions: Archivia · Tag · Esporta (bottom-sheet)
```

### Stati richiesti (Lisa-5 per tab Games, 1-2 esempi per altre)

- **Default** (tab Games, 8+ card list + sezione recente)
- **Empty** (illustrazione entity-themed + "Nessun gioco ancora" + CTA "Aggiungi gioco" → `/library/add` + secondario "Scopri shared" → `/shared-games`)
- **Loading** (skeleton 3 card + hero/tab visibili, NO spinner)
- **Error** (banner "Impossibile caricare la libreria" + retry + "Mostra cache" se disponibile — riusa pattern GamebookErrorBanner)
- **Permission denied** (per shared library privata: "Non hai accesso" + "Chiedi un invito")
- **Offline** (top banner "Modalità offline — dati cache potrebbero essere obsoleti" + tab read-only)
- **Bulk-select mode** (3 selezionati + FAB bottom-sheet actions)
- **Overflow "Più" aperto** (menu Agents/KB)
- **AdvancedFiltersDrawer** (bottom-sheet 80vh aperto)
- **Filtered empty** ("Nessun risultato per questi filtri" + "Resetta filtri")
- **Light + dark**
- **Mobile 375px + tablet 768px** (verifica reflow al breakpoint)

### Deviazioni accettate

- Long-press feedback (haptic + scale) = visual placeholder, rispetta `prefers-reduced-motion`
- Infinite scroll = mockup mostra stato finale + 1 intermedio (12/47)
- Sezione "Recente" = stessa data del RecentActivityRail desktop, layout diverso (no sticky)

---

## Definition of Done

### Token & visual
- [ ] Solo CSS variables da `tokens.css` (zero hex hardcoded)
- [ ] `entityHsl` inline per 9 entity color
- [ ] Light + dark entrambi
- [ ] Mobile 375px (canonical) + tablet 768px

### Componenti
- [ ] EntityChip/Pip per ogni reference
- [ ] MeepleCard `variant="list"` riusato (NON ridisegnare)
- [ ] LibraryTabs esteso con overflow (non nuovo componente)
- [ ] BulkSelectionBar variant mobile FAB

### Stati
- [ ] Default + Empty + Loading + Error per tab Games
- [ ] Permission + Offline + Filtered-empty
- [ ] Bulk-select mode + overflow menu + filters drawer

### A11y
- [ ] `role="tablist"`, `aria-selected`, `aria-label` su kebab "Più"
- [ ] Touch target ≥44×44px
- [ ] `prefers-reduced-motion`: long-press feedback + tab transition + bottom-sheet
- [ ] Live-region per skeleton→content

### Dati
- [ ] Testo UI italiano
- [ ] Giochi reali da data.js · player names italiani
- [ ] NO UUID-like, NO bearer-pattern

---

## File di riferimento da allegare in chat Claude Design

**Obbligatori (preambolo)**:
1. `admin-mockups/briefs/_common.md` — preambolo design system
2. `admin-mockups/briefs/SP8-mobile-parity.md` — questo brief
3. `admin-mockups/design_files/tokens.css` — design tokens
4. `admin-mockups/design_files/components.css` — classi base
5. `admin-mockups/design_files/data.js` — dati finti (**NON rigenerarlo** — read-only, shared)

**Base content-model (CRITICO)**:
6. `admin-mockups/design_files/sp4-library-desktop.html` — il desktop esistente: stesso content model, da adattare a mobile. NON è la versione finale mobile.

**Reference visivi 1:1 prod**:
7. `admin-mockups/design_files/mobile-app.jsx` — full mobile shell (BottomBar, drawer physics, connection pips)
8. `admin-mockups/design_files/02-desktop-patterns.html` — pattern desktop (per il fallback ≥768px)
9. `admin-mockups/design_files/03-drawer-variants.html` — bottom-sheet drawer (filters + bulk actions)

## Risposta attesa nel thread Claude Design

1. Conferma scope SP8 mobile-parity (1 mockup, IA semplificata 3+overflow, mobile-first)
2. Genera il mockup completo (HTML + JSX)
3. Path salvataggio: `admin-mockups/design_files/sp4-library-mobile.{html,jsx}`
4. Note finali: deviazioni flaggate + nuovi componenti v2 emersi + stati coperti

## Note finali per Claude Design

**Tono UI**: warm, casual, italiano informale.

**Microcopy hint**:
- Empty: "Nessun gioco ancora — aggiungi il primo!"
- Offline: "Sei offline — alcuni dati potrebbero non essere aggiornati"
- Bulk: "3 selezionati" (azione chiara, no "items in selection state")

**Mobile-first è canonical**: 375px è il target primario, tablet 768px è adaptation. Desktop ≥1024px → fallback a `sp4-library-desktop.html`.
