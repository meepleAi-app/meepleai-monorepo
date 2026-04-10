'use client';

import { useCallback, useEffect, useState } from 'react';

import {
  Gamepad2,
  Bot,
  Wrench,
  Hash,
  X,
} from 'lucide-react';

import { cn } from '@/lib/utils';
import {
  useContextualHandStore,
  selectContext,
  selectIsLoading,
} from '@/stores/contextual-hand';

import { ContextualHandSlot, type HandSlotType } from './ContextualHandSlot';

// ─── Tab config ───────────────────────────────────────────────────────────

const TABS: { type: HandSlotType; icon: typeof Hash; label: string }[] = [
  { type: 'session', icon: Hash, label: 'Partita' },
  { type: 'game', icon: Gamepad2, label: 'Gioco' },
  { type: 'agent', icon: Bot, label: 'Agente' },
  { type: 'toolkit', icon: Wrench, label: 'Toolkit' },
];

// ─── Component ────────────────────────────────────────────────────────────

/**
 * ContextualHandBottomBar — mobile bottom bar (<md only).
 *
 * Shows 4 icon buttons. Tapping one opens a bottom sheet with that slot's
 * details and actions. Hidden when context is idle (no active session).
 */
export function ContextualHandBottomBar() {
  const context = useContextualHandStore(selectContext);
  const isLoading = useContextualHandStore(selectIsLoading);
  const initialize = useContextualHandStore(s => s.initialize);
  const [expandedTab, setExpandedTab] = useState<HandSlotType | null>(null);

  useEffect(() => {
    initialize();
  }, [initialize]);

  const closeSheet = useCallback(() => setExpandedTab(null), []);

  // Don't render when idle or loading
  if (context === 'idle' || isLoading) return null;

  return (
    <>
      {/* ── Bottom nav bar ──────────────────────────────────────── */}
      <nav
        data-testid="contextual-hand-bottom-bar"
        aria-label="Azioni partita"
        className={cn(
          'md:hidden fixed bottom-0 inset-x-0 z-30',
          'border-t border-[var(--nh-border-default)]',
          'bg-[var(--nh-bg-base)]/95 backdrop-blur-sm',
          'supports-[backdrop-filter]:bg-[var(--nh-bg-base)]/60'
        )}
        style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}
      >
        <div className="flex items-center justify-around h-16 px-1">
          {TABS.map(tab => {
            const Icon = tab.icon;
            const isActive = expandedTab === tab.type;
            return (
              <button
                key={tab.type}
                onClick={() => setExpandedTab(isActive ? null : tab.type)}
                className={cn(
                  'flex flex-col items-center justify-center gap-0.5 rounded-lg px-2 py-1.5 min-w-[56px] min-h-[44px] transition-colors',
                  isActive
                    ? 'bg-primary/10 text-primary'
                    : 'text-muted-foreground hover:text-foreground'
                )}
                aria-label={tab.label}
                aria-expanded={isActive}
              >
                <Icon className="h-5 w-5" />
                <span className="text-[10px] font-medium leading-none">{tab.label}</span>
              </button>
            );
          })}
        </div>
      </nav>

      {/* ── Expanded bottom sheet ───────────────────────────────── */}
      {expandedTab && (
        <div className="md:hidden fixed inset-0 z-40" role="dialog" aria-modal="true">
          {/* Backdrop */}
          <div
            className="absolute inset-0 bg-black/40 animate-in fade-in duration-200"
            onClick={closeSheet}
            aria-hidden="true"
          />

          {/* Sheet */}
          <div
            className={cn(
              'absolute bottom-0 inset-x-0 max-h-[50vh] rounded-t-2xl',
              'border-t border-[var(--nh-border-default)] bg-[var(--nh-bg-base)]',
              'animate-in slide-in-from-bottom duration-300',
              'overflow-y-auto'
            )}
            style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}
          >
            {/* Drag handle */}
            <div className="sticky top-0 z-10 flex items-center justify-center bg-[var(--nh-bg-base)] pt-3 pb-1">
              <div className="h-1.5 w-12 rounded-full bg-muted-foreground/25" />
            </div>

            {/* Header */}
            <div className="flex items-center justify-between px-4 pb-2">
              <h3 className="font-quicksand text-sm font-semibold text-[var(--nh-text-default)]">
                {TABS.find(t => t.type === expandedTab)?.label}
              </h3>
              <button
                onClick={closeSheet}
                className="flex h-8 w-8 items-center justify-center rounded-full hover:bg-muted min-h-[44px] min-w-[44px]"
                aria-label="Chiudi"
              >
                <X className="h-4 w-4 text-muted-foreground" />
              </button>
            </div>

            {/* Slot content */}
            <div className="px-4 pb-4">
              <ContextualHandSlot slotType={expandedTab} />
            </div>
          </div>
        </div>
      )}

      {/* Spacer so page content doesn't hide behind bottom bar */}
      <div className="md:hidden h-16" aria-hidden="true" />
    </>
  );
}
