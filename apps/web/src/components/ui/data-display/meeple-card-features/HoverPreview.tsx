/**
 * HoverPreview - Hover Tooltip Preview for MeepleCard
 * Issue #3827, #3859 - Epic #3820
 * Issue #4620 - Mobile: tap navigates directly to detail page
 *
 * Desktop: displays rich game information on hover with smooth animations.
 * Mobile: wraps children in a Link to navigate directly to game detail page.
 */

'use client';

import * as React from 'react';

import Link from 'next/link';

import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/overlays';
import { useMediaQuery } from '@/hooks/use-media-query';

export interface HoverPreviewData {
  description?: string;
  designer?: string;
  complexity?: number;
  weight?: 'Light' | 'Medium' | 'Heavy';
  categories?: string[];
  mechanics?: string[];
}

export interface HoverPreviewProps {
  gameId: string;
  previewData?: HoverPreviewData;
  onFetchPreview?: (gameId: string) => Promise<HoverPreviewData>;
  delay?: number;
  disabled?: boolean;
  children: React.ReactNode;
  /** Detail page href for mobile direct navigation (default: /games/{gameId}) */
  detailHref?: string;
}

/**
 * HoverPreview Component
 *
 * Desktop: Shows game details in a popover on hover.
 * Mobile: Wraps card in a link to navigate directly to detail page.
 */
export function HoverPreview({
  gameId,
  previewData,
  onFetchPreview,
  delay = 500,
  disabled = false,
  children,
  detailHref,
}: HoverPreviewProps) {
  const isMobile = useMediaQuery('(max-width: 767px)');
  const [data, setData] = React.useState<HoverPreviewData | null>(previewData ?? null);
  const [isLoading, setIsLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [isOpen, setIsOpen] = React.useState(false);

  // Fetch preview data on hover if not provided (desktop only)
  React.useEffect(() => {
    if (!isOpen || data || !onFetchPreview || disabled || isMobile) return;

    let isMounted = true;
    const timeoutId = setTimeout(async () => {
      if (!isMounted) return;

      setIsLoading(true);
      setError(null);

      try {
        const result = await onFetchPreview(gameId);
        if (isMounted) {
          setData(result);
        }
      } catch (err) {
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Failed to load preview');
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }, delay);

    return () => {
      isMounted = false;
      clearTimeout(timeoutId);
    };
  }, [isOpen, data, onFetchPreview, gameId, delay, disabled, isMobile]);

  if (disabled) {
    return <>{children}</>;
  }

  // Mobile: wrap in Link for direct navigation to detail page
  if (isMobile) {
    const href = detailHref || `/games/${gameId}`;
    return (
      <Link href={href} className="block">
        {children}
      </Link>
    );
  }

  // Desktop: popover on hover
  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger asChild>{children}</PopoverTrigger>
      <PopoverContent className="w-full max-w-sm md:w-80" align="start">
        {isLoading ? (
          <div className="space-y-2">
            <div className="h-4 w-3/4 animate-pulse rounded bg-muted" />
            <div className="h-4 w-full animate-pulse rounded bg-muted" />
            <div className="h-4 w-5/6 animate-pulse rounded bg-muted" />
          </div>
        ) : error ? (
          <div className="text-sm text-destructive">
            <p className="font-medium">Error loading preview</p>
            <p className="text-xs text-muted-foreground">{error}</p>
          </div>
        ) : data ? (
          <div className="space-y-3">
            {/* Description */}
            {data.description && (
              <div>
                <p className="text-sm leading-relaxed text-foreground/90">{data.description}</p>
              </div>
            )}

            {/* Designer & Complexity */}
            {(data.designer || data.complexity !== undefined) && (
              <div className="flex flex-wrap gap-3 text-xs text-muted-foreground">
                {data.designer && (
                  <div>
                    <span className="font-medium">Designer:</span>{' '}
                    <span className="text-foreground/80">{data.designer}</span>
                  </div>
                )}
                {data.complexity !== undefined && (
                  <div>
                    <span className="font-medium">Complexity:</span>{' '}
                    <span className="text-foreground/80">{data.complexity}/5</span>
                  </div>
                )}
              </div>
            )}

            {/* Weight */}
            {data.weight && (
              <div className="flex items-center gap-2">
                <span className="text-xs font-medium text-muted-foreground">Weight:</span>
                <span
                  className={`rounded px-2 py-0.5 text-xs font-medium ${
                    data.weight === 'Light'
                      ? 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300'
                      : data.weight === 'Medium'
                        ? 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300'
                        : 'bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-300'
                  }`}
                >
                  {data.weight}
                </span>
              </div>
            )}

            {/* Categories */}
            {data.categories && data.categories.length > 0 && (
              <div className="space-y-1">
                <p className="text-xs font-medium text-muted-foreground">Categories</p>
                <div className="flex flex-wrap gap-1">
                  {data.categories.map((category, idx) => (
                    <span
                      key={idx}
                      className="rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary dark:bg-primary/20"
                    >
                      {category}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Mechanics */}
            {data.mechanics && data.mechanics.length > 0 && (
              <div className="space-y-1">
                <p className="text-xs font-medium text-muted-foreground">Mechanics</p>
                <div className="flex flex-wrap gap-1">
                  {data.mechanics.map((mechanic, idx) => (
                    <span
                      key={idx}
                      className="rounded-full bg-secondary/80 px-2 py-0.5 text-xs font-medium text-secondary-foreground dark:bg-secondary/60"
                    >
                      {mechanic}
                    </span>
                  ))}
                </div>
              </div>
            )}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">No preview available</p>
        )}
      </PopoverContent>
    </Popover>
  );
}
