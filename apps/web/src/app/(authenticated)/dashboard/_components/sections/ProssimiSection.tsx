/**
 * ProssimiSection — priority #1 dashboard slot (Asse C, plan v2 WP2 T2).
 *
 * Renders 2-3 upcoming GameNight cards (Published + InProgress, ASC by date)
 * with an inline "+ Nuova" CTA in the header. Clicking a card opens the
 * GameNight cascade drawer via the asse-B cascade-store (`openDrawer('gameNightEvent', id)`).
 *
 * The "IN CORSO" badge highlights InProgress GameNights (asse A invariante #10:
 * max 1 live per GameNight, but UX displays the status whenever present).
 *
 * Pure props-driven: data fetching (T7) and wiring into `DashboardClient`
 * (T8) are intentionally out of scope.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

import type { RsvpStatus } from '@/lib/api/schemas/game-nights.schemas';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

import { DashboardSection } from './DashboardSection';
import { EmptySection } from './EmptySection';
import { ErrorBanner } from './ErrorBanner';
import { SectionSkeleton } from './SectionSkeleton';

/** Visible statuses on the dashboard upcoming list (Asse A semantic mapping). */
export type ProssimiStatus = 'Published' | 'InProgress';

export interface ProssimiGameNightCard {
  readonly id: string;
  readonly title: string;
  /** ISO datetime — sort key (ascending). */
  readonly date: string;
  readonly status: ProssimiStatus;
  readonly rsvpConfirmedCount: number;
  readonly rsvpPendingCount: number;
  readonly rsvpTotalCount: number;
  // #2978 (invariante #17): the viewer's own RSVP status; when 'Pending' the card shows the
  // pending-invitee treatment (badge + inline RSVP). Omitted/null for non-invitees.
  readonly viewerRsvpStatus?: RsvpStatus | null;
}

export type ProssimiSectionState = 'default' | 'empty' | 'loading' | 'error';

export interface ProssimiSectionProps {
  readonly state: ProssimiSectionState;
  readonly gameNights?: readonly ProssimiGameNightCard[];
  readonly onRetry?: () => void;
  // #2978 (invariante #17): inline RSVP handler for pending-invitee cards.
  readonly onRsvp?: (id: string, response: RsvpStatus) => void;
  // #3191: disable the inline RSVP CTAs when the viewer is offline
  // (mirrors HomeFeed / PR #3189 — a doomed request against an unreachable server).
  readonly isOffline?: boolean;
  // #3191: id of the game night whose RSVP mutation is currently in flight
  // (anti-double-submit), or null/undefined when idle. Threaded from DashboardClient,
  // which owns the `useRsvpGameNight` mutation.
  readonly pendingRsvpId?: string | null;
}

const STATUS_BADGE_LABEL: Record<ProssimiStatus, string> = {
  Published: 'Pianificata',
  InProgress: 'IN CORSO',
};

const SECTION_ICON = '🎲';
const SECTION_TITLE = 'Prossimi';
const VIEW_ALL_HREF = '/game-nights';
const VIEW_ALL_LABEL = 'Vedi tutte';
const NEW_HREF = '/game-nights/new';

export function ProssimiSection({
  state,
  gameNights,
  onRetry,
  onRsvp,
  isOffline,
  pendingRsvpId,
}: ProssimiSectionProps): JSX.Element {
  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);

  // Empty state — entity-tinted CTA card replaces the body.
  if (state === 'empty') {
    return (
      <DashboardSection
        sectionId="prossimi"
        entity="event"
        title={SECTION_TITLE}
        icon={SECTION_ICON}
      >
        <div data-testid="prossimi-empty">
          <EmptySection
            entity="event"
            icon="🎉"
            message="Nessuna Game Night in arrivo. Pianifica la prossima serata di gioco con i tuoi amici."
            cta="+ Crea la tua prima Game Night"
            ctaHref={NEW_HREF}
          />
        </div>
      </DashboardSection>
    );
  }

  // Loading state — twin skeletons preserve the 2-3 card grid silhouette.
  if (state === 'loading') {
    return (
      <DashboardSection
        sectionId="prossimi"
        entity="event"
        title={SECTION_TITLE}
        icon={SECTION_ICON}
      >
        <div className="grid gap-3 sm:grid-cols-2" data-testid="prossimi-skeleton">
          <SectionSkeleton />
          <SectionSkeleton />
        </div>
      </DashboardSection>
    );
  }

  // Error state — danger-tinted banner with optional retry.
  if (state === 'error') {
    return (
      <DashboardSection
        sectionId="prossimi"
        entity="event"
        title={SECTION_TITLE}
        icon={SECTION_ICON}
      >
        <div data-testid="prossimi-error">
          <ErrorBanner
            labels={{
              title: 'Errore di caricamento',
              message: 'Impossibile caricare le prossime Game Night.',
              retry: 'Riprova',
            }}
            onRetry={onRetry}
          />
        </div>
      </DashboardSection>
    );
  }

  // Default state — render cards sorted ASC by date.
  const items = gameNights ?? [];
  const sortedItems = [...items].sort(
    (a, b) => new Date(a.date).getTime() - new Date(b.date).getTime()
  );

  const handleCardClick = (id: string): void => {
    // #1929 WP4: migrated 'event' → 'gameNightEvent' so the cascade drawer
    // renders GameNightEventDrawerContent (with Giocatori tab + pushDrawer)
    // instead of EventDrawerContent which expects a different data shape.
    openDrawer('gameNightEvent', id);
  };

  return (
    <DashboardSection
      sectionId="prossimi"
      entity="event"
      title={SECTION_TITLE}
      icon={SECTION_ICON}
      count={sortedItems.length}
      viewAllHref={VIEW_ALL_HREF}
      viewAllLabel={VIEW_ALL_LABEL}
      headerExtra={
        <Link
          href={NEW_HREF}
          data-testid="prossimi-cta-new"
          className="ml-1 inline-flex items-center rounded-md bg-[hsl(var(--c-event))] px-2.5 py-1 font-quicksand text-[11px] font-extrabold text-[#fff]"
        >
          + Nuova
        </Link>
      }
    >
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3" data-testid="prossimi-cards">
        {sortedItems.map(gn => {
          // #2978 (invariante #17): pending invitee → semitransparent card + "Da confermare"
          // badge + inline RSVP bar. The RSVP buttons live OUTSIDE the card <button> to avoid
          // nested interactive elements.
          const isPending = gn.viewerRsvpStatus === 'Pending';
          // #3191: block the RSVP CTAs when offline, or while THIS card's RSVP is in
          // flight (double-submit). Correlate by id so a sibling card's in-flight RSVP
          // does not freeze the others.
          const rsvpDisabled = Boolean(isOffline) || pendingRsvpId === gn.id;
          return (
            <li key={gn.id}>
              <button
                type="button"
                onClick={() => handleCardClick(gn.id)}
                data-testid={`prossimi-card-${gn.id}`}
                className={clsx(
                  'flex w-full flex-col gap-2 rounded-[10px] border border-border bg-background p-3 text-left transition-colors hover:border-border-strong',
                  isPending && 'opacity-70'
                )}
              >
                <header className="flex items-start justify-between gap-2">
                  <time
                    className="font-mono text-[10px] font-extrabold uppercase text-muted-foreground"
                    dateTime={gn.date}
                  >
                    {formatDateMicroLabel(gn.date)}
                  </time>
                  <div className="flex items-center gap-1">
                    {isPending && (
                      <span
                        data-testid={`prossimi-pending-${gn.id}`}
                        className="rounded-full bg-warning px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase text-[#fff]"
                      >
                        Da confermare
                      </span>
                    )}
                    {gn.status === 'InProgress' && (
                      <span
                        data-testid={`prossimi-badge-${gn.id}`}
                        className="rounded-full bg-[hsl(var(--c-danger))] px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase text-[#fff]"
                      >
                        {STATUS_BADGE_LABEL.InProgress}
                      </span>
                    )}
                  </div>
                </header>
                <h3 className="line-clamp-2 font-quicksand text-[13px] font-extrabold text-foreground">
                  {gn.title}
                </h3>
                <p className="font-mono text-[10px] font-semibold text-muted-foreground">
                  {formatRsvpSummary(gn)}
                </p>
              </button>
              {isPending && onRsvp && (
                <div
                  data-testid={`prossimi-rsvp-${gn.id}`}
                  className="mt-1.5 flex gap-1.5"
                  title={isOffline ? 'Offline — RSVP disponibile alla riconnessione' : undefined}
                >
                  <button
                    type="button"
                    onClick={() => onRsvp(gn.id, 'Accepted')}
                    disabled={rsvpDisabled}
                    className="rounded-md bg-entity-toolkit px-2.5 py-1 font-quicksand text-[11px] font-extrabold text-[#fff] disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Conferma
                  </button>
                  <button
                    type="button"
                    onClick={() => onRsvp(gn.id, 'Maybe')}
                    disabled={rsvpDisabled}
                    className="rounded-md border border-border px-2.5 py-1 font-quicksand text-[11px] font-bold text-muted-foreground disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Forse
                  </button>
                  <button
                    type="button"
                    onClick={() => onRsvp(gn.id, 'Declined')}
                    disabled={rsvpDisabled}
                    className="rounded-md border border-border px-2.5 py-1 font-quicksand text-[11px] font-bold text-muted-foreground disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Declina
                  </button>
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </DashboardSection>
  );
}

function formatDateMicroLabel(iso: string): string {
  try {
    const date = new Date(iso);
    const now = new Date();
    if (date.toDateString() === now.toDateString()) {
      return 'oggi';
    }
    return date.toLocaleDateString('it-IT', {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
    });
  } catch {
    return '—';
  }
}

function formatRsvpSummary(gn: ProssimiGameNightCard): string {
  if (gn.rsvpPendingCount === 0) {
    return `${gn.rsvpConfirmedCount} ✓`;
  }
  return `${gn.rsvpConfirmedCount}/${gn.rsvpTotalCount} pending`;
}
