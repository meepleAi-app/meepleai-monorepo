# Analisi della Navigabilità — MeepleAI Web App

**Data**: 2026-02-21
**Stato**: ✅ Implementato — Tutti i fix critici e alti applicati
**Scope**: Frontend — `apps/web/src`

---

## Indice

1. [Architettura attuale della navigazione](#1-architettura-attuale)
2. [Mappa delle route](#2-mappa-delle-route)
3. [Problemi critici identificati](#3-problemi-critici)
4. [Analisi per componente](#4-analisi-per-componente)
5. [Matrice di accessibilità dei link](#5-matrice-di-accessibilità)
6. [Raccomandazioni](#6-raccomandazioni)
7. [Piano di priorità](#7-piano-di-priorità)

---

## 1. Architettura attuale

### Layout complessivo

```
┌──────────────────────────────────────────────────┐
│  UniversalNavbar (h-14, fixed top, z-110)         │  ← TUTTI i breakpoint
│  [Logo → /dashboard] [Search] [Notifiche] [Profilo]│
├──────────────────────┬───────────────────────────┤
│ Sidebar (solo desktop│ Contenuto principale       │
│ md+, 220px/60px)     │ - Breadcrumb (solo mobile) │
│                      │ - Pagina corrente          │
│  SidebarContextNav:  │                           │
│  • /dashboard        │                           │
│    → DashboardPanel  │                           │
│  • /library/*        │                           │
│    → LibraryPanel    │                           │
│  • /games/*          │                           │
│    → GamesPanel      │                           │
│  • default           │                           │
│    → SidebarNav      │                           │
├──────────────────────┴───────────────────────────┤
│  UnifiedActionBar (h-56, fixed bottom, md:hidden) │  ← Solo mobile
│  [Home] [Libreria] [FAB: Chat] [Catalogo] [...]   │
└──────────────────────────────────────────────────┘
```

### File chiave

| File | Responsabilità |
|------|---------------|
| `config/navigation.ts` | Source of truth — `UNIFIED_NAV_ITEMS` |
| `components/layout/Sidebar/SidebarContextNav.tsx` | Routing context → pannello sidebar |
| `components/layout/Sidebar/SidebarNav.tsx` | Navigazione standard (default context) |
| `components/layout/Navbar/UniversalNavbar.tsx` | Navbar globale con logo → `/dashboard` |
| `components/layout/ActionBar/UnifiedActionBar.tsx` | Bottom nav mobile |
| `components/layout/MobileNavDrawer.tsx` | Drawer hamburger mobile |
| `components/layout/Breadcrumb/Breadcrumb.tsx` | Indicatore contesto (solo mobile) |

---

## 2. Mappa delle route

### Route principali autenticate

```
/dashboard                    ← Hub principale (entry point)
├── /play-records             ← Sessioni recenti (SOLO link da DashboardPanel)
├── /library                  ← Libreria
│   ├── /library/favorites
│   ├── /library/wishlist
│   ├── /library/archived
│   ├── /library/private
│   └── /library/proposals
├── /games                    ← Catalogo (visibile a tutti)
│   └── /games/[id]           ← Dettaglio gioco
├── /players                  ← Giocatori (SOLO link da DashboardPanel)
├── /chat                     ← Chat AI
├── /profile                  ← Profilo (hideFromMainNav: true)
├── /settings                 ← Impostazioni (solo ProfileBar)
├── /agents                   ← Agenti (gruppo "strumenti")
├── /sessions                 ← Sessioni (gruppo "strumenti")
├── /knowledge-base           ← RAG (NESSUN link nella nav principale)
├── /editor                   ← Editor (solo ProfileBar, minRole: editor)
└── /admin/**                 ← Area admin (solo ProfileBar, minRole: admin)
```

### Context map del SidebarContextNav

| Pathname | Pannello mostrato | Link Dashboard? |
|----------|-------------------|-----------------|
| `/dashboard` | DashboardPanel | ✅ Sì (Overview) |
| `/library/*` | LibraryPanel | ❌ No |
| `/games/*` | GamesPanel | ❌ No |
| `/play-records` | SidebarNav (default) | ✅ Sì |
| `/players` | SidebarNav (default) | ✅ Sì |
| `/chat` | SidebarNav (default) | ✅ Sì |
| `/profile` | SidebarNav (default) | ✅ Sì |
| `/settings` | SidebarNav (default) | ✅ Sì |
| `/agents` | SidebarNav (default) | ✅ Sì |
| `/sessions` | SidebarNav (default) | ✅ Sì |
| `/admin/*` | SidebarNav (default) | ✅ Sì |

---

## 3. Problemi critici

### P1 — CRITICO: LibraryPanel senza link "← Dashboard"

**Impatto**: Chiunque navighi in `/library/*` perde visibilità del link Dashboard nella sidebar.

**Riproducibilità**:
1. Vai su `/dashboard`
2. Clicca "Libreria" nella sidebar → vai su `/library`
3. La sidebar diventa LibraryPanel
4. **Non esiste più "Dashboard" nella sidebar**
5. L'unico ritorno è il logo nella navbar (non ovvio)

**File coinvolto**: `SidebarContextNav.tsx` — `LibraryPanel` (riga 110-124)

```tsx
// ❌ Mancante: nessun link a /dashboard in LibraryPanel
function LibraryPanel({ isCollapsed }) {
  return (
    <nav>
      <SidebarLink href="/library" ... />
      <SidebarLink href="/library/favorites" ... />
      // ... solo link interni alla libreria
    </nav>
  );
}
```

---

### P2 — CRITICO: GamesPanel senza link "← Dashboard"

**Impatto**: Uguale a P1 ma per tutte le route `/games/*` incluso `/games/[id]`.

**Riproducibilità**:
1. Vai su `/dashboard`
2. Clicca "Catalogo giochi" → vai su `/games`
3. La sidebar diventa GamesPanel (solo filtri)
4. Naviga su `/games/some-id` (dettaglio gioco)
5. **Nessun link Dashboard né breadcrumb "← Catalogo"**

**File coinvolto**: `SidebarContextNav.tsx` — `GamesPanel` (riga 126-142)

---

### P3 — ALTO: Nessun breadcrumb desktop

**Impatto**: Gli utenti desktop non hanno un indicatore visivo del percorso corrente. Il componente `Breadcrumb` è dichiaratamente mobile-only (position fixed bottom, sopra ActionBar).

**File coinvolto**: `components/layout/Breadcrumb/Breadcrumb.tsx`

```tsx
// Il Breadcrumb usa posizionamento fisso bottom, visibile SOLO su mobile
// Desktop: nessun breadcrumb disponibile
```

**Pagine problematiche**:
- `/library/favorites` → nessun percorso visivo "Dashboard → Libreria → Preferiti"
- `/games/[id]` → nessun percorso visivo "Dashboard → Catalogo → Nome Gioco"
- `/admin/analytics` → nessun percorso "Dashboard → Admin → Analytics"

---

### P4 — ALTO: Route orfane — link esistono solo in DashboardPanel

Le seguenti route sono accessibili **solo** quando si è su `/dashboard` (DashboardPanel) e scompaiono dalla navigazione una volta entrati:

| Route | Dove è linkato | Disponibile fuori dal Dashboard? |
|-------|---------------|----------------------------------|
| `/play-records` | DashboardPanel "Sessioni recenti" | ❌ No (non in UNIFIED_NAV_ITEMS) |
| `/players` | DashboardPanel "Giocatori" | ❌ No (non in UNIFIED_NAV_ITEMS) |

**Scenario problematico**:
- Utente su `/play-records` vuole tornare all'elenco → la sidebar mostra SidebarNav standard → `/play-records` NON è nell'elenco → utente non trova più la voce.
- Navigare "avanti" da `/play-records` (es. su un dettaglio) → ancora più lost.

---

### P5 — ALTO: Route completamente inaccessibili dalla navigazione principale

| Route | Accessibile da | Nota |
|-------|----------------|------|
| `/knowledge-base` | Nessun link nel nav principale | Nessuna voce in UNIFIED_NAV_ITEMS |
| `/toolkit` | Nessun link visibile (priority 4, rimosso da header) | In navigation.ts commento: "removed from header, kept for ActionBar" ma non presente in NAV_ITEMS |
| `/settings` | Solo ProfileBar dropdown | Non nel nav principale |
| `/editor` | Solo ProfileBar dropdown (minRole: editor) | Corretto per ruolo, ma scomparso |

---

### P6 — MEDIO: Cambio di sidebar confuso (context switch)

**Problema UX**: Il contenuto della sidebar cambia completamente a seconda della sezione. Un utente abituale impara dove trovare Dashboard nella SidebarNav standard, ma quando entra in `/library` o `/games`, quella voce sparisce e appare un pannello contestuale diverso.

**Attesa utente**: La navigazione "principale" rimane sempre visibile; i link contestuali si aggiungono.
**Comportamento attuale**: La navigazione principale viene **sostituita** dai link contestuali.

---

### P7 — MEDIO: Logo come unico "tasto Home" persistente non è ovvio

Il logo nella navbar (`<Link href="/dashboard">`) è l'unico elemento di navigazione **sempre visibile** che porta alla dashboard. Questo è un pattern UX noto ma richiede che l'utente sappia che il logo è cliccabile.

**Problematico per**:
- Nuovi utenti non abituati all'app
- Contesti dove il logo è piccolo o collassato

---

### P8 — BASSO: `/games/[id]` mostra GamesPanel con filtri lista, non contesto dettaglio

**Impatto**: Quando si apre un dettaglio gioco (`/games/abc123`), la sidebar mostra filtri per la lista catalogo (Top BGG, 2 Giocatori, ecc.). Questi filtri non sono contestualmente rilevanti per una pagina di dettaglio.

**Aspettativa**: Il pannello dettaglio gioco dovrebbe avere link come "← Torna al catalogo", "Sessioni con questo gioco", "Aggiungi alla libreria".

---

## 4. Analisi per componente

### 4.1 SidebarContextNav — Riepilogo criticità

```
getContextKey(pathname):
  /dashboard  → 'dashboard'  ✅ ha link dashboard
  /library/*  → 'library'    ❌ manca link dashboard
  /games/*    → 'games'      ❌ manca link dashboard (incl. /games/[id])
  default     → 'default'    ✅ SidebarNav include dashboard
```

**Soluzione minima**: Aggiungere un link "← Dashboard" (o separatore + link) in cima a `LibraryPanel` e `GamesPanel`.

### 4.2 UNIFIED_NAV_ITEMS — Route mancanti

Dalla `config/navigation.ts` attuale:

```typescript
// Route presenti in UNIFIED_NAV_ITEMS:
dashboard, library (+ children), chat, catalog, profile, agents, sessions

// Route assenti da UNIFIED_NAV_ITEMS (esistono come pagine):
play-records, players, knowledge-base, toolkit, settings, editor, admin/**
```

**Analisi**: `play-records` e `players` sono route di navigazione frequente (esposte nel DashboardPanel) ma non hanno una voce stabile nella navigazione globale.

### 4.3 UniversalNavbar — Punti di forza e lacune

**Punti di forza**:
- ✅ Logo sempre linkato a `/dashboard` (h-14, fisso top, sempre visibile)
- ✅ Ricerca giochi funzionale con link diretto a `/games/{id}`
- ✅ ProfileBar con accesso a `/profile`, `/settings`, `/editor`, `/admin/overview`

**Lacune**:
- ❌ Nessuna breadcrumb integrata nella navbar su desktop
- ❌ Il ProfileBar mostra `/settings` ma questa non è nella nav principale

### 4.4 UnifiedActionBar (Mobile) — Copertura

| Item | Link | Presente? |
|------|------|-----------|
| Home | `/dashboard` | ✅ |
| Libreria | `/library` | ✅ |
| FAB | `/chat` | ✅ |
| Catalogo | `/games` | ✅ |
| Play Records | `/play-records` | ❌ |
| Giocatori | `/players` | ❌ |
| Sessioni | `/sessions` | ❌ (overflow?) |
| Agenti | `/agents` | ❌ (overflow?) |

La mobile ActionBar copre solo 3 voci + FAB. Il resto va nel hamburger menu o è inaccessibile direttamente.

### 4.5 MobileNavDrawer — Copertura

Il drawer hamburger usa `useNavigationItems()` che restituisce le voci di `UNIFIED_NAV_ITEMS`:
- ✅ Dashboard, Library, Chat, Catalog, Agents, Sessions
- ❌ Play Records, Players, Knowledge Base non sono inclusi

---

## 5. Matrice di accessibilità dei link

La seguente tabella indica da quale pagina si può raggiungere ogni route, tramite quale meccanismo:

| Route destinazione | Via Navbar Logo | Via Sidebar (Desktop) | Via ActionBar (Mobile) | Via Hamburger (Mobile) | Via Breadcrumb | Via Link in pagina |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| `/dashboard` | ✅ sempre | ✅ solo se default context | ✅ Home | ✅ | ❌ | dipende |
| `/library` | ❌ | ✅ via SidebarNav o LibraryPanel | ✅ | ✅ | ❌ | dipende |
| `/games` | ❌ | ✅ | ✅ Catalogo | ✅ | ❌ | dipende |
| `/chat` | ❌ | ✅ | ✅ FAB | ✅ | ❌ | — |
| `/play-records` | ❌ | ✅ solo da Dashboard context | ❌ | ❌ | ❌ | ⚠️ solo da dashboard page |
| `/players` | ❌ | ✅ solo da Dashboard context | ❌ | ❌ | ❌ | ⚠️ solo da dashboard page |
| `/profile` | ❌ | ❌ (hideFromMainNav) | ❌ | ❌ | ❌ | ✅ ProfileBar |
| `/settings` | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ ProfileBar |
| `/agents` | ❌ | ✅ (strumenti) | ❌ | ✅ | ❌ | — |
| `/sessions` | ❌ | ✅ (strumenti) | ❌ | ✅ | ❌ | — |
| `/knowledge-base` | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ **INACCESSIBILE** |
| `/admin/**` | ❌ | ❌ (solo se autenticato admin) | ❌ | ❌ | ❌ | ✅ ProfileBar (admin) |

**Legenda**: ✅ = sempre accessibile | ⚠️ = condizionale | ❌ = non accessibile via questo meccanismo

---

## 6. Raccomandazioni

### R1 — Aggiungere link "← Dashboard" a LibraryPanel e GamesPanel (P1, P2)

**Priorità**: CRITICA
**File**: `SidebarContextNav.tsx`
**Pattern suggerito**:

```tsx
function LibraryPanel({ isCollapsed }) {
  return (
    <nav>
      {/* Back link */}
      <SidebarLink href="/dashboard" icon={ArrowLeft} label="Dashboard" ... />
      <hr />
      {/* Library links */}
      <SidebarLink href="/library" ... />
      ...
    </nav>
  );
}
```

Alternativa: aggiungere sempre una sezione "Home" in cima a ogni pannello contestuale.

---

### R2 — Breadcrumb desktop sopra il contenuto principale (P3)

**Priorità**: ALTA
**File**: `AuthenticatedLayout.tsx`, nuovo componente `DesktopBreadcrumb`
**Approccio**: Breadcrumb orizzontale statico posizionato nella zona di contenuto (sotto navbar, sopra pagina), visibile solo su desktop (`hidden md:flex`).

```
Dashboard > Libreria > Preferiti
Dashboard > Catalogo > Nome Gioco
```

---

### R3 — Aggiungere `/play-records` e `/players` a UNIFIED_NAV_ITEMS (P4)

**Priorità**: ALTA
**File**: `config/navigation.ts`
**Opzione A**: Aggiungerli come voci di primo livello (gruppo "strumenti" o separato)
**Opzione B**: Aggiungerli come figli di "Dashboard" in un pannello dedicato

---

### R4 — Aggiungere `/knowledge-base` alla navigazione (P5)

**Priorità**: ALTA — route attualmente inaccessibile
**File**: `config/navigation.ts`
**Azione**: Aggiungere voce `knowledge-base` in UNIFIED_NAV_ITEMS, probabilmente nel gruppo "strumenti" o come voce principale.

---

### R5 — Breadcrumb o back-button su pagine dettaglio (P8)

**Priorità**: MEDIA
**File**: `app/(authenticated)/games/[id]/page.tsx`
**Approccio**: Aggiungere un `<BackButton>` inline nella pagina o nel layout, per tornare a `/games`.

---

### R6 — Riconsiderare la strategia di context switch della sidebar (P6)

**Priorità**: MEDIA
**Opzioni**:

**A) Navigation primaria sempre visibile** (raccomandato):
- Sidebar divisa in due zone:
  - Zona fissa superiore: link principali sempre visibili (Dashboard, Library, Games, Chat)
  - Zona contestuale inferiore: link specifici del contesto corrente

**B) Back link persistente nel pannello contestuale** (soluzione minima):
- Ogni pannello contestuale mostra "← Dashboard" come prima voce
- Mantiene l'approccio attuale ma risolve il problema di ritorno

**C) Breadcrumb prominente** (complementare):
- Sopra il contenuto principale, breadcrumb fisso (già raccomandato in R2)

---

## 7. Piano di priorità

| # | Issue | Priorità | Stato | Note |
|---|-------|----------|-------|------|
| R1 | Back link in LibraryPanel e GamesPanel | 🔴 CRITICA | ✅ Fatto | `SidebarContextNav.tsx` — link Dashboard in cima a ogni pannello contestuale |
| R4 | `/knowledge-base` nella navigazione | 🔴 CRITICA | ✅ Fatto | `config/navigation.ts` — aggiunto in gruppo `strumenti` |
| R3 | `/play-records` e `/players` in nav | 🟠 ALTA | ✅ Fatto | `config/navigation.ts` — aggiunti in gruppo `strumenti` |
| R2 | Breadcrumb desktop | 🟠 ALTA | ✅ Fatto | Nuovo `DesktopBreadcrumb.tsx` + fix `AuthenticatedLayout.tsx` |
| R5 | Back button su `/games/[id]` | 🟡 MEDIA | ✅ Già presente | Il componente aveva già `<ArrowLeft /> Torna al Catalogo` (riga 248) |
| R6 | Strategia sidebar ibrida | 🟡 MEDIA | 🕐 Posticipato | R1 risolve il caso critico; la ristrutturazione completa è un refactor a sé |

---

## Appendice: Componenti e file di riferimento

```
apps/web/src/
├── config/
│   ├── navigation.ts                   ← UNIFIED_NAV_ITEMS, filterNavItemsByRole
│   └── navigation.types.ts
├── components/layout/
│   ├── AuthenticatedLayout.tsx         ← Layout principale autenticato
│   ├── Navbar/
│   │   ├── UniversalNavbar.tsx         ← Logo → /dashboard, Search, Profile
│   │   └── ProfileBar.tsx              ← /profile, /settings, /editor, /admin
│   ├── Sidebar/
│   │   ├── Sidebar.tsx                 ← Contenitore sidebar desktop
│   │   ├── SidebarContextNav.tsx       ← DashboardPanel, LibraryPanel, GamesPanel
│   │   └── SidebarNav.tsx              ← Navigazione standard (default context)
│   ├── ActionBar/
│   │   └── UnifiedActionBar.tsx        ← Mobile bottom nav
│   ├── Breadcrumb/
│   │   └── Breadcrumb.tsx              ← Mobile-only context indicator
│   └── MobileNavDrawer.tsx             ← Hamburger drawer
└── hooks/
    ├── useNavigationItems.ts           ← Hook per UNIFIED_NAV_ITEMS filtrati
    └── useUnifiedActionBar.ts          ← Hook per ActionBar
```

---

*Documento generato il 2026-02-21 — Analisi della navigabilità MeepleAI Web*
