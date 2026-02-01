# MeepleAI Frontend Documentation

> Design system, layout specifications, and component architecture for MeepleAI web application.

---

## Quick Links

| Document | Description |
|----------|-------------|
| [Layout Spec](./layout-spec.md) | Complete layout specification v1.0 |
| [Layout Wireframes](./layout-wireframes.md) | ASCII wireframes for all viewports |
| [Layout Components](./layout-components.md) | React component breakdown |
| [Mobile Mock](./mocks/layout-mobile-mock.html) | Interactive mobile HTML mock |
| [Desktop Mock](./mocks/layout-desktop-mock.html) | Interactive desktop HTML mock |

---

## Layout System Overview

### Structure

```
┌─────────────────────────────────┐
│           Navbar                │  ← Navigation + Search + Profile
├─────────────────────────────────┤
│                                 │
│         Main Content            │  ← Page-specific content
│                                 │
│                          [FAB]  │  ← Smart FAB (mobile only)
│   [Breadcrumb]                  │  ← Context indicator
├─────────────────────────────────┤
│         ActionBar               │  ← Context-aware actions
└─────────────────────────────────┘
```

### Key Features

| Feature | Mobile | Desktop |
|---------|--------|---------|
| **Navbar** | Hamburger + Logo + Icons | Full nav + Search input |
| **FAB** | Smart contextual FAB | None (hidden) |
| **ActionBar** | 3 actions + overflow | 5-6 actions, sticky bottom |
| **Breadcrumb** | Below FAB | Above ActionBar |

### Smart FAB Contexts

| Context | Icon | Action |
|---------|------|--------|
| Library | ➕ | Add game |
| Game Detail | ▶️ | Start session |
| Session | 🤖 | Ask AI |
| Document | 🤖 | Ask about section |

---

## Design Tokens

The layout uses MeepleAI's existing design system:

- **Colors**: `--color-primary` (orange), `--color-accent` (purple)
- **Fonts**: Quicksand (headings), Nunito (body)
- **Spacing**: 4px base unit system
- **Shadows**: `shadow-sm`, `shadow-md`, `shadow-lg`
- **Radius**: `rounded-lg`, `rounded-xl`, `rounded-2xl`
- **Animations**: 200ms ease-out transitions

See [design-tokens.css](../../apps/web/src/styles/design-tokens.css) for full token list.

---

## Component Hierarchy

```
LayoutProvider
├── Navbar
│   ├── HamburgerButton
│   ├── Logo
│   ├── NavItems
│   ├── GlobalSearch
│   └── ProfileBar
├── HamburgerMenu
├── MainContent
├── SmartFAB
│   └── QuickMenu
├── Breadcrumb
└── ActionBar
    ├── ActionBarItem
    ├── OverflowMenu
    └── MultiSelectBar
```

**Total: 23 components, 10 hooks, 3 config files**

---

## Implementation Phases

### Phase 1: Core Structure
- [ ] LayoutProvider + types
- [ ] Layout wrapper
- [ ] useResponsive hook
- [ ] Basic Navbar

### Phase 2: Navigation
- [ ] NavItems + NavItem
- [ ] HamburgerMenu
- [ ] ProfileBar
- [ ] GlobalSearch

### Phase 3: ActionBar
- [ ] ActionBar + ActionBarItem
- [ ] OverflowMenu
- [ ] useActionBar hook
- [ ] Context-action mapping

### Phase 4: Smart FAB
- [ ] SmartFAB
- [ ] useFAB + useLongPress
- [ ] QuickMenu
- [ ] Visibility logic

### Phase 5: Polish
- [ ] Breadcrumb
- [ ] MultiSelectBar
- [ ] Animations
- [ ] Accessibility audit

---

## Viewing Mocks

Open the HTML mocks in a browser:

```bash
# Mobile mock (375x812 viewport frames)
open docs/frontend/mocks/layout-mobile-mock.html

# Desktop mock (full width)
open docs/frontend/mocks/layout-desktop-mock.html
```

---

## Related Documentation

- [Design Tokens CSS](../../apps/web/src/styles/design-tokens.css)
- [Global Styles](../../apps/web/src/styles/globals.css)
- [UI Components](../../apps/web/src/components/ui/)
- [Tailwind Config](../../apps/web/tailwind.config.js)

---

## Decisions Log

| Decision | Choice | Rationale |
|----------|--------|-----------|
| FAB visibility | Mobile only | Desktop uses sticky ActionBar instead |
| Long-press FAB | Quick menu 2-3 items | Balance discoverability and simplicity |
| ActionBar slots | Dynamic by breakpoint | 3/4/6 based on screen width |
| Transitions | Morph animations | Smooth context switching |
| Breadcrumb position | Below FAB, above ActionBar | Clear context without blocking content |

---

**Last Updated**: 2026-02-01
