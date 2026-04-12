'use client';

import { useEffect, useRef, useState } from 'react';

import { X } from 'lucide-react';

import { entityIcon, entityLabel } from '@/components/ui/data-display/meeple-card/tokens';
import { useCardHand } from '@/lib/stores/card-hand-store';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { cn } from '@/lib/utils';

import { DrawerContent } from './drawer/DrawerContent';

export function HandDrawer() {
  const { state, activeEntityType, activeEntityId, activeTabId, closeCascade } =
    useCascadeNavigationStore();

  const cards = useCardHand(s => s.cards);

  const [dragY, setDragY] = useState(0);
  const startY = useRef(0);
  const panelRef = useRef<HTMLDivElement>(null);

  const isOpen = state === 'drawer';

  // Close on Escape
  useEffect(() => {
    if (!isOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') closeCascade();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [isOpen, closeCascade]);

  // Focus panel on open
  useEffect(() => {
    if (isOpen) panelRef.current?.focus();
  }, [isOpen]);

  if (!isOpen || !activeEntityType || !activeEntityId) return null;

  // Resolve label from hand store, fallback to entityLabel
  const card = cards.find(c => c.entityId === activeEntityId && c.entityType === activeEntityType);
  const headerLabel = card?.label ?? entityLabel[activeEntityType];

  const handleTouchStart = (e: React.TouchEvent) => {
    startY.current = e.touches[0].clientY;
  };
  const handleTouchMove = (e: React.TouchEvent) => {
    const delta = e.touches[0].clientY - startY.current;
    if (delta > 0) setDragY(delta);
  };
  const handleTouchEnd = () => {
    if (dragY > 120) {
      setDragY(0);
      closeCascade();
    } else {
      setDragY(0);
    }
  };

  return (
    <>
      {/* Overlay — mobile only */}
      <div
        data-testid="hand-drawer-overlay"
        className="fixed inset-0 z-40 bg-black/60 md:hidden"
        onClick={closeCascade}
        aria-hidden="true"
      />

      {/* Panel */}
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label={`${headerLabel} drawer`}
        data-testid="hand-drawer-panel"
        tabIndex={-1}
        className={cn(
          'fixed bottom-0 left-0 right-0 z-50 md:hidden',
          'rounded-t-[20px] bg-[hsl(222,20%,10%)]',
          'border-t border-white/10',
          'flex flex-col h-[70vh] outline-none',
          'transition-transform duration-300'
        )}
        style={{ transform: `translateY(${dragY}px)` }}
      >
        {/* Drag handle */}
        <div
          className="flex justify-center pt-2.5 pb-1 cursor-grab active:cursor-grabbing flex-shrink-0"
          onTouchStart={handleTouchStart}
          onTouchMove={handleTouchMove}
          onTouchEnd={handleTouchEnd}
          data-testid="hand-drawer-handle"
        >
          <div className="w-10 h-1 rounded-full bg-white/20" />
        </div>

        {/* Header */}
        <div className="flex items-center gap-3 px-4 pb-3 border-b border-white/8 flex-shrink-0">
          <span className="text-2xl leading-none">{entityIcon[activeEntityType]}</span>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-bold text-white truncate">{headerLabel}</p>
            {card?.sublabel && (
              <p className="text-[11px] text-white/50 truncate">{card.sublabel}</p>
            )}
          </div>
          <button
            type="button"
            onClick={closeCascade}
            aria-label="Chiudi drawer"
            className="p-1.5 rounded-lg text-white/50 hover:text-white transition-colors"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Entity-specific content */}
        <DrawerContent
          entityType={activeEntityType}
          entityId={activeEntityId}
          activeTab={activeTabId ?? undefined}
          onNavigate={closeCascade}
        />
      </div>
    </>
  );
}
