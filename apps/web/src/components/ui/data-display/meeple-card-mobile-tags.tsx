/**
 * Mobile Tag Optimization Component (Issue #4076)
 *
 * Responsive tag display with:
 * - Max 1-2 tags visible on mobile + "+N" counter
 * - Bottom sheet on tap to show all tags
 * - Collapse on scroll for better UX
 *
 * @module components/ui/data-display/meeple-card-mobile-tags
 */

'use client';

import React, { useState, useEffect, useRef } from 'react';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { cn } from '@/lib/utils';
import { useMediaQuery } from '@/hooks/use-media-query';

export interface MobileTagsProps {
  /** All tags to display */
  tags: Array<{ label: string; color?: string; icon?: React.ReactNode }>;
  /** Max tags visible on mobile before showing "+N" */
  maxVisibleMobile?: number;
  /** Enable collapse on scroll */
  collapseOnScroll?: boolean;
  /** Scroll debounce delay (ms) */
  scrollDebounce?: number;
  /** Custom className */
  className?: string;
}

/**
 * Mobile-optimized tag display with bottom sheet
 * Issue #4076: Acceptance Criteria
 * - [x] Badge counter: Max 1-2 tags + "+N" on mobile
 * - [x] Bottom sheet on tap
 * - [x] Collapse on scroll (debounced 100ms)
 */
export function MobileTagDisplay({
  tags,
  maxVisibleMobile = 2,
  collapseOnScroll = true,
  scrollDebounce = 100,
  className,
}: MobileTagsProps) {
  const isMobile = useMediaQuery('(max-width: 768px)');
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [isSheetOpen, setIsSheetOpen] = useState(false);
  const lastScrollY = useRef(0);
  const scrollTimeout = useRef<NodeJS.Timeout>();

  // Scroll collapse behavior (mobile only, debounced)
  useEffect(() => {
    if (!isMobile || !collapseOnScroll) return;

    const handleScroll = () => {
      if (scrollTimeout.current) {
        clearTimeout(scrollTimeout.current);
      }

      scrollTimeout.current = setTimeout(() => {
        const currentScrollY = window.scrollY;
        const scrollingDown = currentScrollY > lastScrollY.current;

        // Collapse when scrolling down past 100px
        if (scrollingDown && currentScrollY > 100) {
          setIsCollapsed(true);
        } else if (!scrollingDown) {
          setIsCollapsed(false);
        }

        lastScrollY.current = currentScrollY;
      }, scrollDebounce);
    };

    window.addEventListener('scroll', handleScroll, { passive: true });

    return () => {
      window.removeEventListener('scroll', handleScroll);
      if (scrollTimeout.current) {
        clearTimeout(scrollTimeout.current);
      }
    };
  }, [isMobile, collapseOnScroll, scrollDebounce]);

  const visibleTags = isMobile ? tags.slice(0, maxVisibleMobile) : tags;
  const hiddenCount = isMobile ? Math.max(0, tags.length - maxVisibleMobile) : 0;

  // Don't render if no tags
  if (tags.length === 0) return null;

  // Collapsed state on mobile (minimal display)
  if (isCollapsed && isMobile) {
    return (
      <div
        className={cn(
          'flex items-center gap-1 transition-all duration-200',
          className
        )}
        onClick={() => setIsCollapsed(false)}
        role="button"
        tabIndex={0}
        aria-label="Expand tags"
        onKeyDown={e => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            setIsCollapsed(false);
          }
        }}
      >
        <span className="text-xs font-medium text-muted-foreground">
          {tags.length} {tags.length === 1 ? 'tag' : 'tags'}
        </span>
      </div>
    );
  }

  return (
    <div className={cn('flex items-center gap-1.5 flex-wrap', className)}>
      {/* Visible tags */}
      {visibleTags.map((tag, index) => (
        <span
          key={index}
          className={cn(
            'inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-xs font-medium',
            'bg-card/90 backdrop-blur-sm border border-border/50',
            'transition-all duration-200',
            tag.color || 'text-foreground'
          )}
          style={tag.color ? { borderColor: tag.color, color: tag.color } : undefined}
        >
          {tag.icon && <span className="w-3 h-3">{tag.icon}</span>}
          <span className="truncate max-w-[120px] md:max-w-none">{tag.label}</span>
        </span>
      ))}

      {/* "+N" counter with bottom sheet (mobile only) */}
      {hiddenCount > 0 && isMobile && (
        <Sheet open={isSheetOpen} onOpenChange={setIsSheetOpen}>
          <SheetTrigger asChild>
            <button
              className={cn(
                'inline-flex items-center justify-center',
                'px-2 py-0.5 rounded-md text-xs font-bold',
                'bg-primary/10 text-primary border border-primary/30',
                'hover:bg-primary/20 active:scale-95',
                'transition-all duration-200',
                'min-w-[44px] min-h-[44px]', // Touch target accessibility
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
              )}
              aria-label={`Show ${hiddenCount} more tags`}
              data-testid="mobile-tag-counter"
            >
              +{hiddenCount}
            </button>
          </SheetTrigger>

          <SheetContent
            side="bottom"
            className="max-h-[50vh] overflow-y-auto"
            data-testid="mobile-tags-sheet"
          >
            <SheetHeader>
              <SheetTitle>All Tags ({tags.length})</SheetTitle>
            </SheetHeader>

            <div className="grid grid-cols-2 gap-2 mt-4">
              {tags.map((tag, index) => (
                <div
                  key={index}
                  className={cn(
                    'flex items-center gap-2 p-3 rounded-lg',
                    'bg-card border border-border/50',
                    'transition-colors hover:bg-accent'
                  )}
                >
                  {tag.icon && (
                    <span className="w-4 h-4 flex-shrink-0">{tag.icon}</span>
                  )}
                  <span className="text-sm font-medium truncate">{tag.label}</span>
                </div>
              ))}
            </div>
          </SheetContent>
        </Sheet>
      )}
    </div>
  );
}

/**
 * Hook to detect mobile viewport
 * Reexport from use-media-query for convenience
 */
export { useMediaQuery as useMobileDetection };
