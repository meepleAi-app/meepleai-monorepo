/**
 * ProssimiSection — priority #1 dashboard slot (Asse C, plan v2 WP2 T2).
 *
 * Renders 2-3 upcoming GameNight cards (Published + InProgress, ASC by date)
 * with an inline "+ Nuova" CTA in the header. Clicking a card opens the
 * GameNight cascade drawer via the asse-B cascade-store (`openDrawer('event', id)`).
 *
 * The "IN CORSO" badge highlights InProgress GameNights (asse A invariante #10:
 * max 1 live per GameNight, but UX displays the status whenever present).
 *
 * Pure props-driven: data fetching (T7) and wiring into `DashboardClient`
 * (T8) are intentionally out of scope.
 */

'use client';

import type { JSX } from 'react';

import Link from 'next/link';

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
}

export type ProssimiSectionState = 'default' | 'empty' | 'loading' | 'error';

export interface ProssimiSectionProps {
  readonly state: ProssimiSectionState;
  readonly gameNights?: readonly ProssimiGameNightCard[];
  readonly onRetry?: () => void;
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

export function ProssimiSection({ state, gameNights, onRetry }: ProssimiSectionProps): JSX.Element {
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
        {sortedItems.map(gn => (
          <li key={gn.id}>
            <button
              type="button"
              onClick={() => handleCardClick(gn.id)}
              data-testid={`prossimi-card-${gn.id}`}
              className="flex w-full flex-col gap-2 rounded-[10px] border border-border bg-background p-3 text-left transition-colors hover:border-border-strong"
            >
              <header className="flex items-start justify-between gap-2">
                <time
                  className="font-mono text-[10px] font-extrabold uppercase text-muted-foreground"
                  dateTime={gn.date}
                >
                  {formatDateMicroLabel(gn.date)}
                </time>
                {gn.status === 'InProgress' && (
                  <span
                    data-testid={`prossimi-badge-${gn.id}`}
                    className="rounded-full bg-[hsl(var(--c-danger))] px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase text-[#fff]"
                  >
                    {STATUS_BADGE_LABEL.InProgress}
                  </span>
                )}
              </header>
              <h3 className="line-clamp-2 font-quicksand text-[13px] font-extrabold text-foreground">
                {gn.title}
              </h3>
              <p className="font-mono text-[10px] font-semibold text-muted-foreground">
                {formatRsvpSummary(gn)}
              </p>
            </button>
          </li>
        ))}
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
