import type { CommunityStats } from '@/components/features/library-public/CommunityStatsRow';
import type { FeaturedGame } from '@/components/features/library-public/FeaturedGamesCarousel';
import { LibraryPublicHome } from '@/components/features/library-public/LibraryPublicHome';

import type { Meta, StoryObj } from '@storybook/react';

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
    // `Default` export covers the canonical `default` state via populated fixtures,
    // not a `mswForState('default')` string literal — lint:storybook-states heuristic
    // can't see it (#2342 Task 4 bonifica).
    canonicalStates: ['default'],
  },
};

export default meta;

type Story = StoryObj<typeof LibraryPublicHome>;

const FEATURED_FIXTURE: FeaturedGame[] = [
  {
    gameId: 'fixture-wingspan',
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    averageRating: 8.1,
  },
  { gameId: 'fixture-catan', title: 'Catan', publisher: 'Kosmos', averageRating: 7.2 },
  {
    gameId: 'fixture-terra',
    title: 'Terraforming Mars',
    publisher: 'FryxGames',
    averageRating: 8.4,
  },
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
