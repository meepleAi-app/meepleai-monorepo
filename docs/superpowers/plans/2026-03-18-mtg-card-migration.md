# MtG Card Migration — Big Bang Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate all 14 legacy card components to MeepleCard with MtG-style enhancements (entity-colored border, subtle parchment texture), remove legacy files, single PR.

**Architecture:** Two-phase approach: (1) enhance MeepleCard base with MtG styling that propagates to all existing cards, (2) migrate each legacy component to use MeepleCard, replacing custom markup with standardized props. Components with complex interactive features (PlayerStateCard, AgentInfoCard) get wrapped in MeepleCard shell while preserving internal logic.

**Tech Stack:** React 19, Tailwind 4, CVA (class-variance-authority), Next.js 16 App Router, shadcn/ui

---

## File Map

### Files to Create
- `apps/web/src/components/playlists/MeeplePlaylistCard.tsx` — Playlist adapter for MeepleCard
- `apps/web/src/components/wishlist/MeepleWishlistCard.tsx` — Wishlist item adapter for MeepleCard
- `apps/web/src/components/game-night/planning/MeepleGameNightCard.tsx` — Game night adapter
- `apps/web/src/components/game-night/planning/MeepleDealtGameCard.tsx` — Dealt game adapter
- `apps/web/src/components/game-night/planning/MeepleAISuggestionCard.tsx` — AI suggestion adapter
- `apps/web/src/components/session/MeepleParticipantCard.tsx` — Participant adapter
- `apps/web/src/components/session/MeepleResumeSessionCard.tsx` — Resume session adapter
- `apps/web/src/components/library/private-game-detail/MeeplePausedSessionCard.tsx` — Paused session adapter
- `apps/web/src/components/game-state/MeeplePlayerStateCard.tsx` — Player state adapter
- `apps/web/src/components/game-detail/MeeplePdfReferenceCard.tsx` — PDF reference adapter
- `apps/web/src/components/shared-games/MeepleContributorCard.tsx` — Contributor adapter for MeepleCard

### Files to Modify
- `apps/web/src/components/ui/data-display/meeple-card-styles.ts` — Add MtG entity border + texture
- `apps/web/src/styles/design-tokens.css` — Add parchment texture CSS variable
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` — Apply entity border
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardList.tsx` — Apply entity border
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardCompact.tsx` — Apply entity border
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardFeatured.tsx` — Apply entity border
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardHero.tsx` — Apply entity border
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardExpanded.tsx` — Apply entity border
- `apps/web/src/app/(authenticated)/library/playlists/client.tsx` — Swap PlaylistCard → MeeplePlaylistCard
- `apps/web/src/app/(authenticated)/library/wishlist/page.tsx` — Swap WishlistCard → MeepleWishlistCard
- `apps/web/src/app/(authenticated)/sessions/_content.tsx` — Swap ResumeSessionCard → MeepleResumeSessionCard
- `apps/web/src/components/library/private-game-detail/PrivateGameHub.tsx` — Swap PausedSessionCard
- `apps/web/src/components/session/Scoreboard.tsx` — Swap ParticipantCard → MeepleParticipantCard
- `apps/web/src/components/game-night/planning/GameNightList.tsx` — Swap GameNightCard
- `apps/web/src/components/game-night/planning/GameNightPlanningLayout.tsx` — Swap DealtGameCard + AISuggestionCard
- `apps/web/src/components/game-state/GameStateEditor.tsx` — Swap PlayerStateCard
- `apps/web/src/components/game-state/GameStateViewer.tsx` — Swap PlayerStateCard
- `apps/web/src/components/game-detail/AgentChatPanel.tsx` — Swap PdfReferenceCard
- `apps/web/src/app/(authenticated)/agents/[id]/page.tsx` — Swap AgentInfoCard → ExtraMeepleCard agent tabs
- `apps/web/src/components/library/game-detail/GameDetailHeroCard.tsx` — Rewrite to MeepleCard hero variant
- Various `index.ts` barrel files — Update re-exports

### Files to Delete (after migration verified)
- `apps/web/src/components/playlists/PlaylistCard.tsx`
- `apps/web/src/components/wishlist/WishlistCard.tsx`
- `apps/web/src/components/shared-games/CatalogGameCard.tsx`
- `apps/web/src/components/session/ResumeSessionCard.tsx`
- `apps/web/src/components/library/private-game-detail/PausedSessionCard.tsx`
- `apps/web/src/components/session/ParticipantCard.tsx`
- `apps/web/src/components/game-night/planning/GameNightCard.tsx`
- `apps/web/src/components/game-night/planning/DealtGameCard.tsx`
- `apps/web/src/components/game-night/planning/AISuggestionCard.tsx`
- `apps/web/src/components/game-state/PlayerStateCard.tsx`
- `apps/web/src/components/game-detail/PdfReferenceCard.tsx`
- `apps/web/src/components/library/game-detail/GameSideCard.tsx` (already delegates to MeepleInfoCard)
- `apps/web/src/components/library/game-detail/GameDetailHeroCard.tsx` (rewritten in-place)
- `apps/web/src/components/shared-games/ContributorCard.tsx`
- All associated legacy test files

---

## Task 0: MtG Visual Enhancements to MeepleCard Base

**Files:**
- Modify: `apps/web/src/styles/design-tokens.css`
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts`

These changes propagate automatically to all existing MeepleCard instances.

- [ ] **Step 1: Add parchment texture token to design-tokens.css**

In `:root` section, after the warm shadow variables (~line 155), add:

```css
/* MtG-style parchment texture (subtle noise overlay) */
--texture-parchment: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='200' height='200'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.65' numOctaves='3' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='200' height='200' filter='url(%23n)' opacity='0.03'/%3E%3C/svg%3E");
```

- [ ] **Step 2: Add entity border + parchment texture to per-variant CVA classes (NOT base)**

**Important:** Do NOT add entity border to base classes — `hero` variant uses full-bleed background image and `overflow-hidden`, so a left border would look broken. Apply per-variant instead.

In `meepleCardVariants`, add entity border + parchment to `grid`, `list`, `featured`, and `expanded` variants:

```typescript
grid: [
  'flex flex-col rounded-2xl overflow-hidden',
  'bg-card border border-border/50',
  'border-l-[3px] [border-left-color:var(--mc-entity-color,transparent)]', // MtG entity border
  '[box-shadow:var(--shadow-warm-sm)] hover:[box-shadow:var(--shadow-warm-xl)]',
  'hover:-translate-y-2',
  '[contain:layout_paint]',
  '[background-image:var(--texture-parchment)]', // MtG parchment
],
```

Apply same two lines (`border-l-[3px]...` and `[background-image:...]`) to `list`, `featured`, and `expanded`.

For `compact`: add only entity border (no parchment — too small).
For `hero`: add only parchment (no entity border — full-bleed image).

Note: `--mc-entity-color` is already set as inline style by all variant renderers (see MeepleCardGrid line 220).

- [ ] **Step 4: Verify visual changes**

Run: `cd apps/web && pnpm dev`

Open the MeepleCardBrowser at `/dev/card-browser` (or any page with MeepleCards). Verify:
- Subtle left border in entity color on every card
- Faint parchment texture visible on card background
- No visual regressions on existing cards

- [ ] **Step 5: Commit MtG base enhancements**

```bash
git add apps/web/src/styles/design-tokens.css apps/web/src/components/ui/data-display/meeple-card-styles.ts
git commit -m "feat(ui): add MtG-style entity border and parchment texture to MeepleCard"
```

---

## Task 1: Migrate PlaylistCard → MeeplePlaylistCard

**Files:**
- Create: `apps/web/src/components/playlists/MeeplePlaylistCard.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/playlists/client.tsx` — swap import
- Delete later: `apps/web/src/components/playlists/PlaylistCard.tsx`

- [ ] **Step 1: Create MeeplePlaylistCard adapter**

```tsx
// apps/web/src/components/playlists/MeeplePlaylistCard.tsx
'use client';

import { Calendar, Gamepad2, Share2 } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

import type { GameNightPlaylistDto } from '@/types/api';

interface MeeplePlaylistCardProps {
  playlist: GameNightPlaylistDto;
}

export function MeeplePlaylistCard({ playlist }: MeeplePlaylistCardProps) {
  const metadata = [
    { icon: Gamepad2, label: `${playlist.gameCount ?? 0}` },
    ...(playlist.scheduledDate
      ? [{ icon: Calendar, label: new Date(playlist.scheduledDate).toLocaleDateString('it-IT') }]
      : []),
  ];

  return (
    <Link href={`/library/playlists/${playlist.id}`} className="block">
      <MeepleCard
        entity="collection"
        variant="list"
        title={playlist.name}
        subtitle={playlist.isShared ? 'Condivisa' : undefined}
        metadata={metadata}
        badge={playlist.isShared ? 'Condivisa' : undefined}
        data-testid="playlist-card"
      />
    </Link>
  );
}
```

- [ ] **Step 2: Swap import in playlists client page**

In `apps/web/src/app/(authenticated)/library/playlists/client.tsx`, replace:
```tsx
// OLD
import { PlaylistCard } from '@/components/playlists/PlaylistCard';
// NEW
import { MeeplePlaylistCard } from '@/components/playlists/MeeplePlaylistCard';
```

And replace all `<PlaylistCard` with `<MeeplePlaylistCard`.

- [ ] **Step 3: Verify playlist page renders correctly**

Run: `cd apps/web && pnpm dev` → navigate to `/library/playlists`
Verify: Cards render with collection entity color (copper), mana badge, warm shadows.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/playlists/MeeplePlaylistCard.tsx apps/web/src/app/\(authenticated\)/library/playlists/client.tsx
git commit -m "feat(playlists): migrate PlaylistCard to MeepleCard collection entity"
```

---

## Task 2: Migrate WishlistCard → MeepleWishlistCard

**Files:**
- Create: `apps/web/src/components/wishlist/MeepleWishlistCard.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/wishlist/page.tsx` — swap import

- [ ] **Step 1: Create MeepleWishlistCard adapter**

```tsx
// apps/web/src/components/wishlist/MeepleWishlistCard.tsx
'use client';

import { DollarSign, Edit, Trash2 } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

import type { WishlistItemDto } from '@/types/api';

const PRIORITY_LABELS: Record<string, string> = {
  high: 'Alta',
  medium: 'Media',
  low: 'Bassa',
};

interface MeepleWishlistCardProps {
  item: WishlistItemDto;
  onRemove?: (id: string) => void;
  onUpdate?: (id: string) => void;
}

export function MeepleWishlistCard({ item, onRemove, onUpdate }: MeepleWishlistCardProps) {
  const metadata = [
    ...(item.targetPrice
      ? [{ icon: DollarSign, label: `${item.targetPrice.toFixed(2)}` }]
      : []),
  ];

  const quickActions = [
    ...(onUpdate
      ? [{ icon: Edit, label: 'Modifica', onClick: () => onUpdate(item.id) }]
      : []),
    ...(onRemove
      ? [{ icon: Trash2, label: 'Rimuovi', onClick: () => onRemove(item.id), destructive: true }]
      : []),
  ];

  return (
    <MeepleCard
      entity="game"
      variant="list"
      title={item.gameTitle ?? item.gameId}
      subtitle={item.notes || undefined}
      metadata={metadata}
      badge={PRIORITY_LABELS[item.priority] ?? item.priority}
      quickActions={quickActions.length > 0 ? quickActions : undefined}
      data-testid="wishlist-card"
    />
  );
}
```

- [ ] **Step 2: Swap import in wishlist page**

In `apps/web/src/app/(authenticated)/library/wishlist/page.tsx`, replace PlaylistCard import and usage.

- [ ] **Step 3: Verify and commit**

```bash
git add apps/web/src/components/wishlist/MeepleWishlistCard.tsx apps/web/src/app/\(authenticated\)/library/wishlist/page.tsx
git commit -m "feat(wishlist): migrate WishlistCard to MeepleCard game entity"
```

---

## Task 3: Migrate GameDetailHeroCard → MeepleCard hero variant

**Files:**
- Modify: `apps/web/src/components/library/game-detail/GameDetailHeroCard.tsx` — rewrite in-place

- [ ] **Step 1: Rewrite GameDetailHeroCard to use MeepleCard hero**

Read the current file first. Replace the body with MeepleCard hero variant, mapping:
- `gameDetail.title` → `title`
- `gameDetail.publisher` + year → `subtitle`
- Players, duration, complexity → `metadata` array
- Rating → `rating` / `ratingMax={10}`
- Cover image → `imageUrl`
- entity="game"
- variant="hero"

Preserve the same exported interface `GameDetailHeroCardProps`.

- [ ] **Step 2: Verify game detail page**

Navigate to `/library/games/[any-game-id]`. Verify hero card renders with warm shadows, entity border, parchment texture.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/library/game-detail/GameDetailHeroCard.tsx
git commit -m "feat(game-detail): rewrite GameDetailHeroCard to MeepleCard hero variant"
```

---

## Task 4: Remove GameSideCard (already deprecated)

**Files:**
- Delete: `apps/web/src/components/library/game-detail/GameSideCard.tsx`
- Modify: `apps/web/src/components/library/game-detail/index.ts` — remove re-export

- [ ] **Step 1: Check all import locations**

Search for `GameSideCard` imports. If any page still imports it directly (not through MeepleInfoCard), update to use MeepleInfoCard.

- [ ] **Step 2: Remove file and update barrel**

- [ ] **Step 3: Commit**

```bash
git commit -m "chore(game-detail): remove deprecated GameSideCard wrapper"
```

---

## Task 5: Migrate CatalogGameCard → MeepleGameCatalogCard

**Files:**
- Modify: Consumer pages to use existing `MeepleGameCatalogCard` instead of `CatalogGameCard`
- Delete later: `apps/web/src/components/shared-games/CatalogGameCard.tsx`

- [ ] **Step 1: Compare props interfaces**

Read both `CatalogGameCard.tsx` and `MeepleGameCatalogCard.tsx`. Diff the prop interfaces:
- `CatalogGameCard` takes `{ game: SharedGame | SharedGameDetail; communityStats?: CommunityStats }`
- `MeepleGameCatalogCard` takes different props (likely `{ game: SharedGame }` or similar)

Map any missing props. If `communityStats` is not supported by `MeepleGameCatalogCard`, extend it or pass data through existing MeepleCard metadata props.

- [ ] **Step 2: Find all CatalogGameCard consumers and swap**

Search for imports of `CatalogGameCard`. Replace with `MeepleGameCatalogCard` from `@/components/catalog/MeepleGameCatalogCard`, adjusting prop names as identified in Step 1.

- [ ] **Step 2: Verify shared games pages render correctly**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(catalog): replace CatalogGameCard with MeepleGameCatalogCard"
```

---

## Task 6: Migrate ResumeSessionCard → MeepleResumeSessionCard

**Files:**
- Create: `apps/web/src/components/session/MeepleResumeSessionCard.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/_content.tsx`

- [ ] **Step 1: Create MeepleResumeSessionCard**

```tsx
// apps/web/src/components/session/MeepleResumeSessionCard.tsx
'use client';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Camera, Hash, Users } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

interface MeepleResumeSessionCardProps {
  sessionId: string;
  gameName: string;
  lastActivityAt: string;
  playerCount: number;
  sessionCode: string;
  photoCount?: number;
}

export function MeepleResumeSessionCard({
  sessionId,
  gameName,
  lastActivityAt,
  playerCount,
  sessionCode,
  photoCount,
}: MeepleResumeSessionCardProps) {
  const timeAgo = formatDistanceToNow(new Date(lastActivityAt), { addSuffix: true, locale: it });

  const metadata = [
    { icon: Users, label: `${playerCount}` },
    { icon: Hash, label: sessionCode },
    ...(photoCount ? [{ icon: Camera, label: `${photoCount}` }] : []),
  ];

  // Note: PrimaryAction.icon is type `string` (emoji/unicode), NOT LucideIcon
  return (
    <Link href={`/sessions/${sessionId}/scoreboard`} className="block">
      <MeepleCard
        entity="session"
        variant="list"
        title={gameName}
        subtitle={`In pausa \u2022 ${timeAgo}`}
        metadata={metadata}
        sessionStatus="paused"
        badge="In pausa"
        primaryActions={[
          { label: 'Riprendi', icon: '▶', onClick: () => {} },
        ]}
        data-testid="resume-session-card"
      />
    </Link>
  );
}
```

- [ ] **Step 2: Swap import in sessions content**

- [ ] **Step 3: Verify and commit**

```bash
git commit -m "feat(sessions): migrate ResumeSessionCard to MeepleCard session entity"
```

---

## Task 7: Migrate PausedSessionCard → MeeplePausedSessionCard

**Files:**
- Create: `apps/web/src/components/library/private-game-detail/MeeplePausedSessionCard.tsx`
- Modify: `apps/web/src/components/library/private-game-detail/PrivateGameHub.tsx`

- [ ] **Step 1: Create MeeplePausedSessionCard**

Map the paused session data to MeepleCard props:
- entity="session", variant="list"
- sessionStatus="paused"
- Participants as subtitle (e.g. "3 giocatori")
- Turn info in metadata
- Resume/Abandon as quickActions
- "Vecchia" badge if >30 days

Read the current PausedSessionCard.tsx first to preserve all logic (confirmation dialog for old sessions, etc). The confirmation dialog can be kept as internal state.

- [ ] **Step 2: Swap import in PrivateGameHub**

- [ ] **Step 3: Verify and commit**

```bash
git commit -m "feat(sessions): migrate PausedSessionCard to MeepleCard session entity"
```

---

## Task 8: Migrate ParticipantCard → MeepleParticipantCard

**Files:**
- Create: `apps/web/src/components/session/MeepleParticipantCard.tsx`
- Modify: `apps/web/src/components/session/Scoreboard.tsx`
- Modify: `apps/web/src/components/session/index.ts`

- [ ] **Step 1: Create MeepleParticipantCard**

Map participant data to MeepleCard:
- entity="player"
- variant based on prop: "compact" → "compact", "full" → "grid"
- avatarUrl from initials (keep avatar generation logic)
- Rank emoji in badge (🥇🥈🥉)
- Score in metadata
- Crown icon for owner via tags
- "Sta scrivendo..." subtitle when typing

- [ ] **Step 2: Swap imports in Scoreboard and barrel**

- [ ] **Step 3: Verify scoreboard page**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(sessions): migrate ParticipantCard to MeepleCard player entity"
```

---

## Task 9: Migrate GameNightCard → MeepleGameNightCard

**Files:**
- Create: `apps/web/src/components/game-night/planning/MeepleGameNightCard.tsx`
- Modify: `apps/web/src/components/game-night/planning/GameNightList.tsx`
- Modify: `apps/web/src/components/game-night/planning/index.ts`

- [ ] **Step 1: Create MeepleGameNightCard**

Map game night data to MeepleCard:
- entity="event"
- variant="grid"
- Status badge (upcoming/draft/completed) via `badge` prop
- Date + location in subtitle
- Player count + game count in metadata
- Player avatar stacking can use existing MeepleCard avatar features

- [ ] **Step 2: Swap imports**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(game-night): migrate GameNightCard to MeepleCard event entity"
```

---

## Task 10: Migrate DealtGameCard → MeepleDealtGameCard

**Files:**
- Create: `apps/web/src/components/game-night/planning/MeepleDealtGameCard.tsx`
- Modify: `apps/web/src/components/game-night/planning/GameNightPlanningLayout.tsx`

- [ ] **Step 1: Create MeepleDealtGameCard**

Map dealt game to MeepleCard:
- entity="game"
- variant="compact"
- CSS rotation via className prop (preserve rotation angle)
- Remove button as quickAction
- Compact size (~140x180px via className override)

- [ ] **Step 2: Swap import in GameNightPlanningLayout**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(game-night): migrate DealtGameCard to MeepleCard compact game entity"
```

---

## Task 11: Migrate AISuggestionCard → MeepleAISuggestionCard

**Files:**
- Create: `apps/web/src/components/game-night/planning/MeepleAISuggestionCard.tsx`
- Modify: `apps/web/src/components/game-night/planning/GameNightPlanningLayout.tsx`

- [ ] **Step 1: Create MeepleAISuggestionCard**

Map AI suggestions to MeepleCard:
- entity="agent"
- variant="expanded"
- Title: "Suggerimenti AI"
- Each suggestion as metadata or rendered in content area
- Empty state: subtitle "Aggiungi giocatori per suggerimenti"
- Sparkles icon preserved

Note: This is a list of suggestions, not a single entity card. Render each suggestion as a MeepleCard compact, or keep as a single "agent" card with custom content.

- [ ] **Step 2: Swap import**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(game-night): migrate AISuggestionCard to MeepleCard agent entity"
```

---

## Task 12: Migrate PlayerStateCard → MeeplePlayerStateCard

**Files:**
- Create: `apps/web/src/components/game-state/MeeplePlayerStateCard.tsx`
- Modify: `apps/web/src/components/game-state/GameStateEditor.tsx`
- Modify: `apps/web/src/components/game-state/GameStateViewer.tsx`
- Modify: `apps/web/src/components/game-state/index.ts`

- [ ] **Step 1: Create MeeplePlayerStateCard**

This is the most complex migration. PlayerStateCard has interactive resource tracking.

Approach: Wrap in MeepleCard expanded shell, keep internal logic:
- entity="player"
- variant="expanded"
- Player name as title, color-based customColor
- "Turno corrente" badge when isCurrentPlayer
- Score display in metadata
- Resource tracking section preserved as children/custom content

Read the current PlayerStateCard fully before implementing. The ResourceTracker and editable controls must be preserved.

- [ ] **Step 2: Swap imports in GameStateEditor, GameStateViewer, barrel**

- [ ] **Step 3: Verify game state page with resource editing**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(game-state): migrate PlayerStateCard to MeepleCard player entity"
```

---

## Task 13: Migrate PdfReferenceCard → MeeplePdfReferenceCard

**Files:**
- Create: `apps/web/src/components/game-detail/MeeplePdfReferenceCard.tsx`
- Modify: `apps/web/src/components/game-detail/AgentChatPanel.tsx`
- Modify: `apps/web/src/components/game-detail/index.ts`

- [ ] **Step 1: Create MeeplePdfReferenceCard**

Map PDF reference to MeepleCard:
- entity="kb"
- variant="compact"
- PDF name as title
- Page number + excerpt in subtitle/metadata
- onClick → onJumpToPage callback

- [ ] **Step 2: Swap imports**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(kb): migrate PdfReferenceCard to MeepleCard kb entity"
```

---

## Task 14: Migrate AgentInfoCard → ExtraMeepleCard tabs

**Files:**
- Modify: `apps/web/src/app/(authenticated)/agents/[id]/page.tsx`
- Delete later: `apps/web/src/components/agent/AgentInfoCard.tsx`

- [ ] **Step 1: Extend AgentExtraMeepleCard with chat tab**

Pre-assessment: `AgentExtraMeepleCard` has 4 tabs (overview, stats, history, kb) but NO embedded chat tab. `AgentInfoCard` has 3 tabs (Chat, Storico, KB) with an embedded `ChatThreadView` with SSE streaming.

The tabs differ — `AgentExtraMeepleCard` must be extended to add a "Chat" tab containing `ChatThreadView`. This requires:
1. Add `'chat'` to the `AgentTab` union type
2. Add a chat `TabsContent` that renders `ChatThreadView` (copy from AgentInfoCard)
3. Add props: `agentId: string`, `enableChat?: boolean` to control chat tab visibility
4. Preserve readiness validation logic from AgentInfoCard (blocks chat if KB empty)

- [ ] **Step 2: Swap in agent detail page**

Replace `<AgentInfoCard agentId={id} agentName={name} />` with `<AgentExtraMeepleCard data={...} enableChat />`, passing the required `AgentDetailData` (fetch from existing hooks on the page).

- [ ] **Step 3: Verify agent detail page**

Navigate to `/agents/[id]`. Verify chat, history, and KB tabs work.

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(agents): replace AgentInfoCard with ExtraMeepleCard agent tabs"
```

---

## Task 14b: Migrate ContributorCard → MeepleContributorCard

**Files:**
- Create: `apps/web/src/components/shared-games/MeepleContributorCard.tsx`
- Modify: consumers of `ContributorCard` (search for imports)
- Delete later: `apps/web/src/components/shared-games/ContributorCard.tsx`

- [ ] **Step 1: Create MeepleContributorCard**

Map contributor data to MeepleCard:
- entity="player" (contributors are users)
- variant="list"
- `contributor.userName` → title
- `contributor.contributionCount` contributions + time ago → subtitle
- Avatar: `contributor.avatarUrl` → avatarUrl
- "Original" badge if `contributor.isPrimaryContributor`
- Badges section: preserve `BadgeIcon` rendering in metadata or via tags
- Link wrapper to `/contributors/{userId}`

```tsx
// apps/web/src/components/shared-games/MeepleContributorCard.tsx
'use client';

import { formatDistanceToNow } from 'date-fns';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

import type { GameContributorDto } from '@/lib/api';

interface MeepleContributorCardProps {
  contributor: GameContributorDto;
  featured?: boolean;
}

export function MeepleContributorCard({ contributor, featured }: MeepleContributorCardProps) {
  const contributions = `${contributor.contributionCount} contribution${contributor.contributionCount !== 1 ? 's' : ''}`;
  const timeAgo = formatDistanceToNow(new Date(contributor.firstContributionAt), { addSuffix: true });

  return (
    <Link href={`/contributors/${contributor.userId}`} className="block">
      <MeepleCard
        entity="player"
        variant="list"
        title={contributor.userName}
        subtitle={`${contributions} \u2022 ${timeAgo}`}
        avatarUrl={contributor.avatarUrl}
        badge={contributor.isPrimaryContributor ? 'Original' : undefined}
        selected={featured}
        data-testid="contributor-card"
      />
    </Link>
  );
}
```

- [ ] **Step 2: Swap imports in consumer pages**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(shared-games): migrate ContributorCard to MeepleCard player entity"
```

---

## Task 15: Delete Legacy Files

**Files to delete:** All original legacy card files listed in the File Map above.

- [ ] **Step 1: Verify no remaining imports of legacy components**

```bash
cd apps/web
grep -r "PlaylistCard\|WishlistCard\|CatalogGameCard\|ResumeSessionCard\|PausedSessionCard\|ParticipantCard\|GameNightCard\|DealtGameCard\|AISuggestionCard\|PlayerStateCard\|PdfReferenceCard\|AgentInfoCard\|GameSideCard\|ContributorCard" src/ --include="*.tsx" --include="*.ts" | grep -v "Meeple" | grep -v "__tests__" | grep -v ".test."
```

Expected: No results (all imports point to Meeple* versions).

- [ ] **Step 2: Delete legacy component files**

```bash
rm apps/web/src/components/playlists/PlaylistCard.tsx
rm apps/web/src/components/wishlist/WishlistCard.tsx
rm apps/web/src/components/shared-games/CatalogGameCard.tsx
rm apps/web/src/components/session/ResumeSessionCard.tsx
rm apps/web/src/components/library/private-game-detail/PausedSessionCard.tsx
rm apps/web/src/components/session/ParticipantCard.tsx
rm apps/web/src/components/game-night/planning/GameNightCard.tsx
rm apps/web/src/components/game-night/planning/DealtGameCard.tsx
rm apps/web/src/components/game-night/planning/AISuggestionCard.tsx
rm apps/web/src/components/game-state/PlayerStateCard.tsx
rm apps/web/src/components/game-detail/PdfReferenceCard.tsx
rm apps/web/src/components/agent/AgentInfoCard.tsx
rm apps/web/src/components/library/game-detail/GameSideCard.tsx
rm apps/web/src/components/shared-games/ContributorCard.tsx
```

- [ ] **Step 3: Delete legacy test files**

Remove corresponding test files in `__tests__/` directories.

- [ ] **Step 4: Update barrel exports (index.ts files)**

Remove re-exports of deleted components from:
- `components/session/index.ts`
- `components/shared-games/index.ts`
- `components/game-night/planning/index.ts`
- `components/game-state/index.ts`
- `components/game-detail/index.ts`
- `components/library/game-detail/index.ts`

Add re-exports for new Meeple* components where applicable.

- [ ] **Step 5: Commit**

Stage only the deleted files and modified barrel exports (avoid `git add -A` which may stage unrelated files):

```bash
git add -u apps/web/src/components/playlists/PlaylistCard.tsx \
  apps/web/src/components/wishlist/WishlistCard.tsx \
  apps/web/src/components/shared-games/CatalogGameCard.tsx \
  apps/web/src/components/session/ResumeSessionCard.tsx \
  apps/web/src/components/library/private-game-detail/PausedSessionCard.tsx \
  apps/web/src/components/session/ParticipantCard.tsx \
  apps/web/src/components/game-night/planning/GameNightCard.tsx \
  apps/web/src/components/game-night/planning/DealtGameCard.tsx \
  apps/web/src/components/game-night/planning/AISuggestionCard.tsx \
  apps/web/src/components/game-state/PlayerStateCard.tsx \
  apps/web/src/components/game-detail/PdfReferenceCard.tsx \
  apps/web/src/components/agent/AgentInfoCard.tsx \
  apps/web/src/components/library/game-detail/GameSideCard.tsx \
  apps/web/src/components/shared-games/ContributorCard.tsx
# Also stage modified index.ts barrel files
git add apps/web/src/components/session/index.ts \
  apps/web/src/components/shared-games/index.ts \
  apps/web/src/components/game-night/planning/index.ts \
  apps/web/src/components/game-state/index.ts \
  apps/web/src/components/game-detail/index.ts \
  apps/web/src/components/library/game-detail/index.ts
git commit -m "chore: remove all legacy card components replaced by MeepleCard"
```

---

## Task 16: Update Tests

**Files:** New test files for each migrated component.

- [ ] **Step 1: Create test files for each new Meeple* adapter**

For each new component, write a minimal test verifying:
1. Renders without crashing
2. Displays the correct entity type (`data-entity` attribute)
3. Shows title text
4. Renders MeepleCard (check for `data-testid="meeple-card"`)

Pattern for each test:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MeeplePlaylistCard } from '../MeeplePlaylistCard';

describe('MeeplePlaylistCard', () => {
  const mockPlaylist = {
    id: '1',
    name: 'Friday Games',
    gameCount: 5,
    isShared: false,
    scheduledDate: '2026-03-20T18:00:00Z',
  };

  it('renders with collection entity type', () => {
    render(<MeeplePlaylistCard playlist={mockPlaylist} />);
    // Note: adapter passes data-testid="playlist-card", so query that (not "meeple-card")
    expect(screen.getByTestId('playlist-card')).toHaveAttribute('data-entity', 'collection');
  });

  it('displays playlist name', () => {
    render(<MeeplePlaylistCard playlist={mockPlaylist} />);
    expect(screen.getByText('Friday Games')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run all tests**

```bash
cd apps/web && pnpm test
```

Fix any failures.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/playlists/__tests__/ \
  apps/web/src/components/wishlist/__tests__/ \
  apps/web/src/components/session/__tests__/ \
  apps/web/src/components/game-night/planning/__tests__/ \
  apps/web/src/components/game-state/__tests__/ \
  apps/web/src/components/game-detail/__tests__/ \
  apps/web/src/components/shared-games/__tests__/ \
  apps/web/src/components/library/private-game-detail/__tests__/
git commit -m "test: add tests for all migrated MeepleCard adapters"
```

---

## Task 17: Final Verification

- [ ] **Step 1: TypeScript check**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 2: Lint check**

```bash
cd apps/web && pnpm lint
```

- [ ] **Step 3: Full test suite**

```bash
cd apps/web && pnpm test
```

- [ ] **Step 4: Visual smoke test**

Navigate through all affected pages:
1. `/library/playlists` — PlaylistCard migrated
2. `/library/wishlist` — WishlistCard migrated
3. `/library/games/[id]` — GameDetailHeroCard + GameSideCard migrated
4. `/shared-games` — CatalogGameCard migrated
5. `/sessions` — ResumeSessionCard migrated
6. `/library/private-games/[id]` — PausedSessionCard migrated
7. `/sessions/[id]/scoreboard` — ParticipantCard migrated
8. `/game-nights` — GameNightCard migrated
9. `/game-nights/[id]/planning` — DealtGameCard + AISuggestionCard migrated
10. `/agents/[id]` — AgentInfoCard migrated

Verify each page:
- MeepleCard renders with correct entity color
- Entity-colored left border visible (MtG enhancement)
- Parchment texture visible
- HoloOverlay activates on hover
- ManaBadge shows in top-left
- Warm shadows present
- No visual regressions

- [ ] **Step 5: Commit any fixes**

- [ ] **Step 6: Create PR**

```bash
git push -u origin feature/mtg-card-migration
gh pr create --title "feat(ui): MtG card migration — unify all entity cards to MeepleCard" --base main-dev --body "$(cat <<'EOF'
## Summary
- Migrate 13 legacy card components to MeepleCard design system
- Add MtG-style enhancements: entity-colored border, parchment texture
- Delete all legacy card files after migration
- Update all consumer pages and barrel exports

## Components Migrated
PlaylistCard, WishlistCard, GameDetailHeroCard, GameSideCard, CatalogGameCard,
ResumeSessionCard, PausedSessionCard, ParticipantCard, GameNightCard, DealtGameCard,
AISuggestionCard, PlayerStateCard, PdfReferenceCard, AgentInfoCard, ContributorCard

## Test plan
- [ ] TypeScript compiles cleanly
- [ ] Lint passes
- [ ] All tests pass
- [ ] Visual smoke test on all affected pages
- [ ] MtG enhancements visible (entity border, texture, holo overlay)
- [ ] No legacy card component imports remain

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Risk Mitigation

**Largest risk:** PlayerStateCard (Task 12) — complex interactive logic. If migration proves too invasive, fallback: wrap in a MeepleCard-styled container div with entity border + warm shadow classes, without full MeepleCard component integration.

**AgentInfoCard (Task 14):** Has embedded chat with SSE streaming. If ExtraMeepleCard agent variant doesn't cover all tabs, create a minimal inline version rather than forcing into drawer pattern.

**Build breakage:** After Task 15 (delete), if any import was missed, TypeScript will catch it immediately. Fix and re-commit.

**Incremental type safety:** Run `cd apps/web && pnpm typecheck` after Tasks 0, 5, 12, and 14 (highest-risk points). Don't wait until Task 17 to catch compile errors.

**Parchment texture constraint:** The `[background-image:var(--texture-parchment)]` class sets `background-image` on the card element. This means Tailwind `bg-gradient-*` utilities on the same element would be overridden. This is acceptable — card gradients are not currently used, and future gradient needs should use a pseudo-element overlay instead.
