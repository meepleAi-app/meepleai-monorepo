# PR2 Post-Implementation Verification Checklist

**Date**: 2026-06-03
**Branch**: feature/library-sp4-pr2-content-surface
**Scope**: Content surface (LibraryHybridGrid layout + RecentActivityRail + EmptyLibrary)
**Out of scope**: MeepleCard primitive (deferred to #1856), BulkSelectionBar/AdvancedFiltersDrawer (PR3)

## Manual verification at `/library` (desktop 1440px, data-theme="light", library populated)

### LibraryHybridGrid layout
- [ ] Grid view: items render in 4 columns at lg breakpoint (2 cols on mobile, 3 cols on md, 4 cols on lg)
- [ ] Container has `data-slot="library-hybrid-grid-container"` + `data-view="grid"`
- [ ] Spacing: gap-3 between cards in grid view
- [ ] Click view toggle to List → 1 column flex-col with gap-1.5
- [ ] Click view toggle to Compact → bordered card container `bg-card border border-border rounded-lg` wrapping stacked rows

### RecentActivityRail (right sidebar)
- [ ] Sidebar visible at lg breakpoint (w-[280px]), hidden on mobile (hidden lg:flex)
- [ ] Header: 🕐 emoji + "Ultime modifiche" title + ›  collapse button (static, with aria-label)
- [ ] Timeline circles entity-colored per kind (game=orange, agent=amber, kb=teal, session=indigo, chat=blue, event=rose)
- [ ] Connecting lines visible between consecutive items (n-1 connectors via `[data-slot="library-activity-rail-connector"]`)
- [ ] Each item shows: timestamp (font-mono uppercase 9px), strong actor + action verb, entity-colored ref link
- [ ] Keyboard shortcuts box at bottom: bg-muted with 3 rows (`/` focus search, `f` filtri avanzati, `?` tutte le scorciatoie)

### EmptyLibrary first-run (empty state)
- [ ] To trigger: clear library, navigate to `/library?state=empty` (or actual empty state)
- [ ] Illustration: 96px circle with game-alpha radial gradient + 📚 emoji 44px
- [ ] Title h2 "La tua libreria è vuota"
- [ ] Subtitle paragraph muted text, max-w-[380px]
- [ ] 2 CTAs: primary "+ Aggiungi il tuo primo gioco" (bg-entity-game with shadow) + secondary "↓ Importa da BGG"
- [ ] Suggestions box: section label "Suggerimenti dalla community" + 3 placeholder cards (Brass/Wingspan/Spirit Island or similar) in 3-col grid
- [ ] Placeholder cards each show: gradient tile icon + title + ★ rating + + add button

### Cards visual conformity status
- [ ] Cards still render with current MeepleCard primitive (giant title initials over per-game gradient) — DEFERRED to issue #1856
- [ ] This PR does NOT fix card visual conformity; card-grid surface visual completion blocked by #1856

### Out of scope (PR3)
- BulkSelectionBar floating dark bar
- AdvancedFiltersDrawer 7-section accordion

### Out of scope (other follow-ups)
- Theme switching (Phase 0 verified default light)
- MeepleCard primitive reskin (#1856)
- BGG hot-games API integration for suggestions box
- SORT chip popover wiring
- RecentActivityRail collapse functionality (currently static)
