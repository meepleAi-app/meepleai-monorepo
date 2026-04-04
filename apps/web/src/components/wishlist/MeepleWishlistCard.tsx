'use client';

/**
 * MeepleWishlistCard — MeepleCard-based display for a single wishlist item.
 *
 * Shows priority badge (Italian labels), optional target price in metadata,
 * notes as subtitle, and Edit/Remove quick actions.
 * Uses the MeepleCard design system with entity="game".
 */

import { DollarSign, Edit, Trash2 } from 'lucide-react';

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
    metadata.push({ icon: DollarSign, label: `Target: ${item.targetPrice.toFixed(2)}` });
  }

  const quickActions: Array<{
    icon: typeof Edit;
    label: string;
    onClick: () => void;
    destructive?: boolean;
  }> = [];

  if (onUpdate) {
    quickActions.push({
      icon: Edit,
      label: 'Edit',
      onClick: () => onUpdate(item.id),
    });
  }

  if (onRemove) {
    quickActions.push({
      icon: Trash2,
      label: 'Remove',
      onClick: () => onRemove(item.id),
      destructive: true,
    });
  }

  return (
    <MeepleCard
      entity="game"
      variant="list"
      title={gameName || item.gameName || `Game ${item.gameId.slice(0, 8)}...`}
      subtitle={item.notes ?? undefined}
      badge={formatPriorityItalian(item.priority)}
      metadata={metadata}
      quickActions={quickActions.length > 0 ? quickActions : undefined}
      data-testid="wishlist-card"
    />
  );
}
