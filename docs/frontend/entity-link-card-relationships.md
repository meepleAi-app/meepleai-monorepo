# EntityLink — Card Relationships Design Spec

> **Status**: Design approved, ready for implementation
> **Last updated**: 2026-02-23
> **Epic**: EntityLink System + Card Navigation Graph Completion

---

## 1. Contesto

Il sistema EntityLink permette di creare collegamenti espliciti tra entità di tipo diverso.
I link sono visibili direttamente sulla MeepleCard e approfondibili nel Drawer.

### Tipi di entità supportati

| Tipo | Colore HSL | Icona |
|---|---|---|
| `game` | `25 95% 45%` (Orange) | Gamepad2 |
| `player` | `262 83% 58%` (Purple) | User |
| `session` | `240 60% 55%` (Indigo) | PlayCircle |
| `agent` | `38 92% 50%` (Amber) | Bot |
| `document` | `210 40% 55%` (Slate) | FileText |
| `chatSession` | `220 80% 55%` (Blue) | MessageCircle |
| `event` | `350 89% 60%` (Rose) | Calendar |
| `toolkit` | `142 70% 45%` (Green) | Wrench | ← **NUOVO** |

---

## 2. Link Type Taxonomy

### Colori per tipo (indipendenti dal colore entità)

| LinkType | Colore | Direzionalità | Descrizione |
|---|---|---|---|
| `expansion_of` | Amber `38 92% 50%` | → directed | Seafarers è espansione di Catan |
| `sequel_of` | Blue `220 80% 55%` | → directed | Pandemic Legacy S2 → S1 |
| `reimplements` | Orange `25 95% 45%` | → directed | Reimplementazione dell'originale |
| `companion_to` | Green `142 70% 45%` | ↔ bilateral | Giochi che si abbinano bene |
| `related_to` | Slate `210 40% 55%` | ↔ bilateral | Collegamento generico |
| `part_of` | Purple `262 83% 58%` | → directed | Session/Collection → Event |
| `collaborates_with` | Indigo `240 60% 55%` | ↔ bilateral | Agent pipeline multi-agente |
| `specialized_by` | Violet `270 70% 58%` | → directed | Agent generalista → specialista |

### Chip visuale per link type

```
[amber  · expansion of →]    [blue  · sequel of →]
[green  · companion ↔   ]    [slate · related    ]
[purple · part of →     ]    [indigo· collaborates↔]
```

---

## 3. Regole di Dominio (Business Rules)

```
BR-01: Game  → auto-crea Toolkit 1:1 quando aggiunto alla library
BR-02: Agent → deve avere min 1 KbCard appartenente al Game associato
BR-03: Session → min 1 Game (non più opzionale), min 1 Player
BR-04: EntityLink scope=user  → IsAdminApproved = true automaticamente
BR-05: EntityLink scope=shared → richiede ruolo Admin
BR-06: KbCard "personal" → OwnerUserId != null (creata dall'utente)
BR-07: KbCard "system"   → da SharedGameCatalog, condivisa
BR-08: EntityLink è unico per (sourceType, sourceId, targetType, targetId, linkType)
```

---

## 4. Mockup — MeepleCard

### 4.1 Game Card (grid variant) — completa

```
┌─────────────────────────────────────────┐
│ [🎲 GAME]                    [🔗 3]     │  ← EntityLinkBadge (top-right corner)
│ ┌───────────────────────────────────┐   │
│ │                                   │   │
│ │           [game image]            │   │
│ │                                   │   │
│ └───────────────────────────────────┘   │
│ Catan                                   │  font-quicksand font-bold
│ ★★★★☆  BGG 7.2  · 2-4 👥  · 60-120min  │  font-nunito text-sm
├─────────────────────────────────────────┤
│  🤖 2 agents   📚 5 KB   🔧 Toolkit    │  ← strutturali (esistenti)
├─────────────────────────────────────────┤
│  — Navigate to —                        │  ← CardNavigationFooter (esistente)
│  [🤖] [📚] [🔧] [🎮]                   │
├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤  ← divider sottile
│  🔗  [amber·exp→] Seafarers  +2        │  ← EntityLinkPreviewRow (NUOVO)
└─────────────────────────────────────────┘
```

### 4.2 Agent Card con link

```
┌─────────────────────────────────────────┐
│ [🤖 AGENT]                   [🔗 1]     │
│ ┌───────────────────────────────────┐   │
│ │      [avatar / bot icon]          │   │
│ └───────────────────────────────────┘   │
│ Catan Expert                            │
│ 3 KB cards · Attivo                     │
├─────────────────────────────────────────┤
│  📚 Rulebook   📚 FAQ   📚 Strategy     │
├─────────────────────────────────────────┤
│  — Navigate to —                        │
│  [🎲] [📚] [💬]                        │
├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤
│  🔗  [indigo·collab↔] CatanCoach        │
└─────────────────────────────────────────┘
```

### 4.3 Session Card con link

```
┌─────────────────────────────────────────┐
│ [🎮 SESSION]                 [🔗 1]     │
│ In corso · 45min                        │
│ Game Night — Venerdì                    │
│ 4 giocatori · Catan                     │
├─────────────────────────────────────────┤
│  👤 Marco  👤 Lisa  👤 Anna  👤 Paolo   │
├─────────────────────────────────────────┤
│  — Navigate to —                        │
│  [🎲] [👤]                             │
├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤
│  🔗  [purple·part of→] Torneo Estivo    │
└─────────────────────────────────────────┘
```

---

## 5. Mockup — EntityLinkBadge

Badge glassmorphism nell'angolo in alto a destra della card image:

```
Position: absolute top-2 right-2
z-index: above image, below quick-actions

┌──────────────┐
│ 🔗 3         │  bg-white/80 backdrop-blur-[8px]
└──────────────┘  border border-white/50 rounded-full
                  px-2 py-0.5 text-xs font-semibold
                  shadow-warm-sm
                  cursor-pointer
                  hover: scale-105, shadow-warm-md
```

- Mostra solo se count > 0
- Click → apre Drawer al tab Links
- Colore testo: neutral (non entity-colored)

---

## 6. Mockup — EntityLinkPreviewRow

Ultima riga del footer card, dopo il CardNavigationFooter:

```
─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ (divider dashed)
🔗  [type chip]  {firstName}  +{N-1}     [→]
```

**Varianti:**
```
N=1:  🔗  [amber·exp→]  Seafarers
N=3:  🔗  [amber·exp→]  Seafarers  +2
N=0:  (hidden)
```

Stile:
```
px-3 py-1.5 flex items-center gap-2
text-xs font-nunito
cursor-pointer hover:bg-white/10 rounded-b-2xl
transition 350ms cubic-bezier(0.4,0,0.2,1)
```

---

## 7. Mockup — Drawer: Links Tab

```
╔══════════════════════════════════════════╗
║  🎲 Catan                               ║
╠══════════════════════════════════════════╣
║  Overview  │  KB  │  Agent  │  🔗 Links ║  ← tab attivo
╠══════════════════════════════════════════╣
║                                          ║
║  EXPANSIONS  (2)              [+ Add]   ║
║  ┌────────────────────────────────────┐  ║
║  │ 🎲  Catan: Seafarers              │  ║
║  │     [amber · expansion of →]      │  ║
║  │                          [→] [✕]  │  ║
║  ├────────────────────────────────────┤  ║
║  │ 🎲  Catan: Cities & Knights       │  ║
║  │     [amber · expansion of →]      │  ║
║  │                          [→] [✕]  │  ║
║  └────────────────────────────────────┘  ║
║                                          ║
║  COMPANIONS  (1)              [+ Add]   ║
║  ┌────────────────────────────────────┐  ║
║  │ 🎲  Ticket to Ride                │  ║
║  │     [green · companion ↔]         │  ║
║  │                          [→] [✕]  │  ║
║  └────────────────────────────────────┘  ║
║                                          ║
║  RELATED  (0)                 [+ Add]   ║
║  ┄┄┄┄┄  nessun collegamento  ┄┄┄┄┄      ║
║                                          ║
╚══════════════════════════════════════════╝
```

**Note UI:**
- Sezioni collassabili (accordion) se count > 3
- `[✕]` visibile solo se user è owner del link
- BGG-imported links: `[🌐 BGG]` badge invece di `[✕]` (non eliminabili)
- `[→]` apre nested drawer o naviga alla entità target

---

## 8. Mockup — EntityLinkCard (mini, riusabile)

```
┌─────────────────────────────────────────┐
│ [entity icon colored]  Nome Entità      │
│ [type chip: amber·expansion of →]       │
│                              [→]  [✕]  │
└─────────────────────────────────────────┘
```

Props:
```typescript
interface EntityLinkCardProps {
  link: EntityLinkDto;
  onNavigate: (entityType: string, entityId: string) => void;
  onRemove?: (linkId: string) => void;  // undefined = non rimuovibile
  showBggBadge?: boolean;
}
```

---

## 9. Mockup — AddEntityLinkModal

```
╔══════════════════════════════════════════╗
║  🔗 Aggiungi collegamento               ║
║  ────────────────────────────           ║
║                                          ║
║  Tipo collegamento                       ║
║  ┌──────────────────────────────────┐   ║
║  │ [amber·🎲] Expansion Of     [▾] │   ║
║  └──────────────────────────────────┘   ║
║                                          ║
║  Cerca gioco                            ║  ← label cambia in base al tipo
║  ┌──────────────────────────────────┐   ║
║  │ 🔍  Catan...                     │   ║
║  ├──────────────────────────────────┤   ║
║  │ ○  🎲  Catan: Seafarers         │   ║
║  │ ○  🎲  Catan: Cities & Knights  │   ║
║  │ ○  🎲  Catan: Traders           │   ║
║  └──────────────────────────────────┘   ║
║                                          ║
║  Nota (opzionale)                       ║
║  ┌──────────────────────────────────┐   ║
║  │ Es: richiede la base game...      │   ║
║  └──────────────────────────────────┘   ║
║                                          ║
║  [Annulla]              [+ Collega →]   ║
╚══════════════════════════════════════════╝
```

**Logica link type → target entity type:**
```typescript
const LINK_TYPE_TARGET_ENTITY: Record<EntityLinkType, MeepleEntityType[]> = {
  expansion_of:     ['game'],
  sequel_of:        ['game'],
  reimplements:     ['game'],
  companion_to:     ['game'],
  related_to:       ['game','agent','document','session','event'],
  part_of:          ['event'],
  collaborates_with:['agent'],
  specialized_by:   ['agent'],
};
```

---

## 10. Mockup — Graph View

### Layout: pagina `/library/[gameId]` — sezione "Connections"

```
┌───────────────────────────────────────────────────────────┐
│  🔗 Connections Graph                    [List] [Graph]   │
├───────────────────────────────────────────────────────────┤
│                                                            │
│         ┌──────────────────┐                              │
│  exp →  │ 🎲 Seafarers     │                              │
│    ╔════╧══════════════════╧════╗                          │
│    ║     🎲  CATAN              ║  ← nodo centrale         │
│    ╚════╤══════════════════╤════╝  (entity color border)  │
│  exp →  │ 🎲 Cities        │                              │
│         └──────────────────┘                              │
│                    │                                       │
│              comp ↔│                                       │
│         ┌──────────┴───────┐                              │
│         │ 🎲 Ticket to Ride│                              │
│         └──────────────────┘                              │
│                                                            │
│  [🤖 Catan Expert] ──has_agent── ●                        │
│  [📚 Rulebook KB ] ──has_kb   ── ●                        │
│                                                            │
│  Zoom: [─────●────]  [Reset]  [Fit]                       │
└───────────────────────────────────────────────────────────┘
```

**Implementazione**: React Flow (`@xyflow/react`)

```typescript
// Node types
interface EntityNode {
  id: string;
  type: 'entityNode';
  data: { entityType: MeepleEntityType; label: string; isCentral: boolean };
}

// Edge types
interface EntityEdge {
  id: string;
  source: string; target: string;
  type: 'entityEdge';
  data: { linkType: EntityLinkType; isBidirectional: boolean };
  // color: LINK_TYPE_COLORS[linkType]
  // markerEnd: arrow (directed) | none (bilateral, arrow on both ends)
}
```

**View toggle**: List (RelatedEntitiesSection) ↔ Graph (React Flow)
Persistenza: `localStorage` per preferenza list/graph per utente

---

## 11. Inventory Componenti

| Componente | File | Priorità | Note |
|---|---|---|---|
| `EntityLinkBadge` | `meeple-card/entity-link-badge.tsx` | 🔴 core | Corner top-right |
| `EntityLinkPreviewRow` | `meeple-card/entity-link-preview-row.tsx` | 🔴 core | Footer last row |
| `EntityLinkChip` | `ui/entity-link-chip.tsx` | 🔴 core | Riusabile ovunque |
| `EntityLinkCard` | `ui/entity-link-card.tsx` | 🔴 core | Mini-card in drawer |
| `RelatedEntitiesSection` | `ui/related-entities-section.tsx` | 🔴 core | Drawer Links tab |
| `AddEntityLinkModal` | `ui/add-entity-link-modal.tsx` | 🔴 core | Modal creazione |
| `EntityRelationshipGraph` | `ui/entity-relationship-graph.tsx` | 🟡 avanzato | React Flow graph |
| Links tab in Drawer | `meeple-card/extra-meeple-card-drawer.tsx` | 🔴 core | Estensione esistente |

---

## 12. Nuovi Props MeepleCard

```typescript
// Aggiungere a MeepleCardProps:

/** Numero totale di EntityLink espliciti (per badge) */
linkCount?: number;

/** Preview del primo link (per EntityLinkPreviewRow) */
firstLinkPreview?: {
  linkType: EntityLinkType;
  targetName: string;
  totalCount: number;
};

/** Callback: apre drawer al tab Links */
onLinksClick?: () => void;
```

---

## 13. Epic Breakdown

### Epic A — EntityRelationships Backend (14 issues)

| # | Issue | BC |
|---|---|---|
| 1 | `EntityRelationships` BC scaffold + DDD folders | nuovo BC |
| 2 | `EntityLink` aggregate + `EntityLinkType` enum | Domain |
| 3 | EF Core config + migration `entity_links` | Infrastructure |
| 4 | `CreateEntityLinkCommand` + Validator + Handler | Application |
| 5 | `DeleteEntityLinkCommand` + Handler | Application |
| 6 | `GetEntityLinksQuery` (per source, con bidirezionalità) | Application |
| 7 | `GetEntityLinkCountQuery` (per badge) | Application |
| 8 | Endpoints user: `GET/POST/DELETE /api/v1/library/entity-links` | Routing |
| 9 | Endpoints admin: `GET/POST/DELETE /api/v1/admin/entity-links` | Routing |
| 10 | Fix: `Session.Games` da opzionale a `1..*` (migration + validator) | SessionTracking |
| 11 | Fix: `Agent` validazione min 1 KbCard dal Game associato | KnowledgeBase |
| 12 | `BggExpansionImporter` (fetch + auto-create EntityLinks) | Infrastructure |
| 13 | `adminClient` methods EntityLink (frontend api client) | Frontend |
| 14 | Tests: unit + integration + E2E (70+ test) | Tests |

### Epic B — GameToolkit (13 issues)

| # | Issue |
|---|---|
| 1 | `Toolkit` + `ToolkitWidget` domain + `WidgetType` enum |
| 2 | EF Core config + migration `toolkits`, `toolkit_widgets` |
| 3 | Auto-create default Toolkit on Game add to library |
| 4 | `GetToolkitQuery` + `OverrideToolkitCommand` |
| 5 | `ToolkitSessionState` in SessionTracking BC |
| 6 | Widget: `RandomGenerator` |
| 7 | Widget: `TurnManager` |
| 8 | Widget: `ScoreTracker` |
| 9 | Widget: `ResourceManager` |
| 10 | Widget: `NoteManager` |
| 11 | Widget: `Whiteboard` |
| 12 | `toolkit` MeepleEntityType + Card + Drawer |
| 13 | Tests |

### Epic C — Card Navigation Graph Completion (9 issues)

| # | Issue |
|---|---|
| 1 | `EntityLinkBadge` component |
| 2 | `EntityLinkPreviewRow` component |
| 3 | `EntityLinkChip` component |
| 4 | `EntityLinkCard` mini-card component |
| 5 | `RelatedEntitiesSection` + Links tab in Drawer |
| 6 | `AddEntityLinkModal` con search autocomplete |
| 7 | `EntityRelationshipGraph` (React Flow, list/graph toggle) |
| 8 | Aggiorna `ENTITY_NAVIGATION_GRAPH` (collection, event, toolkit) |
| 9 | Tests E2E navigation links |

---

## 14. Design Tokens di Riferimento

```css
/* Card */
font-family: 'Quicksand' (headings), 'Nunito' (body)
border-radius: rounded-2xl
transition: 350ms cubic-bezier(0.4,0,0.2,1)
shadow: shadow-warm-sm → shadow-warm-md (hover)

/* EntityLinkBadge */
background: bg-white/80
backdrop-filter: backdrop-blur-[8px]
border: border border-white/50
border-radius: rounded-full
padding: px-2 py-0.5
font-size: text-xs font-semibold

/* EntityLinkChip */
background: hsl({linkTypeColor} / 0.15)
border: 1px solid hsl({linkTypeColor} / 0.4)
color: hsl({linkTypeColor})
border-radius: rounded-full
padding: px-1.5 py-0.5
font-size: text-[10px] font-medium
```
