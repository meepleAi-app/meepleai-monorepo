# Navigation Simplification Design

**Date**: 2026-04-15
**Status**: Draft
**Scope**: Frontend navigation redesign — user area + admin area

## Problem

The current navigation system has 6 overlapping layers creating visual noise, duplicate CTAs (up to 3x), duplicate breadcrumbs (3x), and redundant back buttons. Components compete for attention and confuse the user about where to click.

### Current Architecture (6 layers)

| Layer | Component | What it shows |
|-------|-----------|---------------|
| 1 | TopBar (56px) | Logo + Home/Libreria/Sessioni + Search + Chat + Bell + Avatar |
| 2 | MiniNavSlot (48px) | Breadcrumb + contextual tabs + RecentsBar + CTA |
| 3 | HandRail (64-200px sidebar) | Pinned + recent entity cards |
| 4 | ActionPill (bottom, desktop) | Breadcrumb trail + CTA |
| 5 | FloatingActionPill (bottom) | Section label + CTA + secondary actions |
| 6 | ActionBar (bottom, mobile) | Mini-hand chips + CTA |

### Duplications Identified

- **Breadcrumb x3**: MiniNav text + ActionPill trail + FloatingActionPill label
- **CTA x3-4**: MiniNav + ActionPill + FloatingActionPill + in-page button (e.g., Library)
- **Back nav x2**: MiniNav tabs + explicit "← Back" button (4 pages)
- **Recents x2**: HandRail sidebar + RecentsBar in MiniNav
- **Dead code x2**: AdminBreadcrumbs.tsx + BackToHub.tsx (defined but never imported)

## Solution

Consolidate to **2 layers + ManaPips**: TopBar (global) + in-page content. Relational navigation (between connected entities) handled by ManaPips cascade system. Full menu in side drawer.

### Target Architecture

```
TopBar (48px) — Logo + Hamburger + Search + Avatar
  └─ Side Drawer (hamburger trigger)
Page Content
  ├─ "← Parent" contextual back link
  ├─ Title + CTA
  ├─ In-page tabs (where needed)
  ├─ MeepleCards with ManaPips → DeckStack → Drawer
  └─ Mobile CTA pill (< 768px only)
```

## Design Specifications

### 1. TopBar (48px)

**Layout**: `[☰ Hamburger] [🎲 MeepleAI] ———— [🔍] [Avatar]`

- **Hamburger** (left): opens side drawer
- **Logo**: link to home/dashboard
- **Search** (magnifying glass icon): expands on click as overlay input. Suggests pages + entities (games, sessions, players). ESC or click-outside closes.
- **Avatar**: opens dropdown with Notifications (badge count), Profile, Settings, Logout
- **Height**: 48px (reduced from current 56px)
- **Style**: glass morphism, subtle

**Admin variant**: `[☰] [🛡️ MeepleAI Admin] ———— [🔍] [Avatar]`
- Visually differentiated (accent border or background tint) to distinguish from user area

### 2. Side Drawer

**Trigger**: hamburger button or swipe-from-left (mobile)

**User Drawer**:
```
[Avatar + Name + Role]
─────────────────────────
🏠 Dashboard
📚 Libreria
🎯 Sessioni
🎲 Partite & Statistiche
💬 Chat AI
─────────────────────────
▸ Altro
  👥 Giocatori
  🌙 Serate
  🧰 Toolkit
  📝 Editor Agenti
─────────────────────────
🔔 Notifiche        (badge)
👤 Profilo
⚙️ Impostazioni
🛡️ Admin Hub    (admin only)
─────────────────────────
🚪 Logout
```

**Admin Drawer**:
```
[Avatar + Name + Admin role]
─────────────────────────
← Torna all'app
─────────────────────────
📊 Overview
  Dashboard
  Activity Feed
  System Health
─────────────────────────
🎮 Content
  Giochi
  Knowledge Base
  Email Templates
─────────────────────────
🤖 AI
  Mission Control
  RAG Inspector
  Agent Definitions
  ▸ Altro (Config, Usage, Analytics...)
─────────────────────────
👥 Users
  Tutti gli utenti
  Invitations
  Ruoli & Permessi
  ▸ Altro (Access Requests, Activity...)
─────────────────────────
▸ System (Monitor, Config, Notifications)
▸ Analytics
```

**Behavior**:
- **Desktop**: semi-transparent overlay, drawer from left ~280px, click-outside closes
- **Mobile**: full-height, slide-in animation
- **Active state**: current page highlighted (orange background)
- **"Altro" sub-menu**: expand/collapse with animation. Auto-opens if user is on a page inside it.
- **3 logical groups**: primary nav, secondary nav, account

### 3. In-Page Navigation (3 patterns)

**Pattern A — Page with tabs** (Library, Sessions, Session Detail, etc.):
```
← Libreria                              [+ Aggiungi gioco]
I Miei Giochi                            42 giochi
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Collezione    Wishlist    Proposte
────────────
[active tab content]
```

**Pattern B — Detail page without tabs** (Play Record Detail, etc.):
```
← Partite
Catan — 12 Aprile 2026                  [Modifica]

[content]
```

**Pattern C — Root page** (Dashboard — no back link):
```
Dashboard
[content]
```

**Rules**:
- "← Section" is a small text link above the title, teal color (#4ecdc4), navigates to explicit parent page (not browser back)
- Primary CTA aligned right of the title
- Tabs are underline-style inside the page, below the title
- Root pages (Dashboard, Library, Sessions) have no back link

**Back link mapping**:
| Page | Back link |
|------|-----------|
| `/library/[gameId]` | ← Libreria |
| `/sessions/[id]` | ← Sessioni |
| `/sessions/[id]/notes` | ← Sessione (links to `/sessions/[id]`) |
| `/play-records/[id]` | ← Partite |
| `/games/[id]/faqs` | ← Gioco |
| `/games/[id]/reviews` | ← Gioco |
| `/games/[id]/rules` | ← Gioco |
| `/editor/agent-proposals/create` | ← Proposte |
| `/editor/agent-proposals/[id]/edit` | ← Proposte |
| Admin sub-pages | ← Parent section |

### 4. ManaPips (relational navigation)

**No changes to the cascade flow**: ManaPip → DeckStack → Drawer (cascade-navigation-store unchanged).

**Enhancements**:
- **Visibility scope**: ManaPips render on all MeepleCard instances across all pages (library, sessions, players, etc.), not only in the dashboard/SessionPanel
- **Tooltip on hover** (desktop): shows count + entity type label (e.g., "3 sessioni")
- **Tap feedback** (mobile): ripple animation on pip touch
- **Empty state**: pips with count 0 are not rendered (confirm existing behavior)
- **Icons**: use existing entity-type icons (no text labels next to pips)

**Unchanged components**:
- `cascade-navigation-store.ts` — state machine stays identical
- `DeckStack.tsx` — popover/bottomsheet unchanged
- `ExtraMeepleCardDrawer.tsx` — drawer unchanged
- `ManaPips.tsx` — visual component, only tooltip added
- `mana-config.ts` — entity relationships unchanged

### 5. Mobile CTA Pill (< 768px only)

- **Content**: primary CTA button only, no breadcrumb, no section label
- **Position**: fixed bottom-center, 16px from bottom
- **Style**: rounded pill, orange background, elevated shadow
- **Auto-hide**: hides on scroll down, reappears on scroll up
- **Conditional**: only on pages with a primary action

**Page → CTA mapping**:
| Page | CTA label |
|------|-----------|
| `/library` | + Aggiungi gioco |
| `/sessions` | + Nuova sessione |
| `/play-records` | + Nuova partita |
| `/chat` | + Nuova chat |
| All other pages | no pill |

### 6. Admin Area

Same patterns as user area applied to admin:

- **AdminTabSidebar** (200px fixed) → replaced by admin side drawer
- **AdminBreadcrumb** mobile → replaced by "← Section" in-page pattern
- **Admin header** (ViewModeToggle, NotificationBell) → ViewModeToggle moves inside pages that need it; NotificationBell moves to Avatar dropdown
- **Admin pages** follow Pattern A/B/C like user pages

## Components to Remove

| Component | File | Migrated to |
|-----------|------|-------------|
| MiniNavSlot | `components/layout/UserShell/MiniNavSlot.tsx` | In-page tabs + title |
| useMiniNavConfig | `hooks/useMiniNavConfig.ts` | None (pattern removed) |
| mini-nav-config-store | `lib/stores/mini-nav-config-store.ts` | None |
| HandRail | `components/layout/HandRail/HandRail.tsx` | ManaPips |
| RecentsBar | `components/layout/UserShell/RecentsBar.tsx` | None |
| ActionPill | `components/layout/ActionPill/ActionPill.tsx` | In-page CTA + mobile pill |
| FloatingActionPill | `components/layout/FloatingActionPill.tsx` | In-page CTA + mobile pill |
| TopBarNavLinks | `components/layout/UserShell/TopBarNavLinks.tsx` | Side drawer |
| TopBarChatButton | in TopBar | Side drawer "Chat AI" |
| useNavBreadcrumb | `lib/hooks/useNavBreadcrumb.ts` | None |
| useNavigation (breadcrumb parts) | `hooks/useNavigation.ts` | Simplify, remove breadcrumb |
| AdminBreadcrumbs | `components/admin/AdminBreadcrumbs.tsx` | Dead code, delete |
| BackToHub | `components/admin/BackToHub.tsx` | Dead code, delete |
| AdminTabSidebar | `components/layout/AdminShell/AdminTabSidebar.tsx` | Admin side drawer |
| AdminBreadcrumb | `components/layout/AdminShell/AdminBreadcrumb.tsx` | In-page "← Section" |
| card-hand-store | `lib/stores/card-hand-store.ts` | None (ManaPips replace) |
| HandDrawer | mobile component | Side drawer |
| NotificationBell (from TopBar) | in TopBar | Avatar dropdown |
| Back buttons | 4 page.tsx files | In-page "← Section" pattern |
| ActionBar (mobile) | `components/layout/mobile/ActionBar.tsx` | Mobile CTA pill |

**Total**: ~20 components/files removed or significantly simplified.

## Components to Create

| Component | Purpose |
|-----------|---------|
| SideDrawer | Hamburger menu with nav items, "Altro" sub-menu, account section |
| AdminSideDrawer | Admin-specific drawer with grouped sections |
| PageHeader | Reusable: "← Parent" back link + title + CTA + optional tabs |
| MobileCTAPill | Fixed bottom pill, scroll-aware, page-mapped CTA |
| TopBar v2 | Simplified: hamburger + logo + search icon + avatar |
| SearchOverlay | Expandable search from magnifying glass icon |

## Components Unchanged

- `cascade-navigation-store.ts` — ManaPip state machine
- `DeckStack.tsx` — entity card fan popover
- `ExtraMeepleCardDrawer.tsx` — entity detail drawer
- `ManaPips.tsx` — colored pip dots (add tooltip only)
- `SheetBreadcrumb.tsx` — dashboard internal navigation (different context)
- `DesktopShell.tsx` — simplified (remove HandRail, MiniNavSlot slots)
- `UserMenuDropdown` — add Notifications entry

## Migration Strategy

### Phase 1: New components (no breaking changes)
- Create SideDrawer, AdminSideDrawer, PageHeader, MobileCTAPill, SearchOverlay
- Create TopBar v2 behind feature flag

### Phase 2: Page migration (per-page)
- Migrate each page to use PageHeader with "← Parent" + in-page tabs
- Remove useMiniNavConfig calls from each page
- Test each page individually

### Phase 3: Shell cleanup
- Switch TopBar v2 on
- Remove MiniNavSlot from DesktopShell
- Remove HandRail from DesktopShell
- Remove ActionPill and FloatingActionPill
- Remove ActionBar mobile

### Phase 4: Delete dead components
- Remove all files listed in "Components to Remove"
- Remove unused stores and hooks
- Clean up imports and re-exports

### Phase 5: Admin migration
- Apply same pattern to AdminShell
- Replace AdminTabSidebar with AdminSideDrawer
- Migrate admin pages to PageHeader pattern

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Users lose spatial orientation without breadcrumbs | "← Parent" link + side drawer active state provide location context |
| ManaPips not discoverable for new users | Tooltip on hover + onboarding hint |
| Admin loses quick section switching without sidebar | Admin drawer sections mirror current sidebar; frequently used items at top |
| Mobile users lose quick CTA access | Mobile pill provides primary CTA; secondary actions in page |
| Search removal from persistent nav reduces discoverability | Search icon always visible in TopBar; expands on click |

## Success Criteria

- Navigation layers reduced from 6 to 2 (TopBar + in-page)
- Zero duplicate CTAs on any page
- Zero duplicate breadcrumbs on any page
- All ManaPips functional on all MeepleCard instances
- Mobile pill visible and scroll-aware on CTA pages
- Side drawer accessible from every page
- Admin area follows same patterns as user area
- No regression in page-to-page navigation (all routes reachable)
