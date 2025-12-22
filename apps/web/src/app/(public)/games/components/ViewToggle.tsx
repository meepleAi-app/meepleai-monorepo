/**
 * ViewToggle Component (Issue #1838: PAGE-003)
 *
 * Client component for switching between grid and list view modes.
 * Uses Shadcn ToggleGroup with URL state persistence via searchParams.
 *
 * Features:
 * - Syncs view mode to URL (?view=grid|list)
 * - Preserves other search params (page, search, etc.)
 * - Keyboard accessible (Tab + Enter/Space)
 * - Responsive icons (Grid3x3, AlignJustify from lucide-react)
 */

'use client';

import { useCallback } from 'react';

import { Rows, Square } from 'lucide-react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';

export interface ViewToggleProps {
  /** Current view mode from URL searchParams */
  currentView: 'grid' | 'list';
}

export function ViewToggle({ currentView }: ViewToggleProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const handleViewChange = useCallback(
    (value: string) => {
      if (!value || (value !== 'grid' && value !== 'list')) return;

      const params = new URLSearchParams(searchParams?.toString() || '');
      params.set('view', value);

      // Navigate to updated URL (scroll: false to maintain position)
      router.push(`${pathname}?${params.toString()}`, { scroll: false });
    },
    [router, pathname, searchParams]
  );

  return (
    <ToggleGroup
      type="single"
      value={currentView}
      onValueChange={handleViewChange}
      aria-label="Toggle view mode"
      className="border rounded-md"
    >
      <ToggleGroupItem value="grid" aria-label="Grid view" className="gap-2">
        <Square className="h-4 w-4" />
        <span className="hidden sm:inline">Grid</span>
      </ToggleGroupItem>
      <ToggleGroupItem value="list" aria-label="List view" className="gap-2">
        <Rows className="h-4 w-4" />
        <span className="hidden sm:inline">List</span>
      </ToggleGroupItem>
    </ToggleGroup>
  );
}
