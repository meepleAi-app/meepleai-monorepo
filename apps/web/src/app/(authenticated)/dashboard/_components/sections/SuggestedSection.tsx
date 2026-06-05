/**
 * SuggestedSection — priority #3 dashboard slot (Asse C plan v2 WP4 T4).
 *
 * Renders 4-6 "Potresti giocare" Game suggestion cards in a horizontal scroll
 * layout. Clicking a card navigates to `/library/[id]` (game detail).
 *
 * Empty/error states: HIDDEN (silent fallback per spec MAJ-6 matrix). This slot
 * is a recommendation surface — when there is nothing to recommend or the
 * source fails, we omit the section entirely rather than competing with
 * higher-priority CTAs in ProssimiSection (#1) and RecentiSection (#2).
 *
 * Pattern note (vs reusing `GamesCarousel`): the existing `GamesCarousel`
 * component already wraps `DashboardSection` internally and uses the full
 * `Game` schema (id/title/yearPublished/imageUrl/iconUrl). Wrapping it inside
 * another section would either nest two `<section>` headers or require
 * disabling its internal one. T2/T3 instead render their cards directly inside
 * a `DashboardSection`, and we follow the same pattern here for consistency,
 * using a lean `SuggestedGameCard` projection that drops Game-CRUD-only fields
 * (publisher, BGG id, year, etc.) and adds presentation hints
 * (`playerCount`, `durationMin`) the suggestion algorithm wants surfaced.
 *
 * MIN-2 roadmap (suggestion algorithm):
 *   MVP (fixture): owned games NOT played in last 30 days, sorted by play
 *   count DESC. Backend: future `GET /dashboard/suggestions` endpoint will
 *   compute this server-side from session history + library ownership.
 *   Future iterations may layer collaborative filtering (players in the
 *   user's friend graph who recently rated games highly) and BGG metadata
 *   similarity (same designer, mechanic overlap). See plan §"MIN-2".
 *
 * Pure props-driven: data fetching (T7) and wiring into `DashboardClient`
 * (T8) are intentionally out of scope.
 */

'use client';

import type { JSX } from 'react';

import Link from 'next/link';

import { DashboardSection } from './DashboardSection';
import { SectionSkeleton } from './SectionSkeleton';

export interface SuggestedGameCard {
  readonly id: string;
  readonly title: string;
  /** Optional cover URL; falls back to a gradient placeholder when absent. */
  readonly coverImageUrl?: string;
  /**
   * Display string for player count (e.g. "2-4" or "3"). Free-form to allow
   * BE-side formatting without locking the FE into numeric ranges.
   */
  readonly playerCount: string;
  /** Estimated play duration in minutes (display only). */
  readonly durationMin: number;
}

export type SuggestedSectionState = 'default' | 'empty' | 'loading' | 'error';

export interface SuggestedSectionProps {
  readonly state: SuggestedSectionState;
  readonly games?: readonly SuggestedGameCard[];
  /** Wired but won't render in error state (silent fallback). Reserved for future MIN-2 iterations. */
  readonly onRetry?: () => void;
}

const SECTION_ICON = '🎲';
const SECTION_TITLE = 'Potresti giocare';
const SECTION_ID = 'suggested';

/** Deterministic gradient placeholder from a game id (matches GamesCarousel:28 visual language). */
function coverGradient(seed: string): string {
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) % 360;
  const h2 = (h + 340) % 360;
  const h3 = (h + 30) % 360;
  return `linear-gradient(135deg, hsl(${h}, 70%, 55%), hsl(${h2}, 50%, 30%) 60%, hsl(${h3}, 60%, 40%))`;
}

export function SuggestedSection({ state, games }: SuggestedSectionProps): JSX.Element | null {
  // Empty + error states — hidden entirely (spec MAJ-6 matrix, silent fallback).
  // Priority #3 slot must not surface failure UX that competes with ProssimiSection (#1) CTA.
  if (state === 'empty' || state === 'error') {
    return null;
  }

  // Loading state — twin skeletons preserve the horizontal scroll silhouette.
  if (state === 'loading') {
    return (
      <DashboardSection
        sectionId={SECTION_ID}
        entity="game"
        title={SECTION_TITLE}
        icon={SECTION_ICON}
      >
        <div className="grid gap-3 sm:grid-cols-2" data-testid="suggested-skeleton">
          <SectionSkeleton />
          <SectionSkeleton />
        </div>
      </DashboardSection>
    );
  }

  // Default state — render 4-6 horizontal scroll cards.
  const items = games ?? [];

  // Defensive: if the fixture algorithm returns 0 rows in default state, treat as empty.
  if (items.length === 0) {
    return null;
  }

  return (
    <DashboardSection
      sectionId={SECTION_ID}
      entity="game"
      title={SECTION_TITLE}
      icon={SECTION_ICON}
      count={items.length}
    >
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3" data-testid="suggested-cards">
        {items.map(g => (
          <li key={g.id}>
            <Link
              href={`/library/${g.id}`}
              data-testid={`suggested-card-${g.id}`}
              data-slot="dashboard-suggested-card"
              className="flex w-full flex-col gap-2 overflow-hidden rounded-[10px] border border-border bg-background transition-colors hover:border-border-strong"
            >
              <div
                aria-hidden="true"
                className="relative flex items-center justify-center overflow-hidden"
                style={{
                  aspectRatio: '5 / 3',
                  background: coverGradient(g.id),
                }}
                data-testid={`suggested-cover-${g.id}`}
              >
                {g.coverImageUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element -- avoid Next/Image domain config for community-provided BGG URLs (alignment with GamesCarousel:86)
                  <img
                    src={g.coverImageUrl}
                    alt=""
                    loading="lazy"
                    className="absolute inset-0 h-full w-full object-cover"
                  />
                ) : (
                  <span
                    className="text-[36px]"
                    style={{ filter: 'drop-shadow(0 2px 8px rgba(0,0,0,0.35))' }}
                  >
                    🎲
                  </span>
                )}
              </div>
              <div className="flex flex-col gap-1 px-3 pb-3 pt-1">
                <h3 className="line-clamp-2 font-quicksand text-[13px] font-extrabold text-foreground">
                  {g.title}
                </h3>
                <p className="font-mono text-[10px] font-semibold text-muted-foreground">
                  {formatMetaLabel(g)}
                </p>
              </div>
            </Link>
          </li>
        ))}
      </ul>
    </DashboardSection>
  );
}

function formatMetaLabel(g: SuggestedGameCard): string {
  return `${g.playerCount} giocatori · ${g.durationMin} min`;
}
