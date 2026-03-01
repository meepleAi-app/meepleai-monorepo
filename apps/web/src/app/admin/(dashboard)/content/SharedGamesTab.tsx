'use client';

import { CategoriesTable } from '@/components/admin/shared-games/categories-table';
import { GameCatalogGrid } from '@/components/admin/shared-games/game-catalog-grid';
import { GameFilters } from '@/components/admin/shared-games/game-filters';

interface SharedGamesTabProps {
  showCategories?: boolean;
}

export function SharedGamesTab({ showCategories }: SharedGamesTabProps) {
  return (
    <div className="space-y-6">
      <GameFilters />
      <GameCatalogGrid />
      {showCategories && (
        <div className="space-y-4 pt-4 border-t border-border/50">
          <div>
            <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
              Game Categories
            </h2>
            <p className="text-sm text-muted-foreground mt-1">
              Manage and organize game categories
            </p>
          </div>
          <CategoriesTable />
        </div>
      )}
    </div>
  );
}
