'use client';

/**
 * MeepleWishlistCard — MeepleCard-based display for a single wishlist item.
 *
 * Issue #3007 (Task B3). Wraps `MeepleCard` (entity="game", variant="list")
 * for the shared card chrome (cover, title, subtitle, target-price metadata)
 * and layers on the wishlist-specific chrome that neither MeepleCard variant
 * supports out of the box:
 *   - a *clickable* priority badge (`ListCard`'s `badge` prop renders a
 *     static `<span>`, see `meeple-card/variants/ListCard.tsx`)
 *   - a high-priority left-border accent
 *   - an added-at relative-time line
 *   - Edit/Remove footer actions (`ListCard` never renders the `actions`
 *     prop at all — only `GridCard` does, and only as hover-only icon
 *     buttons over the cover, which doesn't match the mockup's always-visible
 *     text actions)
 *
 * Mockup: `admin-mockups/design_files/sp4-library-wishlist-ui.jsx`
 * (`WishlistCard`, ~lines 54-109). The mockup's `MetaBox`
 * (category/players/duration/BGG) is intentionally omitted — `WishlistItemDto`
 * has none of those fields.
 */

import {
  normalizePriorityString,
  type Priority,
} from '@/app/(authenticated)/library/wishlist/_lib/wishlist-filters';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { useTranslation } from '@/hooks/useTranslation';
import type { WishlistItemDto } from '@/lib/api/schemas/wishlist.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface MeepleWishlistCardProps {
  item: WishlistItemDto;
  /** Resolved game name — falls back to item.gameName, then a localized placeholder. */
  gameName?: string;
  onRemove: (id: string) => void;
  /** When omitted, the Edit action is not rendered. */
  onEdit?: (item: WishlistItemDto) => void;
  /** When omitted, the priority badge renders as a non-interactive `<span>`. */
  onFilterPriority?: (priority: Priority) => void;
}

// ============================================================================
// Helpers
// ============================================================================

const MS_PER_MINUTE = 60_000;
const MS_PER_HOUR = 60 * MS_PER_MINUTE;
const MS_PER_DAY = 24 * MS_PER_HOUR;
const MS_PER_MONTH = 30 * MS_PER_DAY;
const MS_PER_YEAR = 12 * MS_PER_MONTH;

/**
 * Buckets an ISO timestamp relative to `now` into an `Intl.RelativeTimeFormat`
 * value/unit pair, escalating minute → hour → day → month → year.
 */
function getRelativeTimeParts(
  iso: string,
  now: Date
): { value: number; unit: Intl.RelativeTimeFormatUnit } {
  const diffMs = new Date(iso).getTime() - now.getTime();

  const minutes = Math.round(diffMs / MS_PER_MINUTE);
  if (Math.abs(minutes) < 60) return { value: minutes, unit: 'minute' };

  const hours = Math.round(diffMs / MS_PER_HOUR);
  if (Math.abs(hours) < 24) return { value: hours, unit: 'hour' };

  const days = Math.round(diffMs / MS_PER_DAY);
  if (Math.abs(days) < 30) return { value: days, unit: 'day' };

  const months = Math.round(diffMs / MS_PER_MONTH);
  if (Math.abs(months) < 12) return { value: months, unit: 'month' };

  const years = Math.round(diffMs / MS_PER_YEAR);
  return { value: years, unit: 'year' };
}

// ============================================================================
// Component
// ============================================================================

export function MeepleWishlistCard({
  item,
  gameName,
  onRemove,
  onEdit,
  onFilterPriority,
}: MeepleWishlistCardProps) {
  const { t, formatNumber, formatDate, formatRelativeTime } = useTranslation();

  const priority = normalizePriorityString(item.priority);
  const isHighPriority = priority === 'high';
  const priorityLabel = t(`pages.library.wishlist.priority.${priority}`);
  const resolvedGameName =
    gameName ?? item.gameName ?? t('pages.library.wishlist.card.unknownGame');

  const metadata: MeepleCardMetadata[] = [];
  if (item.targetPrice != null) {
    metadata.push({
      label: t('pages.library.wishlist.card.target'),
      value: formatNumber(item.targetPrice, { style: 'currency', currency: 'EUR' }),
    });
  }

  const now = new Date();
  const { value: relValue, unit: relUnit } = getRelativeTimeParts(item.addedAt, now);
  const addedAtLabel = t('pages.library.wishlist.card.addedAt', {
    when: formatRelativeTime(relValue, relUnit),
  });
  const addedAtTitle = t('pages.library.wishlist.card.addedTitle', {
    date: formatDate(new Date(item.addedAt), { dateStyle: 'medium' }),
  });

  return (
    <div
      className={cn(
        'flex flex-col gap-1.5 rounded-xl',
        isHighPriority && 'border-l-4 border-l-destructive'
      )}
      data-testid="wishlist-card-shell"
    >
      <div className="flex items-center justify-between px-1">
        <span aria-hidden="true">❤️</span>
        {onFilterPriority ? (
          <button
            type="button"
            onClick={() => onFilterPriority(priority)}
            aria-label={t('pages.library.wishlist.priority.filterAria', { label: priorityLabel })}
            className="rounded-full border border-border bg-muted px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-foreground transition-colors hover:bg-muted/70"
          >
            {priorityLabel}
          </button>
        ) : (
          <span
            className="rounded-full border border-border bg-muted px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-foreground"
            data-testid="wishlist-priority-badge-static"
          >
            {priorityLabel}
          </span>
        )}
      </div>

      <MeepleCard
        entity="game"
        variant="list"
        title={resolvedGameName}
        subtitle={item.notes ?? t('pages.library.wishlist.card.noNote')}
        metadata={metadata}
        headingLevel={2}
        data-testid="wishlist-card"
      />

      <div className="flex items-center px-1 text-[11px] text-muted-foreground">
        <span title={addedAtTitle}>{addedAtLabel}</span>
      </div>

      <div className="flex items-center justify-end gap-2 px-1">
        {onEdit && (
          <button
            type="button"
            onClick={() => onEdit(item)}
            aria-label={t('pages.library.wishlist.card.editAria', { name: resolvedGameName })}
            className="rounded-md px-2 py-1 text-xs font-semibold text-foreground transition-colors hover:bg-muted"
          >
            {t('pages.library.wishlist.card.edit')}
          </button>
        )}
        <button
          type="button"
          onClick={() => onRemove(item.id)}
          aria-label={t('pages.library.wishlist.card.removeAria', { name: resolvedGameName })}
          className="rounded-md px-2 py-1 text-xs font-semibold text-destructive transition-colors hover:bg-destructive/10"
        >
          {t('pages.library.wishlist.card.remove')}
        </button>
      </div>
    </div>
  );
}
