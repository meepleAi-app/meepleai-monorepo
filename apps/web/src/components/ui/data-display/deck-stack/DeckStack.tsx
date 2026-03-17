'use client';
import { memo, useEffect, useCallback } from 'react';

import { createPortal } from 'react-dom';

import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { cn } from '@/lib/utils';

import { DeckStackCard } from './DeckStackCard';

import type { DeckStackItem, DeckStackPresentation } from './deck-stack-types';

const MAX_VISIBLE = 5;

interface DeckStackProps {
  isOpen: boolean;
  items: DeckStackItem[];
  onItemClick: (id: string, entityType: string) => void;
  onClose: () => void;
  anchorRect?: DOMRect | null;
  className?: string;
  presentation?: DeckStackPresentation;
}

export const DeckStack = memo(function DeckStack({
  isOpen,
  items,
  onItemClick,
  onClose,
  anchorRect,
  className,
  presentation = 'popover',
}: DeckStackProps) {
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    },
    [onClose]
  );

  useEffect(() => {
    if (!isOpen || presentation === 'bottomSheet') return;
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, handleKeyDown, presentation]);

  if (!isOpen || items.length === 0) return null;

  const visibleItems = items.slice(0, MAX_VISIBLE);
  const overflow = items.length - MAX_VISIBLE;

  /* ── Bottom Sheet presentation ── */
  if (presentation === 'bottomSheet') {
    return (
      <Sheet
        open={isOpen}
        onOpenChange={open => {
          if (!open) onClose();
        }}
      >
        <SheetContent side="bottom" className={cn('rounded-t-2xl', className)}>
          <SheetHeader>
            <SheetTitle className="text-sm font-medium">
              {items.length} related {items.length === 1 ? 'item' : 'items'}
            </SheetTitle>
          </SheetHeader>
          <div className="flex flex-wrap gap-2 py-4">
            {visibleItems.map((item, index) => (
              <DeckStackCard key={item.id} item={item} index={index} onClick={onItemClick} />
            ))}
          </div>
          {overflow > 0 && (
            <button
              type="button"
              onClick={onClose}
              className="text-xs text-muted-foreground hover:text-foreground"
            >
              View all {items.length}
            </button>
          )}
        </SheetContent>
      </Sheet>
    );
  }

  /* ── Popover presentation (default) ── */
  const content = (
    <>
      {/* Backdrop */}
      <div data-testid="deck-stack-backdrop" className="fixed inset-0 z-40" onClick={onClose} />
      {/* Fan container */}
      <div
        className={cn(
          'fixed z-50 flex items-end gap-1',
          'animate-in fade-in-0 zoom-in-95 duration-300',
          className
        )}
        style={
          anchorRect
            ? {
                top: anchorRect.bottom + 8,
                left: anchorRect.left,
              }
            : {
                top: '50%',
                left: '50%',
                transform: 'translate(-50%, -50%)',
              }
        }
      >
        {visibleItems.map((item, index) => (
          <DeckStackCard key={item.id} item={item} index={index} onClick={onItemClick} />
        ))}
        {overflow > 0 && (
          <button
            type="button"
            onClick={onClose}
            className="text-[10px] text-slate-400 hover:text-white ml-1 whitespace-nowrap"
          >
            View all {items.length}
          </button>
        )}
      </div>
    </>
  );

  return createPortal(content, document.body);
});
