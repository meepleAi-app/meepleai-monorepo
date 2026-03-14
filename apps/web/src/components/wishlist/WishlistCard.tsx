'use client';

/**
 * WishlistCard — Displays a single wishlist item.
 *
 * Shows game ID, priority badge (color-coded), optional target price,
 * optional notes, and the date the item was added.
 * Includes a remove action.
 */

import { CalendarDays, DollarSign, FileText, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { WishlistItemDto } from '@/lib/api/schemas/wishlist.schemas';

// ============================================================================
// Types
// ============================================================================

interface WishlistCardProps {
  item: WishlistItemDto;
  onRemove?: (id: string) => void;
  onUpdate?: (id: string) => void;
}

// ============================================================================
// Helpers
// ============================================================================

function formatDate(dateStr: string): string {
  const date = new Date(dateStr);
  return date.toLocaleDateString(undefined, {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });
}

function getPriorityBadgeClass(priority: string): string {
  switch (priority.toLowerCase()) {
    case 'high':
      return 'border-transparent bg-red-500 text-white hover:bg-red-600';
    case 'medium':
      return 'border-transparent bg-yellow-500 text-white hover:bg-yellow-600';
    case 'low':
    default:
      return 'border-transparent bg-green-500 text-white hover:bg-green-600';
  }
}

function formatPriority(priority: string): string {
  return priority.charAt(0).toUpperCase() + priority.slice(1).toLowerCase();
}

// ============================================================================
// Component
// ============================================================================

export function WishlistCard({ item, onRemove, onUpdate }: WishlistCardProps) {
  return (
    <Card className="h-full flex flex-col">
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-2">
          <CardTitle className="text-sm font-semibold font-mono text-muted-foreground truncate">
            {item.gameId}
          </CardTitle>
          <Badge className={getPriorityBadgeClass(item.priority)}>
            {formatPriority(item.priority)}
          </Badge>
        </div>
      </CardHeader>

      <CardContent className="flex-1 space-y-2">
        {item.targetPrice != null && (
          <div className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <DollarSign className="h-4 w-4 shrink-0" />
            <span>Target: {item.targetPrice.toFixed(2)}</span>
          </div>
        )}

        {item.notes && (
          <div className="flex items-start gap-1.5 text-sm text-muted-foreground">
            <FileText className="h-4 w-4 shrink-0 mt-0.5" />
            <span className="line-clamp-2">{item.notes}</span>
          </div>
        )}

        <div className="flex items-center gap-1.5 text-sm text-muted-foreground">
          <CalendarDays className="h-4 w-4 shrink-0" />
          <span>{formatDate(item.addedAt)}</span>
        </div>
      </CardContent>

      <div className="flex items-center justify-end gap-2 px-6 pb-4">
        {onUpdate && (
          <Button variant="outline" size="sm" onClick={() => onUpdate(item.id)}>
            Edit
          </Button>
        )}
        {onRemove && (
          <Button
            variant="ghost"
            size="icon"
            className="text-destructive hover:text-destructive hover:bg-destructive/10"
            onClick={() => onRemove(item.id)}
            aria-label="Remove from wishlist"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        )}
      </div>
    </Card>
  );
}
