# Layout & Navigazione — Redesign

**Data**: 2026-03-30
**Stato**: Approvato
**Scope**: `apps/web/src/components/layout/`

---

## Obiettivo

Semplificare il layout e la navigazione di MeepleAI eliminando elementi ridondanti, correggere il bug della doppia bottom nav mobile, e introdurre una CTA contestuale che cambia in base alla sezione attiva.

---

## Decisioni di Design

### 1. Paradigma generale
**TopNav sticky + Sidebar icone (hover-expand)** — invariato rispetto all'attuale.

### 2. TopNav

| Posizione | Contenuto |
|-----------|-----------|
| Sinistra | emoji sezione + titolo / breadcrumb |
| Centro | CTA contestuale (vedi tabella sotto) |
| Destra | NotificationBell + UserMenuDropdown |

**Rimosso**: search globale (⌘K), badge "sessione live", bottone "+ Nuova" fisso, NavContextTabs.

#### CTA contestuale per sezione

| Sezione | Label CTA | Azione |
|---------|-----------|--------|
| `/library` | + Aggiungi gioco | Apre ricerca catalogo BGG |
| `/sessions` | + Nuova sessione | Avvia wizard nuova sessione |
| `/chat` | + Nuova chat | Apre nuova conversazione AI |
| `/game-nights` | + Organizza serata | Crea nuova game night |
| `/agents` | + Nuovo agente | Crea / configura agente |
| Altre sezioni | *(vuoto)* | Nessun elemento al centro |

La CTA desktop usa lo stesso colore/azione del FAB mobile per coerenza visiva.

### 3. Sidebar desktop (lg+)

- **Comportamento**: 52px a riposo → 220px on hover/focus-within (invariato)
- **Fix**: usa `UNIFIED_NAV_ITEMS` da `config/navigation.ts` invece di items hardcoded

#### Struttura voci

**Sezione Navigazione**
1. Dashboard 🏠 — `/dashboard`
2. Libreria 📚 — `/library` (include wishlist come tab interna)
3. Sessioni 🎲 — `/sessions`
4. Giocatori 👥 — `/players`
5. Storico partite 🕐 — `/play-records`

**Sezione AI**
6. Chat RAG ✨ — `/chat`
7. Agenti 🤖 — `/agents`

**Pinned bottom**
8. Impostazioni ⚙️ — `/settings`

**Rimossi dalla sidebar**: Wishlist (tab di Libreria), Serate, Documenti, Knowledge Base.

### 4. Mobile — Bottom Tab Bar

**Unica** bottom nav a 5 posizioni con FAB centrale contestuale.

| Posizione | Voce | Route |
|-----------|------|-------|
| 1 | Home 🏠 | `/dashboard` |
| 2 | Libreria 📚 | `/library` |
| 3 | **FAB +** | CTA contestuale (stessa logica topnav) |
| 4 | Chat ✨ | `/chat` |
| 5 | Profilo 👤 | `/profile` |

**Fix**: eliminare `MobileBottomNav` (duplicata) — tenere e aggiornare `UserTabBar`.

**Comportamento FAB**: stessa azione della CTA desktop per la sezione corrente. Su sezioni senza CTA definita, apre un bottom sheet con le azioni rapide globali (nuova sessione, aggiungi gioco, nuova chat).

### 5. Elementi rimossi

| Componente | Motivo |
|------------|--------|
| `NavContextTabs` | Seconda riga di nav ridondante — i tab di sezione tornano come UI contestuale dentro la pagina |
| `MobileBottomNav` | Duplicata di `UserTabBar`, causa sovrapposizione fixed su mobile |
| Search globale in TopNav | Non richiesta come feature di navigazione |
| Badge "sessione live" in TopNav | Se necessario, andrà in un elemento contestuale dentro la pagina sessione |

---

## Architettura dei Componenti

### Componenti modificati

```
UserShellClient.tsx         — rimuove MobileBottomNav, rimuove ContextBar (o la mantiene opzionale)
UserTopNav.tsx              — sostituisce search/badge con ContextualCTA
NavContextTabs.tsx          — eliminare
HybridSidebar.tsx           — usa UNIFIED_NAV_ITEMS, aggiorna lista voci
UserTabBar.tsx              — aggiunge FAB centrale contestuale, aggiunge tab Profilo
```

### Nuovo componente

```
ContextualCTA.tsx           — legge pathname, restituisce config CTA (label, href/action, colore)
```

### Config CTA

```typescript
// config/contextual-cta.ts
export interface CtaConfig {
  label: string;
  href?: string;
  onClick?: () => void;
  gradient: string; // es. 'from-amber-600 to-amber-400'
}

export function getCtaForPathname(pathname: string): CtaConfig | null
```

---

## Comportamento Responsive

| Breakpoint | Sidebar | TopNav CTA | Bottom Nav |
|------------|---------|------------|------------|
| Mobile (`< lg`) | nascosta | nascosta | FAB nel tab bar |
| Desktop (`≥ lg`) | visibile (52→220px hover) | visibile al centro | nascosta |

---

## Cosa NON cambia

- Logica di autenticazione e routing
- `AdminShell` e navigazione admin (scope separato)
- `ContextBar` store e logica (il componente resta, ma viene rimosso dal `UserShellClient` default — le pagine che lo usano lo attivano esplicitamente)
- Design tokens, colori, font
- `DashboardEngineProvider` e game mode

---

## File da modificare

```
apps/web/src/components/layout/UserShell/UserShellClient.tsx
apps/web/src/components/layout/UserShell/UserTopNav.tsx
apps/web/src/components/layout/UserShell/HybridSidebar.tsx
apps/web/src/components/layout/UserShell/UserTabBar.tsx
apps/web/src/components/layout/UserShell/NavContextTabs.tsx   ← eliminare
apps/web/src/config/navigation.ts                              ← aggiungere Dashboard (mancante da UNIFIED_NAV_ITEMS), Giocatori, Storico partite
```

## File da creare

```
apps/web/src/config/contextual-cta.ts
apps/web/src/components/layout/UserShell/ContextualCTA.tsx
```

## File da eliminare

```
apps/web/src/components/ui/navigation/MobileBottomNav.tsx     ← dopo migrazione
```
