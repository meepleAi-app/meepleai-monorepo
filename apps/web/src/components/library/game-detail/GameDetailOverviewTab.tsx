'use client';

/**
 * GameDetailOverviewTab — Default tab wrapping CatalogDetails + UserAction
 */

import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

import { CatalogDetailsSection } from './CatalogDetailsSection';
import { UserActionSection } from './UserActionSection';

export interface GameDetailOverviewTabProps {
  gameDetail: LibraryGameDetail;
}

export function GameDetailOverviewTab({ gameDetail }: GameDetailOverviewTabProps) {
  return (
    <div className="space-y-6">
      <CatalogDetailsSection
        description={gameDetail.description}
        categories={gameDetail.categories}
        mechanics={gameDetail.mechanics}
        designers={gameDetail.designers}
        bggId={gameDetail.bggId}
        gameTitle={gameDetail.gameTitle}
      />
      <UserActionSection gameDetail={gameDetail} />
    </div>
  );
}
