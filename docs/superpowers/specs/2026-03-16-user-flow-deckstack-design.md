# User Flow & Deckstack Design

**Date**: 2026-03-16
**Status**: Draft (v3 — post-review round 2)
**Scope**: Redesign completo del percorso utente post-registrazione

## Overview

Estensione del sistema **CardHand** esistente (`useCardHand` + `CardStack` + `HandDrawer`) con 4 **placeholder action cards** e nuovi flussi di ricerca/creazione. Il flusso auth (invite-only → welcome → dashboard) resta invariato.

### Goals

1. Aggiungere 4 placeholder action cards al CardStack/HandDrawer esistente
2. Flusso "Cerco agente" con ricerca perimetrata e selezione KB smart
3. Estensione DocumentSelector (oggi admin-only) agli utenti nel wizard agent
4. Toolkit generico con strumenti da tavolo slegati da gioco specifico
5. Estendere `AgentCreationSheet` per accettare KB pre-selezionati

### Non-Goals

- Modifiche al flusso di registrazione/auth
- Onboarding post-registrazione
- Redesign delle pagine dettaglio (agent, game, session, KB)
- Riscrivere CardStack/HandDrawer — si estendono, non si sostituiscono

### Backend Changes Required

> La selezione KB per utenti richiede un endpoint user-facing per i documenti di un gioco. Oggi esiste solo `GET /api/v1/admin/shared-games/{gameId}/documents`. Serve un nuovo endpoint: `GET /api/v1/shared-games/{gameId}/documents` (senza prefisso admin) con accesso filtrato per utenti autenticati.

## Existing Infrastructure

Il sistema Card Hand e gia implementato in `UnifiedShell`:

| Componente | File | Ruolo |
|-----------|------|-------|
| **Store** | `src/stores/use-card-hand.ts` | Zustand store: `drawCard`, `discardCard`, `pinCard`, `focusCard`, FIFO (max 10) |
| **Desktop** | `src/components/layout/UnifiedShell/CardStack.tsx` | Sidebar sinistra, expand/collapse, pinned section in basso |
| **Mobile** | `src/components/layout/UnifiedShell/HandDrawer.tsx` | Drawer orizzontale, scroll, grip handle |
| **Item desktop** | `src/components/layout/UnifiedShell/CardStackItem.tsx` | Mini (icona) o Card (icona + label), pin indicator, discard |
| **Item mobile** | `src/components/layout/UnifiedShell/HandDrawerCard.tsx` | 36x36px, emoji gradient o immagine |
| **Layout** | `src/components/layout/UnifiedShell/UnifiedShellClient.tsx` | Orchestratore: desktop=CardStack left, mobile=HandDrawer top |
| **Config** | `src/config/entity-actions.ts` | `DEFAULT_PINNED_CARDS`: Home, Libreria, Sessioni, Chat, Profilo |
| **Colors** | `src/components/layout/UnifiedShell/entity-hand-constants.ts` | 16 entity types con HSL + emoji |

**Persistenza attuale**:
- `sessionStorage` (`meeple-card-hand`): cards — sopravvive solo nella sessione
- `localStorage` (`meeple-card-pins`): pinned IDs — persiste
- `localStorage` (`meeple-card-stack-expanded`): stato expand
- `localStorage` (`meeple-hand-collapsed`): stato collapse drawer

**`HandCard` interface attuale**:
```typescript
interface HandCard {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
}
```

## Design Changes

### 1. Placeholder Action Cards

Le placeholder card sono card speciali nel CardStack che, invece di navigare a una pagina, aprono uno sheet/flusso di creazione.

**Approccio**: Estendere `HandCard` con campi opzionali e definire placeholder cards come costante separata.

**Nota**: `DEFAULT_PINNED_CARDS` in `entity-actions.ts` e attualmente una costante derivata:
```typescript
export const DEFAULT_PINNED_CARDS = DEFAULT_ACTIONS.filter(a => a.drawCard).map(a => ({...}));
```
Non si puo appendere direttamente. La soluzione e definire `PLACEHOLDER_ACTION_CARDS` come costante separata e esportare un array combinato `ALL_DEFAULT_CARDS`.

```typescript
// Estensione di HandCard in use-card-hand.ts
interface HandCard {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
  isPlaceholder?: boolean;   // NEW — true per action cards
  placeholderAction?: string; // NEW — 'search-agent' | 'search-game' | 'start-session' | 'toolkit'
}
```

**4 Placeholder cards** (nuova costante in `entity-actions.ts`):

```typescript
// Costante separata, NON derivata da DEFAULT_ACTIONS
export const PLACEHOLDER_ACTION_CARDS: HandCard[] = [
  {
    id: 'action-search-agent',
    entity: 'agent',
    title: 'Cerca Agente',
    href: '#action-search-agent',  // href non navigabile
    isPlaceholder: true,
    placeholderAction: 'search-agent',
  },
  {
    id: 'action-search-game',
    entity: 'game',
    title: 'Cerca Gioco',
    href: '#action-search-game',
    isPlaceholder: true,
    placeholderAction: 'search-game',
  },
  {
    id: 'action-start-session',
    entity: 'session',
    title: 'Avvia Sessione',
    href: '#action-start-session',
    isPlaceholder: true,
    placeholderAction: 'start-session',
  },
  {
    id: 'action-toolkit',
    entity: 'toolkit',
    title: 'Toolkit',
    href: '#action-toolkit',
    isPlaceholder: true,
    placeholderAction: 'toolkit',
  },
];

// Array combinato per seeding
export const ALL_DEFAULT_CARDS: HandCard[] = [
  ...DEFAULT_PINNED_CARDS,
  ...PLACEHOLDER_ACTION_CARDS,
];
```

**Migrazione per utenti esistenti**: Il seeding in `UnifiedShellClient.tsx` oggi esegue solo quando `cards.length === 0`. Per utenti returning che hanno gia card in storage, serve una migrazione una tantum:

```typescript
// In UnifiedShellClient.tsx, dopo il seeding iniziale
useEffect(() => {
  const hasPlaceholders = cards.some(c => c.isPlaceholder);
  if (!hasPlaceholders) {
    // Inietta placeholder per utenti esistenti (migrazione v2)
    PLACEHOLDER_ACTION_CARDS.forEach(card => {
      drawCard(card);
      pinCard(card.id);
    });
  }
}, []); // eslint-disable-line react-hooks/exhaustive-deps
```

**Rendering in CardStack/HandDrawer**: Le card placeholder hanno stile visivo distinto:
- **Desktop (`CardStackItem`)**: Bordo dashed, icona con sfondo muted, label in corsivo. No discard button (non removibili).
- **Mobile (`HandDrawerCard`)**: Bordo dashed, opacita piena (non 45% come le non-focused), icona pulsante.
- **Comportamento click**: Per placeholder cards, il root element deve essere `<button>` invece di `<Link>`. Implementazione:
  - `CardStackItem`: Conditional render `isPlaceholder ? <button onClick={handleAction}> : <Link href={card.href}>`
  - `HandDrawerCard`: Stessa logica — `<button>` per placeholder, `<Link>` per card normali
  - Attributi a11y: `role="button"`, `aria-label="Azione: {card.title}"` per placeholder

**`usePlaceholderActions` hook**:
```typescript
// src/hooks/usePlaceholderActions.ts
function usePlaceholderActions(): {
  handleCardClick: (card: HandCard) => boolean; // true se gestito come placeholder
  activeSheet: 'search-agent' | 'search-game' | 'start-session' | 'toolkit' | null;
  closeSheet: () => void;
};
```

Questo hook viene usato in `CardStackItem` e `HandDrawerCard` per intercettare click su placeholder cards e aprire lo sheet corretto.

### 2. Modifiche a `CardStackItem.tsx`

```typescript
// Aggiungere al componente:
// 1. Controllo isPlaceholder per stile dashed
// 2. Nascondere discard button per placeholder
// 3. Intercettare click via usePlaceholderActions

// Mini mode placeholder: icona con bordo dashed, background trasparente
// Card mode placeholder: stesso layout ma bordo dashed e label con stile "action" (es. "+ Cerca Agente")
```

### 3. Modifiche a `HandDrawerCard.tsx`

```typescript
// Aggiungere:
// 1. Stile dashed border per placeholder
// 2. Click handler che usa usePlaceholderActions invece di Link
// 3. Indicatore visivo "action" (es. icona "+" overlay)
```

### 4. Auto-push card nelle pagine dettaglio (`drawCard` wrapper)

Le pagine dettaglio sono Server Components (async, no `'use client'`). Per chiamare `drawCard()` serve un **Client Component wrapper**.

**Nuovo componente**:
```typescript
// src/components/layout/DeckTrackerSync.tsx
'use client';

import { useCardHand } from '@/stores/use-card-hand';
import { useEffect } from 'react';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';

interface DeckTrackerSyncProps {
  entity: MeepleEntityType;
  id: string;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
}

export function DeckTrackerSync(props: DeckTrackerSyncProps) {
  const drawCard = useCardHand((s) => s.drawCard);

  useEffect(() => {
    drawCard({
      id: props.id,
      entity: props.entity,
      title: props.title,
      href: props.href,
      subtitle: props.subtitle,
      imageUrl: props.imageUrl,
    });
  }, [props.id]); // eslint-disable-line react-hooks/exhaustive-deps

  return null; // render-less component
}
```

**Uso nelle pagine dettaglio**:

Le pagine dettaglio possono essere Server Components (async) o Client Components. L'approccio dipende dal tipo:

**Server Components** (es. `/agents/[id]/page.tsx` — async, no `'use client'`):
```tsx
export default async function AgentDetailPage({ params }: Props) {
  const agent = await fetchAgent(params.id);
  return (
    <>
      <DeckTrackerSync
        entity="agent"
        id={agent.id}
        title={agent.name}
        href={`/agents/${agent.id}`}
        subtitle={agent.typology}
      />
      {/* rest of page */}
    </>
  );
}
```

**Client Components** (es. `/knowledge-base/[id]/page.tsx` — gia `'use client'`):
```tsx
// Non serve DeckTrackerSync — chiamare drawCard direttamente
'use client';
import { useCardHand } from '@/stores/use-card-hand';

export default function KBDetailPage({ params }: Props) {
  const drawCard = useCardHand((s) => s.drawCard);

  useEffect(() => {
    if (document) {
      drawCard({ id: document.id, entity: 'kb', title: document.title, href: `/knowledge-base/${document.id}` });
    }
  }, [document?.id]);

  // ...
}
```

**Pagine da aggiornare**:
- `src/app/(authenticated)/agents/[id]/page.tsx` — RSC → usa `DeckTrackerSync`
- `src/app/(authenticated)/knowledge-base/[id]/page.tsx` — Client Component → usa `drawCard` direttamente
- Altre pagine dettaglio (gioco, sessione, chat): verificare RSC vs client e usare l'approccio appropriato

### 5. AgentCreationSheet — Estensione per KB pre-fill

L'attuale `AgentCreationSheetProps`:
```typescript
interface AgentCreationSheetProps {
  isOpen: boolean;
  onClose: () => void;
  initialGameId?: string;
  initialGameTitle?: string;
}
```

**Nuove props**:
```typescript
interface AgentCreationSheetProps {
  isOpen: boolean;
  onClose: () => void;
  initialGameId?: string;
  initialGameTitle?: string;
  // NEW
  initialDocumentIds?: string[];       // KB pre-selezionati
  initialDocumentSummary?: string;     // Es. "2 KB selezionati (Regolamento v2.1, Errata v2.1.3)"
  skipGameSelection?: boolean;         // true → salta step GameSelector
  skipKBUpload?: boolean;              // true → salta step upload PDF, mostra riepilogo read-only
}
```

**Comportamento con pre-fill**:
- Se `skipGameSelection=true`: Step 1 (GameSelector) non viene renderizzato, il gioco e gia selezionato
- Se `skipKBUpload=true` + `initialDocumentIds`: Step 2 mostra un riepilogo read-only dei KB selezionati (badge tipo + titolo) con link "Modifica selezione" che torna al SearchAgentSheet
- `useCreateAgentFlow` hook esteso per accettare `documentIds: string[]` nel payload di creazione

**Estensione `useCreateAgentFlow`**:
```typescript
// Attuale: { gameId, addToCollection, agentType, agentName, strategyName }
// Nuovo:   { gameId, addToCollection, agentType, agentName, strategyName, documentIds?: string[] }
```

### 6. Rimozione AgentsSidebar — Migrazione graduale

L'`AgentsSidebar` nella dashboard viene rimossa perche la funzionalita e assorbita dal CardStack (gli agenti appaiono come card nel deck).

**Strategia**: Rimozione diretta (non phased), poiche il CardStack gia mostra agenti. I test `AgentsSidebar.test.tsx` vengono eliminati.

**In `DashboardRenderer.tsx`**: Rimuovere il render di `AgentsSidebar` dal layout flex. Le card zone (Recent Games, Sessions, Chats) occupano la larghezza piena.

## User Flows

### Flow 1: Cerco agente per gioco

**Trigger**: Click su placeholder "Cerca Agente" nel CardStack/HandDrawer.

**Step 1 — Ricerca gioco (`SearchAgentSheet`)**

Si apre come bottom sheet (mobile, 90vh) o right drawer (desktop, 480px). Stesso pattern di `AgentCreationSheet`.

- Input ricerca con filtri scope:
  - "La mia collezione" — giochi nella UserLibrary dell'utente
  - "Shared con KB" — giochi nel SharedGameCatalog con almeno un documento KB
- Risultati come card row con:
  - Titolo, publisher, provenienza (collezione/shared)
  - Badge conteggio KB: "3 KB" (verde) o "No KB" (giallo, disabilitato)
- Solo giochi con almeno un KB sono selezionabili
- Click su gioco → Step 2

**API coinvolte**:
- `GET /api/v1/users/{userId}/library` — giochi in collezione (esistente)
- `GET /api/v1/shared-games` — shared games (esistente, filtro client-side per `hasUploadedPdf=true`)

**Step 2 — Selezione Knowledge Base (`KBSelectionPanel`)**

Pannello inline nello stesso sheet, con navigazione back a Step 1.

**Auto-selezione smart**:
- Il sistema pre-seleziona automaticamente:
  - L'ultimo KB di tipo "Regole Base" (Rulebook, documentType=0) nella versione piu recente
  - L'ultima Errata (documentType=1) se disponibile
- Le card auto-selezionate hanno bordo verde e checkbox checked

**Selezione manuale opzionale**:
- Sezione "Espansioni disponibili" con checkbox per:
  - Espansioni (tag-based o documentType custom)
  - Homerule (documentType=2)
  - Altre versioni del regolamento
- Ogni documento mostra: tipo (badge colorato), titolo, versione, chunk count, data caricamento

**Vincoli**:
- Almeno 1 KB regole base obbligatorio (non deselezionabile se unico)
- Una sola versione regole base selezionabile alla volta
- Espansioni e errata: multi-select libero

**API coinvolte**:
- `GET /api/v1/shared-games/{gameId}/documents` — **NUOVO endpoint user-facing** (vedi sezione Backend Changes)

**Nuovo hook React Query necessario** (`src/hooks/queries/useSharedGameDocuments.ts`):
```typescript
// ATTENZIONE: NON usare useDocumentsByGame esistente — quello chiama
// /api/v1/games/{gameId}/pdfs (documenti privati utente, non shared).
// Questo hook chiama il NUOVO endpoint user-facing per documenti shared.
export function useSharedGameDocuments(gameId: string | null) {
  return useQuery({
    queryKey: ['shared-game-documents', gameId],
    queryFn: () => api.sharedGames.getDocuments(gameId!),
    enabled: !!gameId,
  });
}
```
Richiede anche un nuovo metodo `getDocuments(gameId)` in `sharedGamesClient.ts` che chiama `GET /api/v1/shared-games/{gameId}/documents`.

**Step 3 — Wizard agente (AgentCreationSheet con pre-fill)**

Bottone "Crea Agente con N KB →" chiude il SearchAgentSheet e apre l'AgentCreationSheet con:
- `initialGameId`, `initialGameTitle` — gioco pre-selezionato
- `initialDocumentIds` — KB selezionati
- `skipGameSelection=true`, `skipKBUpload=true`
- L'utente configura: nome, tipologia, strategia, modello, costi/slot

Al submit → crea agente + thread → redirect a `/chat/{threadId}` → card agente auto-pushata nel CardStack.

### Flow 2: Cerco gioco

**Trigger**: Click su placeholder "Cerca Gioco" nel CardStack/HandDrawer.

**Comportamento**: Si apre `SearchGameSheet` (bottom sheet/drawer) con:
- Input ricerca con scope: "La mia collezione" + "Catalogo shared"
- Risultati come card row con badge info (giocatori, complessita, KB disponibili)
- Nessun filtro "solo con KB" — mostra tutti i giochi
- Click su risultato → naviga a pagina dettaglio gioco
- La card del gioco visitato viene auto-pushata nel CardStack via `DeckTrackerSync`

### Flow 3: Avvio sessione

**Trigger**: Click su placeholder "Avvia Sessione" nel CardStack/HandDrawer.

**Comportamento**: Si apre `SessionSheet` con due stati:

**Se sessioni attive/in pausa esistono**:
- Lista sessioni con stato badge (Attiva, In pausa)
- Ogni sessione come card row (entity="session")
- Metadata: gioco, partecipanti, durata, codice sessione
- Click → naviga alla sessione esistente
- Bottone "Nuova sessione" in fondo alla lista

**Se nessuna sessione attiva**:
- Form creazione diretta:
  - Seleziona gioco (dalla collezione, riusa `GameSelector` esistente)
  - Tipo sessione: Generica / Specifica per gioco
  - Aggiungi partecipanti (utenti registrati o ospiti)
  - Submit → crea sessione → naviga a `/sessions/{id}`

**API coinvolte**:
- `GET /api/v1/sessions` con filtro status — sessioni attive utente (verificare formato filtro)
- `POST /api/v1/sessions` — crea nuova sessione (esistente)

### Flow 4: Toolkit generico

**Trigger**: Click su placeholder "Toolkit" nel CardStack/HandDrawer.

**Comportamento**: Si apre `ToolkitSheet` con griglia di strumenti. Ogni strumento si apre inline nel sheet. Strumenti stateless — non richiedono backend.

**Tier 1 — Core (primo rilascio)**:

| Strumento | Icona | Descrizione |
|-----------|-------|-------------|
| Dado | 🎲 | Lancia 1-N dadi configurabili (d4, d6, d8, d10, d12, d20) |
| Timer | ⏱️ | Countdown o cronometro per turni |
| Contatore | 🔢 | +/- contatore multiplo (punti vita, risorse, etc.) |
| Segnapunti | 📊 | Tabellone segnapunti per N giocatori |
| Randomizer | 🎰 | Selezione casuale da lista personalizzata |

**Tier 2 — Espansione (rilascio futuro)**:

| Strumento | Icona | Descrizione |
|-----------|-------|-------------|
| Primo giocatore | 👆 | Selezione casuale primo giocatore tra N |
| Tracker turni/round | 🔄 | Segna round corrente, fase, turno attivo |
| Notepad | 📝 | Appunti rapidi di partita |
| Coin flip | 🪙 | Testa o croce con animazione |
| Calcolatrice punti | 🧮 | Calcolo finale con campi per categorie |
| Assegna colori/team | 🎨 | Distribuzione casuale colori/squadre |
| Card draw | 🃏 | Pesca da mazzo personalizzabile |
| Ambient sound | 🔊 | Atmosfera sonora a tema |

## Data Flow

### CardStack + Placeholder Lifecycle

```
1. User logs in → dashboard loads
2. UnifiedShellClient hydrates useCardHand from sessionStorage + localStorage
3. DEFAULT_PINNED_CARDS (incl. 4 placeholder) seeded if first load
4. CardStack renders: dynamic cards (top) → separator → pinned cards (bottom, incl. placeholders)
5. User navigates to /agents/123
   → <DeckTrackerSync entity="agent" id="123" ... /> mounts
   → drawCard() updates store → CardStack re-renders with new card
6. User clicks placeholder "Cerca Agente" in CardStack
   → usePlaceholderActions intercepts click (card.isPlaceholder=true)
   → SearchAgentSheet opens
   → User searches, selects game, selects KB
   → AgentCreationSheet opens with pre-filled data
   → Agent created → redirect to /chat/{threadId}
   → New agent card auto-pushed to CardStack via drawCard()
```

### KB Selection Data Flow

```
1. User selects game in SearchAgentSheet
2. Fetch documents: GET /api/v1/shared-games/{gameId}/documents (NEW endpoint)
3. Auto-selection algorithm:
   a. Filter by documentType
   b. Select latest Rulebook (type=0) by version (semver or date)
   c. Select latest Errata (type=1) if exists
   d. Mark as pre-selected (checked)
4. User optionally toggles expansions/homerules
5. Selected document IDs + summary passed to AgentCreationSheet
6. AgentCreationSheet renders with skipGameSelection + skipKBUpload
7. On submit: useCreateAgentFlow sends { ...config, documentIds }
```

## Files to Create

| File | Purpose |
|------|---------|
| `src/hooks/usePlaceholderActions.ts` | Hook per intercettare click su placeholder cards e aprire sheet |
| `src/components/layout/DeckTrackerSync.tsx` | Client component per auto-push card in Server Component pages |
| `src/components/sheets/SearchAgentSheet.tsx` | Sheet ricerca agente con scope collezione + shared |
| `src/components/sheets/KBSelectionPanel.tsx` | Pannello selezione KB con auto-select smart |
| `src/components/sheets/SearchGameSheet.tsx` | Sheet ricerca gioco |
| `src/components/sheets/SessionSheet.tsx` | Sheet sessioni attive + creazione |
| `src/components/sheets/ToolkitSheet.tsx` | Sheet toolkit generico con griglia strumenti |
| `src/hooks/queries/useSharedGameDocuments.ts` | Hook React Query per documenti shared game (user-facing) |
| `src/components/toolkit/DiceRoller.tsx` | Strumento dado |
| `src/components/toolkit/Timer.tsx` | Strumento timer/countdown |
| `src/components/toolkit/Counter.tsx` | Strumento contatore +/- |
| `src/components/toolkit/Scoreboard.tsx` | Strumento segnapunti N giocatori |
| `src/components/toolkit/Randomizer.tsx` | Strumento selezione casuale da lista |

## Files to Modify

| File | Change |
|------|--------|
| `src/stores/use-card-hand.ts` | Aggiungere `isPlaceholder?: boolean` e `placeholderAction?: string` a `HandCard` |
| `src/config/entity-actions.ts` | Aggiungere `PLACEHOLDER_ACTION_CARDS` costante + `ALL_DEFAULT_CARDS` export combinato |
| `src/components/layout/UnifiedShell/CardStackItem.tsx` | Stile dashed per placeholder, no discard, click intercept |
| `src/components/layout/UnifiedShell/HandDrawerCard.tsx` | Stile dashed per placeholder, click intercept |
| `src/components/agent/config/AgentCreationSheet.tsx` | Nuove props: `initialDocumentIds`, `skipGameSelection`, `skipKBUpload`, `initialDocumentSummary` |
| `src/hooks/queries/useCreateAgentFlow.ts` | Aggiungere `documentIds?: string[]` a `CreateAgentFlowInput` |
| `src/lib/api/clients/agentsClient.ts` | Metodo `createWithSetup` deve forwardare `documentIds` nel payload API |
| `src/lib/api/clients/sharedGamesClient.ts` | Nuovo metodo `getDocuments(gameId)` per endpoint user-facing |
| `src/components/layout/UnifiedShell/UnifiedShellClient.tsx` | Migrazione placeholder per utenti returning (seeding guard) |
| `src/components/dashboard/DashboardRenderer.tsx` | Rimuovere `AgentsSidebar` dal layout |
| `src/app/(authenticated)/agents/[id]/page.tsx` | Aggiungere `<DeckTrackerSync>` |
| `src/app/(authenticated)/knowledge-base/[id]/page.tsx` | Aggiungere `drawCard` direttamente (gia Client Component) |

## Backend Changes

### Nuovo endpoint: User-facing documents per gioco

L'endpoint `GET /api/v1/admin/shared-games/{gameId}/documents` esiste gia per admin. Serve un equivalente user-facing:

```
GET /api/v1/shared-games/{gameId}/documents
```

**Differenze dall'endpoint admin**:
- Autenticazione: utente autenticato (non admin)
- Filtro: solo documenti `isActive=true`
- Accesso: verificare che l'utente abbia il gioco in libreria O che il gioco sia `isRagPublic=true` O che l'utente abbia dichiarato ownership
- Response: stessa struttura `SharedGameDocument[]` (id, documentType, version, tags, isActive, createdAt)

**Implementazione**: Nuova query CQRS `GetGameDocumentsForUserQuery` nel bounded context SharedGameCatalog o KnowledgeBase.

## Testing Strategy

### Unit Tests
- `useCardHand` esteso: placeholder card non discardable, placeholderAction routing
- `usePlaceholderActions`: apertura sheet corretto per ogni action type
- `DeckTrackerSync`: chiama drawCard on mount, no duplicate
- KB auto-selection algorithm: versione corretta rulebook, errata selection, edge cases (no errata, versioni multiple, un solo KB)
- Toolkit components: DiceRoller (distribuzione, config dadi), Timer (countdown, reset), Counter (+/-/reset), Scoreboard (add player, update score), Randomizer (selezione da lista)

### Integration Tests
- SearchAgentSheet: search → select game → KB panel → conferma → wizard opens con pre-fill
- SessionSheet: lista sessioni attive → click naviga → crea nuova
- PlaceholderCard click → sheet corretto si apre
- AgentCreationSheet con pre-fill: skippa steps, mostra riepilogo KB

### E2E Tests
- Full flow: login → dashboard → click "Cerca Agente" → search → select game → select KB → create agent → chat opens → agent card nel CardStack
- CardStack persistence: aggiungi card → refresh → card presenti (sessionStorage) + pinned (localStorage)
- Placeholder cards sempre visibili dopo clear/refresh
- Toolkit: apri sheet → usa dado → risultato visualizzato

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Placeholder cards confuse con card normali | UX confusion | Stile dashed distinto, no discard button, label con prefisso "+" |
| Backend endpoint documenti non pronto | KB selection bloccata | Implementare endpoint early, o mock con dati statici per frontend dev |
| AgentCreationSheet pre-fill rompe stato wizard | Bug nel wizard | Test estensivi su tutti i path: con pre-fill, senza, parziale |
| Rimozione AgentsSidebar perde funzionalita | Utenti non trovano agenti | CardStack gia mostra agenti; verificare che DEFAULT_PINNED_CARDS includa link agenti |
| Performance CardStack con placeholder + cards | Rendering lento | Placeholder sono solo 4, impatto minimo. Max 10 cards (FIFO). No virtualizzazione necessaria |

## Open Questions (v2)

1. **Sync backend**: Quando implementare persistenza server-side delle card pinned?
2. **Notifiche nel deck**: Le card dovrebbero mostrare badge notifica (es. "nuovi messaggi in chat")?
3. **Placeholder personalizzabili**: L'utente potra aggiungere/rimuovere placeholder action in futuro?
4. **Toolkit state persistence**: Gli strumenti toolkit dovrebbero salvare ultimo stato (es. ultimo dado configurato)?
