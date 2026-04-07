'use client';

/**
 * MeepleWishlistCard — MeepleCard-based display for a single wishlist item.
 *
 * Shows priority badge (Italian labels), optional target price in metadata,
 * notes as subtitle, and Edit/Remove quick actions.
 * Uses the MeepleCard design system with entity="game".
 */

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import type { WishlistItemDto } from '@/lib/api/schemas/wishlist.schemas';

// ============================================================================
// Types
// ============================================================================

interface MeepleWishlistCardProps {
  item: WishlistItemDto;
  /** Resolved game name — falls back to item.gameName or truncated gameId */
  gameName?: string;
  onRemove?: (id: string) => void;
  onUpdate?: (id: string) => void;
}

// ============================================================================
// Helpers
// ============================================================================

function formatPriorityItalian(priority: string): string {
  switch (priority.toLowerCase()) {
    case 'high':
      return 'Alta';
    case 'medium':
      return 'Media';
    case 'low':
      return 'Bassa';
    default:
      return priority.charAt(0).toUpperCase() + priority.slice(1).toLowerCase();
  }
}

// ============================================================================
// Component
// ============================================================================

export function MeepleWishlistCard({
  item,
  gameName,
  onRemove,
  onUpdate,
}: MeepleWishlistCardProps) {
  const metadata: MeepleCardMetadata[] = [];

  if (item.targetPrice != null) {
    metadata.push({ label: `Target: ${item.targetPrice.toFixed(2)}` });
  }

  const actions = [
    ...(onUpdate ? [{ icon: '✏️', label: 'Edit', onClick: () => onUpdate(item.id) }] : []),
    ...(onRemove
      ? [
          {
            icon: '🗑️',
            label: 'Remove',
            onClick: () => onRemove(item.id),
            variant: 'danger' as const,
          },
        ]
      : []),
  ];

  return (
    <MeepleCard
      entity="game"
      variant="list"
      title={gameName || item.gameName || `Game ${item.gameId.slice(0, 8)}...`}
      subtitle={item.notes ?? undefined}
      badge={formatPriorityItalian(item.priority)}
      metadata={metadata}
      actions={actions.length > 0 ? actions : undefined}
      data-testid="wishlist-card"
    />
  );
}
