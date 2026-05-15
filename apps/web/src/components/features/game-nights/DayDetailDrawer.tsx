/**
 * DayDetailDrawer - v2 SP4 #1170 commit 2
 *
 * Bottom sheet (mobile) / right drawer (desktop) listing all events on a
 * specific day. Renders a list of `GameNightListCard` and a dashed "add
 * here" button.
 *
 * Mapped from `admin-mockups/design_files/sp4-game-nights-index.jsx`
 * (DayDetailDrawer).
 */

'use client';

import { useEffect, useId, useRef } from 'react';

import clsx from 'clsx';

import { useFocusTrap } from '@/hooks/useFocusTrap';
import type { GameNightVM } from '@/lib/game-nights/view-model';

import {
  GameNightListCard,
  type GameNightListCardAction,
  type GameNightListCardLabels,
} from './GameNightListCard';

import type { AvatarPlayer } from './PlayerAvatars';

export interface DayDetailDrawerLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly close: string;
  readonly addOnDay: string;
  readonly card: GameNightListCardLabels;
  readonly monthAbbrev: string;
}

export interface DayDetailDrawerProps {
  readonly open: boolean;
  readonly day: number;
  readonly events: readonly GameNightVM[];
  readonly isMobile?: boolean;
  readonly labels: DayDetailDrawerLabels;
  readonly onClose: () => void;
  readonly onAddOnDay?: () => void;
  readonly onCardAction?: (id: string, action: GameNightListCardAction) => void;
  readonly resolvePlayers?: (vm: GameNightVM) => readonly AvatarPlayer[];
  readonly resolveGameTitle?: (vm: GameNightVM) => string | undefined;
}

export function DayDetailDrawer({
  open,
  day,
  events,
  isMobile = false,
  labels,
  onClose,
  onAddOnDay,
  onCardAction,
  resolvePlayers,
  resolveGameTitle,
}: DayDetailDrawerProps): React.JSX.Element | null {
  const headingId = useId();
  const closeBtnRef = useRef<HTMLButtonElement | null>(null);
  const drawerRef = useRef<HTMLElement | null>(null);

  useFocusTrap(drawerRef, open);

  useEffect(() => {
    if (!open) return;
    closeBtnRef.current?.focus();
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent): void => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      role="presentation"
      onClick={onClose}
      data-testid="game-nights-day-detail-backdrop"
      className="absolute inset-0 z-50 flex items-end justify-stretch bg-foreground/50 md:items-stretch md:justify-end"
    >
      <aside
        ref={drawerRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={headingId}
        data-testid="game-nights-day-detail-drawer"
        onClick={e => e.stopPropagation()}
        className={clsx(
          'max-h-[88vh] w-full max-w-full overflow-auto rounded-t-xl bg-background shadow-xl',
          'md:h-full md:max-h-full md:w-[480px] md:rounded-tr-none',
          'motion-safe:animate-in motion-safe:slide-in-from-bottom motion-safe:duration-300 md:motion-safe:slide-in-from-right'
        )}
      >
        {isMobile && (
          <div
            aria-hidden="true"
            className="mx-auto mt-2.5 h-1 w-9 rounded-full bg-border-strong"
          />
        )}

        <header className="sticky top-0 z-[1] flex items-start gap-3 border-b border-border bg-background p-4">
          <div className="flex h-[50px] w-[50px] flex-col items-center justify-center rounded-md border border-entity-event/22 bg-entity-event/12">
            <span className="font-mono text-[9px] font-extrabold uppercase tracking-wider text-entity-event">
              {labels.monthAbbrev}
            </span>
            <span className="font-display text-xl font-extrabold leading-none tabular-nums text-entity-event">
              {day}
            </span>
          </div>
          <div className="min-w-0 flex-1">
            <h2 id={headingId} className="font-display text-base font-extrabold text-foreground">
              {labels.title}
            </h2>
            <p className="font-mono text-xs font-bold text-muted-foreground">{labels.subtitle}</p>
          </div>
          <button
            ref={closeBtnRef}
            type="button"
            aria-label={labels.close}
            onClick={onClose}
            className="rounded-md border border-border bg-card px-2 py-1 font-mono text-sm font-extrabold text-foreground"
          >
            ✕
          </button>
        </header>

        <div className="flex flex-col gap-2.5 p-4 pt-3.5">
          {events.map(vm => (
            <GameNightListCard
              key={vm.id}
              vm={vm}
              isMobile={isMobile}
              labels={labels.card}
              onAction={onCardAction}
              gameTitle={resolveGameTitle?.(vm)}
              players={resolvePlayers?.(vm)}
            />
          ))}
          {onAddOnDay && (
            <button
              type="button"
              onClick={onAddOnDay}
              className="rounded-lg border-2 border-dashed border-border-strong px-3 py-3 font-display text-xs font-extrabold text-muted-foreground"
            >
              {labels.addOnDay}
            </button>
          )}
        </div>
      </aside>
    </div>
  );
}
