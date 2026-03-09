# RuleSourceCard Component Design

**Date**: 2026-03-08
**Status**: Approved
**Parent Epic**: Content Strategy / Rule Arbiter UX

## Problem

When the AI answers a game rule question, it includes `Citation` objects with `documentId`, `pageNumber`, `snippet`, and `relevanceScore`. Currently these render as small inline `CitationBadge` pills (`p.12`, `p.45`) that open a `PdfPageModal`. This is functional but doesn't surface the actual quoted text, publisher attribution, or relevance confidence — all of which build user trust in the answer.

## Solution

Replace inline `CitationBadge` pills with a single collapsible `RuleSourceCard` block at the end of each assistant message that groups all citations, shows quoted text, and links to both the internal PDF viewer and the publisher's official rulebook page.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Layout | Inline collapsible (not sidebar/modal) | Keeps reading flow, no context switch |
| Multi-citation | Tab chips (one selected at a time) | Compact, scannable, familiar pattern |
| Actions | Both PdfPageModal + publisher link | Internal detail view + legal attribution |
| Relevance display | Casual=hidden, Power=colored border+% | Respects user preference, progressive disclosure |

## User Mode System

A new user setting `appMode: 'casual' | 'power'` controls UI detail level across the app. The RuleSourceCard is the first consumer:

- **Casual**: No relevance scores, clean minimal display
- **Power**: Chip borders colored by relevance (green >= 0.8, amber >= 0.5, default otherwise), percentage badge on chips

This setting will be stored in Zustand user preferences store (localStorage-persisted).

## Component API

```tsx
interface RuleSourceCardProps {
  citations: Citation[];
  gameTitle?: string;
  publisherUrl?: string;
  mode?: 'casual' | 'power'; // from user settings store
  className?: string;
}
```

## Visual States

### 1. Collapsed (default)
```
▸ 📖 3 fonti dal regolamento di Catan
```

### 2. Expanded — Casual Mode (3 citations, chip "p.12" selected)
```
▾ 📖 3 fonti dal regolamento di Catan

  [ p.12 ]  [ p.23 ]  [ p.45 ]

  │ "Quando un giocatore costruisce un insediamento
  │  su un incrocio adiacente a un terreno con il
  │  numero uscito, riceve la risorsa corrispondente."
  │                               — Regolamento, p.12

  [📄 Vedi nel PDF]  [🔗 Regolamento ufficiale ↗]
```

### 3. Expanded — Power Mode
Same as casual but chips show relevance:
- `[ p.12 92% ]` with green left border
- `[ p.23 67% ]` with amber left border
- `[ p.45 41% ]` with default border

### 4. Single Citation (no chips)
When only 1 citation, skip chip row — show quote directly.

## Styling

```
Container:  bg-orange-50/50 dark:bg-orange-950/20
            border border-orange-200/60 dark:border-orange-800/40
            rounded-lg
Header:     font-quicksand text-sm font-semibold text-orange-900 dark:text-orange-100
Chips:      bg-orange-100 dark:bg-orange-900/40 rounded-md px-2 py-1 text-xs
            Active: ring-1 ring-orange-400
Quote:      border-l-2 border-orange-300 dark:border-orange-600
            pl-3 italic text-sm text-stone-700 dark:text-stone-300
            font-nunito
Actions:    text-xs text-orange-600 dark:text-orange-400 hover:underline
```

## Integration Point

In `ChatThreadView.tsx`, replace the current citation rendering:

```tsx
// BEFORE: inline CitationBadge pills scattered in message
{msg.citations?.map(c => <CitationBadge key={...} citation={c} />)}

// AFTER: single RuleSourceCard block after message content
{msg.citations?.length > 0 && (
  <RuleSourceCard
    citations={msg.citations}
    gameTitle={gameName}
    publisherUrl={gamePublisherUrl}
  />
)}
```

`CitationBadge` is NOT deleted — it remains available for other contexts. `RuleSourceCard` is the new default for chat messages.

## File Structure

```
components/chat-unified/
├── RuleSourceCard.tsx          # Main component
├── RuleSourceCard.test.tsx     # → __tests__/
├── CitationBadge.tsx           # Existing (kept, not modified)
├── PdfPageModal.tsx            # Existing (reused by RuleSourceCard)
└── ChatThreadView.tsx          # Modified: swap CitationBadge → RuleSourceCard
```

## Dependencies

- `shadcn/ui` Collapsible (already in project)
- `lucide-react` icons: BookOpen, FileText, ExternalLink, ChevronRight
- Existing `PdfPageModal` component
- Existing `Citation` type from `@/types/domain`
- New: `useAppMode()` hook from user preferences store

## Accessibility

- Collapsible header: `role="button"`, `aria-expanded`, keyboard Enter/Space
- Chip row: `role="tablist"` + `role="tab"` with arrow key navigation
- Quote block: `role="blockquote"`
- Publisher link: `target="_blank" rel="noopener noreferrer"`
- All interactive elements focusable with visible focus rings

## Out of Scope

- Backend changes (Citation type already has all needed fields)
- PDF hosting/redistribution (links to publisher site only)
- User settings page UI (just the Zustand store + hook for now)
- Localization (hardcoded Italian strings, i18n later)
