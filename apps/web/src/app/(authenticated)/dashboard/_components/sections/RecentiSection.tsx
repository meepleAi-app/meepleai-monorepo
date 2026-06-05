/**
 * RecentiSection — priority #2 dashboard slot (Asse C, plan v2 WP3 T3).
 *
 * Renders 2-3 completed GameNight cards (DESC by date) with MVP, mini cover
 * thumbnails (max 3), session count, and a "Vedi tutti i completati" footer
 * link (MAJ-7 pagination). Clicking a card opens the GameNight cascade drawer
 * via the asse-B cascade-store (`openDrawer('event', id)`).
 *
 * Invariante #7 (CRITICAL): 1 card = 1 GameNight wrapper. We DO NOT render N
 * cards per N session inside the GameNight; sessionCount is shown as text.
 *
 * Empty state: hidden (no section rendered, per spec MAJ-6 matrix). New users
 * have nothing yet completed; surfacing an empty CTA would compete with
 * ProssimiSection's primary onboarding CTA.
 *
 * Pure props-driven: data fetching (T7) and wiring into `DashboardClient`
 * (T8) are intentionally out of scope.
 */

'use client';

import type { JSX } from 'react';

import Link from 'next/link';

import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

import { DashboardSection } from './DashboardSection';
import { ErrorBanner } from './ErrorBanner';
import { SectionSkeleton } from './SectionSkeleton';

export interface RecentiGameNightCard {
  readonly id: string;
  readonly title: string;
  /** ISO datetime — sort key (descending, most recent first). */
  readonly date: string;
  /** Number of completed sessions inside the GameNight (Invariante #7: display only, not card count). */
  readonly sessionCount: number;
  /** Optional MVP display name; omitted when null/undefined. */
  readonly mvpDisplayName?: string;
  /** 1-3 mini cover URLs (truncated to 3 visually). */
  readonly gamePreviewThumbnails: readonly string[];
}

export type RecentiSectionState = 'default' | 'empty' | 'loading' | 'error';

export interface RecentiSectionProps {
  readonly state: RecentiSectionState;
  readonly gameNights?: readonly RecentiGameNightCard[];
  readonly onRetry?: () => void;
}

const SECTION_ICON = '📚';
const SECTION_TITLE = 'Recenti';
const SECTION_ID = 'recenti';
const VIEW_ALL_HREF = '/game-nights?status=completed';
const VIEW_ALL_LABEL = 'Vedi tutti i completati';
const MAX_THUMBNAILS = 3;

export function RecentiSection({
  state,
  gameNights,
  onRetry,
}: RecentiSectionProps): JSX.Element | null {
  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);

  // Empty state — hidden entirely (spec MAJ-6 matrix). No DOM emitted.
  if (state === 'empty') {
    return null;
  }

  // Loading state — twin skeletons preserve the 2-3 card grid silhouette.
  if (state === 'loading') {
    return (
      <DashboardSection
        sectionId={SECTION_ID}
        entity="event"
        title={SECTION_TITLE}
        icon={SECTION_ICON}
      >
        <div className="grid gap-3 sm:grid-cols-2" data-testid="recenti-skeleton">
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
        sectionId={SECTION_ID}
        entity="event"
        title={SECTION_TITLE}
        icon={SECTION_ICON}
      >
        <div data-testid="recenti-error">
          <ErrorBanner
            labels={{
              title: 'Errore di caricamento',
              message: 'Impossibile caricare le Game Night recenti.',
              retry: 'Riprova',
            }}
            onRetry={onRetry}
          />
        </div>
      </DashboardSection>
    );
  }

  // Default state — render cards sorted DESC by date (most recent first).
  const items = gameNights ?? [];
  const sortedItems = [...items].sort(
    (a, b) => new Date(b.date).getTime() - new Date(a.date).getTime()
  );

  const handleCardClick = (id: string): void => {
    openDrawer('event', id);
  };

  return (
    <DashboardSection
      sectionId={SECTION_ID}
      entity="event"
      title={SECTION_TITLE}
      icon={SECTION_ICON}
      count={sortedItems.length}
    >
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3" data-testid="recenti-cards">
        {sortedItems.map(gn => {
          // Invariante #7: 1 GameNight wrapper card, NOT N session cards.
          const thumbnails = gn.gamePreviewThumbnails.slice(0, MAX_THUMBNAILS);
          const sessionsLabel = gn.sessionCount === 1 ? '1 partita' : `${gn.sessionCount} partite`;
          return (
            <li key={gn.id}>
              <button
                type="button"
                onClick={() => handleCardClick(gn.id)}
                data-testid={`recenti-card-${gn.id}`}
                className="flex w-full flex-col gap-2 rounded-[10px] border border-border bg-background p-3 text-left transition-colors hover:border-border-strong"
              >
                <header className="flex items-start justify-between gap-2">
                  <time
                    className="font-mono text-[10px] font-extrabold uppercase text-muted-foreground"
                    dateTime={gn.date}
                  >
                    {formatRelativeDate(gn.date)}
                  </time>
                </header>
                <h3 className="line-clamp-2 font-quicksand text-[13px] font-extrabold text-foreground">
                  {gn.title}
                </h3>
                <p className="font-mono text-[10px] font-semibold text-muted-foreground">
                  {sessionsLabel}
                </p>
                {gn.mvpDisplayName && (
                  <p className="flex items-center gap-1.5 text-[11px]">
                    <span className="rounded-sm bg-[hsl(var(--c-event)/0.12)] px-1.5 py-0.5 font-mono text-[9px] font-extrabold uppercase text-[hsl(var(--c-event))]">
                      MVP
                    </span>
                    <span className="font-quicksand font-bold text-foreground">
                      {gn.mvpDisplayName}
                    </span>
                  </p>
                )}
                {thumbnails.length > 0 && (
                  <ul
                    className="flex gap-1"
                    aria-label="Giochi giocati"
                    data-testid={`recenti-thumbnails-${gn.id}`}
                  >
                    {thumbnails.map((url, i) => (
                      <li
                        key={`${gn.id}-thumb-${i}`}
                        className="h-8 w-8 overflow-hidden rounded-sm border border-border bg-muted"
                      >
                        {/* eslint-disable-next-line @next/next/no-img-element -- avoid Next/Image domain config for community-provided BGG URLs (alignment with GamesCarousel:86) */}
                        <img src={url} alt="" className="h-full w-full object-cover" />
                      </li>
                    ))}
                  </ul>
                )}
              </button>
            </li>
          );
        })}
      </ul>
      {/* MAJ-7: pagination footer link to the full completed list. */}
      <footer className="mt-4 border-t border-border pt-3 text-center">
        <Link
          href={VIEW_ALL_HREF}
          data-testid="recenti-vedi-tutti"
          className="inline-flex items-center gap-1 rounded-md px-2 py-1 font-quicksand text-[11px] font-bold text-[hsl(var(--c-event))] sm:text-xs"
        >
          {VIEW_ALL_LABEL}
          <span aria-hidden="true">→</span>
        </Link>
      </footer>
    </DashboardSection>
  );
}

function formatRelativeDate(iso: string): string {
  try {
    const date = new Date(iso);
    const now = new Date();
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
    const startOfDate = new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
    const diffDays = Math.floor((startOfToday - startOfDate) / 86_400_000);

    if (diffDays === 0) return 'oggi';
    if (diffDays === 1) return 'ieri';
    if (diffDays > 1 && diffDays < 7) return `${diffDays}g fa`;
    return date.toLocaleDateString('it-IT', {
      day: 'numeric',
      month: 'short',
    });
  } catch {
    return '—';
  }
}
