# DS-17-10 sp3 Cluster Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate 8 sp3 cluster mockups to Storybook stories + create new `/library-public` route + LibraryPublicHome component, closing DS-17 Phase C-1 step 3/3 and unblocking Phase C-2 (SP4 core 106 mockup + sp4-sessions 50 mockup).

**Architecture:** Pure FE work. Stage 0 BGG cleanup (3 mockup JSX twin edits). Stage 1 NEW `/library-public` route + LibraryPublicHome component + 2 new primitives (CommunityStatsRow + FeaturedGamesCarousel). Stage 2 AI dispatch 7 standard stems migrate to Storybook stories (mockup-reading subagent per stem, sequential). Stage 3 baseline capture 8 PNG Desktop 1440x900. Stage 4 quality gates. Stage 5 merge + closure.

**Tech Stack:** Next.js App Router (server + client components), Storybook 8, Tailwind semantic tokens, MeepleCard primitive (entity-driven), MSW handlers for story mocking, `pnpm lint:*` gates (tokens / bgg / fidelity / annotations).

**Spec ref:** [`docs/superpowers/specs/2026-06-11-ds-17-10-sp3-cluster-design.md`](../specs/2026-06-11-ds-17-10-sp3-cluster-design.md)

**Sub-issue:** [#2208](https://github.com/meepleAi-app/meepleai-monorepo/issues/2208)

**Branch:** `feature/issue-2208-ds-17-10-sp3-cluster` (already on this branch — pre-flight P124 done, spec committed `67455f369` + `ad8014a66`)

**Constraints:**
- ❌ DEC-Memory-1: sp3-library-public route-create MANDATORY (cannot skip)
- ❌ DEC-Memory-2: BGG cleanup MUST run Stage 0 atomic commit BEFORE Stage 1+
- ❌ DEC-Memory-3: sp3-shared-game-detail story MUST render POST-rebuild `GameDetailDesktop`
- ❌ DEC-1: 1 sub-issue combined (1 PR final)
- ❌ DEC-5: 1 Agent dispatch per stem (NO batch, NO parallel)
- ❌ NO new tab IDs (#2010)
- ❌ NO BGG references in code (#1903 #2123)
- ❌ NO mobile viewports (DEC viewports: desktop only across 8 sp3 stems)
- ✅ Reuse predecessor pattern DS-17-11 sp6-7-nano (PR #2173 sess.46o)

---

## File Structure

### Files modified (Stage 0 BGG cleanup)

| Path | Action | Edit |
|---|---|---|
| `admin-mockups/design_files/sp3-how-it-works.jsx` | Edit | Line 231 + 461 (BGG removal) |
| `admin-mockups/design_files/sp3-shared-game-detail.jsx` | Edit | Lines 67-68 (KB entry remove) |
| `admin-mockups/design_files/sp3-faq-enhanced.jsx` | Edit | Line 51 (BGG text replace) |

### Files created (Stage 1 sp3-library-public route + component)

| Path | Responsibility |
|---|---|
| `apps/web/src/app/(public)/library-public/page.tsx` | Server component wrapper; fetches featured games + community stats; passes to client |
| `apps/web/src/app/(public)/library-public/page.stories.tsx` | Storybook entry; MSW handlers fixture |
| `apps/web/src/components/features/library-public/LibraryPublicHome.tsx` | Client component; orchestrates HeroGradient + CommunityStatsRow + FeaturedGamesCarousel sections |
| `apps/web/src/components/features/library-public/CommunityStatsRow.tsx` | NEW primitive; stats banner (totalGames · totalPlayers · totalSessions · totalCommunityContent) |
| `apps/web/src/components/features/library-public/FeaturedGamesCarousel.tsx` | NEW primitive; horizontal scroll carousel of 4-6 MeepleCard hero variant |
| `apps/web/src/components/features/library-public/__tests__/LibraryPublicHome.test.tsx` | Smoke test (1-2 it() — DEC-5 manual+smoke only, regression-resilient assertions) |

### Files modified (Stage 1 fidelity + index + tracking)

| Path | Action |
|---|---|
| `admin-mockups/design_files/sp3-library-public.fidelity.json` | Set `obsolete_tracking_issue` to NEW tracking issue number |
| `admin-mockups/MOCKUPS_INDEX.md` | Add row mapping sp3-library-public mockup → `/library-public` route |

### Files created (Stage 2 AI dispatch 7 stems)

7 story files via Agent dispatch (1 per stem, sequential):

| Stem | Story file |
|---|---|
| sp3-shared-games | `apps/web/src/app/(public)/shared-games/page.stories.tsx` |
| sp3-shared-game-detail | `apps/web/src/app/(public)/shared-games/[id]/page.stories.tsx` (POST-rebuild integration) |
| sp3-legal | `apps/web/src/app/(public)/legal/page.stories.tsx` |
| sp3-join | `apps/web/src/app/(public)/join/page.stories.tsx` |
| sp3-how-it-works | `apps/web/src/app/(public)/how-it-works/page.stories.tsx` |
| sp3-faq-enhanced | `apps/web/src/app/(public)/faq/page.stories.tsx` |
| sp3-accept-invite | `apps/web/src/app/(public)/accept-invite/page.stories.tsx` |

### Files created (Stage 3 baseline capture)

8 PNG files in Storybook test runner snapshot directory (per stem, Desktop 1440x900).

### Files referenced (read-only by implementers)

| Path | Why |
|---|---|
| `admin-mockups/design_files/sp3-*.jsx` | Mockup canonical source per stem |
| `admin-mockups/design_files/sp3-*.fidelity.json` | design_intent classification |
| `apps/web/src/components/ui/data-display/meeple-card/` | MeepleCard primitive (hero/grid variant) |
| `apps/web/src/components/ui/HeroGradient*` (grep to locate) | HeroGradient primitive reuse |
| `apps/web/src/components/ui/data-display/entity-chip.tsx` | EntityChip primitive reuse |
| `apps/web/src/components/game-detail/GameDetailDesktop.tsx` | sp3-shared-game-detail story renders this |
| `apps/web/src/__tests__/mocks/server.ts` | MSW test infrastructure |
| Previous patterns | `apps/web/src/app/(public)/how-it-works/page.stories.tsx` (DS-17-11 sp6-7-nano pattern reference, if exists) |

---

## Stage 0 — BGG cleanup atomic commit (~30 min)

### Task 0.1: Edit 3 mockup JSX twins + extend #2151

**Files:**
- Modify: `admin-mockups/design_files/sp3-how-it-works.jsx` lines 231 + 461
- Modify: `admin-mockups/design_files/sp3-shared-game-detail.jsx` lines 67-68
- Modify: `admin-mockups/design_files/sp3-faq-enhanced.jsx` line 51
- Side effect: gh issue comment 2151

- [ ] **Step 1: Verify branch state**

Run: `git branch --show-current`
Expected: `feature/issue-2208-ds-17-10-sp3-cluster`

Run: `git log --oneline -3`
Expected: top 2 commits are `ad8014a66` + `67455f369` (spec + spec self-review).

- [ ] **Step 2: Edit sp3-how-it-works.jsx line 231**

Read context around line 231 to confirm exact string. Then Edit:

```
Find:    <span>cerca su BGG…</span>
Replace: <span>cerca nel catalogo…</span>
```

If exact string differs from above, adapt to actual mockup text removing `BGG`/`BoardGameGeek` reference.

- [ ] **Step 3: Edit sp3-how-it-works.jsx line 461**

Read context around line 461. Then Edit:

```
Find:    'Cerca giochi direttamente da BoardGameGeek o aggiungili manualmente'
Replace: 'Aggiungi giochi dal catalogo interno'
```

- [ ] **Step 4: Edit sp3-shared-game-detail.jsx lines 67-68**

Read context around lines 67-68 to confirm exact KB entry. Then Edit (delete the entire entry):

```
Find:    { id:'kb-wing-bgg', title:'BoardGameGeek FAQ', kind:'URL', url:'boardgamegeek.com/wingspan/faq' },
Replace: (empty — remove entire entry)
```

Verify that the surrounding array structure remains valid JS (no trailing comma issue).

- [ ] **Step 5: Edit sp3-faq-enhanced.jsx line 51**

Read context around line 51. Then Edit:

```
Find:    (text containing "link BoardGameGeek")
Replace: "Suggerisci giochi via /contact"
```

Adjust to preserve surrounding sentence structure.

- [ ] **Step 6: Run lint:bgg-mockups gate**

Run from project root:
```bash
pnpm --filter @meepleai/web lint:bgg-mockups
```

Expected: 0 violations in sp3 cluster mockup files (codebase already clean per #1903).

If violations remain in sp3 files: re-grep the 3 files for `BGG|BoardGameGeek|boardgamegeek`:
```bash
grep -in "BGG\|BoardGameGeek\|boardgamegeek" admin-mockups/design_files/sp3-{how-it-works,shared-game-detail,faq-enhanced}.jsx
```
Apply additional Edits as needed.

- [ ] **Step 7: Commit BGG cleanup**

```bash
git add admin-mockups/design_files/sp3-how-it-works.jsx \
        admin-mockups/design_files/sp3-shared-game-detail.jsx \
        admin-mockups/design_files/sp3-faq-enhanced.jsx

git commit -m "$(cat <<'EOF'
chore(mockups): #2208 DS-17-10 BGG removal sp3 cluster

DEC-Memory-2 + DEC-2: BGG cleanup Stage 0 prep work pre-AI dispatch (DEC-Pilot-7 pattern from DS-17-11).

3 JSX twins edit (HTML twins clean):
- sp3-how-it-works.jsx line 231: "cerca su BGG…" → "cerca nel catalogo…"
- sp3-how-it-works.jsx line 461: BoardGameGeek bullet → "catalogo interno"
- sp3-shared-game-detail.jsx lines 67-68: KB entry boardgamegeek.com remove
- sp3-faq-enhanced.jsx line 51: BoardGameGeek link → "Suggerisci giochi via /contact"

Post-cleanup: AI dispatch reads BGG-free state guaranteed.

Refs: #2208, #2151 BGG ToS umbrella, #1903 #2123 codebase clean.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 8: Extend #2151 comment with 3 new findings**

```bash
gh issue comment 2151 --body "$(cat <<'EOF'
**DS-17-10 sub-issue #2208 sess.46p — 3 nuovi findings sp3 cluster** (Phase B audit miss):

| Mockup | Line | Severity | Description |
|---|---|---|---|
| sp3-faq-enhanced.jsx | 51 | LOW | "link BoardGameGeek" in FAQ guidance text (replaced "Suggerisci giochi via /contact") |
| sp3-shared-game-detail.jsx | 67-68 | MEDIUM | KB document entry `boardgamegeek.com/wingspan/faq` (removed entire KB entry) |
| sp3-how-it-works.jsx | 231, 461 | HIGH | "cerca su BGG…" placeholder + "Cerca giochi direttamente da BoardGameGeek" onboarding bullet (already documented in #2151, re-confirmed and addressed) |

Cleanup atomic commit landed in `feature/issue-2208-ds-17-10-sp3-cluster` branch. Post-cleanup verified via `pnpm lint:bgg-mockups` (sp3 cluster clean).

Pattern: DEC-Pilot-7 (BGG cleanup as Stage 0 prep work pre-AI dispatch) identico a DS-17-11 sp6-7-nano sess.46o.
EOF
)"
```

Expected: gh CLI returns comment URL.

---

## Stage 1 — sp3-library-public route + LibraryPublicHome component (~6h)

### Task 1.1: Read sp3-library-public mockup + identify reuse primitives

**Files (read-only):**
- `admin-mockups/design_files/sp3-library-public.jsx` (816 lines)
- `admin-mockups/design_files/sp3-library-public.fidelity.json`

- [ ] **Step 1: Read full mockup**

```bash
cat admin-mockups/design_files/sp3-library-public.jsx
```

Note: 25 component definitions inside the mockup. Identify TOP-LEVEL Page component (likely named `LibraryPublicHome` or similar).

- [ ] **Step 2: Identify reusable primitives via grep**

```bash
grep -rln "HeroGradient" apps/web/src/components --include="*.tsx" | head -5
grep -rln "MeepleCard" apps/web/src/components --include="*.tsx" | head -5
grep -rln "EntityChip" apps/web/src/components --include="*.tsx" | head -5
```

Confirm primitive paths for Step 3-7 imports.

- [ ] **Step 3: Document component scaffold structure**

Note in scratchpad (or this task's report):
- Top-level page component name
- Sub-sections inside (Hero / Stats / Featured / FAQ teaser / CTA / etc)
- Reuse primitives identified vs NEW primitives required (must be at least `CommunityStatsRow` + `FeaturedGamesCarousel` per spec § 4.2)
- Mock data shape needed (featured games array + community stats object)

### Task 1.2: Create CommunityStatsRow primitive

**Files:**
- Create: `apps/web/src/components/features/library-public/CommunityStatsRow.tsx`

- [ ] **Step 1: Scaffold component file**

Create `apps/web/src/components/features/library-public/CommunityStatsRow.tsx`:

```tsx
'use client';

import { cn } from '@/lib/utils';

export interface CommunityStats {
  totalGames: number;
  totalPlayers: number;
  totalSessions: number;
  totalCommunityContent: number;
}

interface CommunityStatsRowProps {
  stats: CommunityStats;
  className?: string;
}

/**
 * Community stats banner — 4-column grid (Games / Players / Sessions / Content).
 *
 * #2208 DS-17-10 sub-issue: NEW primitive for sp3-library-public route.
 * Mockup parity ref: `admin-mockups/design_files/sp3-library-public.jsx`.
 * Each cell: big number (font-display) + label (uppercase small).
 */
export function CommunityStatsRow({ stats, className }: CommunityStatsRowProps) {
  const items: Array<{ key: keyof CommunityStats; label: string }> = [
    { key: 'totalGames', label: 'Giochi' },
    { key: 'totalPlayers', label: 'Giocatori' },
    { key: 'totalSessions', label: 'Partite' },
    { key: 'totalCommunityContent', label: 'Contenuti community' },
  ];

  return (
    <div
      className={cn(
        'grid grid-cols-2 gap-6 rounded-2xl border border-border/50 bg-card/90 p-6 backdrop-blur-md sm:grid-cols-4',
        className
      )}
      role="region"
      aria-label="Statistiche community MeepleAI"
    >
      {items.map(item => (
        <div key={item.key} className="flex flex-col items-start gap-1">
          <span className="font-quicksand text-3xl font-bold tabular-nums text-foreground sm:text-4xl">
            {stats[item.key].toLocaleString('it-IT')}
          </span>
          <span className="text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
            {item.label}
          </span>
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Run lint + typecheck on file**

```bash
pnpm --filter @meepleai/web lint -- apps/web/src/components/features/library-public/CommunityStatsRow.tsx
pnpm --filter @meepleai/web typecheck
```

Expected: 0 errors.

### Task 1.3: Create FeaturedGamesCarousel primitive

**Files:**
- Create: `apps/web/src/components/features/library-public/FeaturedGamesCarousel.tsx`

- [ ] **Step 1: Scaffold component file**

Create `apps/web/src/components/features/library-public/FeaturedGamesCarousel.tsx`:

```tsx
'use client';

import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { cn } from '@/lib/utils';

export interface FeaturedGame {
  gameId: string;
  title: string;
  publisher?: string;
  coverUrl?: string;
  averageRating?: number;
  ratingCount?: number;
  playerCount?: { min: number; max: number };
}

interface FeaturedGamesCarouselProps {
  games: FeaturedGame[];
  className?: string;
}

/**
 * Featured games carousel — horizontal scroll list of 4-6 MeepleCard hero/grid.
 *
 * #2208 DS-17-10 sub-issue: NEW primitive for sp3-library-public route.
 * Mockup parity ref: `admin-mockups/design_files/sp3-library-public.jsx`.
 * Cards use entity=game variant grid (standard catalog presentation).
 *
 * NOTE: MeepleCard primitive has no `href` prop. Wrap each card in a
 * <Link> (Next.js client navigation) to make it clickable.
 */
export function FeaturedGamesCarousel({ games, className }: FeaturedGamesCarouselProps) {
  if (games.length === 0) {
    return (
      <p className={cn('text-sm italic text-muted-foreground', className)}>
        Nessun gioco in evidenza al momento.
      </p>
    );
  }

  return (
    <ul
      className={cn(
        'flex gap-4 overflow-x-auto pb-2 [scrollbar-width:thin] [&::-webkit-scrollbar]:h-2',
        className
      )}
      aria-label="Giochi in evidenza"
    >
      {games.map(game => (
        <li key={game.gameId} className="w-[260px] shrink-0 sm:w-[280px]">
          <Link href={`/shared-games/${game.gameId}`} className="block">
            <MeepleCard
              entity="game"
              variant="grid"
              title={game.title}
              subtitle={game.publisher}
              imageUrl={game.coverUrl}
              rating={game.averageRating}
              ratingMax={10}
            />
          </Link>
        </li>
      ))}
    </ul>
  );
}
```

NOTE: verify `MeepleCard` props match the canonical interface in `apps/web/src/components/ui/data-display/meeple-card/`. If `entity="game" variant="grid"` props differ from what's available, adapt to actual API. Reference: `docs/for-developers/frontend/meeple-card-design-tokens.md`.

- [ ] **Step 2: Run lint + typecheck**

```bash
pnpm --filter @meepleai/web lint -- apps/web/src/components/features/library-public/FeaturedGamesCarousel.tsx
pnpm --filter @meepleai/web typecheck
```

Expected: 0 errors. If typecheck fails on MeepleCard props, adapt to actual canonical props.

### Task 1.4: Create LibraryPublicHome client component

**Files:**
- Create: `apps/web/src/components/features/library-public/LibraryPublicHome.tsx`

- [ ] **Step 1: Read mockup to extract hero/sections structure**

```bash
grep -n "function\|const.*=>" admin-mockups/design_files/sp3-library-public.jsx | head -30
```

Note top-level page sections from mockup (e.g. Hero / Stats / Featured / WhatYouCanDo / CTA / Footer).

- [ ] **Step 2: Identify HeroGradient primitive path**

```bash
grep -rln "HeroGradient\|hero-gradient\|HeroSection" apps/web/src/components --include="*.tsx" | head -5
```

If a `HeroGradient` primitive exists, note its import path + props. If not, inline the hero section directly in LibraryPublicHome.

- [ ] **Step 3: Scaffold LibraryPublicHome component**

**Hero primitive note (I-2 reviewer finding)**: existing `HeroGradient` primitive lives at `apps/web/src/components/ui/hero-gradient/hero-gradient.tsx` and uses `Btn` from `@/components/ui/btn` with `primaryCta`/`secondaryCta` API. IF its API matches the mockup needs (title + subtitle + 2 CTAs), prefer `HeroGradient` reuse over inline custom hero. Read the primitive first via `cat apps/web/src/components/ui/hero-gradient/hero-gradient.tsx` to confirm API. If shape diverges substantially (mockup has 3+ CTAs, custom badge, etc.) fall back to inline hero as scaffolded below.

Create `apps/web/src/components/features/library-public/LibraryPublicHome.tsx`:

```tsx
'use client';

import Link from 'next/link';

import { CommunityStatsRow, type CommunityStats } from './CommunityStatsRow';
import {
  FeaturedGamesCarousel,
  type FeaturedGame,
} from './FeaturedGamesCarousel';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

interface LibraryPublicHomeProps {
  featured: FeaturedGame[];
  stats: CommunityStats;
}

/**
 * Public landing page for /library-public — community-facing showcase.
 *
 * #2208 DS-17-10 sub-issue (forward-refactor design_intent 0.6 conf per memory):
 * - Hero gradient + headline + CTA
 * - CommunityStatsRow (4-column grid)
 * - FeaturedGamesCarousel (4-6 MeepleCard hero)
 * - WhatYouCanDo section (3 bullets w/ icon)
 * - CTA strip "Crea il tuo account"
 *
 * Mockup parity: `admin-mockups/design_files/sp3-library-public.jsx` (816 LOC, 25 components — simplified scaffold here, full forward-refactor verification deferred to designer review tracking issue).
 */
export function LibraryPublicHome({ featured, stats }: LibraryPublicHomeProps) {
  return (
    <main className="flex flex-col gap-12 px-4 py-12 sm:px-8 lg:px-16">
      {/* HERO */}
      <section
        className={cn(
          'relative flex flex-col gap-6 overflow-hidden rounded-3xl border border-border/50 px-6 py-12 sm:px-12 sm:py-16',
          'bg-gradient-to-br from-entity-game/12 via-entity-toolkit/10 to-entity-session/12 backdrop-blur-md'
        )}
        aria-labelledby="library-public-hero-title"
      >
        <h1
          id="library-public-hero-title"
          className="font-quicksand text-4xl font-bold tracking-tight text-foreground sm:text-5xl lg:text-6xl"
        >
          Scopri la community board game di MeepleAI
        </h1>
        <p className="max-w-3xl text-base leading-relaxed text-muted-foreground sm:text-lg">
          Migliaia di giochi catalogati, regole spiegate dall&apos;AI, partite condivise, contenuti dalla community. Tutto in un posto.
        </p>
        <div className="flex flex-wrap items-center gap-3">
          <Button asChild>
            <Link href="/join">Inizia gratis</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/how-it-works">Come funziona</Link>
          </Button>
        </div>
      </section>

      {/* STATS */}
      <section aria-labelledby="library-public-stats-title">
        <h2 id="library-public-stats-title" className="sr-only">
          Statistiche community
        </h2>
        <CommunityStatsRow stats={stats} />
      </section>

      {/* FEATURED */}
      <section aria-labelledby="library-public-featured-title" className="flex flex-col gap-4">
        <h2
          id="library-public-featured-title"
          className="font-quicksand text-2xl font-semibold tracking-tight text-foreground sm:text-3xl"
        >
          Giochi in evidenza
        </h2>
        <FeaturedGamesCarousel games={featured} />
      </section>

      {/* WHAT YOU CAN DO (3 bullets) */}
      <section
        aria-labelledby="library-public-features-title"
        className="grid grid-cols-1 gap-4 sm:grid-cols-3"
      >
        <h2 id="library-public-features-title" className="sr-only">
          Cosa puoi fare con MeepleAI
        </h2>
        {[
          {
            title: 'Chiedi le regole',
            body: 'AI esperti rispondono a qualsiasi dubbio sul regolamento.',
          },
          {
            title: 'Organizza partite',
            body: 'Crea serate, invita amici, traccia punteggi automaticamente.',
          },
          {
            title: 'Condividi contenuti',
            body: 'Toolkit, agenti AI, guide pubblicabili per la community.',
          },
        ].map(item => (
          <article
            key={item.title}
            className="flex flex-col gap-2 rounded-2xl border border-border/50 bg-card/90 p-6 backdrop-blur-md"
          >
            <h3 className="font-quicksand text-lg font-semibold text-foreground">
              {item.title}
            </h3>
            <p className="text-sm leading-relaxed text-muted-foreground">{item.body}</p>
          </article>
        ))}
      </section>

      {/* CTA FOOTER */}
      <section
        className="flex flex-col items-center gap-4 rounded-2xl border border-border/50 bg-card/90 px-6 py-12 text-center backdrop-blur-md"
        aria-labelledby="library-public-cta-title"
      >
        <h2
          id="library-public-cta-title"
          className="font-quicksand text-3xl font-bold text-foreground sm:text-4xl"
        >
          Pronto a giocare con noi?
        </h2>
        <p className="max-w-2xl text-base text-muted-foreground">
          Crea il tuo account e accedi al catalogo, agli agenti AI, ai toolkit della community.
        </p>
        <Button asChild size="lg">
          <Link href="/join">Crea account gratis</Link>
        </Button>
      </section>
    </main>
  );
}
```

NOTE: The mockup is 816 LOC and `design_intent: forward-refactor` (0.6 conf per memory). This scaffold simplifies the mockup to 5 sections matching the core navigational intent. Full forward-refactor parity is deferred to the designer review tracking issue (DEC-4).

- [ ] **Step 4: Run lint + typecheck**

```bash
pnpm --filter @meepleai/web lint -- apps/web/src/components/features/library-public/LibraryPublicHome.tsx
pnpm --filter @meepleai/web typecheck
```

Expected: 0 errors.

If `Button asChild` prop fails typecheck: verify Button primitive supports `asChild` (Radix Slot pattern). If not, use `<Link><Button>...</Button></Link>` wrap or use Button onClick.

### Task 1.5: Create page.tsx server component wrapper

**Files:**
- Create: `apps/web/src/app/(public)/library-public/page.tsx`

- [ ] **Step 1: Scaffold server component**

Create `apps/web/src/app/(public)/library-public/page.tsx`:

```tsx
import type { Metadata } from 'next';

import { LibraryPublicHome } from '@/components/features/library-public/LibraryPublicHome';
import type { CommunityStats } from '@/components/features/library-public/CommunityStatsRow';
import type { FeaturedGame } from '@/components/features/library-public/FeaturedGamesCarousel';

export const metadata: Metadata = {
  title: 'Community MeepleAI — Scopri i giochi',
  description:
    'Scopri il catalogo board game della community MeepleAI. Toolkit, AI agents, partite, contenuti collaborativi.',
};

// NOTE: do NOT add @mockup JSDoc block manually here. The injector
// (`pnpm mockup-annotations:inject --apply`) runs in Task 4.1 Step 4
// after MOCKUPS_INDEX.md is updated in Task 1.7. The injector reads the
// index mapping and writes the full MOCKUP-ANNOTATION marker block.
// Manual injection would conflict with the idempotency check.
export default async function LibraryPublicPage() {
  // Stage 1: mock fixtures inline. Future iteration: replace with real
  // server-side fetch from backend (e.g. /api/v1/library-public/featured + /api/v1/community/stats).
  const featured: FeaturedGame[] = [
    { gameId: '00000000-0000-4000-8000-000000000001', title: 'Wingspan', publisher: 'Stonemaier Games', coverUrl: undefined, averageRating: 8.1 },
    { gameId: '00000000-0000-4000-8000-000000000002', title: 'Catan', publisher: 'Kosmos', coverUrl: undefined, averageRating: 7.2 },
    { gameId: '00000000-0000-4000-8000-000000000003', title: 'Terraforming Mars', publisher: 'FryxGames', coverUrl: undefined, averageRating: 8.4 },
    { gameId: '00000000-0000-4000-8000-000000000004', title: '7 Wonders', publisher: 'Repos Production', coverUrl: undefined, averageRating: 7.7 },
  ];
  const stats: CommunityStats = {
    totalGames: 1247,
    totalPlayers: 8520,
    totalSessions: 14392,
    totalCommunityContent: 318,
  };

  return <LibraryPublicHome featured={featured} stats={stats} />;
}
```

- [ ] **Step 2: Run lint + typecheck**

```bash
pnpm --filter @meepleai/web lint -- apps/web/src/app/\(public\)/library-public/page.tsx
pnpm --filter @meepleai/web typecheck
```

Expected: 0 errors.

### Task 1.6: Create page.stories.tsx + smoke test

**Files:**
- Create: `apps/web/src/app/(public)/library-public/page.stories.tsx`
- Create: `apps/web/src/components/features/library-public/__tests__/LibraryPublicHome.test.tsx`

- [ ] **Step 1: Create Storybook story file**

Create `apps/web/src/app/(public)/library-public/page.stories.tsx`:

```tsx
import type { Meta, StoryObj } from '@storybook/react';

import { LibraryPublicHome } from '@/components/features/library-public/LibraryPublicHome';
import type { CommunityStats } from '@/components/features/library-public/CommunityStatsRow';
import type { FeaturedGame } from '@/components/features/library-public/FeaturedGamesCarousel';

const meta: Meta<typeof LibraryPublicHome> = {
  title: 'Public / sp3-library-public',
  component: LibraryPublicHome,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2208 DS-17-10 sub-issue. Mockup parity: `admin-mockups/design_files/sp3-library-public.jsx` (816 LOC, forward-refactor 0.6 conf). Full designer review deferred per DEC-4 tracking issue.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof LibraryPublicHome>;

const FEATURED_FIXTURE: FeaturedGame[] = [
  { gameId: 'fixture-wingspan', title: 'Wingspan', publisher: 'Stonemaier Games', averageRating: 8.1 },
  { gameId: 'fixture-catan', title: 'Catan', publisher: 'Kosmos', averageRating: 7.2 },
  { gameId: 'fixture-terra', title: 'Terraforming Mars', publisher: 'FryxGames', averageRating: 8.4 },
  { gameId: 'fixture-7w', title: '7 Wonders', publisher: 'Repos Production', averageRating: 7.7 },
];

const STATS_FIXTURE: CommunityStats = {
  totalGames: 1247,
  totalPlayers: 8520,
  totalSessions: 14392,
  totalCommunityContent: 318,
};

export const Default: Story = {
  args: {
    featured: FEATURED_FIXTURE,
    stats: STATS_FIXTURE,
  },
};

export const EmptyFeatured: Story = {
  args: {
    featured: [],
    stats: STATS_FIXTURE,
  },
};

export const ZeroStats: Story = {
  args: {
    featured: FEATURED_FIXTURE,
    stats: { totalGames: 0, totalPlayers: 0, totalSessions: 0, totalCommunityContent: 0 },
  },
};
```

- [ ] **Step 2: Create smoke test**

Create `apps/web/src/components/features/library-public/__tests__/LibraryPublicHome.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { LibraryPublicHome } from '../LibraryPublicHome';
import type { CommunityStats } from '../CommunityStatsRow';
import type { FeaturedGame } from '../FeaturedGamesCarousel';

const FEATURED: FeaturedGame[] = [
  { gameId: 'g-1', title: 'Wingspan', publisher: 'Stonemaier', averageRating: 8.1 },
];
const STATS: CommunityStats = { totalGames: 100, totalPlayers: 200, totalSessions: 300, totalCommunityContent: 50 };

describe('LibraryPublicHome — DS-17-10 smoke', () => {
  it('renders the hero headline + sub copy', () => {
    render(<LibraryPublicHome featured={FEATURED} stats={STATS} />);
    expect(
      screen.getByRole('heading', { name: /scopri la community board game/i })
    ).toBeInTheDocument();
  });

  it('renders stats numbers from props', () => {
    render(<LibraryPublicHome featured={FEATURED} stats={STATS} />);
    // Stats are formatted with it-IT locale; 100 → "100", 200 → "200" (no thousands separator under 1k)
    expect(screen.getByText('100')).toBeInTheDocument();
    expect(screen.getByText('200')).toBeInTheDocument();
    expect(screen.getByText('300')).toBeInTheDocument();
    expect(screen.getByText('50')).toBeInTheDocument();
  });

  it('renders featured games + empty state copy when list is empty', () => {
    const { rerender } = render(<LibraryPublicHome featured={FEATURED} stats={STATS} />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    rerender(<LibraryPublicHome featured={[]} stats={STATS} />);
    expect(screen.getByText(/nessun gioco in evidenza/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run smoke test**

```bash
pnpm --filter @meepleai/web test LibraryPublicHome --run
```

Expected: 3/3 tests pass.

If MeepleCard render fails in test (it's a complex composite component): the test might need to mock `@/components/ui/data-display/meeple-card`. Add at top of test file:

```tsx
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: ({ title }: { title: string }) => <div>{title}</div>,
}));
```

Then re-run.

- [ ] **Step 4: Commit Stage 1 components**

```bash
git add apps/web/src/app/\(public\)/library-public/ \
        apps/web/src/components/features/library-public/

git commit -m "$(cat <<'EOF'
feat(library-public): #2208 sp3-library-public route + components

DS-17-10 Stage 1: NEW /library-public route (server component) + LibraryPublicHome client component + 2 NEW primitives (CommunityStatsRow + FeaturedGamesCarousel) + Storybook story + smoke test.

5 sections in LibraryPublicHome (mockup parity simplified per DEC-Memory-1 + DEC-4):
1. Hero gradient + headline + 2 CTAs (Inizia gratis + Come funziona)
2. CommunityStatsRow (4-column grid: Giochi / Giocatori / Partite / Contenuti)
3. FeaturedGamesCarousel (4-6 MeepleCard hero variant horizontal scroll)
4. WhatYouCanDo (3-bullet grid: Chiedi le regole / Organizza / Condividi)
5. CTA footer (Crea account gratis)

Storybook: 3 stories (Default + EmptyFeatured + ZeroStats).
Test: 3 smoke (hero copy + stats numbers + featured + empty fallback).

Mockup forward-refactor 0.6 conf — full parity deferred a designer review tracking issue (DEC-4).

Refs: #2208, mockup sp3-library-public.jsx (816 LOC), spec docs/superpowers/specs/2026-06-11-ds-17-10-sp3-cluster-design.md.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

### Task 1.7: Open designer review tracking issue + update fidelity.json

**Files:**
- Modify: `admin-mockups/design_files/sp3-library-public.fidelity.json`
- Modify: `admin-mockups/MOCKUPS_INDEX.md` (add row for /library-public)
- Side effect: gh issue create

- [ ] **Step 1: Open tracking issue**

```bash
gh issue create --title "Designer review sp3-library-public forward-refactor (DS-17-10 #2208 follow-up)" \
  --label "area/frontend,design-review,mockup-drift" \
  --body "$(cat <<'EOF'
Follow-up tracking issue per DS-17-10 sub-issue #2208 DEC-4.

**Context**: sp3-library-public mockup è `design_intent: forward-refactor` (0.6 conf per memory note `ds-17-10-sp3-deferred-decisions`). DS-17-10 ha shipped una versione semplificata (5 sezioni core: Hero + Stats + Featured + WhatYouCanDo + CTA) ma il mockup originale è 816 LOC con 25 component definitions.

**Scope**:
- [ ] Designer review LibraryPublicHome vs mockup 816-line full forward-refactor design
- [ ] Identify gaps + iteration plan (additional sections, animations, content)
- [ ] Update fidelity.json `design_intent` se design lockato (`current`) vs ulteriore iterazione (`forward-refactor` retained)
- [ ] Close OR re-design tracking iteration

**Refs**:
- Spec: docs/superpowers/specs/2026-06-11-ds-17-10-sp3-cluster-design.md
- Implementation: PR #<DS-17-10 PR_NUM filled at Stage 5> (sub-issue #2208)
- Memory: ds-17-10-sp3-deferred-decisions
EOF
)"
```

Record the NEW tracking issue number (output URL contains issue #).

- [ ] **Step 2: Update sp3-library-public.fidelity.json with tracking issue**

Read `admin-mockups/design_files/sp3-library-public.fidelity.json`. Update `obsolete_tracking_issue` field:

```json
{
  "_comment": "Generated by Phase B audit (sub-issue #2127). See audits/2026-06-10-mockup-design-intent-audit.json for source.",
  "mockup": {
    "source": "admin-mockups/design_files/sp3-library-public.html",
    "states": ["default"]
  },
  "acceptance": {
    "visual_diff_max_px": 5,
    "color_delta_e_max": 3,
    "tokens_used": "canonical_only",
    "legacy_token_names_forbidden": true,
    "states_covered": ["default"],
    "a11y_axe": "AA",
    "a11y_violations_max": 0,
    "responsive_breakpoints": [375, 768, 1024, 1440],
    "designer_approved_by": "",
    "designer_approved_on": "",
    "story_path": "apps/web/src/app/(public)/library-public/page.stories.tsx",
    "fixtures_path": "",
    "design_intent": "forward-refactor",
    "viewports": ["desktop"],
    "obsolete_tracking_issue": "<TRACKING_ISSUE_NUM from Step 1>"
  }
}
```

Replace `<TRACKING_ISSUE_NUM from Step 1>` with the actual issue number returned by `gh issue create`. Also set `story_path` to the new stories file.

- [ ] **Step 3: Update MOCKUPS_INDEX.md**

Add row for sp3-library-public → /library-public route. Read current file structure first:

```bash
grep -n "sp3-library-public\|sp3-shared-games" admin-mockups/MOCKUPS_INDEX.md | head -5
```

Find the sp3 cluster section + add row (mirror sibling entries' format):

```
| sp3-library-public | /library-public | apps/web/src/app/(public)/library-public/page.tsx | DS-17-10 #2208 forward-refactor designer review tracking #<NUM> |
```

Exact format depends on existing table structure. Adapt accordingly.

- [ ] **Step 4: Run lint:fidelity + mockup-annotations:audit**

```bash
pnpm --filter @meepleai/web lint:fidelity
pnpm --filter @meepleai/web mockup-annotations:audit
```

Expected: both pass. If `mockup-annotations:audit` fails per sp3-library-public mapping missing in MOCKUPS_INDEX.md: re-check Step 3 row format.

- [ ] **Step 5: Commit fidelity + index update**

```bash
git add admin-mockups/design_files/sp3-library-public.fidelity.json \
        admin-mockups/MOCKUPS_INDEX.md

git commit -m "$(cat <<'EOF'
chore(mockups): #2208 DS-17-10 sp3-library-public fidelity + index

Update sp3-library-public.fidelity.json:
- story_path → apps/web/src/app/(public)/library-public/page.stories.tsx
- obsolete_tracking_issue → #<TRACKING_NUM> (designer review forward-refactor follow-up per DEC-4)

Update MOCKUPS_INDEX.md: add row mapping sp3-library-public mockup → /library-public route.

Refs: #2208, follow-up tracking #<TRACKING_NUM>.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Stage 2 — AI dispatch 7 standard stems story migration (~4-5h sequential)

### Pattern (applied to each of Tasks 2.1–2.7)

Each task dispatches ONE implementer subagent for ONE stem. Subagent reads mockup, scaffolds Storybook story, commits. Controller (you) then dispatches spec compliance reviewer + code quality reviewer per the subagent-driven-development skill.

**Common dispatch context** (re-used in every task):

```
You are implementing story migration for sp3 mockup stem `<STEM_NAME>`.

Pattern: DS-17-11 sp6-7-nano (PR #2173 sess.46o predecessor) story migration pattern.

Working directory: D:\Repositories\meepleai-monorepo-frontend

Constraints:
- DEC-Memory-2: BGG cleanup ALREADY done in Stage 0 (commit before this task). Don't re-introduce BGG references.
- DEC-Memory-3 (sp3-shared-game-detail only): story renders POST-rebuild GameDetailDesktop via MSW handlers.
- DEC-3: baseline capture in Stage 3 (don't capture in this task).
- DEC viewports: desktop only.
- DEC-5: 1 Agent per stem isolated scope (don't read other stems).

Read these files first:
1. Mockup canonical: admin-mockups/design_files/<STEM_NAME>.jsx
2. Fidelity: admin-mockups/design_files/<STEM_NAME>.fidelity.json
3. Existing route page.tsx at <ROUTE_PATH>/page.tsx (if exists — for component import reference)
4. Sibling story pattern (if exists): apps/web/src/app/(public)/<SIBLING_ROUTE>/page.stories.tsx

Write the story file at the target path. Per mockup multi-frame pattern P239:
- export default meta (title: 'Public / <STEM_NAME>', component: <Route page component>, parameters: layout/nextjs/viewport)
- One Story per Frame in the mockup (Frame matrix argTypes if applicable)
- mockup label name mirror (FrameNN_ShortName per Phase 2.5 pattern)
- MSW handlers if route has data fetch

Run lint + typecheck on the new story file. Commit:
git add <story file path>
git commit -m "feat(stories): #2208 <STEM_NAME> Storybook migration"

Report: Status DONE | DONE_WITH_CONCERNS | BLOCKED | NEEDS_CONTEXT + files changed + lint + typecheck results + commit SHA.
```

### Task 2.1: sp3-shared-games story migration

**Files:**
- Create: `apps/web/src/app/(public)/shared-games/page.stories.tsx`
- Read-only: `admin-mockups/design_files/sp3-shared-games.{jsx,fidelity.json}`, existing route `page.tsx`

- [ ] **Step 1: Dispatch implementer subagent for sp3-shared-games**

Use the "Common dispatch context" template above, substituting `<STEM_NAME> = sp3-shared-games`, `<ROUTE_PATH> = apps/web/src/app/(public)/shared-games`.

Notes specific to this stem:
- Route already exists (`page.tsx` + `page-client.tsx`). Story renders the existing client component.
- MSW handlers needed for `/api/v1/shared-games?limit=N` list response.
- Story Frame matrix: Default state + EmptyList + LoadingState + ErrorState (if mockup shows these).

- [ ] **Step 2: Dispatch spec reviewer**

Per subagent-driven-development skill. Verify:
- Story file exists at correct path
- Title format matches `'Public / sp3-shared-games'`
- Stories render existing route component (no duplicate component logic)
- MSW handlers present if data fetch needed
- No BGG references (Stage 0 cleanup respected)

- [ ] **Step 3: Dispatch code quality reviewer**

Per subagent-driven-development skill. Verify pattern adherence to DS-17-11 predecessor + token compliance.

### Task 2.2: sp3-shared-game-detail story migration (POST-rebuild integration)

**Files:**
- Create: `apps/web/src/app/(public)/shared-games/[id]/page.stories.tsx`
- Read-only: `admin-mockups/design_files/sp3-shared-game-detail.{jsx,fidelity.json}`, `apps/web/src/components/game-detail/GameDetailDesktop.tsx`

- [ ] **Step 1: Dispatch implementer subagent for sp3-shared-game-detail**

**SPECIAL CASE**: Story renders POST-#2096 rebuild `GameDetailDesktop` component to verify M1-M7 deliverables wire correctly.

Use Common dispatch context with these additions:
- `<STEM_NAME> = sp3-shared-game-detail`
- `<ROUTE_PATH> = apps/web/src/app/(public)/shared-games/[id]`
- DEC-Memory-3: import `GameDetailDesktop` from `@/components/game-detail/GameDetailDesktop`
- Story Frames: Default (Info tab) + ToolboxTab + HouseRulesTab + AgentChatTab + PartiteTab (per tab ID enumeration in M2 #2102)
- MSW handlers needed:
  - `/api/v1/library/[gameId]` returns LibraryGameDetail fixture (game with title/publisher/year/designers/categories/description)
  - `/api/v1/games/[gameId]/session-contributors` returns Contributor[] (M5 #2036 deliverable)

Sample fixture shape (refer to `apps/web/src/hooks/queries/useLibrary.ts` for LibraryGameDetail interface):

```tsx
const GAME_FIXTURE = {
  libraryEntryId: 'entry-1',
  userId: 'user-1',
  gameId: 'sp3-detail-fixture',
  gameTitle: 'Wingspan',
  gamePublisher: 'Stonemaier Games',
  gameYearPublished: 2019,
  description: 'A competitive bird-collection engine-building game.',
  designers: [{ id: 'd-1', name: 'Elizabeth Hargrave' }],
  categories: [{ id: 'c-1', name: 'Strategy', slug: 'strategy' }],
  mechanics: [{ id: 'm-1', name: 'Engine Building', slug: 'engine-building' }],
  minPlayers: 1,
  maxPlayers: 5,
  playingTimeMinutes: 70,
  complexityRating: 2.4,
  averageRating: 8.1,
  addedAt: '2025-01-01T00:00:00Z',
  agentCount: 2,
  chatThreadCount: 0,
  timesPlayed: 5,
  // ... + remaining fields per LibraryGameDetail interface
};
```

- [ ] **Step 2: Dispatch spec reviewer**

Verify GameDetailDesktop import path + MSW handlers cover all data fetches + 5 tab Frames present.

- [ ] **Step 3: Dispatch code quality reviewer**

Verify MSW fixture shape matches LibraryGameDetail interface + no fixture drift.

### Task 2.3: sp3-legal story migration

**Files:**
- Create: `apps/web/src/app/(public)/terms/page.stories.tsx`

- [ ] **Step 1: Dispatch implementer subagent**

`<STEM_NAME> = sp3-legal`, `<ROUTE_PATH> = apps/web/src/app/(public)/terms`. **Multi-route note**: `sp3-legal.html` is mapped in `MOCKUPS_INDEX.md` to 4 routes (`/privacy` + `/terms` + `/cookies` + `/cookie-settings`). Canonical story target is `/terms` (most representative). Story title should note "covers shared sp3-legal.html mockup serving /privacy /terms /cookies /cookie-settings routes". Static content, no MSW handlers.

- [ ] **Step 2: Dispatch spec reviewer**

- [ ] **Step 3: Dispatch code quality reviewer**

### Task 2.4: sp3-join story migration

**Files:**
- Create: `apps/web/src/app/(public)/join/page.stories.tsx`

- [ ] **Step 1: Dispatch implementer subagent**

`<STEM_NAME> = sp3-join`, `<ROUTE_PATH> = apps/web/src/app/(public)/join`. Multi-route mockup (with /event /session variants in existing route subdir); canonical story = /join base.

- [ ] **Step 2: Dispatch spec reviewer**

- [ ] **Step 3: Dispatch code quality reviewer**

### Task 2.5: sp3-how-it-works story migration

**Files:**
- Create: `apps/web/src/app/(public)/how-it-works/page.stories.tsx`

- [ ] **Step 1: Dispatch implementer subagent**

`<STEM_NAME> = sp3-how-it-works`, `<ROUTE_PATH> = apps/web/src/app/(public)/how-it-works`. POST Stage 0 BGG cleanup — verify no BGG references in resulting story.

- [ ] **Step 2: Dispatch spec reviewer**

- [ ] **Step 3: Dispatch code quality reviewer**

### Task 2.6: sp3-faq-enhanced story migration

**Files:**
- Create: `apps/web/src/app/(public)/faq/page.stories.tsx`

- [ ] **Step 1: Dispatch implementer subagent**

`<STEM_NAME> = sp3-faq-enhanced`, `<ROUTE_PATH> = apps/web/src/app/(public)/faq`. Multi-route (`/faq` + `/games/[id]/faqs`); canonical = `/faq`. POST Stage 0 BGG cleanup.

- [ ] **Step 2: Dispatch spec reviewer**

- [ ] **Step 3: Dispatch code quality reviewer**

### Task 2.7: sp3-accept-invite story migration

**Files:**
- Create: `apps/web/src/app/(public)/accept-invite/page.stories.tsx`

- [ ] **Step 1: Dispatch implementer subagent**

`<STEM_NAME> = sp3-accept-invite`, `<ROUTE_PATH> = apps/web/src/app/(public)/accept-invite`. Multi-route (with `/join/[inviteToken]` variant); canonical = `/accept-invite`. Static fallback if no token.

- [ ] **Step 2: Dispatch spec reviewer**

- [ ] **Step 3: Dispatch code quality reviewer**

---

## Stage 3 — Baseline capture inline (~1h)

### Task 3.1: Capture 8 PNG Desktop 1440x900

**Files:**
- Create: 8 PNG snapshots via Storybook test runner

- [ ] **Step 1: Verify all 8 stories present**

```bash
ls -la apps/web/src/app/\(public\)/library-public/page.stories.tsx \
       apps/web/src/app/\(public\)/shared-games/page.stories.tsx \
       apps/web/src/app/\(public\)/shared-games/\[id\]/page.stories.tsx \
       apps/web/src/app/\(public\)/legal/page.stories.tsx \
       apps/web/src/app/\(public\)/join/page.stories.tsx \
       apps/web/src/app/\(public\)/how-it-works/page.stories.tsx \
       apps/web/src/app/\(public\)/faq/page.stories.tsx \
       apps/web/src/app/\(public\)/accept-invite/page.stories.tsx
```

Expected: all 8 files exist.

- [ ] **Step 2: Run Storybook snapshot update (Playwright-based)**

Canonical command (verified against `apps/web/package.json`):
```bash
pnpm --filter @meepleai/web test:storybook:snapshots:update
```

This invokes Playwright with `playwright.storybook.config.ts`. Requires Storybook to be running on the configured `baseURL` (typically `localhost:6006`). Start in separate terminal first if needed:
```bash
pnpm --filter @meepleai/web storybook
```

Fallback if Playwright config not present: per-story manual capture via Chrome MCP (EPIC #2096 P4.3 pattern).

- [ ] **Step 3: Verify 8 PNG baselines generated**

```bash
find apps/web -name "*.png" -newer audits/2026-05-12-token-violations.md -path "*sp3*" 2>/dev/null | head -20
```

Expected: at least 8 PNG files generated (1 per stem, possibly more if multi-Frame).

- [ ] **Step 4: Commit baselines**

```bash
git add apps/web  # baseline PNG paths vary by Storybook config

git commit -m "$(cat <<'EOF'
chore(stories): #2208 DS-17-10 sp3 cluster baselines (8 PNG Desktop 1440x900)

DEC-3: Visual gate active immediately — baseline capture inline per stem (NOT deferred Phase D batch).

8 stems baseline:
- sp3-library-public
- sp3-shared-games
- sp3-shared-game-detail (POST-rebuild integration)
- sp3-legal
- sp3-join
- sp3-how-it-works
- sp3-faq-enhanced
- sp3-accept-invite

Risk R5: rebaseline may be needed post-PR designer feedback (accepted vs P247 defer).

Refs: #2208.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Stage 4 — Quality gates (~30 min)

### Task 4.1: Full quality gate sweep

**Files:**
- Read-only verification only

- [ ] **Step 1: Run all tests**

```bash
pnpm --filter @meepleai/web test --run
```

Expected: 0 regressions vs main-dev baseline.

If LibraryPublicHome smoke test fails: re-check Task 1.6 Step 3 (mock MeepleCard if needed).

- [ ] **Step 2: Run lint suite**

```bash
pnpm --filter @meepleai/web lint
pnpm --filter @meepleai/web lint:tokens
pnpm --filter @meepleai/web lint:tokens:mockups --strict --max-baseline 1500
pnpm --filter @meepleai/web lint:bgg
pnpm --filter @meepleai/web lint:bgg-mockups
pnpm --filter @meepleai/web lint:fidelity
```

Expected: 0 errors / 0 violations on each.

If `lint:tokens` flags new tokens used in LibraryPublicHome: verify all CSS classes used are semantic tokens (no hardcoded colors). The plan uses:
- Layout: `flex`, `gap-*`, `grid`, `px-*`, `py-*`, `rounded-2xl`, `border` (Tailwind defaults — OK)
- Colors: `border-border/50`, `bg-card/90`, `text-foreground`, `text-muted-foreground`, `bg-entity-*/12`, `bg-entity-*/8` (all semantic — OK)
- Typography: `font-quicksand`, `font-bold`, `text-*`, `tabular-nums`, `tracking-*` (Tailwind — OK)

If `lint:bgg-mockups` flags violations: re-verify Stage 0 cleanup (Task 0.1) covered all 3 files.

- [ ] **Step 3: Run typecheck**

```bash
pnpm --filter @meepleai/web typecheck
```

Expected: 0 errors.

- [ ] **Step 4: Run annotation audit gate**

```bash
pnpm --filter @meepleai/web mockup-annotations:audit --denominator mappable --threshold 80
```

Expected: ≥80% coverage. If fails per missing sp3-library-public mapping: verify Task 1.7 Step 3 added MOCKUPS_INDEX.md row correctly.

If MOCKUPS_INDEX.md row is correct but audit still fails: run injector:
```bash
pnpm --filter @meepleai/web mockup-annotations:inject --apply
```

This injects the `@mockup` JSDoc block in page.tsx files. Then re-run audit.

- [ ] **Step 5: Commit any annotation fix if needed**

If Step 4 had to run inject:
```bash
git add apps/web/src/app/\(public\)/library-public/page.tsx
git commit -m "chore(annotations): #2208 inject @mockup JSDoc per sp3-library-public route"
```

---

## Stage 5 — Merge + closure (~30 min)

### Task 5.1: Push branch + open PR

**Files:**
- Side effect: gh pr create

- [ ] **Step 1: Verify all commits present**

```bash
git log --oneline main-dev..HEAD
```

Expected: multiple commits including:
- spec doc (`67455f369` + `ad8014a66`)
- plan doc (this file commit)
- Stage 0 BGG cleanup
- Stage 1 library-public route + components + fidelity + index
- Stage 2 7 story commits (1 per stem)
- Stage 3 baselines

- [ ] **Step 2: Push branch (pre-push hook ~6-10 min `pnpm build`)**

```bash
git push -u origin feature/issue-2208-ds-17-10-sp3-cluster
```

Pre-push hook runs full `pnpm build` (~10 min per CLAUDE.md). Wait for completion. If hook fails ENOENT on `.next/static/*`: `rm -rf apps/web/.next && git push -u origin feature/issue-2208-ds-17-10-sp3-cluster` retry.

- [ ] **Step 3: Open PR**

```bash
gh pr create --base main-dev \
  --title "feat(stories): #2208 DS-17-10 sp3 cluster — 8 stems ship + route-create" \
  --body "$(cat <<'EOF'
## Summary

Closes DS-17 Phase C-1 step 3/3 (post DS-17 auth #2160 + DS-17-11 sp6-7-nano #2166). Unblocks Phase C-2 (SP4 core 106 mockup + sp4-sessions 50 mockup).

8 sp3 cluster mockups shipped (NO skip per DEC-Memory-1):
1. **sp3-library-public** — NEW route + LibraryPublicHome component + 2 NEW primitives (CommunityStatsRow + FeaturedGamesCarousel)
2. sp3-shared-games — story migration
3. **sp3-shared-game-detail** — POST-#2096 rebuild integration (renders `GameDetailDesktop` POST-PR #2207)
4. sp3-legal — story migration
5. sp3-join — story migration
6. sp3-how-it-works — story migration POST Stage 0 BGG cleanup
7. sp3-faq-enhanced — story migration POST Stage 0 BGG cleanup
8. sp3-accept-invite — story migration

## ⏳ DESIGNER REVIEW (Opzione C precedent)

Per DEC-6 pattern + DS-17-11 precedent: skip designer review pre-merge (user è designer). Designer review può essere applicato post-merge se needed.

Special case: sp3-library-public forward-refactor → tracking issue #<TRACKING_NUM> OPENED per future iteration.

## Spec + plan

- Design spec: `docs/superpowers/specs/2026-06-11-ds-17-10-sp3-cluster-design.md`
- Implementation plan: `docs/superpowers/plans/2026-06-11-ds-17-10-sp3-cluster-plan.md`
- 8 DEC user-locked (3 memory preserved + 5 new sess.46p, see spec § 2)

## Commits

Approximate commit list (squashed at merge):
- `67455f369` docs(specs) — design spec initial
- `ad8014a66` docs(specs) — self-review fix
- (plan commit) — implementation plan
- (Stage 0) chore(mockups) — BGG cleanup 3 JSX twins
- (Stage 1) feat(library-public) — route + 4 component files + smoke test
- (Stage 1) chore(mockups) — fidelity + index update
- (Stage 2) 7 × feat(stories) — sp3 cluster story migration
- (Stage 3) chore(stories) — 8 PNG baselines

## Test plan

- [ ] `pnpm test LibraryPublicHome` → smoke pass
- [ ] `pnpm test` (full suite) → 0 regressions
- [ ] `pnpm lint` → 0 errors
- [ ] `pnpm lint:tokens` → 0 violations
- [ ] `pnpm lint:tokens:mockups --strict --max-baseline 1500` → no regression
- [ ] `pnpm lint:bgg` + `lint:bgg-mockups` → clean
- [ ] `pnpm lint:fidelity` → 0 violations
- [ ] `pnpm mockup-annotations:audit --denominator mappable --threshold 80` → ≥80%
- [ ] `pnpm typecheck` → 0 errors
- [ ] Backend build clean (pre-push hook)
- [ ] 8 baseline PNG captured
- [ ] Designer review SKIP per user waiver (Opzione C precedent)

## Refs

- Closes #2208 (sub-issue)
- Closes DS-17 Phase C-1 step 3/3 (umbrella #2063 progress)
- Unblocks Phase C-2 (SP4 core 106 + sp4-sessions 50 mockup)
- Predecessor sub-issues: #2160 + #2166
- EPIC #2096 closure trigger (PR #2207 `b98e4328b`)
- BGG ToS: #1903 #2123 #2151
- Designer review follow-up tracking: #<TRACKING_NUM>

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Replace `<TRACKING_NUM>` with the actual issue number from Task 1.7 Step 1.

### Task 5.2: Admin-squash merge

- [ ] **Step 1: Verify PR ready**

```bash
gh pr checks <PR_NUM>
```

Expected: most checks pass (some may pend at merge time).

- [ ] **Step 2: Admin-squash merge with branch delete**

```bash
gh pr merge <PR_NUM> --admin --squash --delete-branch
```

P145 38a volta. Expected: PR merged on main-dev, branch deleted.

- [ ] **Step 3: Pull main-dev locally**

```bash
git checkout main-dev
git pull --ff-only
```

If pull fails due to local uncommitted changes (e.g. lint regen audits): `git stash && git pull --ff-only && git stash pop`.

### Task 5.3: Close sub-issue + EPIC progress

- [ ] **Step 1: Close #2208 with AC evidence**

```bash
gh issue close 2208 --reason completed --comment "$(cat <<'EOF'
🎉 Shipped via PR #<PR_NUM> (`<merge_commit_sha>` admin-squash P145 38a volta).

## AC closure evidence

### Stage 0 BGG cleanup ✅
- ✅ 3 JSX twins edited (how-it-works lines 231/461 + shared-game-detail lines 67-68 + faq-enhanced line 51)
- ✅ `pnpm lint:bgg-mockups` clean post-cleanup
- ✅ #2151 comment con 3 nuovi findings appended

### Stage 1 sp3-library-public ✅
- ✅ NEW route `(public)/library-public/page.tsx` server component
- ✅ NEW `LibraryPublicHome` client component (5 sections: Hero + Stats + Featured + WhatYouCanDo + CTA)
- ✅ NEW `CommunityStatsRow` + `FeaturedGamesCarousel` primitives
- ✅ Smoke test 3/3 pass
- ✅ Storybook 3 stories (Default + EmptyFeatured + ZeroStats)
- ✅ `sp3-library-public.fidelity.json` updated with designer review tracking #<TRACKING_NUM>
- ✅ Tracking issue OPENED #<TRACKING_NUM>

### Stage 2 7 standard stems ✅
- ✅ 7 stories created via Agent dispatch sequential (1 per stem per DEC-5)
- ✅ sp3-shared-game-detail story renders POST-rebuild `GameDetailDesktop` (M1-M7 wire verified)

### Stage 3 baseline ✅
- ✅ 8 PNG baseline Desktop 1440x900 captured

### Stage 4 quality gates ✅
- ✅ `pnpm test` 0 regression
- ✅ `pnpm lint` + `lint:tokens` + `lint:tokens:mockups` + `lint:bgg` + `lint:bgg-mockups` + `lint:fidelity` 0 violations
- ✅ `pnpm mockup-annotations:audit --denominator mappable --threshold 80` ≥80%
- ✅ `pnpm typecheck` 0 errors

### Stage 5 closure ✅
- ✅ Admin-squash merge + branch deleted
- ✅ EPIC #2063 (DS-17 umbrella) progress Phase C-1 step 3/3 complete

DS-17 Phase C-1 closure: 3/3 step shipped (auth #2160 + sp6-7-nano #2166 + sp3 #2208).
Phase C-2 SP4 core (106) + sp4-sessions (50) NEXT.
EOF
)"
```

- [ ] **Step 2: Update EPIC #2063 (DS-17 umbrella) progress**

```bash
gh issue comment 2063 --body "$(cat <<'EOF'
🎉 **DS-17 Phase C-1 step 3/3 SHIPPED via PR #<PR_NUM>** (sub-issue #2208 sess.46p 2026-06-11).

## Phase C-1 closure

| Step | Sub-issue | PR | Status |
|---|---|---|---|
| 1 — auth cluster | #2160 | #2164 | ✅ MERGED sess.46n |
| 2 — sp6-7-nano cluster | #2166 | #2173 | ✅ MERGED sess.46o |
| **3 — sp3 cluster** | **#2208** | **#<PR_NUM>** | **✅ MERGED sess.46p** |

Phase C-1 **CLOSED**. 3/3 step shipped.

## Phase C-2 preview

NEXT: SP4 core 106 mockup + sp4-sessions 50 mockup migration. New brainstorming required per cluster split strategy.

DS-17 umbrella progress: Phase C-1 ✅ | Phase C-2 🚧 | Phase D ⏳ | Phase E ⏳.
EOF
)"
```

### Task 5.4: Memory entry + Phase C-2 preview

**Files:**
- Create: `~/.claude/projects/D--Repositories-meepleai-monorepo-frontend/memory/ds-17-10-sp3-cluster-shipped.md`
- Modify: `~/.claude/projects/D--Repositories-meepleai-monorepo-frontend/memory/MEMORY.md`

- [ ] **Step 1: Write memory entry**

Create `C:\Users\Utente\.claude\projects\D--Repositories-meepleai-monorepo-frontend\memory\ds-17-10-sp3-cluster-shipped.md`:

```markdown
---
name: ds-17-10-sp3-cluster-shipped
description: "DS-17-10 sp3 cluster migration — 8 stems shipped + route-create sess.46p — Phase C-1 step 3/3 closure"
metadata:
  node_type: memory
  type: project
---

# DS-17-10 sp3 cluster shipped sess.46p

DS-17 Phase C-1 step 3/3 **CLOSED** sess.46p 2026-06-11 via PR #<PR_NUM> (`<merge_sha>` admin-squash P145 38a volta). Sub-issue #2208 chiusura cluster sp3 (8 mockup ship + new route + 2 new primitives).

## Shipped scope (8 stems)

| Stem | Action | Notes |
|---|---|---|
| sp3-library-public | Route-create + ship | NEW (public)/library-public/page.tsx + LibraryPublicHome + 2 NEW primitives. Designer review tracking #<TRACKING_NUM>. |
| sp3-shared-games | Ship | Story migration standard pattern |
| sp3-shared-game-detail | Ship POST-#2096 | Renders POST-rebuild GameDetailDesktop verifying M1-M7 deliverables wire |
| sp3-legal | Ship | Static |
| sp3-join | Ship | Multi-route canonical /join |
| sp3-how-it-works | Ship POST BGG cleanup | Stage 0 prep edits |
| sp3-faq-enhanced | Ship POST BGG cleanup | Stage 0 prep edits |
| sp3-accept-invite | Ship | Multi-route canonical /accept-invite |

## Effort recap

~13-14h cumulative (~3.5gg) sess.46p:
- Stage 0 BGG cleanup ~30 min
- Stage 1 sp3-library-public ~6h
- Stage 2 7 stems sequential ~4-5h
- Stage 3 baselines 1h
- Stage 4 quality gates 30 min
- Stage 5 merge + closure 30 min

## Pipeline superpowers applicata

Full-chain 6a volta consecutiva:
1. `superpowers:brainstorming` 6 clarifying questions → 8 DEC (3 memory + 5 new)
2. `superpowers:writing-plans` ~20 task TDD-style plan 5-stage
3. `superpowers:subagent-driven-development` 7 Agent dispatch (Stage 2 sequential per DEC-5) + multi-stage review per stem

## Patterns confirmed

- P145 admin-squash 38a volta consecutiva
- P74 close-as-shipped via PR body
- P124 pre-decomposition full search
- P181 spec-panel-style 8 DEC user-locked
- DEC-Pilot-7 BGG cleanup as Stage 0 prep (identico DS-17-11)
- P244 AI dispatch hybrid scaffolds + human iteration
- P249 transitive test mock propagation (carried over from DS-17 #2208 ifnecessary)
- P250 designer self-waiver Opzione C
- Mockup multi-frame pattern P239 (Frame matrix argTypes per mockup HTML stage)

## DS-17 Phase C-1 closure milestone

3/3 step shipped:
| Step | Sub-issue | PR |
|---|---|---|
| 1 — auth | #2160 | #2164 |
| 2 — sp6-7-nano | #2166 | #2173 |
| 3 — sp3 | #2208 | #<PR_NUM> |

## Phase C-2 preview (NEXT)

SP4 core 106 mockup + sp4-sessions 50 mockup. Required:
- New brainstorming per cluster split strategy
- Likely 2+ sub-issue (cluster size justifies decomposition)
- Reference patterns: DS-17 auth + DS-17-11 + DS-17-10 predecessor stacks

## NEW patterns discovered

(To be filled in during execution based on novel findings.)

## Links

- Sub-issue closed: #2208
- PR: #<PR_NUM> (`<merge_sha>`)
- Spec: docs/superpowers/specs/2026-06-11-ds-17-10-sp3-cluster-design.md
- Plan: docs/superpowers/plans/2026-06-11-ds-17-10-sp3-cluster-plan.md
- Designer review follow-up: #<TRACKING_NUM>
- Predecessor: [[ds-17-phase-c-1-auth-shipped]] + [[ds-17-phase-c-1-sp6-7-nano-shipped]]
- EPIC closure trigger: [[epic-2096-closure-shipped]] PR #2207
- DS-17 umbrella: #2063
```

Replace `<PR_NUM>`, `<merge_sha>`, `<TRACKING_NUM>` with actual values.

- [ ] **Step 2: Update MEMORY.md index**

Read current `~/.claude/projects/D--Repositories-meepleai-monorepo-frontend/memory/MEMORY.md` and add new entry at top:

```markdown
- [DS-17-10 sp3 cluster shipped](ds-17-10-sp3-cluster-shipped.md) — DS-17 Phase C-1 step 3/3 **CLOSED** sess.46p 2026-06-11 via PR #<PR_NUM> (admin-squash P145 38a). Sub-issue #2208 cluster sp3 (8 stems ship + route-create). NEW /library-public route + LibraryPublicHome + 2 new primitives (CommunityStatsRow + FeaturedGamesCarousel). sp3-shared-game-detail POST-#2096 rebuild integration verified. 8 DEC totali (3 memory + 5 new). Effort ~13-14h. Phase C-1 closure: 3/3 step shipped (auth #2160 + sp6-7-nano #2166 + sp3 #2208). NEXT Phase C-2 SP4 core 106 + sp4-sessions 50 mockup.
```

- [ ] **Step 3: Notify completion**

Final message to user: "DS-17-10 sp3 cluster CLOSED. DS-17 Phase C-1 3/3 step shipped. Phase C-2 SP4 core ready per next brainstorming."

---

## Self-review checklist (run BEFORE marking plan complete)

- [ ] Stage 0 covers 3 JSX twin edits + #2151 extend
- [ ] Stage 1 covers full sp3-library-public implementation (route + 4 component files + fidelity + index + tracking issue)
- [ ] Stage 2 covers 7 Agent dispatch with full Common dispatch context template
- [ ] Stage 2.2 sp3-shared-game-detail has special POST-rebuild integration notes (MSW handlers + 5 tab Frames)
- [ ] Stage 3 covers baseline capture for all 8 stems
- [ ] Stage 4 covers all quality gates from spec § 6
- [ ] Stage 5 covers PR open + admin-squash merge + sub-issue close + EPIC progress + memory entry
- [ ] All 8 DEC respected
- [ ] No TBD/TODO placeholders (runtime values <TRACKING_NUM> / <PR_NUM> / <merge_sha> documented as runtime fill)
- [ ] Code samples complete (CommunityStatsRow + FeaturedGamesCarousel + LibraryPublicHome + page.tsx + page.stories.tsx full code embedded)
- [ ] Tab ID lock #2010 respected (no tab rename in sp3-shared-game-detail story)
- [ ] BGG ToS #1903 respected (Stage 0 cleanup + lint:bgg gate)
- [ ] Pre-flight P124 already done (verified in spec sequencing)
- [ ] Branch hygiene #806 respected (already on feature branch)

---

**End of implementation plan.**
