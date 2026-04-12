# La Mia Mano — Quick Action Bar
**Feature Specification v1.0**
**Data**: 2026-04-09
**Bounded Context**: UserLibrary / UserPreferences (TBD)
**Stato**: ⚠️ **SUPERSEDED** — sostituita da `docs/superpowers/specs/2026-04-09-session-gameplay-flow-v2.md`

> **Nota (2026-04-09)**: Questa v1 modellava MyHand come 4 slot fissi persistiti lato server, popolati manualmente dall'utente. A seguito di brainstorming panel è emerso che il vero use case è un'esperienza contestuale legata alla Session/GamePlay, con Hand dinamica derivata e Diario eventi. Il plan v1 (`docs/superpowers/plans/2026-04-09-my-hand-feature.md`) è congelato. Non eseguire i Task 1+ sulla persistenza `UserHandSlot`. Riferirsi alla v2 per il nuovo modello.

---

---

## 1. Vision

**La Mia Mano** è una barra di azioni rapide persistente che permette all'utente di ancorare 4 entità (una per tipo) e accedere alle relative azioni frequenti in un singolo tocco, senza abbandonare la pagina corrente.

### Distinzione da DesktopHandRail (esistente)

| Aspetto | `DesktopHandRail` (esistente) | `MyHand` (nuovo) |
|---|---|---|
| **Scopo** | Context tracking navigazione | Quick actions persistenti |
| **Popolazione** | Automatica (visita entità) | Manuale (utente sceglie) |
| **Slot** | N dinamici, qualsiasi tipo | 4 fissi, tipo-vincolato |
| **Persistenza** | SessionStorage | Server + LocalStorage fallback |
| **Posizione desktop** | Left rail 76px | Right sidebar 280px |
| **Mobile** | Assente | Bottom action bar |

---

## 2. Slot Principali

| Slot | Entity Type | Colore token | Icona |
|---|---|---|---|
| **Toolkit** | `toolkit` | `--e-toolkit` | 🔧 |
| **Game** | `game` | `--e-game` | 🎮 |
| **Partita / Session** | `session` | `--e-session` | 🎯 |
| **AI** | `agent` | `--e-agent` | 🤖 |

---

## 3. Requisiti Funzionali

| ID | Requisito | Priorità |
|---|---|---|
| F-01 | Sistema a 4 slot fissi: toolkit, game, session, ai | P0 |
| F-02 | Slot inizialmente vuoti al primo login | P0 |
| F-03 | Ogni slot accetta solo l'entity type corrispondente | P0 |
| F-04 | Slot vuoto mostra CTA contestuale per tipo | P0 |
| F-05 | Slot popolato mostra MeepleCard (variant compact) + quick actions | P0 |
| F-06 | Quick actions eseguibili in 1 click senza navigazione full-page | P0 |
| F-07 | Stato slot persistito server-side (GET/PUT /api/v1/users/me/hand) | P0 |
| F-08 | Slot con entità eliminata mostra stato degradato + CTA reassign | P0 |
| F-09 | Desktop: sidebar destra collapsible (280px espanso / 52px collapsed) | P0 |
| F-10 | Mobile: bottom action bar (height auto, safe-area-inset-bottom) | P0 |
| F-11 | Swipe-up su mobile espande le quick actions | P1 |
| F-12 | Quick action "Dado casuale" per toolkit | P0 |
| F-13 | Quick action "Punteggio gruppo" per session | P0 |
| F-14 | Quick action "Chiedi all'agente" per AI | P0 |
| F-15 | Quick action "Apri gioco" per game | P0 |
| F-16 | Picker entità (modal/drawer) per assegnazione slot | P0 |
| F-17 | Long-press su MeepleCard esistente → "Aggiungi a La Mia Mano" | P1 |
| F-18 | Azioni che richiedono API disabilitate offline con badge visivo | P1 |

---

## 4. Quick Actions per Slot

### Slot Toolkit
| Action | Tipo | Descrizione |
|---|---|---|
| 🎲 Dado casuale | Local | Lancia dado configurato nel toolkit — risultato inline |
| 📋 Apri toolkit | Navigate | Naviga a `/library/games/{id}/toolkit` |
| ⚙️ Configura | Navigate | Apre configurazione toolkit |

### Slot Game
| Action | Tipo | Descrizione |
|---|---|---|
| 🎮 Vai al gioco | Navigate | Naviga a `/library/games/{id}` |
| ➕ Nuova partita | Modal | Apre modal creazione sessione pre-compilato |
| ❤️ Wishlist toggle | API | Toggle wishlist — feedback toast |

### Slot Partita/Session
| Action | Tipo | Descrizione |
|---|---|---|
| 🏆 Punteggio gruppo | Inline panel | Mostra scoreboard sessione senza cambio pagina |
| 📝 Nota rapida | Drawer | Apre mini-editor per nota sessione |
| 🎯 Stato sessione | Inline | Toggle attiva/pausa sessione |

### Slot AI
| Action | Tipo | Descrizione |
|---|---|---|
| 💬 Chiedi all'agente | Chat panel | Apre chat panel con agente preselezionato |
| 💡 Suggerimento tattico | SSE inline | Streaming risposta agente inline |
| 📚 Regole rapide | Inline panel | FAQ/regole dal KB dell'agente |

---

## 5. UI/UX Specification

### Desktop (≥768px)

```
┌─────────────────────────────────────────────────────┬──────────┐
│  TopBar (64px)                                       │          │
├────────┬──────────────────────────────────────────── │  MyHand  │
│ Hand   │                                             │ Sidebar  │
│ Rail   │   Main Content                              │  280px   │
│(sinistra│                                            │          │
│  76px) │                                             │[🔧 Slot] │
│        │                                             │[🎮 Slot] │
│        │                                             │[🎯 Slot] │
│        │                                             │[🤖 Slot] │
└────────┴──────────────────────────────────────────── └──────────┘
```

- **Posizione**: `right`, `sticky`, `full-height`
- **Width**: 280px espanso, 52px collapsed (solo icone slot)
- **Toggle**: pulsante collapse in header sidebar
- **z-index**: 30 (sotto TopBar z-40, sopra content)
- **Default**: espanso su ≥1280px, collapsed su 768–1279px

### Mobile (<768px)

```
┌─────────────────────────────────┐
│  TopBar (56px)                  │
├─────────────────────────────────┤
│                                 │
│   Main Content                  │
│                                 │
├─────────────────────────────────┤
│ MyHand Bottom Bar               │  ← 64px collapsed
│ [🔧][🎮][🎯][🤖]              │     auto expanded
└─────────────────────────────────┘
  ↑ env(safe-area-inset-bottom)
```

- Bottom bar: 4 icone slot centrate, tap espande quick actions
- Expanded: sheet bottom (`max-height: 40vh`, `border-radius-top: 16px`)
- Swipe-down per chiudere expanded state
- Touch target: ≥44×44px per ogni action button

### Slot States

| Stato | Visualizzazione |
|---|---|
| **Empty** | Icona tipo + label "Nessun [tipo]" + CTA "+ Seleziona" |
| **Populated** | MeepleCard (compact) + quick actions inline |
| **Degraded** | Badge ⚠️ + label "Non più disponibile" + CTA "Seleziona nuovo" |
| **Loading** | Skeleton animation |

---

## 6. Data Model

```typescript
type MyHandSlotType = 'toolkit' | 'game' | 'session' | 'ai'

interface MyHandSlot {
  slotType: MyHandSlotType
  entityId: string | null
  entityType: MeepleEntityType | null    // 'toolkit' | 'game' | 'session' | 'agent'
  entityLabel: string | null             // cached display name
  entityImageUrl: string | null          // cached thumbnail
  pinnedAt: string | null                // ISO 8601
  isEntityValid: boolean                 // false se entità eliminata/non accessibile
}

interface MyHandState {
  slots: Record<MyHandSlotType, MyHandSlot>
  isExpanded: boolean           // mobile: bottom bar open
  isSidebarCollapsed: boolean   // desktop: icon-only mode
  isLoading: boolean
  lastSyncedAt: string | null
}

// Mapping slot → entity type ammessi
const SLOT_ENTITY_MAP: Record<MyHandSlotType, MeepleEntityType[]> = {
  toolkit: ['toolkit'],
  game:    ['game'],
  session: ['session'],
  ai:      ['agent'],
}
```

---

## 7. API Endpoints (Backend)

```
GET    /api/v1/users/me/hand
       Response: { slots: MyHandSlotDto[] }

PUT    /api/v1/users/me/hand/{slotType}
       Body:     { entityId: string, entityType: string }
       Response: { slot: MyHandSlotDto }
       Errori:   400 (entityType non valido per slot), 404 (entità non trovata)

DELETE /api/v1/users/me/hand/{slotType}
       Response: 204 No Content
```

**Validazione server**: Verifica che `entityId` esista nel BC corrispondente allo `slotType`.

**Bounded Context di riferimento**: `UserLibrary` o `UserPreferences` — da decidere.

**Entity validation cross-BC**:
- `toolkit` slot → `GameToolbox` BC
- `game` slot → `GameManagement` / `SharedGameCatalog` BC
- `session` slot → `SessionTracking` BC
- `ai` slot → `KnowledgeBase` / `AgentMemory` BC

---

## 8. Acceptance Criteria (BDD)

```gherkin
# AC-01 — Stato iniziale
Given: utente al primo login
Then: tutti e 4 gli slot sono vuoti
 And: ogni slot mostra CTA specifica per il suo tipo

# AC-02 — Assegnazione slot
Given: slot Toolkit è vuoto
When: utente tocca lo slot
Then: si apre picker con i suoi toolkit disponibili
When: utente seleziona un toolkit
Then: slot mostra MeepleCard del toolkit selezionato
 And: stato aggiornato su server entro 2 secondi

# AC-03 — Quick action dado
Given: slot Toolkit ha un toolkit con dado configurato
When: utente preme "🎲 Dado casuale"
Then: risultato dado mostrato inline entro 500ms
 And: nessuna navigazione avviene

# AC-04 — Scoreboard inline
Given: slot Session ha una sessione attiva con partecipanti
When: utente preme "🏆 Punteggio gruppo"
Then: panel inline mostra punteggi correnti
 And: nessuna navigazione avviene

# AC-05 — Stato degradato
Given: slot Game contiene "Agricola"
When: l'utente rimuove Agricola dalla libreria
Then: slot mostra stato "Non più disponibile"
 And: CTA "Seleziona nuovo gioco" è visibile
 And: quick actions precedenti sono disabilitate

# AC-06 — Responsive
Given: viewport ≥ 768px
Then: MyHand visibile come sidebar destra

Given: viewport < 768px
Then: MyHand visibile come bottom bar con 4 icone slot

# AC-07 — Offline resilience
Given: utente offline
Then: quick actions che richiedono API mostrano icona cloud-off
 And: azioni locali (dado) restano funzionali

# AC-08 — Persistenza cross-session
Given: utente assegna un toolkit allo slot
When: utente fa logout e re-login
Then: slot Toolkit mostra ancora lo stesso toolkit
```

---

## 9. Architecture Decisions

| Decisione | Scelta | Rationale |
|---|---|---|
| Componente separato da DesktopHandRail | Sì | Semantica diversa: navigazione vs quick actions |
| Posizione desktop | Right sidebar | HandRail è a sinistra; simmetria e separazione semantica |
| Persistenza | Server + localStorage fallback | Consistenza multi-device; offline graceful |
| Slot fissi vs dinamici | 4 fissi per MVP | Riduce complessità UX e implementativa |
| Entity type per slot | 1 tipo per slot (toolkit/game/session/agent) | Coerenza semantica e azioni tipizzate |
| Mobile pattern | Bottom bar | Ergonomia mobile; allineamento con FloatingActionPill esistente |
| Optimistic update | Sì | UX immediata; rollback su errore server |

---

## 10. File da Creare/Modificare

### Nuovi file

```
apps/web/src/components/layout/MyHand/
├── MyHandSidebar.tsx          ← Desktop: sidebar right (280px / 52px collapsed)
├── MyHandBottomBar.tsx        ← Mobile: bottom bar + sheet expanded
├── MyHandSlot.tsx             ← Singolo slot con 3 stati (empty/populated/degraded)
├── MyHandSlotPicker.tsx       ← Modal/drawer selezione entità per slot
├── MyHandQuickActions/
│   ├── ToolkitActions.tsx     ← Dado, Apri toolkit, Configura
│   ├── GameActions.tsx        ← Vai al gioco, Nuova partita, Wishlist
│   ├── SessionActions.tsx     ← Punteggio, Nota rapida, Stato
│   └── AgentActions.tsx       ← Chat, Suggerimento, Regole rapide
└── index.ts

apps/web/src/stores/my-hand/store.ts    ← Zustand, localStorage + sync server
```

### File modificati

```
apps/web/src/components/layout/UserShell/DesktopShell.tsx
  → Aggiungere <MyHandSidebar /> nel layout right column

apps/web/src/app/layout.tsx (o UserShell root)
  → Aggiungere <MyHandBottomBar /> su mobile (responsive)
```

### Backend (nuovo)

```
apps/api/src/Api/BoundedContexts/UserLibrary/Hand/
  (oppure UserPreferences/Hand/ — da decidere)
  ├── GetUserHandQuery.cs
  ├── UpdateHandSlotCommand.cs
  ├── ClearHandSlotCommand.cs
  └── UserHandEndpoints.cs
```

---

## 11. Rischi e Open Questions

| Priorità | Tema | Decisione richiesta |
|---|---|---|
| 🔴 Alta | Bounded context backend | `UserLibrary` o `UserPreferences`? |
| 🔴 Alta | Conflitto visivo desktop | 3 elementi fissi (HandRail sx + MyHand dx + FloatingActionPill): gestire overlap su schermi stretti |
| 🟡 Media | Onboarding slot vuoti | Tooltip inline vs wizard guidato? |
| 🟡 Media | Sessione terminata nello slot | Auto-clear o mostra stato "Terminata" con CTA? |
| 🟢 Bassa | Estensibilità slot | Architettura `slots: Record<>` permette 5°+ slot in futuro |
| 🟢 Bassa | Long-press su MeepleCard | Implementare "Aggiungi a La Mia Mano" come P1 |

---

*Spec generata con sc:spec-panel — Expert Panel (Wiegers, Adzic, Cockburn, Fowler, Nygard, Newman, Crispin)*
*Ultimo aggiornamento: 2026-04-09*
