# Sidebar Icons & Navbar Redesign

**Date**: 2026-03-26
**Status**: Approved
**Scope**: Authenticated user shell only (no admin, auth, or public pages)

## Goal

Replace generic lucide-react sidebar icons with native emoji for a more cheerful, board-game-themed feel. Redesign the navbar to be more functional with search, breadcrumbs, quick actions, session status, and contextual tabs.

## Approach

Modify existing components in-place (no V2 copies).

---

## 1. Sidebar Emoji Icons

Replace lucide-react `<Icon>` components with native emoji `<span>` elements (font-size 20px).

### Emoji mapping

| Menu Item | Current Icon | New Emoji | Rationale |
|-----------|-------------|-----------|-----------|
| Dashboard | `House` | 🏠 | Home, intuitive |
| Libreria | `BookOpen` | 📚 | Books, recognizable |
| Wishlist | `Heart` | 💝 | Gift heart = desires |
| Sessioni | `Dice5` | 🎲 | Dice = playing games |
| Chat RAG | `MessageCircle` | ✨ | Sparkle = AI magic |
| Documenti | `FileText` | 📜 | Scroll = rulebooks |
| Agenti | `Bot` | 🤖 | Robot = AI agent |
| Impostazioni | `Settings` | ⚙️ | Universal, functional |

### Structural changes to HybridSidebar.tsx

- Remove all lucide-react icon imports.
- Change `NavItem.icon` field from `LucideIcon` to `string` (the emoji character).
- Keep the existing `NavSection[]` structure (`title`, `items` with `label`, `icon`, `href`, `activeMatch`, `activeSearchParam`) — only change the type and values of `icon`.
- Remove the "Collezioni" section (3rd entry in `NAV_SECTIONS` array — Preferiti, Con amici, Strategici are placeholder `href: '#'`).
- Keep two sections: "Navigazione" (4 items) + "AI Assistant" (3 items) + Settings at bottom.
- In `SidebarLink`, replace `<Icon className="w-5 h-5 shrink-0" />` with `<span className="text-xl leading-none shrink-0" role="img" aria-label={item.label}>{item.icon}</span>`.
- All existing `activeMatch` and `activeSearchParam` logic in `useIsActive` remains unchanged.

### Documenti href: preserve existing

The existing `Documenti` item uses `href: '/library?tab=private'` with `activeMatch: '/library'` and `activeSearchParam: { key: 'tab', value: 'private' }`. This is correct and must be preserved. The new config (section 7) must match this.

### Mobile tab bar (UserTabBar.tsx)

Same emoji treatment:

| Tab | Current Icon | New Emoji |
|-----|-------------|-----------|
| Home | `House` | 🏠 |
| Libreria | `BookOpen` | 📚 |
| Gioca | `Dice5` | 🎲 |
| Chat | `MessageCircle` | ✨ |

**Active state change**: Since emoji glyphs cannot be recolored via CSS `color` property on most OSes, replace the current SVG fill/stroke active treatment with:
- Active: `scale-110` transform + colored background circle (`bg-primary/15 rounded-full p-1.5`) + label color stays via `style={{ color: tab.colorVar }}`.
- Inactive: normal scale, no background, muted label.

Remove `fill`, `strokeWidth` props (SVG-specific, meaningless on `<span>`). The `<span>` emoji replaces `<Icon>`.

---

## 2. Navbar Redesign

### Structure: Two rows

**Row 1 — Main bar (52px, `h-[52px]`)**

```
[emoji + Breadcrumb]  [🔍 Search trigger]  ...flex...  [Session status] [+ Nuova] [🔔] [Avatar]
```

- **Breadcrumb with emoji**: shows current section emoji + navigation path (e.g., "📚 Libreria › Catan"). Emoji resolved from the `NAVIGATION_CONFIG` (section 7) by matching `usePathname()` against each entry's `activeMatch` prefix.
- **Search trigger**: visually styled as an always-visible input bar (max-width 320px, placeholder "Cerca giochi, documenti, chat..."), but on click dispatches the existing `⌘K` keyboard event to open the command palette. This preserves the existing `CommandPalette` component and keyboard shortcut. It is NOT a real `<input>` — it is a styled `<button>` that triggers the same `dispatch(new KeyboardEvent('keydown', { key: 'k', metaKey: true }))` pattern already in `UserTopNav.tsx`.
- **Session status pill**: green pill with live dot, shown when an active session exists. Data source: `useSessionStore(s => s.activeSession)` — the same store already imported in `UserTopNav.tsx`. This tracks the currently-open game-mode session (XState-managed). NOT `useActiveSessions()` (which is an API query used by `BackToSessionFAB`). The pill links to `/sessions/live/${activeSession.id}`.
- **Quick action "+ Nuova"**: green gradient button (`bg-gradient-to-r from-emerald-500 to-teal-400`), links to `/sessions/new`.
- **Notifications + Avatar**: same functionality as today, updated border-radius for consistency.

**Row 2 — Contextual tabs (36px, `h-9`)**

A NEW component `NavContextTabs` rendered inside `UserTopNav` below row 1. This does NOT modify `ContextBar.tsx`.

### ContextBar.tsx: unchanged

The existing `ContextBar` remains a store-driven content slot. Pages that currently inject content via `ContextBarRegistrar` (sessions live layout, chat layout, game-nights layout) continue to work exactly as before. The contextual tabs are a separate component rendered inside `UserTopNav`, not inside `ContextBar`.

When `ContextBar` has content AND `NavContextTabs` has tabs for the current route, both render: tabs in the navbar, `ContextBar` below. This is intentional — tabs are navigation, `ContextBar` is contextual actions/info.

### Contextual tabs — complete configuration

Route matching strategy: `pathname.startsWith(key)`. Query params are NOT part of the key lookup — the key is always a pathname prefix. Active tab within a section is determined by exact `href` match against `pathname + search`.

```typescript
// src/config/contextual-tabs.ts
export const CONTEXTUAL_TABS: Record<string, { label: string; href: string }[]> = {
  '/library': [
    { label: 'Collezione', href: '/library?tab=collection' },
    { label: 'Wishlist', href: '/library?tab=wishlist' },
    { label: 'Documenti', href: '/library?tab=private' },
    { label: 'Cronologia', href: '/library?tab=history' },
  ],
  '/sessions': [
    { label: 'In corso', href: '/sessions?tab=active' },
    { label: 'Completate', href: '/sessions?tab=completed' },
    { label: 'Pianificate', href: '/sessions?tab=planned' },
  ],
  '/chat': [
    { label: 'Thread', href: '/chat' },
    { label: 'Agenti', href: '/agents' },
  ],
  '/dashboard': [
    { label: 'Overview', href: '/dashboard' },
    { label: 'Attivita recente', href: '/dashboard?tab=activity' },
  ],
  '/settings': [
    { label: 'Profilo', href: '/settings?tab=profile' },
    { label: 'Preferenze', href: '/settings?tab=preferences' },
    { label: 'Account', href: '/settings?tab=account' },
  ],
};
```

Note: Chat "Agenti" tab links to `/agents` (a separate route). The `/agents` route itself does not have its own tabs — it is only reachable as a tab within the Chat section or from the sidebar.

- Active tab: brand color underline (border-bottom-2 `border-primary`) + `font-semibold`.
- If a route has no mapped tabs, row 2 is hidden (navbar height = 52px only).

### Glassmorphism

Maintained on both rows, consistent with current design (`backdrop-blur-xl`, subtle border-bottom).

---

## 3. Dynamic navbar height

The navbar height varies: 52px (no tabs) or 88px (52+36, with tabs).

**Mechanism**: A CSS variable `--navbar-height` set on the `<header>` parent in `UserTopNav`. The `NavContextTabs` component reads the current route and determines if tabs are visible. `UserTopNav` renders both rows and sets the variable.

`HybridSidebar.tsx` consumes `--navbar-height`:
- `HybridSidebar`: `top` offset and `height: calc(100dvh - var(--navbar-height))` replace hardcoded `top-12` and `h-[calc(100dvh-48px)]`.

`UserShellClient.tsx` does NOT need a hardcoded height replacement — it uses flex layout with `UserTopNav` as the first flex child, so the navbar's natural height in the flex flow handles the offset automatically. No `h-12` or `pt-12` exists in `UserShellClient.tsx`.

Alternatively, since the tabs component knows its own visibility: export a `useNavbarHeight()` hook from a small store that `NavContextTabs` writes to on mount/route change, and `HybridSidebar` reads from. This avoids CSS variable complexity. Implementation choice left to developer — either approach works.

---

## 4. Responsive behavior

| Breakpoint | Sidebar | Navbar |
|------------|---------|--------|
| Desktop (lg+) | Emoji sidebar, collapsible 52px→220px on hover | Full 2-row navbar (88px with tabs, 52px without) |
| Mobile (<lg) | Hidden | Row 1 simplified (breadcrumb + avatar only, no search bar, no quick action). No row 2 tabs (bottom tab bar handles navigation). Search accessible via command palette shortcut. |

---

## 5. Files changed

| File | Change |
|------|--------|
| `HybridSidebar.tsx` | Change `icon` type from `LucideIcon` to `string`, replace icon rendering with emoji `<span>`, remove Collezioni section, remove lucide-react imports, update `top`/`height` to use dynamic navbar height |
| `UserTabBar.tsx` | Replace lucide icon rendering with emoji `<span>`, update active state from SVG fill/stroke to scale+background treatment, remove lucide-react imports |
| `UserTopNav.tsx` | Add breadcrumb emoji, search trigger button styled as input, quick action button, session status pill, render new `NavContextTabs` component, set `--navbar-height` CSS var |
| `UserShellClient.tsx` | Minor: no hardcoded height to replace (flex layout handles it), but may need mobile breakpoint adjustments if bottom padding changes |
| `HybridSidebar.test.tsx` | Remove `Collezioni` section assertion, add emoji rendering assertions, update any icon-related test expectations |
| `src/config/contextual-tabs.ts` | **NEW** — route-to-tabs mapping config |
| `src/components/layout/UserShell/NavContextTabs.tsx` | **NEW** — contextual tab bar component |

## 6. Files NOT changed

- `ContextBar.tsx` — remains store-driven content slot, no changes
- `ContextBarRegistrar.tsx` — unchanged, continues to work for sessions/chat/game-nights
- `context-bar-store.ts` — unchanged
- `AdminShell` / `AdminTabSidebar` — admin stays as-is
- `UnifiedHeader` — public pages unchanged
- Auth layouts — unchanged
- `BackToSessionFAB` — stays as-is (uses separate `useActiveSessions` hook, unrelated to navbar pill)
- Routing logic — unchanged

## 7. Shared navigation config

The existing `NAV_SECTIONS` array in `HybridSidebar.tsx` is the source of truth for sidebar items. It keeps its current structure (with `activeMatch`, `activeSearchParam`). The only change is `icon: LucideIcon` → `icon: string` (emoji).

For breadcrumb emoji resolution in `UserTopNav`, extract a lightweight lookup:

```typescript
// src/config/navigation-emoji.ts
export const SECTION_EMOJI: Record<string, string> = {
  '/dashboard': '🏠',
  '/library': '📚',
  '/sessions': '🎲',
  '/chat': '✨',
  '/agents': '🤖',
  '/settings': '⚙️',
};

/** Given a pathname, return the matching section emoji */
export function getSectionEmoji(pathname: string): string {
  for (const [prefix, emoji] of Object.entries(SECTION_EMOJI)) {
    if (pathname.startsWith(prefix)) return emoji;
  }
  return '🏠'; // fallback to home
}
```

This is intentionally separate from the sidebar `NAV_SECTIONS` to avoid coupling the breadcrumb to the sidebar's data structure. The sidebar keeps its own config with routing logic; the emoji map is a simple pathname→emoji lookup.

## 8. Accessibility notes

- Emoji `<span>` elements must include `role="img"` and `aria-label` with the item label for screen reader support.
- The search trigger `<button>` must have `aria-label="Cerca"` and visually indicate it opens the command palette.
- Contextual tabs use `role="tablist"` / `role="tab"` / `aria-selected` consistent with the existing `UserTabBar` pattern.
