# MeepleCard Redesign — Design Spec

**Date:** 2026-04-02
**Status:** Approved
**Goal:** Ridisegnare MeepleCard come elemento visivo distintivo dell'app, con linguaggio visivo ispirato a Magic: The Gathering, dimensioni fisse di carta fisica, e metriche specifiche per ogni tipo di entità.

---

## 1. Principi fondamentali

- La MeepleCard è il **tratto identitario** dell'app: chiunque la veda deve riconoscere MeepleAI
- Dimensioni **fisse** (non fluide): come una vera carta fisica
- Aspect ratio **5:7** (stessa di MTG e carte da gioco standard: 63mm × 88mm)
- Il frame visivo rimane **coerente** tra tutti i tipi di entità; solo colori e contenuti cambiano

---

## 2. Frame visivo — "Warm Heritage MTG"

Stile ibrido tra:
- **Opzione A (Classic MTG)**: bordo doppio ornato, struttura a zone ben definite
- **Opzione C (Board Game Tactile)**: palette ambra calda/pergamena, texture warmth

### Specifiche frame

```
Outer border:    2px solid amber/gold gradient  (#c8963a → #e8b84b → #c8963a)
Inner border:    1px solid rgba(200,150,58, 0.4)
Corner radius:   8px outer, 6px inner
Background:      radial-gradient(parchment warm: #1a1208 center → #0d0905 edges)
Frame padding:   4px between outer and inner border (ornate zone)
```

### Colori entity (HSL, già presenti in `meeple-card-styles.ts`)

| Entità | HSL | Uso frame |
|--------|-----|-----------|
| game | `hsl(25, 95%, 45%)` | amber |
| player | `hsl(262, 83%, 58%)` | purple |
| session | `hsl(240, 60%, 55%)` | indigo |
| agent | `hsl(38, 92%, 50%)` | amber caldo |
| kb | `hsl(174, 60%, 40%)` | teal |

---

## 3. Dimensioni fisse

| Variant | Larghezza | Altezza | Note |
|---------|-----------|---------|------|
| `grid` | 200px | 280px | Default, griglia |
| `compact` | 160px | 224px | Lista compatta |
| `featured` | 280px | 392px | Card in evidenza |
| `hero` | 360px | 504px | Banner hero |
| `list` (thumbnail) | 80px | 112px | Thumbnail laterale in lista |
| `expanded` | 320px | 448px | Dettaglio espanso |

**Regola**: `width` e `height` sono **sempre fisse**. Nessun `w-full` o `h-auto` sul contenitore root.

---

## 4. Anatomia della card (top → bottom)

```
┌────────────────────────────────┐
│  TITLE BAR           [pip]     │  ~10% altezza
├────────────────────────────────┤
│                                │
│           ART BOX              │  ~38% altezza
│                                │
├────────────────────────────────┤
│  [chip] [chip]    [pill][pill] │  SYMBOL STRIP ~8%
├────────────────────────────────┤
│                                │
│          TEXT BOX              │  ~26% altezza
│                                │
├────────────────────────────────┤
│  ○ ○ ○  [+N]                  │  MANA LINK FOOTER ~9%
├────────────────────────────────┤
│  Publisher · Anno    Stat Key  │  BOTTOM BAR ~9%
└────────────────────────────────┘
```

### 4.1 Title Bar

- Nome entità a **sinistra** (font Quicksand Bold, text-sm/text-xs per compact)
- **ManaCostBar** a **destra** (posizione mana MTG) — componente esistente, da preservare
- Background: `rgba(0,0,0,0.6)` con `backdrop-blur-sm`

### 4.2 Art Box

- Immagine full-width, no overlay angolari
- `object-fit: cover`, altezza fissa proporzionale
- Placeholder gradient con entity color quando imageUrl assente

### 4.3 Symbol Strip (nuovo componente — rimpiazza type line MTG)

Due zone orizzontali separate:

**Sinistra — Identity Chips** (solo testo, no icone):

| Entità | Chip 1 | Chip 2 |
|--------|--------|--------|
| game | Genere | Meccanica |
| player | Stile gioco | Specializzazione |
| session | Modalità | Stato |
| agent | Specializzazione | Tier |
| kb | Tipo documento | Stato |

Stile chip: `text-[10px] font-medium bg-white/10 border border-white/20 rounded-full px-2 py-0.5`

**Destra — Metric Pills** (icona emoji + valore):

| Entità | Pills |
|--------|-------|
| game | `👥 2-4` · `⏱ 45min` |
| player | `🎮 42` · `🏆 67%` |
| session | `🏆 128 pts` · `📅 12 Mar` |
| agent | `💬 34` · `🎯 94%` · `📚 3` |
| kb | `📄 48` · `🔍 312` |

Stile pill: `text-[10px] font-mono bg-black/40 border border-white/10 rounded px-1.5 py-0.5`

### 4.4 Text Box

- Descrizione breve / tagline entità
- `text-xs`, line-clamp-3 (grid), line-clamp-2 (compact)
- Background: `rgba(10,6,2,0.8)`

### 4.5 ManaLinkFooter

- Componente esistente — **preservare senza modifiche**
- Posizione: tra text box e bottom bar
- Pips cliccabili → cascade navigation

### 4.6 Bottom Bar

| Entità | Sinistra | Destra |
|--------|----------|--------|
| game | Publisher · Anno | BGG Rank #N |
| player | Membro da | Rank globale |
| session | Durata | N turni |
| agent | Version | Tier badge |
| kb | Ultima agg. | N query |

---

## 5. Componenti da creare/modificare

| Componente | Azione | Note |
|------------|--------|------|
| `SymbolStrip` | **Nuovo** | Rimpiazza type line |
| `MeepleCardFrame` | **Nuovo** | CSS frame Warm Heritage MTG |
| `MeepleCardGrid` | **Modifica** | Usa nuove dimensioni fisse + frame |
| `MeepleCardCompact` | **Modifica** | Usa nuove dimensioni fisse + frame |
| `MeepleCardFeatured` | **Modifica** | Usa nuove dimensioni fisse + frame |
| `MeepleCardHero` | **Modifica** | Usa nuove dimensioni fisse + frame |
| `MeepleCardList` | **Modifica** | Thumbnail 80×112px |
| `meeple-card-styles.ts` | **Modifica** | Aggiunge token frame + dimensioni |
| `types.ts` | **Modifica** | Aggiunge props per symbol strip |
| `ManaCostBar` | **Preservare** | Nessuna modifica |
| `ManaLinkFooter` | **Preservare** | Nessuna modifica |

---

## 6. Nuove props MeepleCardProps (delta)

```typescript
// Symbol Strip — Identity Chips
identityChip1?: string        // es. "Strategia", "Euro"
identityChip2?: string        // es. "Worker Placement"

// Symbol Strip — Metric Pills (per entity type)
// Game
playerCount?: string          // es. "2-4"
playTime?: string             // es. "45min"

// Player
gamesPlayed?: number
winRate?: number              // 0-100

// Session
winnerScore?: number | string
sessionDate?: string

// Agent
conversationCount?: number
accuracy?: number             // 0-100
linkedKbCount?: number

// KnowledgeBase
pageCount?: number
chunkCount?: number

// Bottom bar
publisher?: string            // già esistente
releaseYear?: number
bottomStatLabel?: string      // es. "BGG Rank", "Rank", "Tier"
bottomStatValue?: string      // es. "#42", "Gold"
```

---

## 7. Design tokens CSS (nuovi)

```css
/* Da aggiungere in meeple-card-styles.ts o globals.css */

--mc-outer-border: linear-gradient(180deg, #c8963a, #e8b84b 50%, #c8963a);
--mc-inner-border: rgba(200, 150, 58, 0.35);
--mc-frame-bg: radial-gradient(ellipse at center, #1a1208 0%, #0d0905 100%);
--mc-frame-padding: 3px;
--mc-corner-radius-outer: 8px;
--mc-corner-radius-inner: 5px;

--mc-title-bar-h: 32px;
--mc-symbol-strip-h: 26px;
--mc-footer-h: 28px;
--mc-bottom-bar-h: 24px;
/* Art box e text box: spazio rimanente, diviso ~38%/26% */
```

---

## 8. Vincoli tecnici

- **Nessun `w-full`** sul contenitore root — dimensioni sempre fisse
- **`aspect-ratio: 5/7`** come fallback se le dimensioni fisse vengono override
- Varianti esistenti (expanded, flip, hover-preview) devono continuare a funzionare
- `React.memo` mantenuto su tutti i componenti
- CVA pattern esistente mantenuto in `meeple-card-styles.ts`
- CQRS: nessuna modifica al backend richiesta da questa spec
- Componenti esistenti `ManaCostBar`, `ManaLinkFooter`, `ManaSymbol` **non modificati**

---

## 9. Entità non trattate in questa spec

I 16 tipi di entità esistenti (expansion, collection, achievement, event, ecc.) ricevono:
- Frame identico (Warm Heritage MTG)
- Symbol strip con chip generici (`identityChip1/2`) e nessuna metric pill specifica
- Bottom bar generico

Metriche specifiche per entità aggiuntive: futura iterazione.

---

## 10. Criteri di accettazione

- [ ] Card mostra dimensioni fisse nei variant grid/compact/featured/hero/list
- [ ] Frame Warm Heritage MTG visibile (bordo doppio ambra, background pergamena scura)
- [ ] Symbol strip presente con chip testo-only a sx e metric pills a dx
- [ ] ManaCostBar (entity pip) in title bar top-right
- [ ] ManaLinkFooter tra text box e bottom bar — cliccabile
- [ ] Bottom bar con publisher/data e stat chiave
- [ ] Art box pulita, nessun overlay angolare
- [ ] Storybook / visual test aggiornato per ogni variant
- [ ] Nessuna regressione sui componenti esistenti
