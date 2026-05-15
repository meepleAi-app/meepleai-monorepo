/**
 * GameNightRsvpActionBar - v2 SP7 #951 commit 2
 *
 * Three-button RSVP control (Accept / Maybe / Decline). Replaces the inline
 * Card+buttons block in legacy game-nights/[id]/page.tsx.
 *
 * Behavioural contract (AC-H1 + AC-H2):
 *   - `currentResponse` reflects the viewer's RSVP from the server snapshot.
 *   - `pendingResponse` reflects an in-flight optimistic mutation (button shows
 *     a "saving" indicator). When set, all three buttons are disabled to
 *     prevent double-submit while the network call is in-flight.
 *   - `disabled` overrides everything (e.g. terminal Cancelled/Expired states,
 *     verified upstream via rsvp-state-machine.evaluateRsvpTransition).
 *
 * The component does NOT call the network — it only emits `onSelect(response)`.
 * Hook composition layer (commit 3) wires this to `useOptimisticMutation`.
 *
 * AC: T A V
 */

'use client';

import clsx from 'clsx';

import { Button } from '@/components/ui/primitives/button';
import type { RsvpStatus } from '@/lib/api/schemas/game-nights.schemas';

type RsvpResponse = Exclude<RsvpStatus, 'Pending'>;

export interface GameNightRsvpActionBarLabels {
  readonly sectionTitle: string;
  readonly accept: string;
  readonly maybe: string;
  readonly decline: string;
  readonly saving: string;
}

export interface GameNightRsvpActionBarProps {
  /** Localized labels. Caller resolves t('gameNightDetail.rsvp.*'). */
  readonly labels: GameNightRsvpActionBarLabels;
  /** Current server-truth response, or undefined for pending. */
  readonly currentResponse: RsvpResponse | undefined;
  /** In-flight optimistic response, or null when no mutation pending. */
  readonly pendingResponse: RsvpResponse | null;
  /** When true, all buttons disabled regardless of pending state. */
  readonly disabled?: boolean;
  /** Click handler. The hook layer is responsible for transition validation. */
  readonly onSelect: (response: RsvpResponse) => void;
  readonly className?: string;
}

interface ButtonConfig {
  readonly response: RsvpResponse;
  readonly icon: string;
  readonly labelKey: keyof Pick<GameNightRsvpActionBarLabels, 'accept' | 'maybe' | 'decline'>;
  /**
   * Selected-state highlight (when currentResponse matches). Uses entity tokens
   * to convey semantic meaning at a glance.
   */
  readonly selectedClass: string;
}

const BUTTONS: readonly ButtonConfig[] = [
  {
    response: 'Accepted',
    icon: '✓',
    labelKey: 'accept',
    selectedClass: 'border-success bg-success/10 text-success hover:bg-success/15',
  },
  {
    response: 'Maybe',
    icon: '?',
    labelKey: 'maybe',
    selectedClass: 'border-warning bg-warning/10 text-warning hover:bg-warning/15',
  },
  {
    response: 'Declined',
    icon: '×',
    labelKey: 'decline',
    selectedClass: 'border-destructive bg-destructive/10 text-destructive hover:bg-destructive/15',
  },
];

export function GameNightRsvpActionBar({
  labels,
  currentResponse,
  pendingResponse,
  disabled = false,
  onSelect,
  className,
}: GameNightRsvpActionBarProps): React.JSX.Element {
  const anyPending = pendingResponse !== null;
  const allDisabled = disabled || anyPending;

  return (
    <section
      data-testid="game-night-rsvp-action-bar"
      aria-label={labels.sectionTitle}
      className={clsx('rounded-lg border border-border bg-card p-4', className)}
    >
      <h2 className="mb-3 font-display text-base font-extrabold text-foreground">
        {labels.sectionTitle}
      </h2>

      <div className="flex flex-wrap gap-2">
        {BUTTONS.map(btn => {
          const isCurrent = currentResponse === btn.response;
          const isPending = pendingResponse === btn.response;
          return (
            <Button
              key={btn.response}
              type="button"
              variant="outline"
              size="sm"
              data-testid={`rsvp-btn-${btn.response.toLowerCase()}`}
              data-selected={isCurrent ? 'true' : 'false'}
              data-pending={isPending ? 'true' : 'false'}
              aria-pressed={isCurrent}
              disabled={allDisabled}
              onClick={() => onSelect(btn.response)}
              className={clsx('flex-1 min-w-[120px]', isCurrent && !isPending && btn.selectedClass)}
            >
              <span aria-hidden="true" className="mr-1 text-base leading-none">
                {btn.icon}
              </span>
              <span>{isPending ? labels.saving : labels[btn.labelKey]}</span>
            </Button>
          );
        })}
      </div>
    </section>
  );
}
