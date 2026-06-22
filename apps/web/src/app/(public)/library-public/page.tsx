import type { CommunityStats } from '@/components/features/library-public/CommunityStatsRow';
import type { FeaturedGame } from '@/components/features/library-public/FeaturedGamesCarousel';
import { LibraryPublicHome } from '@/components/features/library-public/LibraryPublicHome';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Community MeepleAI — Scopri i giochi',
  description:
    'Scopri il catalogo board game della community MeepleAI. Toolkit, AI agents, partite, contenuti collaborativi.',
};

// NOTE: do NOT add @mockup JSDoc block manually here. The injector
// (`pnpm mockup-annotations:inject --apply`) runs in Stage 4 after
// MOCKUPS_INDEX.md is updated. The injector reads the index mapping
// and writes the full MOCKUP-ANNOTATION marker block. Manual injection
// would conflict with the idempotency check.
export default async function LibraryPublicPage() {
  // Stage 1: mock fixtures inline. Future iteration: replace with real
  // server-side fetch from backend (e.g. /api/v1/library-public/featured + /api/v1/community/stats).
  const featured: FeaturedGame[] = [
    {
      gameId: '00000000-0000-4000-8000-000000000001',
      title: 'Wingspan',
      publisher: 'Stonemaier Games',
      coverUrl: undefined,
      averageRating: 8.1,
    },
    {
      gameId: '00000000-0000-4000-8000-000000000002',
      title: 'Catan',
      publisher: 'Kosmos',
      coverUrl: undefined,
      averageRating: 7.2,
    },
    {
      gameId: '00000000-0000-4000-8000-000000000003',
      title: 'Terraforming Mars',
      publisher: 'FryxGames',
      coverUrl: undefined,
      averageRating: 8.4,
    },
    {
      gameId: '00000000-0000-4000-8000-000000000004',
      title: '7 Wonders',
      publisher: 'Repos Production',
      coverUrl: undefined,
      averageRating: 7.7,
    },
  ];

  const stats: CommunityStats = {
    totalGames: 1247,
    totalPlayers: 8520,
    totalSessions: 14392,
    totalCommunityContent: 318,
  };

  return <LibraryPublicHome featured={featured} stats={stats} />;
}
