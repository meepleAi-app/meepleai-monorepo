# Wiring Guide — Dal Mock JSX al Componente Reale

## Anatomia di un mock

Ogni file in `design/` ha questa struttura:

```
sp4-game-detail.html        ← Host page: carica React/Babel/tokens
sp4-game-detail.jsx         ← Componente showcase con N stati side-by-side
```

Il `.jsx` contiene:
- Header del file con `route`, `US`, `componenti v2 nuovi` (vedi commenti)
- Componenti riusabili dichiarati (es. `EntityChip`, `ConnectionBar`, `ChatBubble`)
- Componente di stato (es. `<GameDetailDesktop game={...}>`)
- Un `<Root>` che mostra TUTTI gli stati impilati (mobile + desktop, default/empty/loading/error)

Quando porti nel codebase reale, **scarta il Root showcase**. Ti interessano solo:
1. I componenti riusabili
2. Il componente di stato principale (es. `GameDetailDesktop`)

## Esempio: portare `sp4-game-detail.jsx` nel codebase

### Step 1 — Leggi il file mock con Claude Code

```
Leggi design/sp4-game-detail.jsx ed estrai:
- I componenti riusabili (escluso Root)
- Il componente principale GameDetailDesktop
- Tutti i prop in input
- Tutti gli stati interni (useState)
- Tutti gli eventi (onClick, onChange)
- Le entity coinvolte (game, kb, agent, session, player)
- Quali sub-flow apre (modali, drawer, navigation)
```

### Step 2 — Mappa al backend

Esempio di output atteso da Claude Code:

```yaml
componente: GameDetailDesktop
route_target: /games/[id]
data_loader:
  endpoint: GET /api/games/{id}?include=stats,sessions:5,agents,kb
  shape:
    id: string
    title: string
    cover: { gradient: string, emoji: string }
    publisher, year, players, weight: ...
    stats: { sessionsCount, winsByPlayer, avgDuration }
    sessions: Session[]
    agents: Agent[]
    kb: { docs, chunks, lastIndexed }
    
write_endpoints:
  - POST /api/games/{id}/sessions  (start new session)
  - PATCH /api/games/{id}          (edit metadata)
  - DELETE /api/games/{id}          (remove from library)

navigation:
  - tabs change URL hash: #info, #sessions, #chat, #stats, #kb
  - "Avvia sessione" → /games/{id}/sessions/new

sub_components_to_extract:
  - GameHero (cover + title + stats)
  - GameTabs (5 tab navigation)
  - ConnectionBar (already in prod ✓)
  - SessionRow
  - AgentMiniCard
```

### Step 3 — Implementa nel codebase

Prompt a Claude Code:

```
Crea il componente <GameDetail> in src/features/games/GameDetail.tsx
basato sul mock design/sp4-game-detail.jsx.

Vincoli:
- TypeScript, React 18
- Usa il client API esistente (src/lib/api.ts) — non hardcodare URL
- Riusa <ConnectionBar /> e <MeepleCard /> da src/components/ui/
- Stati richiesti: default | loading (skeleton) | error | empty (no sessions yet)
- Estrai i sub-componenti: GameHero, GameTabs (controlled), SessionRow, AgentMiniCard
- Cabla onClickStartSession → router.push(`/games/${id}/sessions/new`)
- Cabla onClickEditMetadata → apre <EditGameDrawer /> (componente esistente?)

Per i dati: usa SWR / React Query (qualunque sia nel codebase).
Quando l'endpoint backend non esiste, lascia un TODO commentato + mock locale.

Mostrami solo i file diff prima di applicare.
```

## Pattern di mapping ricorrenti

### 1. `data.js` → API client

I mock importano `data.js` con array hardcoded:

```jsx
// in design/data.js
const games = [{ id: 'g-wingspan', title: 'Wingspan', ... }];

// in mock
const game = DS.games.find(g => g.id === 'g-wingspan');
```

Da sostituire con:

```ts
// in src
const { data: game, isLoading, error } = useGame(id);
```

### 2. `useState` per drawer/modal → URL state o context

I mock usano `useState` interno:

```jsx
const [drawerOpen, setDrawerOpen] = useState(false);
```

Nel codebase, preferisci URL state o un Drawer global store:

```ts
// URL: /games/wingspan?drawer=add-session
const drawer = useSearchParams().get('drawer');
```

### 3. Entity Chips → componente unico

I mock dichiarano `EntityChip` inline. Estrailo una volta sola in `src/components/ui/EntityChip.tsx` e tipizza:

```ts
export type EntityType = 'game' | 'player' | 'session' | 'agent' | 'kb' | 'chat' | 'event' | 'toolkit' | 'tool';

export function EntityChip({ entity, children, size = 'sm' }: Props) {
  // ...
}
```

### 4. ConnectionBar → riusabile cross-entity

`ConnectionBar` è già in produzione (PR #549 / #552). NON ricrearlo. Importalo:

```ts
import { ConnectionBar } from '@/components/ui/v2/connection-bar';

<ConnectionBar
  connections={[
    { entityType: 'session', count: 12, label: 'Sessioni', isEmpty: false },
    // ...
  ]}
/>
```

### 5. Drawer cascade (cascadeNavigationStore)

I mock mostrano drawer come `Bottom-sheet` su mobile e `Side-panel right` su desktop. Nel codebase c'è `cascadeNavigationStore` che gestisce lo stack. Per ogni nuovo drawer:

```ts
import { useCascadeNavigation } from '@/lib/cascade';

const { push, pop } = useCascadeNavigation();
push({ component: 'HouseRuleDrawer', props: { gameId, originalQuery } });
```

## Quando il mock include un componente nuovo

Esempi di componenti che NON esistono ancora in produzione (sono "v2 nuovi"):

| Componente | Definito in | Dove metterlo nel codebase |
|---|---|---|
| `HouseRuleDrawer` | `sp7-library-game-agent.jsx`, `sp6-libro-game-play-session.jsx` | `src/components/ui/v2/house-rule-drawer/` |
| `ConfidenceBadge` | `sp6-libro-game-play-session.jsx`, `sp7-library-game-agent.jsx` | `src/components/ui/v2/confidence-badge/` |
| `SuggestedQueriesRow` | `sp7-library-game-agent.jsx` | `src/components/ui/v2/suggested-queries-row/` |
| `ChatHistoryTimeline` | `sp4-agent-detail.jsx` | `src/components/ui/v2/chat-history-timeline/` |
| `KbDocList` | `sp4-agent-detail.jsx`, `sp4-kb-detail.jsx` | `src/components/ui/v2/kb-doc-list/` |
| `CitationExpandedPanel` | `sp7-library-game-agent.jsx`, `sp4-citation-pdf-viewer.jsx` | `src/components/ui/v2/citation-expanded-panel/` |
| `LibraryGameAgentShell` | `sp7-library-game-agent.jsx` | `src/components/ui/v2/library-game-agent-shell/` |
| `StepIndicator` | `sp6-libro-game-*.jsx`, `sp7-game-night-create.jsx` | `src/components/ui/v2/step-indicator/` |
| `ConfirmModal` | shared SP6/SP7 | `src/components/ui/v2/confirm-modal/` |

Per ognuno, prompt a Claude Code:

```
Estrai <HouseRuleDrawer> dal mock design/sp7-library-game-agent.jsx (vedi
linea ~415 a ~620). Crea src/components/ui/v2/house-rule-drawer/ con:
- index.tsx
- HouseRuleDrawer.tsx (componente)
- HouseRuleDrawer.test.tsx (test base: open/close, submit, validation)
- types.ts (props + Rule shape)

Cabla onSave → POST /api/games/{gameId}/house-rules
con payload { title, body, originalQuestion, citation, tags }.

L'API restituisce { id, createdAt, createdBy } da merge nello store.

Storybook story con 3 stati: empty/create/edit.
```

## Errori comuni da evitare

1. **Non importare React via Babel CDN** in produzione. I mock lo fanno solo per essere standalone.
2. **Non riusare i nomi delle props 1:1** se nel codebase ne hai versioni più tipizzate. Adatta.
3. **Non scordare i `data-testid`** quando un mock ne ha (es. SSE streaming).
4. **Non sovrascrivere `EntityChip` se esiste già** nel codebase — verifica prima.
5. **Non implementare animazioni custom** se il codebase usa `framer-motion` o equivalente — converti.

## Quando completare un'iterazione

Una schermata è "fatta" quando:

- [x] Render 1:1 con il mock (mobile + desktop)
- [x] Tutti gli stati (default/loading/empty/error) presenti
- [x] Dati arrivano da API reale (non mock locale)
- [x] Eventi cablati (click, form submit, navigazione)
- [x] Test unit + integration base
- [x] Storybook o equivalente
- [x] `data-comment-anchor` preservati (se nei mock ci sono)
- [x] Accessibility: focus visibile, aria-label, `prefers-reduced-motion`
- [x] PR review passa

Solo dopo passi alla schermata successiva.
