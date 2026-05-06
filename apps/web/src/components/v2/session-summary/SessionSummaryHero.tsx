/**
 * SessionSummaryHero — Wave D.3 v2 component (Issue #756).
 *
 * 3-place podium with center-elevated 1st, smaller 2nd/3rd flanks. When the
 * top-ranked group is tied, ALL tied 1st-place participants render at the
 * center elevation with a "T" tied-badge and a banner above the podium.
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary-parts.jsx (SummaryHeroPodium)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §5.1.
 *
 * MeepleCard divergence (Gate C, contract §5):
 *   The bespoke 3-place vertical layout with confetti container, gradient
 *   pedestal heights, crown decoration, and tied-state vertical stacking
 *   cannot be expressed via MeepleCard's avatar/title/subtitle/rating API.
 *   Direct DOM composition required.
 *
 * Pure component: all i18n strings injected via `labels` — no `useTranslation`.
 *
 * A11y:
 *   - `<section role="region" aria-labelledby={headlineId}>`
 *   - Each podium place `aria-label={t('hero.podiumPlaceAriaLabel')}` per
 *     contract §5.1 (resolved by orchestrator with ICU substitution).
 *   - Confetti `aria-hidden="true"` (parent owns accessible name).
 *   - Reduced-motion fallback: static medal emoji `<span role="img"
 *     aria-label={confettiSkippedLabel}>🏅</span>` rendered in a sibling that
 *     CSS reveals via `@media (prefers-reduced-motion: reduce)`.
 *
 * WCAG: AA contrast for entity-coloured text/backgrounds. Tied-badge "T"
 * uses toolkit l31 → 4.64:1 vs white.
 */

'use client';

import type { ReactElement } from 'react';
import { useId } from 'react';

import clsx from 'clsx';

import type { RankedParticipant } from '@/lib/sessions-summary/tie-groups';

import { Confetti } from './Confetti';

export interface SessionSummaryHeroLabels {
  /** Heading text — already resolved by orchestrator (e.g., "Riepilogo partita"). */
  readonly title: string;
  /** Banner shown when 2+ participants tie at rank 1 (resolved by orchestrator). */
  readonly tiedBanner?: string;
  /** A11y label for confetti/medal fallback. */
  readonly confettiAriaLabel: string;
  readonly confettiSkippedLabel: string;
  /** Function returning the per-place a11y label given participant info. */
  readonly podiumPlaceAriaLabel: (place: number, name: string, score: number) => string;
}

export interface SessionSummaryHeroProps {
  /**
   * Already-ranked participants from `computeRankedParticipants`. Top 3 (or
   * fewer) are placed on the podium. When the top group is tied at rank 1
   * with 2+ members, all tied 1st-place participants render at the center
   * podium height; remaining slots fall back to next ranks.
   */
  readonly rankedParticipants: readonly RankedParticipant[];
  /** Whether to render the celebratory confetti animation overlay. */
  readonly showConfetti: boolean;
  /**
   * Test override for `prefers-reduced-motion` (CSS media query is the
   * production source of truth — this prop only affects the
   * fallback-medal visibility branch in unit tests).
   */
  readonly prefersReducedMotion?: boolean;
  readonly labels: SessionSummaryHeroLabels;
  readonly className?: string;
}

interface PodiumLayoutEntry {
  readonly participant: RankedParticipant;
  /** Visual place — 1, 2, or 3. Derived from rank with tie collapse. */
  readonly place: 1 | 2 | 3;
}

/**
 * Compute the visual podium layout (left=2nd, center=1st, right=3rd) from
 * the ranked participants. Handles tied 1st place specially: when 2+ share
 * rank 1, both render at center height with `tied=true` markers. Tied at
 * 2nd or 3rd place: each renders at their respective height (no collapse).
 *
 * Render order in DOM: [2nd, 1st, 3rd] for compact layout. Solo (1
 * participant) renders only the center spot.
 */
function buildPodiumLayout(
  rankedParticipants: readonly RankedParticipant[]
): readonly PodiumLayoutEntry[] {
  if (rankedParticipants.length === 0) return [];

  // Single participant → center only (solo case).
  if (rankedParticipants.length === 1) {
    return [{ participant: rankedParticipants[0], place: 1 }];
  }

  const firstPlace = rankedParticipants.filter(p => p.rank === 1);
  const tiedAtTop = firstPlace.length > 1;

  if (tiedAtTop) {
    // All tied 1st in center; ranks ≥3 fill remaining flanks left to right.
    // Mockup: tied [Marco, Anna] both place=1 plus next non-tied [Luca] place=3.
    const rest = rankedParticipants.filter(p => p.rank !== 1).slice(0, 3 - firstPlace.length);
    const entries: PodiumLayoutEntry[] = firstPlace.map(p => ({
      participant: p,
      place: 1 as const,
    }));
    for (const p of rest) {
      entries.push({ participant: p, place: 3 as const });
    }
    return entries;
  }

  // Standard layout: 2nd · 1st · 3rd
  const out: PodiumLayoutEntry[] = [];
  const sorted = [...rankedParticipants].slice(0, 3);
  // DOM order: [second, first, third] for visual flank-elevation-flank
  const second = sorted.find(p => p.rank === 2);
  const first = sorted.find(p => p.rank === 1);
  const third = sorted.find(p => p.rank >= 3);
  if (second) out.push({ participant: second, place: 2 });
  if (first) out.push({ participant: first, place: 1 });
  if (third) out.push({ participant: third, place: 3 });
  return out;
}

/**
 * Generate a deterministic HSL hue for the participant avatar.
 * Frontend-only color (Gate B) since backend ParticipantDto has no color.
 */
function hashHue(seed: string): number {
  let h = 0;
  for (let i = 0; i < seed.length; i += 1) {
    h = (h * 31 + seed.charCodeAt(i)) | 0;
  }
  return Math.abs(h) % 360;
}

const PEDESTAL_HEIGHT_PX: Record<1 | 2 | 3, number> = {
  1: 96,
  2: 72,
  3: 56,
};

const AVATAR_SIZE_PX: Record<1 | 2 | 3, number> = {
  1: 76,
  2: 60,
  3: 56,
};

interface PodiumPlaceProps {
  readonly entry: PodiumLayoutEntry;
  readonly tied: boolean;
  readonly podiumPlaceAriaLabel: (place: number, name: string, score: number) => string;
}

function PodiumPlace({ entry, tied, podiumPlaceAriaLabel }: PodiumPlaceProps): ReactElement {
  const { participant, place } = entry;
  const isWinner = place === 1;
  const hue = hashHue(participant.userId ?? participant.id ?? participant.displayName);
  const ariaLabel = podiumPlaceAriaLabel(place, participant.displayName, participant.totalScore);

  return (
    <div
      data-slot="podium-place"
      data-place={place}
      data-tied={tied && isWinner ? 'true' : undefined}
      aria-label={ariaLabel}
      className="relative z-10 flex min-w-0 flex-1 flex-col items-center gap-1.5"
    >
      {isWinner && (
        <span
          aria-hidden="true"
          className={clsx(
            'mb-[-4px] leading-none drop-shadow-[0_0_12px_hsla(142,70%,31%,0.6)]',
            'text-2xl sm:text-3xl'
          )}
        >
          🏆
        </span>
      )}
      <div
        className="relative flex items-center justify-center rounded-full font-extrabold text-white"
        style={{
          width: AVATAR_SIZE_PX[place],
          height: AVATAR_SIZE_PX[place],
          background: `linear-gradient(135deg, hsl(${hue}, 70%, 65%), hsl(${hue}, 60%, 42%))`,
          border: isWinner ? '3px solid hsl(142, 70%, 31%)' : '2px solid var(--bg-card, white)',
          boxShadow: isWinner
            ? '0 0 30px hsla(142,70%,31%,0.4), 0 4px 14px hsla(142,70%,31%,0.3)'
            : '0 4px 12px rgba(0,0,0,.18)',
          fontSize: place === 1 ? '1.5rem' : '1.125rem',
        }}
      >
        {participant.displayName.charAt(0).toUpperCase()}
        {!isWinner && (
          <span aria-hidden="true" className="absolute -bottom-1 -right-1 text-base sm:text-xl">
            {place === 2 ? '🥈' : '🥉'}
          </span>
        )}
        {tied && isWinner && (
          <span
            data-slot="podium-tied-badge"
            className="absolute -top-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full text-[9px] font-extrabold text-white"
            style={{
              background: 'hsl(142, 70%, 31%)',
              border: '2px solid var(--bg-card, white)',
            }}
            aria-hidden="true"
          >
            T
          </span>
        )}
      </div>
      <div className="text-center text-sm font-extrabold text-foreground">
        {participant.displayName}
      </div>
      <div
        className={clsx(
          'tabular-nums leading-none font-extrabold',
          isWinner ? 'text-3xl text-[hsl(142,70%,31%)]' : 'text-xl text-foreground'
        )}
      >
        {participant.totalScore}
      </div>
      {/* Pedestal */}
      <div
        className={clsx(
          'mt-1 flex w-[88%] items-center justify-center rounded-t-md text-base font-extrabold',
          isWinner
            ? 'border-t-[3px] border-x border-t-[hsl(142,70%,31%)] border-x-border text-[hsl(142,70%,31%)]'
            : 'border border-border text-muted-foreground'
        )}
        style={{
          height: PEDESTAL_HEIGHT_PX[place],
          background: isWinner
            ? 'linear-gradient(180deg, hsla(142,70%,31%,0.18), hsla(142,70%,31%,0.08))'
            : 'linear-gradient(180deg, var(--bg-muted, #f8fafc), var(--bg-hover, #f1f5f9))',
        }}
      >
        {place}°
      </div>
    </div>
  );
}

export function SessionSummaryHero({
  rankedParticipants,
  showConfetti,
  prefersReducedMotion = false,
  labels,
  className,
}: SessionSummaryHeroProps): ReactElement {
  const headlineId = useId();
  const layout = buildPodiumLayout(rankedParticipants);
  const isTied =
    layout.some(entry => entry.place === 1) && layout.filter(e => e.place === 1).length > 1;
  const isSolo = rankedParticipants.length === 1;

  return (
    <section
      role="region"
      aria-labelledby={headlineId}
      data-slot="session-summary-hero"
      data-tied={isTied || undefined}
      data-solo={isSolo || undefined}
      className={clsx(
        'relative overflow-hidden px-4 py-6 sm:px-8 sm:py-8',
        'border-b border-[hsla(240,60%,55%,0.2)]',
        className
      )}
      style={{
        background:
          'radial-gradient(ellipse at 50% 0%, hsla(240,60%,55%,0.2) 0%, transparent 60%), linear-gradient(180deg, hsla(142,70%,31%,0.04) 0%, transparent 100%)',
      }}
    >
      {showConfetti && !prefersReducedMotion && <Confetti active />}
      {/* Reduced-motion fallback medal — sibling overlay; CSS hides confetti
       * and shows medal via parent .mai-confetti rules. We render the medal
       * unconditionally so reduced-motion users still see the celebratory
       * indicator while motion-tolerant users have it visually hidden via
       * absolute layering behind the confetti. */}
      {showConfetti && (
        <span
          data-slot="confetti-skipped-fallback"
          role="img"
          aria-label={labels.confettiSkippedLabel}
          className={clsx(
            'pointer-events-none absolute right-4 top-4 text-3xl',
            // Default visible; CSS reduced-motion media query in globals.css
            // already controls confetti animation. Component-level test override
            // forces visibility when prefersReducedMotion is true.
            !prefersReducedMotion && 'sr-only motion-reduce:not-sr-only'
          )}
        >
          🏅
        </span>
      )}
      <h1
        id={headlineId}
        className="relative z-10 mb-4 text-center text-2xl font-extrabold tracking-tight text-foreground sm:text-3xl"
      >
        {labels.title}
      </h1>
      {isTied && labels.tiedBanner && (
        <div
          data-slot="hero-tied-banner"
          className="relative z-10 mx-auto mb-4 flex max-w-md items-center justify-center gap-1.5 rounded-full border border-[hsla(142,70%,31%,0.3)] bg-[hsla(142,70%,31%,0.12)] px-3 py-1.5 text-center text-xs font-extrabold uppercase tracking-wide text-[hsl(142,70%,31%)]"
        >
          {labels.tiedBanner}
        </div>
      )}
      {layout.length > 0 && !isSolo && (
        <div
          className="relative z-10 mx-auto flex max-w-xl items-end justify-center gap-2 sm:gap-4"
          data-slot="podium-row"
        >
          {layout.map(entry => (
            <PodiumPlace
              key={entry.participant.id}
              entry={entry}
              tied={isTied}
              podiumPlaceAriaLabel={labels.podiumPlaceAriaLabel}
            />
          ))}
        </div>
      )}
      {isSolo && layout[0] && (
        <div
          className="relative z-10 mx-auto flex flex-col items-center gap-2"
          data-slot="podium-solo"
        >
          <PodiumPlace
            entry={layout[0]}
            tied={false}
            podiumPlaceAriaLabel={labels.podiumPlaceAriaLabel}
          />
        </div>
      )}
    </section>
  );
}
