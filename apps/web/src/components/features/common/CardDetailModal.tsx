'use client';

/**
 * CardDetailModal — Responsive entity detail view for the alpha layout.
 *
 * Bridges the alpha nav store (detailEntityId / detailEntityType) to the
 * existing ExtraMeepleCardDrawer component, which handles entity routing,
 * data fetching, and rendering for all entity types.
 *
 * - Mobile (< 1024px): Full-width right drawer (Sheet side="right", w-full)
 * - Desktop (>= 1024px): 600px right side panel (Sheet side="right", sm:w-[600px])
 *
 * Both modes support: Escape to close, backdrop click to close, close button.
 * All provided by the underlying Radix Sheet component.
 */

import { ExtraMeepleCardDrawer } from '@/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer';
import type { DrawerEntityType } from '@/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer';
import { useNavigation } from '@/hooks/useNavigation';

export function CardDetailModal() {
  const { detailEntityId, detailEntityType, closeDetail } = useNavigation();

  const isOpen = detailEntityId !== null && detailEntityType !== null;

  // Always render the drawer so exit animations can play.
  // The Sheet component controls visibility via the `open` prop.
  return (
    <ExtraMeepleCardDrawer
      entityType={(detailEntityType ?? 'game') as DrawerEntityType}
      entityId={detailEntityId ?? ''}
      open={isOpen}
      onClose={closeDetail}
    />
  );
}
