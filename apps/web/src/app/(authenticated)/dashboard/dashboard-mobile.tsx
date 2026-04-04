'use client';

import React, { useCallback, useState } from 'react';

import { Search } from 'lucide-react';

import { AddGameDrawer } from '@/app/(authenticated)/library/AddGameDrawer';
import { QuickActionCards } from '@/components/dashboard/QuickActionCards';
import { RecentGamesRow } from '@/components/dashboard/RecentGamesRow';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';

/**
 * Mobile-first dashboard.
 *
 * - MobileHeader with title "MeepleAI"
 * - Button that opens AddGameDrawer (catalog search / manual entry)
 * - RecentGamesRow horizontal scroll
 * - QuickActionCards with add-game trigger
 *
 * Note: BGG search was removed from user pages (restricted to admin only
 * due to BGG commercial use licensing). Users add games via the shared
 * catalog or manual entry through AddGameDrawer.
 */
export function DashboardMobile() {
  const [addDrawerOpen, setAddDrawerOpen] = useState(false);

  const openAddDrawer = useCallback(() => setAddDrawerOpen(true), []);
  const closeAddDrawer = useCallback(() => setAddDrawerOpen(false), []);

  return (
    <div className="flex min-h-dvh flex-col bg-[var(--gaming-bg-base)]">
      {/* Sticky header */}
      <MobileHeader title="MeepleAI" />

      {/* Add game bar */}
      <div className="px-4 pt-4 pb-2">
        <button
          type="button"
          onClick={openAddDrawer}
          className="flex w-full items-center gap-3 rounded-xl border border-[var(--gaming-border-glass)] bg-white/5 px-4 py-3 text-left transition-colors hover:bg-white/10"
        >
          <Search className="h-5 w-5 shrink-0 text-[var(--gaming-text-secondary)]" />
          <span className="text-sm text-[var(--gaming-text-secondary)]">Aggiungi un gioco...</span>
        </button>
      </div>

      {/* Recent games horizontal scroll */}
      <RecentGamesRow />

      {/* Quick action cards */}
      <QuickActionCards onSearchClick={openAddDrawer} />

      {/* Add game drawer (catalog search / manual entry) */}
      <AddGameDrawer open={addDrawerOpen} onClose={closeAddDrawer} />
    </div>
  );
}
