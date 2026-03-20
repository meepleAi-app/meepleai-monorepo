'use client';

import { useEffect, useCallback, useRef, type ReactNode } from 'react';

import { useOverlayStore } from '@/lib/stores/overlay-store';
import { cn } from '@/lib/utils';

interface OverlayRenderProps {
  entityType: string;
  entityId: string;
  onClose: () => void;
  deckItems: Array<{ entityType: string; entityId: string }> | null;
  deckIndex: number;
  onDeckIndexChange: (index: number) => void;
}

interface OverlayHybridProps {
  children: (props: OverlayRenderProps) => ReactNode;
  enableDeepLink?: boolean;
}

export function OverlayHybrid({ children, enableDeepLink = false }: OverlayHybridProps) {
  const { isOpen, entityType, entityId, deckItems, deckIndex, close, setDeckIndex, toUrlParam } =
    useOverlayStore();

  // Close on Escape
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, close]);

  // Deep link: push on first open, replace on entity changes within same overlay
  const wasOpenRef = useRef(false);
  useEffect(() => {
    if (!enableDeepLink || !isOpen) {
      wasOpenRef.current = false;
      return;
    }
    const param = toUrlParam();
    if (!param) return;
    const url = new URL(window.location.href);
    url.searchParams.set('overlay', param);

    if (!wasOpenRef.current) {
      // First open: push new history entry
      window.history.pushState({ overlay: true }, '', url.toString());
      wasOpenRef.current = true;
    } else {
      // Entity change while already open (e.g. deck navigation): replace, don't stack
      window.history.replaceState({ overlay: true }, '', url.toString());
    }

    const handlePopState = () => close();
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, [enableDeepLink, isOpen, entityType, entityId, toUrlParam, close]);

  // Deep link: restore on mount
  useEffect(() => {
    if (!enableDeepLink) return;
    const url = new URL(window.location.href);
    const param = url.searchParams.get('overlay');
    if (param) {
      useOverlayStore.getState().fromUrlParam(param);
    }
  }, [enableDeepLink]);

  const handleBackdropClick = useCallback(() => close(), [close]);

  if (!isOpen || !entityType || !entityId) return null;

  return (
    <>
      {/* Backdrop */}
      <div
        data-testid="overlay-backdrop"
        className="fixed inset-0 z-40 bg-black/40 motion-reduce:transition-none transition-opacity duration-200"
        onClick={handleBackdropClick}
        aria-hidden="true"
      />

      {/* Panel: bottom sheet on mobile, side panel on desktop */}
      <div
        data-testid="overlay-hybrid"
        role="dialog"
        aria-modal="true"
        className={cn(
          'fixed z-50 bg-background shadow-xl overflow-hidden',
          'transition-transform duration-250 ease-out motion-reduce:transition-none',
          // Mobile: bottom sheet
          'inset-x-0 bottom-0 h-[60vh] rounded-t-2xl',
          // Desktop: side panel
          'md:inset-y-0 md:right-0 md:left-auto md:bottom-auto md:w-[400px] md:h-full md:rounded-t-none md:rounded-l-2xl'
        )}
      >
        {/* Drag handle (mobile) */}
        <div className="flex justify-center pt-2 pb-1 md:hidden">
          <div className="w-10 h-1 rounded-full bg-muted-foreground/30" />
        </div>

        {/* Close button */}
        <button
          type="button"
          onClick={close}
          className="absolute top-3 right-3 w-8 h-8 flex items-center justify-center rounded-full bg-muted/50 hover:bg-muted text-muted-foreground hover:text-foreground transition-colors z-10"
          aria-label="Chiudi"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            className="w-4 h-4"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <line x1="18" x2="6" y1="6" y2="18" />
            <line x1="6" x2="18" y1="6" y2="18" />
          </svg>
        </button>

        {/* Content area */}
        <div className="h-full overflow-y-auto p-4 pt-2 md:pt-4">
          {children({
            entityType,
            entityId,
            onClose: close,
            deckItems,
            deckIndex,
            onDeckIndexChange: setDeckIndex,
          })}
        </div>

        {/* Deck navigation footer */}
        {deckItems && deckItems.length > 1 && (
          <div className="absolute bottom-0 inset-x-0 flex items-center justify-center gap-2 py-3 bg-gradient-to-t from-background to-transparent">
            <button
              type="button"
              onClick={() => setDeckIndex(deckIndex - 1)}
              disabled={deckIndex === 0}
              className="p-1 rounded-full disabled:opacity-30 hover:bg-muted transition-colors"
              aria-label="Precedente"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="w-5 h-5"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <polyline points="15 18 9 12 15 6" />
              </svg>
            </button>
            <div className="flex gap-1.5" role="tablist">
              {deckItems.map((_, i) => (
                <button
                  key={i}
                  type="button"
                  role="tab"
                  aria-selected={i === deckIndex}
                  aria-label={`Card ${i + 1} di ${deckItems.length}`}
                  onClick={() => setDeckIndex(i)}
                  className={cn(
                    'w-2 h-2 rounded-full transition-colors',
                    i === deckIndex ? 'bg-primary' : 'bg-muted-foreground/30'
                  )}
                />
              ))}
            </div>
            <button
              type="button"
              onClick={() => setDeckIndex(deckIndex + 1)}
              disabled={deckIndex === deckItems.length - 1}
              className="p-1 rounded-full disabled:opacity-30 hover:bg-muted transition-colors"
              aria-label="Successivo"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="w-5 h-5"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <polyline points="9 18 15 12 9 6" />
              </svg>
            </button>
          </div>
        )}
      </div>
    </>
  );
}
