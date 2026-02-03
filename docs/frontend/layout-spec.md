# MeepleAI Layout Specification v1.0

> **Status**: Approved
> **Created**: 2026-02-01
> **Authors**: Brainstorming Session

---

## Overview

Layout system per MeepleAI con focus su mobile-first, AI-first experience. Il design separa navigazione (top) da azioni contestuali (bottom) con un Smart FAB dinamico su mobile.

---

## 1. Struttura Generale

### Mobile (<640px)

```
┌─────────────────────────┐
│ ☰ │   Logo   │ 🔍 │ 👤 │  ← Navbar (collapsible)
├─────────────────────────┤
│                         │
│                         │
│        Content          │
│                         │
│                   [FAB] │  ← Smart FAB contestuale
│   [Breadcrumb: ctx]     │  ← Context indicator
├─────────────────────────┤
│      ActionBar          │  ← 3 azioni + overflow
└─────────────────────────┘
```

### Tablet (640-1024px)

```
┌───────────────────────────────────┐
│ Logo │ Nav Items │ 🔍 │ Profile   │  ← Navbar espansa
├───────────────────────────────────┤
│                                   │
│            Content                │
│                                   │
│   [Breadcrumb: context]           │
├───────────────────────────────────┤
│        ActionBar (4 azioni)       │
└───────────────────────────────────┘
```

### Desktop (>1024px)

```
┌─────────────────────────────────────────────┐
│ Logo │ Libreria │ Catalogo │ 🔍 │ Login/Reg │  ← Navbar completa
├─────────────────────────────────────────────┤
│                                             │
│                                             │
│              Content                        │
│                                             │
│                                             │
├─────────────────────────────────────────────┤
│   [Breadcrumb: context]                     │  ← Sopra ActionBar
├─────────────────────────────────────────────┤
│   ActionBar sticky (5-6 azioni)             │  ← Sempre visibile in basso
└─────────────────────────────────────────────┘
```

---

## 2. Header Components

### 2.1 Navbar

| Elemento | Mobile | Tablet | Desktop |
|----------|--------|--------|---------|
| **Logo** | Centro, link home | Sinistra | Sinistra |
| **Nav primaria** | Hamburger (☰) | Inline parziale | Inline completo |
| **Search** | Icona → espande | Input medio | Input largo |
| **Search scope** | SharedGameCatalog | SharedGameCatalog | SharedGameCatalog |

#### Nav Items (quando visibili)
1. Libreria (`/library`)
2. Catalogo (`/catalog`)
3. Sessioni (`/sessions`)
4. (Hamburger only) Impostazioni, Aiuto, Documenti

### 2.2 ProfileBar

| Stato | Mobile | Desktop |
|-------|--------|---------|
| **Guest** | 👤 icona → Login modal | "Login" / "Registrati" buttons |
| **Logged in** | Avatar → dropdown | Avatar + username + dropdown |

#### Dropdown Menu (logged in)
- Profilo
- Le mie statistiche
- Impostazioni
- ---
- Logout

---

## 3. Smart FAB (Mobile Only)

### 3.1 Principio

FAB singolo che cambia icona/azione in base al contesto corrente. Mostra sempre l'azione più probabile per quel contesto.

### 3.2 Context Mapping

| Contesto | FAB Icon | Azione Primaria | Long-press Menu |
|----------|----------|-----------------|-----------------|
| Library Overview | ➕ | Aggiungi gioco | 🔍 Cerca, 📷 Scan barcode |
| Library vuota | ➕ | Aggiungi primo gioco | 🔗 Importa BGG, 📷 Scan |
| Game Detail (in libreria) | ▶️ | Nuova sessione | 🤖 Chiedi AI, 📤 Condividi |
| Game Detail (non in libreria) | ➕ | Aggiungi a libreria | ❤️ Wishlist, 📤 Condividi |
| Session Active | 🤖 | Chiedi all'AI | ⏱️ Timer, 📝 Punteggi |
| Session Setup | ▶️ | Inizia partita | 👥 Add player, 🎲 Randomize |
| Session End | 💾 | Salva risultati | 📤 Condividi, 🔄 Rigioca |
| Document Viewer | 🤖 | Chiedi su sezione | 🔍 Cerca nel doc, 📋 Copia |
| Catalog Browse | 🔍 | Ricerca avanzata | ➕ Proponi gioco |
| Search Results | ➕ | Aggiungi top result | 📋 Confronta giochi |
| Wishlist | 🛒 | Cerca dove comprare | ➕ Sposta in libreria |
| Notifications | ✅ | Segna tutte lette | 🗑️ Elimina tutte |

### 3.3 Visibility Rules

```typescript
interface FABVisibility {
  hideWhen: [
    'keyboard_open',
    'modal_open',
    'chat_input_focused',
    'fast_scroll',      // velocity > threshold
    'error_critical'
  ];
  showAfterScrollStop: 300; // ms delay
}
```

### 3.4 Positioning

```typescript
interface FABPosition {
  right: 16;      // dp from edge
  bottom: 80;     // dp from bottom (above breadcrumb + actionbar)
  size: 56;       // dp diameter
  elevation: 6;   // default shadow
}
```

### 3.5 Long-press Quick Menu

```
         ┌─────────────┐
         │ 🤖 Chiedi AI │  ← Azione alternativa 1
         ├─────────────┤
         │ 📤 Condividi │  ← Azione alternativa 2
         └──────┬──────┘
                │
              [▶️]  ← FAB principale
```

- **Max items**: 2-3 azioni
- **Animazione**: scale + fade in (150ms)
- **Dismiss**: tap outside, selezione azione, back gesture
- **Haptic**: medium feedback on appear

### 3.6 Visual States

| Stato | Appearance |
|-------|------------|
| Default | 56dp, elevation 6, brand color |
| Hover/Focus | elevation 8, subtle glow |
| Pressed | 54dp, elevation 4, darker shade |
| Disabled | 40% opacity, no elevation |
| Loading | Spinner icon, pulsing |
| Hidden | Fade out 150ms |

---

## 4. Breadcrumb Contestuale

### 4.1 Scopo

Indicatore visivo del contesto corrente, sincronizzato con FAB e ActionBar.

### 4.2 Formato

```
[Icon] Label breve
```

**Esempi:**
- 🎲 Settlers of Catan
- 📚 La mia libreria
- 🤖 Chat AI
- 📄 Regolamento.pdf
- 🔍 Risultati ricerca

### 4.3 Posizionamento

```
Mobile:
                    [FAB]
        ┌──────────────────────┐
        │  🎲 Settlers of Catan │  ← Breadcrumb
        ├──────────────────────┤
        │     ActionBar        │
        └──────────────────────┘

Desktop:
        ┌──────────────────────────────────────┐
        │  🎲 Settlers of Catan                │  ← Breadcrumb
        ├──────────────────────────────────────┤
        │     ActionBar integrata              │
        └──────────────────────────────────────┘
```

### 4.4 Animazione

- **Transition**: fade + slide 8dp
- **Duration**: 150ms
- **Sync**: morphs insieme a FAB e ActionBar

---

## 5. ActionBar

### 5.1 Responsive Slots

| Breakpoint | Azioni Visibili | Overflow Menu |
|------------|-----------------|---------------|
| Mobile <640px | 3 | ⋮ sempre presente |
| Tablet 640-1024px | 4 | ⋮ se >4 azioni |
| Desktop >1024px | 5-6 | ⋮ se >6 azioni |

### 5.2 Priority System

Ogni azione ha priority 1-6. Le azioni con priority più bassa appaiono prima.

```typescript
interface Action {
  id: string;
  icon: string;
  label: string;
  priority: 1 | 2 | 3 | 4 | 5 | 6;
  contexts: Context[];
}

function getVisibleActions(context: Context, screenWidth: number): Action[] {
  const slots = screenWidth < 640 ? 3 : screenWidth < 1024 ? 4 : 6;
  return ACTIONS
    .filter(a => a.contexts.includes(context))
    .sort((a, b) => a.priority - b.priority)
    .slice(0, slots);
}
```

### 5.3 Context Action Definitions

#### Library Overview
| Priority | Azione | Icon |
|----------|--------|------|
| 1 | Aggiungi gioco | ➕ |
| 2 | Filtra | 🔍 |
| 3 | Ordina | ↕️ |
| 4 | Cambia vista | 📊 |
| 5 | Esporta | 📤 |

#### Game Detail
| Priority | Azione | Icon |
|----------|--------|------|
| 1 | Nuova sessione | ▶️ |
| 2 | Aggiungi a libreria | ➕ |
| 3 | Chiedi regole AI | 🤖 |
| 4 | Wishlist | ❤️ |
| 5 | Condividi | 📤 |
| 6 | Segnala errore | 🐛 |

#### Session Active
| Priority | Azione | Icon |
|----------|--------|------|
| 1 | Chiedi all'AI | 🤖 |
| 2 | Timer | ⏱️ |
| 3 | Punteggi | 📝 |
| 4 | Pausa | ⏸️ |
| 5 | Termina | ✅ |

#### AI Chat
| Priority | Azione | Icon |
|----------|--------|------|
| 1 | Invia | 📤 |
| 2 | Allega | 📎 |
| 3 | Voice input | 🎤 |
| 4 | Nuova chat | 🔄 |
| 5 | Cronologia | 📋 |

### 5.4 Empty States

| Stato | ActionBar Behavior |
|-------|-------------------|
| Libreria vuota | CTA: `[➕ Aggiungi il tuo primo gioco]` |
| Nessun risultato | CTA: `[🔄 Modifica ricerca]` |
| Errore | Azioni disabilitate (grayed) + retry button |
| Loading | Skeleton placeholders |

### 5.5 Multi-selection Mode

Quando utente seleziona multipli elementi:

```
┌─────────────────────────────────────┐
│ ✕ │ 3 selezionati │ 🏷️ │ 📁 │ 🗑️ │ ⋮ │
└─────────────────────────────────────┘
```

- Prima icona: deseleziona tutto
- Counter: elementi selezionati
- Azioni: batch operations

---

## 6. Transitions & Animations

### 6.1 Morph Configuration

```typescript
const morphConfig = {
  duration: 200,
  easing: 'cubic-bezier(0.4, 0, 0.2, 1)', // Material ease-out

  fab: {
    iconRotation: 90,     // degrees during morph
    scale: [0.9, 1.0],    // shrink then grow
    colorTransition: true
  },

  actionBar: {
    stagger: 50,          // ms between each icon
    direction: 'left-to-right'
  },

  breadcrumb: {
    fade: 150,            // ms
    slideDistance: 8      // dp
  }
};
```

### 6.2 Haptic Feedback (Mobile)

| Event | Feedback |
|-------|----------|
| FAB tap | Ripple + haptic light |
| FAB long-press | Tooltip + haptic medium |
| Context change | Subtle pulse + haptic light |
| Action success | Haptic success pattern |
| Action error | Haptic error pattern |

---

## 7. Conflict Resolution

### 7.1 Priority Rules

| Situazione | Comportamento |
|------------|---------------|
| Modal/Dialog aperto | ActionBar hidden, FAB hidden |
| Sidebar aperta (es. chat) | Primary content ActionBar prevale |
| Multi-selezione attiva | ActionBar switch a batch mode |
| Keyboard aperta (mobile) | ActionBar hidden, FAB hidden |
| Scroll veloce | FAB hidden temporarily |
| Error state critico | Focus su recovery, azioni limitate |

### 7.2 Z-Index Hierarchy

```
Modal overlay:     1000
Modal content:     1001
FAB:               900
Quick menu:        901
Breadcrumb:        800
ActionBar:         800
Content:           1
```

---

## 8. Accessibility

### 8.1 Requirements

- [ ] FAB ha aria-label dinamico basato su contesto
- [ ] ActionBar navigabile via keyboard (Tab, Enter, Space)
- [ ] Quick menu dismissable via Escape
- [ ] Breadcrumb è landmark region
- [ ] Tutti i touch target >= 44dp
- [ ] Color contrast ratio >= 4.5:1
- [ ] Reduced motion rispettato

### 8.2 Screen Reader Announcements

| Event | Announcement |
|-------|-------------|
| Context change | "Ora in [context]. Azione principale: [action]" |
| FAB action | "[action] attivato" |
| Multi-select enter | "Modalità selezione multipla. [n] elementi selezionati" |
| Multi-select exit | "Selezione annullata" |

---

## 9. Technical Notes

### 9.1 State Management

```typescript
interface LayoutState {
  currentContext: Context;
  fabVisible: boolean;
  fabAction: Action;
  quickMenuOpen: boolean;
  actionBarActions: Action[];
  breadcrumbLabel: string;
  multiSelectMode: boolean;
  selectedCount: number;
}
```

### 9.2 Context Detection

Il contesto viene determinato da:
1. Route corrente (`/library`, `/games/:id`, etc.)
2. Stato applicazione (sessione attiva, chat aperta)
3. Selezione utente (elementi selezionati)
4. Focus (input focused, modal open)

### 9.3 Performance Considerations

- FAB e ActionBar usano `will-change: transform` per GPU acceleration
- Morph animations usano solo `transform` e `opacity` (no layout thrashing)
- Long-press detection: 500ms threshold
- Scroll velocity detection: 50ms sampling rate

---

## Appendix A: Component Checklist

- [ ] `<Navbar />`
- [ ] `<ProfileBar />`
- [ ] `<HamburgerMenu />`
- [ ] `<GlobalSearch />`
- [ ] `<SmartFAB />`
- [ ] `<QuickMenu />`
- [ ] `<Breadcrumb />`
- [ ] `<ActionBar />`
- [ ] `<ActionBarItem />`
- [ ] `<OverflowMenu />`
- [ ] `<MultiSelectBar />`

---

## Appendix B: Revision History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-01 | Initial specification |
