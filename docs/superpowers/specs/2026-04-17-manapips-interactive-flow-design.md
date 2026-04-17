# ManaPips Interactive Flow — Design Spec

**Date:** 2026-04-17
**Status:** Approved
**Scope:** ManaPips interattivi, popup conferma possesso, pagina dettaglio gioco, KB selection nel wizard sessione

## Overview

Trasformare i ManaPips da indicatori decorativi a sistema di navigazione e creazione universale tra entità MeepleCard. Il flusso target: utente cerca gioco → aggiunge a libreria via ManaPip → atterra su pagina dettaglio → crea sessione via ManaPip → gioca con agente RAG.

## Decisioni di Design

| Decisione | Scelta |
|-----------|--------|
| ManaPips interazione | count > 0 → popover lista + "Crea nuovo"; count = 0 → creazione diretta |
| Aggiungi a libreria | Pip `game` (arancione) su card catalogo, count=0 → crea |
| Popup possesso | Conferma con motivazione copyright/KB |
| KB nel wizard sessione | Auto-skip se singola KB, selezione solo se 2+ |
| Pagina dettaglio gioco | Hero MeepleCard + ManaPips lg + tabs sezioni |
| Ricerca | Filtro locale esistente, no full-text backend (YAGNI) |

## 1. ManaPips Interattivi

### Evoluzione componente

**File:** `ManaPips.tsx` (modifica) + `ManaPipPopover.tsx` (nuovo)

**Interfaccia aggiornata:**

```ts
interface ManaPip {
  entityType: MeepleEntityType;
  count?: number;
  // Nuove props:
  items?: { id: string; label: string; href: string }[];
  onCreate?: () => void;
  createLabel?: string; // es. "Nuova Sessione", "Aggiungi a libreria"
}
```

**Comportamento click:**

- `count = 0` → invoca `onCreate()` direttamente (nessun popover)
- `count > 0` → apre `ManaPipPopover` con:
  - Lista entita collegate (`items[]`): titolo + mini-info, click naviga a `href`
  - Bottone "Crea nuovo" in fondo (invoca `onCreate()`)

**Rendering:**

- Il pip diventa `<button>` (da `<span>`)
- Hover: scale-up + glow colore entita (gia presente su MeepleCard, riusare pattern)
- Stato disabled se azione non disponibile
- Popover: Radix `Popover` (gia in uso nel progetto)

**Varianti size:**

| Size | Dot | Badge | Popover | Label | Uso |
|------|-----|-------|---------|-------|-----|
| `sm` | 6px | no | no — click diretto | no | compact card |
| `md` | 8px | count | si | no | grid card |
| `lg` | 12px | count | si | testo accanto ("3 Sessioni") | hero card |

## 2. Popup Conferma Possesso

### Nuovo componente

**File:** `OwnershipConfirmDialog.tsx`

**Trigger:** Click su pip `game` (arancione, count=0) di MeepleCard catalogo

**Nota:** Il pip `game` appare solo su card catalogo di giochi NON in libreria. Su card di giochi gia in libreria, il pip `game` non viene renderizzato (il gioco e gia "tuo").

**Contenuto:**

- Icona gioco + titolo
- Testo: "Per accedere alla Knowledge Base di questo gioco (regolamento, FAQ, strategie), conferma di possederne una copia fisica. Questo ci permette di offrirti il contenuto nel rispetto del copyright."
- Bottone primario: "Possiedo il gioco" → `useAddGameToLibrary()` → redirect `/games/[id]`
- Bottone secondario: "Annulla" → chiude

**Tecnico:**

- Basato su Radix `AlertDialog`
- Nessuna nuova API backend — usa `AddGameToLibraryCommand` esistente
- Optimistic update esistente in `useAddGameToLibrary`

## 3. Pagina Dettaglio Gioco

### Nuova pagina

**File:** `apps/web/src/app/(authenticated)/games/[id]/page.tsx`

**Layout:**

1. **Hero MeepleCard** (variant `hero` o `featured`)
   - Immagine grande, titolo, publisher, rating
   - ManaPips size `lg` con label testuali, interattivi:
     - `session` 🎯 — popover sessioni o crea nuova
     - `kb` 📚 — popover KB disponibili o stato "nessun documento"
     - `agent` 🤖 — popover agenti o crea nuovo
     - `toolkit` 🧰 — popover strumenti
   - Badge "In libreria" se presente, pip `game` se non in libreria

2. **Tabs** sotto la hero card (contenuto inline dalle sub-pagine esistenti):
   - Regole (`/games/[id]/rules`)
   - FAQ (`/games/[id]/faqs`)
   - Sessioni (`/games/[id]/sessions`)
   - Strategie (`/games/[id]/strategies`)

**Data fetching:**

- `useGame(id)` — esistente
- `useLibraryEntry(gameId)` — per stato libreria
- `useGameManaPips(id)` — **nuovo hook**, aggrega:
  - Conteggio sessioni per gameId
  - Conteggio KB/documenti per gameId
  - Conteggio agenti per gameId
  - Lista items per ogni tipo (per il popover)

**Navigazione verso questa pagina:**

- Click titolo/immagine di qualsiasi MeepleCard game
- Redirect dopo conferma possesso
- Link da dashboard, ricerca, liste

## 4. KB Selection nel Wizard Sessione

### Step condizionale

**Modifica a:** `session-wizard-mobile.tsx`

**Logica condizionale:**

- Query `useGameDocuments(gameId)` dopo selezione gioco
- **0 KB** → step saltato, sessione senza RAG
- **1 KB** → auto-selezionata, step saltato
- **2+ KB** → mostra step "Scegli Knowledge Base"

**UI step KB:**

- Lista MeepleCard variant `compact` per ogni KB:
  - Titolo documento (es. "Regolamento Base v2", "Espansione: The Lost Legion")
  - Badge tipo (Rulebook, Homerule, Errata)
  - Toggle on/off
- Pre-selezione automatica: ultimo Rulebook + Errata attivi (logica da `KBSelectionPanel` esistente)

**Backend:**

- Wizard passa `documentIds[]` selezionati a `CreateSessionCommand`
- Agente in sessione usa solo i documenti selezionati per RAG context

**Step wizard aggiornati:**

1. Choose Game
2. Add Players
3. Turn Order
4. **Select KB** (condizionale, solo se 2+ KB)
5. Ready

## 5. Flusso E2E

```
Login → Dashboard
  └─ Sezione "Giochi" catalogo (utente nuovo)
  └─ Filtro: "Mage Knight"
  └─ MeepleCard con pip game 🎲 (count=0)

Click pip game → OwnershipConfirmDialog
  └─ "Possiedo il gioco"
  └─ addGameToLibrary → redirect /games/[id]

Pagina dettaglio /games/[id]
  └─ Hero MeepleCard + ManaPips lg
  └─ Pip session 🎯 (count=0)
  └─ Click → wizard /sessions/new?gameId=...

Wizard sessione
  └─ Step 1: game pre-selezionato (skip)
  └─ Step 2: giocatori (nomi + colori)
  └─ Step 3: ordine turno
  └─ Step 4: KB selection (se 2+ KB)
  └─ Step 5: pronto → crea sessione

Sessione live /sessions/live/[id]/agent
  └─ Chat agente RAG
  └─ "Come si fa il setup di Mage Knight?"
```

## Scope Escluso (YAGNI)

- Full-text search backend (filtro locale sufficiente)
- ManaPips interattivi su entita non-game (player, session cards) — estensione futura
- Creazione agente custom — usa auto-creazione esistente (`AutoCreateAgentOnPdfReadyHandler`)
- ManaPips su variant `list` — solo `grid`, `compact`, `hero`/`featured`

## File Impattati

### Nuovi
- `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPipPopover.tsx`
- `apps/web/src/components/dialogs/OwnershipConfirmDialog.tsx`
- `apps/web/src/app/(authenticated)/games/[id]/page.tsx`
- `apps/web/src/hooks/queries/useGameManaPips.ts`

### Modificati
- `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx` — button + click handlers
- `apps/web/src/components/ui/data-display/meeple-card/types.ts` — ManaPip interface estesa
- `apps/web/src/app/(authenticated)/sessions/new/session-wizard-mobile.tsx` — step KB condizionale
- `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx` — ManaPips su card catalogo

### Backend (minimo)
- `CreateSessionCommand` — accettare `documentIds[]` opzionale
- Nuovo endpoint o query per aggregare ManaPips data per game
