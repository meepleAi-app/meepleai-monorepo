/**
 * PageThumb — SP6 Phase C.2.B v2 component (Issue #789).
 *
 * Single-page tile inside the /gamebook/upload Step 3 indexing grid (4 cols
 * mobile · responsive). Pure component (Wave D.3 pattern): all i18n labels
 * are resolved by orchestrator and injected via `labels` prop.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (`PageThumb` component inside `IndexingScreen`).
 *
 * 4 visual states (per contract §5.7):
 *   - processing       spinner overlay, no badge, neutral border
 *   - indexed-high     thumb image + ✓ green badge in footer
 *   - indexed-medium   thumb image + ◐ amber badge in footer
 *   - indexed-low      thumb image + ⚠ red badge in footer + red border
 *   - retake (boolean) overlays a "Riscatta" CTA on the thumb (only when
 *                      `retakeRequested === true` AND `onRetakeClick` provided)
 *
 * Gate C — MeepleCard fit decision (DIVERGE):
 *   The shared `MeepleCard` API (avatar/title/subtitle/rating/cover) cannot
 *   express the page-thumb requirements:
 *     1. Aspect-ratio cover (3:4) with linear-gradient placeholder OR
 *        skeleton — MeepleCard's image slot is single-tone.
 *     2. Footer with page number + ConfidenceBadge inline — MeepleCard's
 *        StatusBadge is rectangular/single-state.
 *     3. Inline retake CTA overlay (absolute-positioned bottom strip on
 *        thumb) — MeepleCard has no overlay-action API.
 *     4. Conditional red border on `low` confidence — MeepleCard's border
 *        is themed via entity tokens, not semantic confidence.
 *   Mirror Wave D.1 (PR #736) cards-diverge-from-MeepleCard pattern.
 *   Documented per contract §13 Gate C.
 *
 * a11y:
 *   - `<li>` (children of `<ul role="list">` in indexing grid orchestrator)
 *   - When processing: `role="status"` + `aria-live="polite"` for SR
 *     announcement of "Pagina N in elaborazione"
 *   - Page number always visible (decorative tabular-nums)
 *   - ConfidenceBadge carries semantic aria-label ("Bassa confidenza")
 *   - Retake button has explicit `aria-label` from labels prop
 *   - prefers-reduced-motion: spinner animation disabled
 *
 * data-slot="page-thumb" + data-page-number + data-confidence-level +
 * data-processing for E2E selectors.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { ConfidenceLevel } from '@/lib/gamebook-upload';

import { ConfidenceBadge, type ConfidenceBadgeLabels } from './ConfidenceBadge';

export interface PageThumbLabels {
  /** Pre-resolved screenreader text "Pagina {n}" — orchestrator interpolates. */
  readonly pageLabel: string;
  /** Pre-resolved aria-label for retake button (e.g. "Riscatta pagina 3"). */
  readonly retakeAria: string;
  /** Pre-resolved screenreader text for processing state ("Pagina N in elaborazione"). */
  readonly processingAria: string;
  /** Visible CTA text on retake overlay (e.g. "Riscatta"). */
  readonly retakeCta: string;
  /** ConfidenceBadge labels (passed through). */
  readonly confidence: ConfidenceBadgeLabels;
}

export interface PageThumbProps {
  /** 1-based page number (display + key). */
  readonly pageNumber: number;
  /** Optional thumbnail object URL — orchestrator manages via createObjectURL. */
  readonly thumbnailUrl: string | null;
  /** Discrete confidence bucket OR null while still processing. */
  readonly confidence: ConfidenceLevel | null;
  /** True between batch start and per-page settlement. */
  readonly processing: boolean;
  /** True when classifier returned 'low' AND user surfaces retake prompt. */
  readonly retake: boolean;
  /** Optional click handler — only renders CTA overlay when both retake=true AND onRetakeClick provided. */
  readonly onRetakeClick?: () => void;
  readonly labels: PageThumbLabels;
  readonly className?: string;
}

// event + toolkit entity colours replaced with Tailwind entity-token classes (P2 #807 Task 6+7+8)

/**
 * Generate a deterministic placeholder gradient seeded by page number, mirror
 * of mockup `linear-gradient(135deg, hsl(${30 + num*5} 40% 78%), hsl(${30 + num*5} 30% 58%))`.
 */
function pagePlaceholderGradient(pageNumber: number): string {
  const hue = 30 + pageNumber * 5;
  return `linear-gradient(135deg, hsl(${hue}, 40%, 78%), hsl(${hue}, 30%, 58%))`;
}

export function PageThumb({
  pageNumber,
  thumbnailUrl,
  confidence,
  processing,
  retake,
  onRetakeClick,
  labels,
  className,
}: PageThumbProps): ReactElement {
  const showRetakeCta = retake && typeof onRetakeClick === 'function';
  // border set via className below — dynamic on retake
  const confidenceLevel = confidence ?? null;

  return (
    <li
      data-slot="page-thumb"
      data-page-number={pageNumber}
      data-confidence-level={confidenceLevel ?? 'processing'}
      data-processing={processing ? 'true' : 'false'}
      data-retake={retake ? 'true' : 'false'}
      role={processing ? 'status' : undefined}
      aria-live={processing ? 'polite' : undefined}
      className={clsx(
        'relative flex flex-col overflow-hidden rounded-md border bg-card',
        'transition-colors motion-reduce:transition-none',
        retake ? 'border-entity-event' : 'border-slate-200',
        className
      )}
    >
      {/* Cover (3:4 aspect ratio) */}
      <div
        data-slot="page-thumb-img"
        aria-hidden="true"
        className="flex aspect-[3/4] items-center justify-center"
        style={{
          background: thumbnailUrl
            ? `url("${thumbnailUrl}") center/cover no-repeat`
            : processing
              ? 'hsl(215, 16%, 92%)'
              : pagePlaceholderGradient(pageNumber),
        }}
      >
        {processing && (
          <span
            data-slot="page-thumb-spinner"
            aria-hidden="true"
            className={clsx(
              'h-3.5 w-3.5 rounded-full border-[2px] border-slate-300 [border-top-color:var(--color-entity-toolkit)]',
              'motion-safe:animate-spin motion-reduce:animate-none'
            )}
          />
        )}
      </div>

      {/* Footer: page number + ConfidenceBadge (or sr-only processing announce) */}
      <div
        data-slot="page-thumb-foot"
        className="flex items-center justify-between gap-1 bg-card px-1.5 py-1"
      >
        <span
          data-slot="page-thumb-num"
          className="font-mono text-[10px] font-bold leading-none text-slate-700 tabular-nums"
        >
          {pageNumber}
        </span>
        {!processing && confidence && (
          <ConfidenceBadge level={confidence} size="sm" labels={labels.confidence} />
        )}
        {processing && <span className="sr-only">{labels.processingAria}</span>}
      </div>

      {/* Retake overlay (only when retake=true AND onRetakeClick provided) */}
      {showRetakeCta && (
        <button
          type="button"
          data-slot="page-thumb-retake"
          aria-label={labels.retakeAria}
          onClick={onRetakeClick}
          className={clsx(
            'absolute inset-x-0 bottom-0 flex items-center justify-center gap-1',
            'cursor-pointer border-none px-1 py-1',
            'text-[9px] font-bold uppercase tracking-wider text-white',
            'transition-opacity motion-reduce:transition-none',
            'hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/80'
          )}
          style={{ backgroundColor: 'var(--color-entity-event)' }}
        >
          <span aria-hidden="true">📷</span>
          <span>{labels.retakeCta}</span>
        </button>
      )}

      {/* Decorative event-rose dim border accent on retake state */}
      {retake && (
        <span
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 rounded-md"
          // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: event entity inset ring with alpha; JS style prop, Tailwind ring utilities cannot target inset with alpha
          style={{ boxShadow: 'inset 0 0 0 1px hsla(350,89%,48%,0.4)' }}
        />
      )}
    </li>
  );
}
