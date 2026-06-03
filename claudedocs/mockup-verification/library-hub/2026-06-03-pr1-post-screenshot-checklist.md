# PR1 Post-Implementation Verification Checklist

**Date**: 2026-06-03
**Branch**: feature/library-sp4-pr1-header-chrome
**Scope**: Header chrome (Hero + Tabs + Filters)

## Manual verification at `/library` (desktop 1440px, data-theme="light")

### Hero (LibraryHeroDesktop)
- [ ] Eyebrow pill "📚 Library · power-user view" visible above title (font-mono, uppercase, 9px)
- [ ] Title "La tua libreria" rendered as h1 with font-display, 38px
- [ ] Subtitle "Tutti i tuoi giochi, agenti e documenti in un posto." muted color, 14.5px
- [ ] Gradient background visible (game → agent → kb, alpha 0.10/0.06/0.08)
- [ ] Decorative blob top-right with game-alpha radial gradient
- [ ] 4 stats inline pills (Giochi/Agenti/Documenti/Chat) with entity-colored borders
- [ ] 3 CTAs aligned to right end:
  - "+ Aggiungi gioco" entity-game-colored with shadow
  - "↓ Importa BGG" secondary (bg-card border-strong)
  - "↗" Export icon (with aria-label="Esporta", disabled if onExport undefined)

### Tabs (LibraryTabs)
- [ ] 6 tabs in order: ⌗ Tutti / 🎲 Giochi / 🤖 Agenti / 📚 KB / 🎯 Sessioni / 💬 Chat
- [ ] Active tab background bg-entity-{ent}/10, text text-entity-{ent}, font-extrabold
- [ ] Animated indicator bar at bottom moves smoothly between tabs (entity-colored)
- [ ] 'all' tab uses game accent when active
- [ ] Count pill on active tab: bg-entity-{ent} text-white; inactive: bg-muted text-muted
- [ ] Animated indicator stays aligned on window resize (verify by dragging viewport)
- [ ] With OS `prefers-reduced-motion: reduce`, indicator snaps instantly (no slide)

### Filters (CrossEntityFilters)
- [ ] 2-row layout: search row on top, chips+toggle row below
- [ ] Search input full-width with `⌕` left icon and `<kbd>/</kbd>` right hint
- [ ] Press `/` anywhere on page (NOT in an input) → search input gains focus
- [ ] Press `/` while typing in another input → no focus theft (typed `/` appears in that input)
- [ ] Press `Cmd+/` or `Ctrl+/` → no focus theft (modifier keys ignored)
- [ ] 4 FilterChip stubs visible: STATO Tutti, GIOCO Tutti, DATA Sempre, SORT Recenti (last has entity-game color)
- [ ] Vertical divider between SORT chip and "⚙ Filtri avanzati" button
- [ ] "⚙ Filtri avanzati" button bg-card border-strong; when activeFiltersCount>0 → bg-entity-agent/10 text-entity-agent + count badge
- [ ] View toggle on the right: 3 buttons ▦/☰/≡ with active state bg-entity-game/12 text-entity-game
- [ ] View toggle is `role="radiogroup"`, each button `role="radio" aria-checked`

### Out of scope (PR2/PR3)
- Cards still use placeholder pattern (giant initials) — visual conformity blocked by #1856
- RecentActivityRail not yet re-skinned (PR2)
- EmptyLibrary not yet re-skinned (PR2)
- BulkSelectionBar not yet re-skinned (PR3)
- AdvancedFiltersDrawer not yet re-skinned (PR3)

### Out of scope (theme)
- Default theme is light (Phase 0 verified). User with `localStorage.theme="dark"` will see dark — that's user preference.
