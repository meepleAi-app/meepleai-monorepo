# MeepleAI Layout Components Breakdown

> **Companion to**: `layout-spec.md`, `layout-wireframes.md`
> **Purpose**: Component architecture per sviluppatori React
> **Created**: 2026-02-01

---

## Component Tree Overview

```
<LayoutProvider>                    # Context provider per layout state
├── <Navbar>                        # Header navigation
│   ├── <HamburgerButton>           # Mobile menu trigger
│   ├── <Logo>                      # Brand logo
│   ├── <NavItems>                  # Desktop nav links
│   │   └── <NavItem>               # Single nav link
│   ├── <GlobalSearch>              # Search input/trigger
│   │   ├── <SearchTrigger>         # Mobile icon button
│   │   ├── <SearchInput>           # Actual input
│   │   ├── <SearchResults>         # Dropdown results
│   │   │   └── <SearchResultItem>  # Single result
│   │   └── <RecentSearches>        # Recent queries
│   └── <ProfileBar>                # User section
│       ├── <GuestActions>          # Login/Register
│       └── <UserMenu>              # Logged-in dropdown
│           └── <UserMenuItem>      # Menu item
├── <HamburgerMenu>                 # Mobile slide-out menu
│   └── <HamburgerMenuItem>         # Menu item
├── <MainContent>                   # Content wrapper
│   └── {children}                  # Page content
├── <SmartFAB>                      # Floating action button
│   └── <QuickMenu>                 # Long-press menu
│       └── <QuickMenuItem>         # Menu action
├── <Breadcrumb>                    # Context indicator
└── <ActionBar>                     # Bottom actions
    ├── <ActionBarItem>             # Single action
    ├── <OverflowMenu>              # More actions dropdown
    │   └── <OverflowMenuItem>      # Menu item
    └── <MultiSelectBar>            # Batch actions mode
        └── <MultiSelectAction>     # Batch action button
```

---

## 1. Layout Provider

### `LayoutProvider`

Context provider che gestisce lo stato globale del layout.

```typescript
// components/layout/LayoutProvider.tsx

interface LayoutContext {
  // Current context
  currentContext: LayoutContext;
  setContext: (ctx: LayoutContext) => void;

  // FAB
  fabVisible: boolean;
  fabAction: FABAction;
  quickMenuOpen: boolean;
  setQuickMenuOpen: (open: boolean) => void;

  // ActionBar
  actionBarActions: Action[];

  // Multi-select
  multiSelectMode: boolean;
  selectedItems: string[];
  toggleMultiSelect: (enabled: boolean) => void;
  toggleItemSelection: (id: string) => void;
  clearSelection: () => void;

  // Breadcrumb
  breadcrumbLabel: string;
  breadcrumbIcon: string;

  // Responsive
  isMobile: boolean;
  isTablet: boolean;
  isDesktop: boolean;

  // Visibility
  isKeyboardOpen: boolean;
  isScrolling: boolean;
}

interface LayoutProviderProps {
  children: React.ReactNode;
}

export function LayoutProvider({ children }: LayoutProviderProps): JSX.Element;
```

**State derivato da:**
- Route corrente (next/navigation)
- Selezione utente
- Focus state
- Scroll position
- Viewport size

---

## 2. Navbar Components

### `Navbar`

```typescript
// components/layout/Navbar/Navbar.tsx

interface NavbarProps {
  className?: string;
}

export function Navbar({ className }: NavbarProps): JSX.Element;

// Internal state
// - isSearchExpanded: boolean
// - isMenuOpen: boolean (mobile)
```

**Breakpoint behavior:**
- Mobile: Hamburger + Logo (center) + Search icon + Profile icon
- Tablet: Logo + Some nav items + Search + Profile
- Desktop: Logo + All nav items + Search input + Profile with name

---

### `HamburgerButton`

```typescript
// components/layout/Navbar/HamburgerButton.tsx

interface HamburgerButtonProps {
  isOpen: boolean;
  onClick: () => void;
  className?: string;
}

export function HamburgerButton({ isOpen, onClick, className }: HamburgerButtonProps): JSX.Element;
```

**Animazione:** Hamburger ↔ X morph (200ms)

---

### `Logo`

```typescript
// components/layout/Navbar/Logo.tsx

interface LogoProps {
  variant?: 'full' | 'icon';  // full = logo + text, icon = just icon
  className?: string;
}

export function Logo({ variant = 'full', className }: LogoProps): JSX.Element;
```

---

### `NavItems` / `NavItem`

```typescript
// components/layout/Navbar/NavItems.tsx

interface NavItemConfig {
  id: string;
  label: string;
  href: string;
  icon: React.ReactNode;
  activePattern?: RegExp;  // Per matching route attiva
}

interface NavItemsProps {
  items: NavItemConfig[];
  variant?: 'horizontal' | 'vertical';  // horizontal = desktop, vertical = hamburger
  className?: string;
}

export function NavItems({ items, variant, className }: NavItemsProps): JSX.Element;

// NavItem (internal)
interface NavItemProps {
  item: NavItemConfig;
  isActive: boolean;
  variant: 'horizontal' | 'vertical';
}
```

**Default items:**
```typescript
const DEFAULT_NAV_ITEMS: NavItemConfig[] = [
  { id: 'library', label: 'Libreria', href: '/library', icon: <LibraryIcon /> },
  { id: 'catalog', label: 'Catalogo', href: '/catalog', icon: <CatalogIcon /> },
  { id: 'sessions', label: 'Sessioni', href: '/sessions', icon: <SessionsIcon /> },
];
```

---

### `GlobalSearch`

```typescript
// components/layout/Navbar/GlobalSearch/GlobalSearch.tsx

interface GlobalSearchProps {
  className?: string;
}

export function GlobalSearch({ className }: GlobalSearchProps): JSX.Element;

// Internal state
// - isExpanded: boolean (mobile)
// - query: string
// - results: SearchResult[]
// - isLoading: boolean
// - recentSearches: string[]
```

**Sub-components:**

```typescript
// SearchTrigger.tsx - Mobile icon button
interface SearchTriggerProps {
  onClick: () => void;
}

// SearchInput.tsx - Actual input field
interface SearchInputProps {
  value: string;
  onChange: (value: string) => void;
  onFocus: () => void;
  onBlur: () => void;
  isExpanded: boolean;
}

// SearchResults.tsx - Dropdown with results
interface SearchResultsProps {
  results: SearchResult[];
  isLoading: boolean;
  onSelect: (result: SearchResult) => void;
}

// SearchResultItem.tsx - Single result row
interface SearchResultItemProps {
  result: SearchResult;
  onClick: () => void;
}

// RecentSearches.tsx - Recent queries list
interface RecentSearchesProps {
  searches: string[];
  onSelect: (query: string) => void;
  onClear: () => void;
}
```

---

### `ProfileBar`

```typescript
// components/layout/Navbar/ProfileBar/ProfileBar.tsx

interface ProfileBarProps {
  className?: string;
}

export function ProfileBar({ className }: ProfileBarProps): JSX.Element;
```

**Sub-components:**

```typescript
// GuestActions.tsx - Login/Register buttons
interface GuestActionsProps {
  onLogin: () => void;
  onRegister: () => void;
  variant?: 'buttons' | 'icon';  // buttons = desktop, icon = mobile
}

// UserMenu.tsx - Logged-in user dropdown
interface UserMenuProps {
  user: User;
  isOpen: boolean;
  onToggle: () => void;
}

// UserMenuItem.tsx - Menu item
interface UserMenuItemProps {
  icon: React.ReactNode;
  label: string;
  onClick: () => void;
  variant?: 'default' | 'danger';  // danger = logout
}
```

---

### `HamburgerMenu`

```typescript
// components/layout/HamburgerMenu/HamburgerMenu.tsx

interface HamburgerMenuProps {
  isOpen: boolean;
  onClose: () => void;
}

export function HamburgerMenu({ isOpen, onClose }: HamburgerMenuProps): JSX.Element;
```

**Animazione:** Slide-in from left (250ms) + overlay fade

---

## 3. Smart FAB

### `SmartFAB`

```typescript
// components/layout/SmartFAB/SmartFAB.tsx

interface SmartFABProps {
  className?: string;
}

export function SmartFAB({ className }: SmartFABProps): JSX.Element;

// State from LayoutContext:
// - currentContext → determines icon/action
// - fabVisible → show/hide
// - quickMenuOpen → long-press menu state
```

**Hooks interni:**

```typescript
// useFABAction.ts
function useFABAction(context: LayoutContext): FABAction;

// useFABVisibility.ts
function useFABVisibility(): boolean;
// Considers: keyboard, modal, scroll, chat input

// useLongPress.ts
function useLongPress(callback: () => void, delay?: number): {
  onTouchStart: () => void;
  onTouchEnd: () => void;
  onMouseDown: () => void;
  onMouseUp: () => void;
};
```

---

### `QuickMenu`

```typescript
// components/layout/SmartFAB/QuickMenu.tsx

interface QuickMenuItem {
  id: string;
  icon: React.ReactNode;
  label: string;
  action: () => void;
}

interface QuickMenuProps {
  items: QuickMenuItem[];
  isOpen: boolean;
  onClose: () => void;
  anchorRef: React.RefObject<HTMLElement>;
}

export function QuickMenu({ items, isOpen, onClose, anchorRef }: QuickMenuProps): JSX.Element;
```

**Animazione:** Scale + fade in from FAB position (150ms)

---

## 4. Breadcrumb

### `Breadcrumb`

```typescript
// components/layout/Breadcrumb/Breadcrumb.tsx

interface BreadcrumbProps {
  className?: string;
}

export function Breadcrumb({ className }: BreadcrumbProps): JSX.Element;

// State from LayoutContext:
// - breadcrumbLabel
// - breadcrumbIcon
```

**Animazione:** Fade + slide 8dp on context change (150ms)

---

## 5. ActionBar

### `ActionBar`

```typescript
// components/layout/ActionBar/ActionBar.tsx

interface ActionBarProps {
  className?: string;
}

export function ActionBar({ className }: ActionBarProps): JSX.Element;

// Automatically shows MultiSelectBar when multiSelectMode = true
```

---

### `ActionBarItem`

```typescript
// components/layout/ActionBar/ActionBarItem.tsx

interface ActionBarItemProps {
  action: Action;
  variant?: 'icon-only' | 'icon-label';  // mobile vs desktop
  isActive?: boolean;
  isDisabled?: boolean;
}

export function ActionBarItem({ action, variant, isActive, isDisabled }: ActionBarItemProps): JSX.Element;
```

---

### `OverflowMenu`

```typescript
// components/layout/ActionBar/OverflowMenu.tsx

interface OverflowMenuProps {
  actions: Action[];
  isOpen: boolean;
  onToggle: () => void;
}

export function OverflowMenu({ actions, isOpen, onToggle }: OverflowMenuProps): JSX.Element;
```

---

### `MultiSelectBar`

```typescript
// components/layout/ActionBar/MultiSelectBar.tsx

interface MultiSelectBarProps {
  selectedCount: number;
  onClear: () => void;
  actions: Action[];  // Batch actions
}

export function MultiSelectBar({ selectedCount, onClear, actions }: MultiSelectBarProps): JSX.Element;
```

---

## 6. Shared Types

```typescript
// types/layout.ts

// Contexts
type LayoutContext =
  | 'library'
  | 'library_empty'
  | 'library_selection'
  | 'game_detail'
  | 'game_detail_not_owned'
  | 'session_active'
  | 'session_setup'
  | 'session_end'
  | 'chat'
  | 'document_viewer'
  | 'catalog'
  | 'search_results'
  | 'wishlist'
  | 'profile'
  | 'settings'
  | 'notifications';

// Actions
interface Action {
  id: string;
  icon: React.ReactNode;
  label: string;
  priority: 1 | 2 | 3 | 4 | 5 | 6;
  contexts: LayoutContext[];
  onClick: () => void;
  isDisabled?: boolean;
  isDestructive?: boolean;
}

// FAB
interface FABAction {
  icon: React.ReactNode;
  label: string;  // For aria-label
  action: () => void;
  quickMenuItems: QuickMenuItem[];
}

// Search
interface SearchResult {
  id: string;
  type: 'game' | 'document' | 'session';
  title: string;
  subtitle?: string;
  imageUrl?: string;
}
```

---

## 7. Hooks

### `useLayoutContext`

```typescript
// hooks/useLayoutContext.ts

// Access layout context
export function useLayoutContext(): LayoutContext;
```

### `useActionBar`

```typescript
// hooks/useActionBar.ts

// Get actions for current context
export function useActionBar(): {
  visibleActions: Action[];
  overflowActions: Action[];
  isEmpty: boolean;
};
```

### `useFAB`

```typescript
// hooks/useFAB.ts

// Get FAB configuration for current context
export function useFAB(): {
  isVisible: boolean;
  action: FABAction;
  triggerQuickMenu: () => void;
};
```

### `useMultiSelect`

```typescript
// hooks/useMultiSelect.ts

// Multi-selection management
export function useMultiSelect<T extends { id: string }>(): {
  isActive: boolean;
  selectedIds: Set<string>;
  selectedItems: T[];
  toggle: (id: string) => void;
  selectAll: (items: T[]) => void;
  clearSelection: () => void;
  enterMultiSelect: () => void;
  exitMultiSelect: () => void;
};
```

### `useResponsive`

```typescript
// hooks/useResponsive.ts

export function useResponsive(): {
  isMobile: boolean;   // < 640px
  isTablet: boolean;   // 640-1024px
  isDesktop: boolean;  // > 1024px
  breakpoint: 'mobile' | 'tablet' | 'desktop';
};
```

### `useScrollDirection`

```typescript
// hooks/useScrollDirection.ts

export function useScrollDirection(): {
  direction: 'up' | 'down' | null;
  isScrolling: boolean;
  velocity: number;
};
```

---

## 8. Animation Utilities

### `useMorphTransition`

```typescript
// hooks/useMorphTransition.ts

interface MorphConfig {
  duration?: number;
  easing?: string;
  onStart?: () => void;
  onComplete?: () => void;
}

export function useMorphTransition(config?: MorphConfig): {
  isTransitioning: boolean;
  triggerTransition: () => void;
  transitionStyles: React.CSSProperties;
};
```

### `useStaggeredAnimation`

```typescript
// hooks/useStaggeredAnimation.ts

interface StaggerConfig {
  staggerDelay: number;  // ms between items
  itemDuration: number;
  direction?: 'forward' | 'reverse';
}

export function useStaggeredAnimation(
  itemCount: number,
  config: StaggerConfig
): {
  getItemStyles: (index: number) => React.CSSProperties;
  isAnimating: boolean;
};
```

---

## 9. Context Configuration

### Action Definitions

```typescript
// config/actions.ts

export const CONTEXT_ACTIONS: Record<LayoutContext, Action[]> = {
  library: [
    { id: 'add', icon: <PlusIcon />, label: 'Aggiungi gioco', priority: 1, ... },
    { id: 'filter', icon: <FilterIcon />, label: 'Filtra', priority: 2, ... },
    { id: 'sort', icon: <SortIcon />, label: 'Ordina', priority: 3, ... },
    { id: 'view', icon: <GridIcon />, label: 'Vista', priority: 4, ... },
    { id: 'export', icon: <ExportIcon />, label: 'Esporta', priority: 5, ... },
  ],
  game_detail: [
    { id: 'play', icon: <PlayIcon />, label: 'Gioca', priority: 1, ... },
    { id: 'add', icon: <PlusIcon />, label: 'Aggiungi', priority: 2, ... },
    { id: 'ask_ai', icon: <AIIcon />, label: 'Chiedi AI', priority: 3, ... },
    { id: 'wishlist', icon: <HeartIcon />, label: 'Wishlist', priority: 4, ... },
    { id: 'share', icon: <ShareIcon />, label: 'Condividi', priority: 5, ... },
    { id: 'report', icon: <BugIcon />, label: 'Segnala', priority: 6, ... },
  ],
  // ... altri contesti
};
```

### FAB Configuration

```typescript
// config/fab.ts

export const FAB_CONFIG: Record<LayoutContext, FABAction> = {
  library: {
    icon: <PlusIcon />,
    label: 'Aggiungi gioco',
    action: () => router.push('/library/add'),
    quickMenuItems: [
      { id: 'search', icon: <SearchIcon />, label: 'Cerca', action: ... },
      { id: 'scan', icon: <CameraIcon />, label: 'Scansiona', action: ... },
    ],
  },
  game_detail: {
    icon: <PlayIcon />,
    label: 'Nuova sessione',
    action: () => startSession(),
    quickMenuItems: [
      { id: 'ask_ai', icon: <AIIcon />, label: 'Chiedi AI', action: ... },
      { id: 'share', icon: <ShareIcon />, label: 'Condividi', action: ... },
    ],
  },
  // ... altri contesti
};
```

---

## 10. File Structure

```
apps/web/src/
├── components/
│   └── layout/
│       ├── index.ts                    # Export all
│       ├── LayoutProvider.tsx          # Context provider
│       ├── Layout.tsx                  # Main layout wrapper
│       │
│       ├── Navbar/
│       │   ├── index.ts
│       │   ├── Navbar.tsx
│       │   ├── HamburgerButton.tsx
│       │   ├── Logo.tsx
│       │   ├── NavItems.tsx
│       │   ├── GlobalSearch/
│       │   │   ├── index.ts
│       │   │   ├── GlobalSearch.tsx
│       │   │   ├── SearchTrigger.tsx
│       │   │   ├── SearchInput.tsx
│       │   │   ├── SearchResults.tsx
│       │   │   └── RecentSearches.tsx
│       │   └── ProfileBar/
│       │       ├── index.ts
│       │       ├── ProfileBar.tsx
│       │       ├── GuestActions.tsx
│       │       └── UserMenu.tsx
│       │
│       ├── HamburgerMenu/
│       │   ├── index.ts
│       │   └── HamburgerMenu.tsx
│       │
│       ├── SmartFAB/
│       │   ├── index.ts
│       │   ├── SmartFAB.tsx
│       │   └── QuickMenu.tsx
│       │
│       ├── Breadcrumb/
│       │   ├── index.ts
│       │   └── Breadcrumb.tsx
│       │
│       └── ActionBar/
│           ├── index.ts
│           ├── ActionBar.tsx
│           ├── ActionBarItem.tsx
│           ├── OverflowMenu.tsx
│           └── MultiSelectBar.tsx
│
├── hooks/
│   ├── useLayoutContext.ts
│   ├── useActionBar.ts
│   ├── useFAB.ts
│   ├── useMultiSelect.ts
│   ├── useResponsive.ts
│   ├── useScrollDirection.ts
│   ├── useMorphTransition.ts
│   ├── useStaggeredAnimation.ts
│   └── useLongPress.ts
│
├── config/
│   ├── actions.ts                      # Action definitions per context
│   ├── fab.ts                          # FAB config per context
│   └── navigation.ts                   # Nav items config
│
└── types/
    └── layout.ts                       # Shared types
```

---

## 11. Component Count Summary

| Category | Components | Hooks | Config Files |
|----------|------------|-------|--------------|
| Layout Core | 3 | 2 | - |
| Navbar | 10 | - | 1 |
| HamburgerMenu | 2 | - | - |
| SmartFAB | 3 | 3 | 1 |
| Breadcrumb | 1 | - | - |
| ActionBar | 4 | 1 | 1 |
| Utilities | - | 4 | - |
| **Total** | **23** | **10** | **3** |

---

## 12. Implementation Priority

### Phase 1: Core Structure
1. `LayoutProvider` + types
2. `Layout` wrapper
3. `useResponsive` hook
4. Basic `Navbar` (logo + hamburger)

### Phase 2: Navigation
5. `NavItems` + `NavItem`
6. `HamburgerMenu`
7. `ProfileBar` (guest + user)
8. `GlobalSearch` (basic)

### Phase 3: ActionBar
9. `ActionBar` + `ActionBarItem`
10. `OverflowMenu`
11. `useActionBar` hook
12. Context-action mapping

### Phase 4: Smart FAB
13. `SmartFAB`
14. `useFAB` + `useLongPress`
15. `QuickMenu`
16. FAB visibility logic

### Phase 5: Polish
17. `Breadcrumb`
18. `MultiSelectBar`
19. All animations
20. Accessibility audit

---

## Appendix: Dependencies

```json
{
  "dependencies": {
    "framer-motion": "^10.x",      // Animations
    "@radix-ui/react-dropdown-menu": "^2.x",  // Menus
    "@radix-ui/react-dialog": "^1.x",         // Modals
    "use-long-press": "^3.x",      // Long press detection
    "usehooks-ts": "^2.x"          // Utility hooks
  }
}
```
