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

import { Button } from '@/components/ui/primitives/button';
import type { RsvpStatus } from '@/lib/api/schemas/game-nights.schemas';
import { cn } from '@/lib/utils';

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
  /**
   * Render mode (issue #1169). Defaults to `'authenticated'`. In `'public'`
   * mode the "Maybe" affordance is hidden — the public RSVP backend only
   * supports `Accepted` and `Declined`, mirroring the
   * `POST /api/v1/game-nights/invitations/{token}/respond` validator.
   */
  readonly mode?: 'authenticated' | 'public';
  /**
   * Compact rendering below `md` (#2989 Screen C). When true, the outer card
   * chrome (border / surface / padding) and the visible heading are dropped
   * *only at <md* so the control can sit directly inside a mobile sticky bar
   * that already supplies the surface — keeping the pinned bar short enough to
   * clear with a modest scroll reservation. At md+ the full card is preserved
   * (desktop is unchanged). The section keeps its `aria-label` landmark, so the
   * heading is only visually hidden on mobile, never removed for assistive tech.
   */
  readonly compact?: boolean;
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
    selectedClass:
      'border-[hsl(var(--c-success))] bg-[hsl(var(--c-success)/0.1)] text-[hsl(var(--c-success-ink))] hover:bg-[hsl(var(--c-success)/0.15)]',
  },
  {
    response: 'Maybe',
    icon: '?',
    labelKey: 'maybe',
    selectedClass:
      'border-[hsl(var(--c-warning))] bg-[hsl(var(--c-warning)/0.1)] text-[hsl(var(--c-warning-ink))] hover:bg-[hsl(var(--c-warning)/0.15)]',
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
  mode = 'authenticated',
  compact = false,
  className,
}: GameNightRsvpActionBarProps): React.JSX.Element {
  const anyPending = pendingResponse !== null;
  const allDisabled = disabled || anyPending;
  // Public RSVP backend (POST /game-nights/invitations/{token}/respond) only
  // accepts Accepted/Declined per the validator. Surfacing Maybe in that mode
  // would let users click a button that would return 400 (issue #1169).
  const visibleButtons = mode === 'public' ? BUTTONS.filter(b => b.response !== 'Maybe') : BUTTONS;

  return (
    <section
      data-testid="game-night-rsvp-action-bar"
      data-mode={mode}
      data-compact={compact ? 'true' : 'false'}
      aria-label={labels.sectionTitle}
      className={cn(
        'rounded-lg border border-border bg-card p-4',
        // Compact (<md only): shed the card chrome so the mobile sticky wrapper
        // is the sole surface; the full card returns at md+. tailwind-merge lets
        // these max-md overrides win over the base above at <md.
        compact && 'max-md:rounded-none max-md:border-0 max-md:bg-transparent max-md:p-0',
        className
      )}
    >
      <h2
        className={cn(
          'mb-3 font-display text-base font-extrabold text-foreground',
          // Hidden on mobile in compact mode (the sticky bar needs no heading;
          // the section aria-label keeps the landmark named for a11y).
          compact && 'max-md:hidden'
        )}
      >
        {labels.sectionTitle}
      </h2>

      <div className="flex flex-wrap gap-2">
        {visibleButtons.map(btn => {
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
              className={cn(
                // #2989 Screen C: 44px touch-target floor on mobile (SP8 B-02
                // regression guard). `size="sm"` renders h-9 (36px); min-height
                // wins over the fixed height, lifting the tap area to 44px. At
                // ≤~380px the three min-w-[120px] CTAs wrap to two rows — the
                // sticky-bar clearance in GameNightDetailView accounts for that.
                'flex-1 min-w-[120px] min-h-[44px]',
                isCurrent && !isPending && btn.selectedClass
              )}
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
