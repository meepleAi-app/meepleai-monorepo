'use client';

import React, { useCallback, useState } from 'react';

import { Search } from 'lucide-react';

import { QuickActionCards } from '@/components/dashboard/QuickActionCards';
import { RecentGamesRow } from '@/components/dashboard/RecentGamesRow';
import { FullScreenSearch } from '@/components/search/FullScreenSearch';
import type { BggGameResult } from '@/components/search/FullScreenSearch';
import { GamePreviewSheet } from '@/components/search/GamePreviewSheet';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import type { BggGameSummary } from '@/lib/api/clients/gameNightBggClient';

/**
 * Mobile-first dashboard with search flow.
 *
 * - MobileHeader with title "MeepleAI"
 * - Fake search bar that opens FullScreenSearch overlay
 * - RecentGamesRow horizontal scroll
 * - QuickActionCards with search trigger
 * - FullScreenSearch + GamePreviewSheet integration
 */
export function DashboardMobile() {
  const [searchOpen, setSearchOpen] = useState(false);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [selectedGame, setSelectedGame] = useState<BggGameSummary | null>(null);

  const openSearch = useCallback(() => setSearchOpen(true), []);
  const closeSearch = useCallback(() => setSearchOpen(false), []);

  const handleSelectGame = useCallback((game: BggGameResult) => {
    // Map BggSearchResult (name) → BggGameSummary (title)
    const summary: BggGameSummary = {
      bggId: game.bggId,
      title: game.name,
      yearPublished: game.yearPublished,
      thumbnailUrl: game.thumbnailUrl,
    };
    setSelectedGame(summary);
    setSearchOpen(false);
    setPreviewOpen(true);
  }, []);

  const handleAdded = useCallback(() => {
    // Close the preview sheet after a short delay so the user sees the success state
    setTimeout(() => {
      setPreviewOpen(false);
      setSelectedGame(null);
    }, 1500);
  }, []);

  return (
    <div className="flex min-h-dvh flex-col bg-[var(--gaming-bg-base)]">
      {/* Sticky header */}
      <MobileHeader title="MeepleAI" />

      {/* Fake search bar */}
      <div className="px-4 pt-4 pb-2">
        <button
          type="button"
          onClick={openSearch}
          className="flex w-full items-center gap-3 rounded-xl border border-[var(--gaming-border-glass)] bg-white/5 px-4 py-3 text-left transition-colors hover:bg-white/10"
        >
          <Search className="h-5 w-5 shrink-0 text-[var(--gaming-text-secondary)]" />
          <span className="text-sm text-[var(--gaming-text-secondary)]">
            Cerca un gioco su BGG...
          </span>
        </button>
      </div>

      {/* Recent games horizontal scroll */}
      <RecentGamesRow />

      {/* Quick action cards */}
      <QuickActionCards onSearchClick={openSearch} />

      {/* Full-screen search overlay */}
      <FullScreenSearch open={searchOpen} onClose={closeSearch} onSelectGame={handleSelectGame} />

      {/* Game preview bottom sheet */}
      <GamePreviewSheet
        open={previewOpen}
        game={selectedGame}
        onOpenChange={setPreviewOpen}
        onAdded={handleAdded}
      />
    </div>
  );
}
