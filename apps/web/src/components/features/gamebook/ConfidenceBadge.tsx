/**
 * ConfidenceBadge — SP6 Phase C.2.B v2 component (Issue #789).
 *
 * Compact circular confidence indicator (high/medium/low) for `PageThumb`
 * footer in the /gamebook/upload Step 3 indexing grid. Pure component (Wave
 * D.3 pattern): all i18n labels are resolved by orchestrator and injected via
 * `labels` prop.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (`ConfidenceBadge` inside `PageThumb`). Uses `CONF_COLORS` semantic palette
 * (NOT entity colors): green/amber/red with WCAG AA contrast on light bg.
 *
 * Glyphs:
 *   - high   ✓ (auto-accept)
 *   - medium ◐ (manual review allowed)
 *   - low    ⚠ (retake recommended)
 *
 * Gate C — MeepleCard fit decision (DIVERGE):
 *   The shared `MeepleCard` family does not expose a circular confidence
 *   primitive. `MeepleCard.StatusBadge` is rectangular pill-shaped and uses
 *   slate-* tokens (status palette), not the green/amber/red semantic
 *   confidence palette required here. Mirror Wave D.2 inline-status banner
 *   divergence pattern. Documented per contract §13 Gate C.
 *
 * a11y:
 *   - `<span role="img">` (single-character semantic glyph)
 *   - `aria-label={labels[level]}` carries the human-readable confidence
 *     description ("Alta", "Media", "Bassa") so SR users hear meaning, not
 *     glyph metadata
 *   - Color is supplemental — glyph + aria-label carry meaning (WCAG SC 1.4.1
 *     use-of-color)
 *
 * data-slot="confidence-badge" + data-level for E2E + a11y exclusion scoping.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { ConfidenceLevel } from '@/lib/gamebook-upload';

export interface ConfidenceBadgeLabels {
  /** SR description for high confidence (e.g. "Alta confidenza"). */
  readonly high: string;
  /** SR description for medium confidence (e.g. "Media confidenza"). */
  readonly medium: string;
  /** SR description for low confidence (e.g. "Bassa confidenza"). */
  readonly low: string;
}

export interface ConfidenceBadgeProps {
  /** Discrete confidence bucket (per `classifyConfidence`). */
  readonly level: ConfidenceLevel;
  /** Visual size (default 'sm' = 16px circle, 'md' = 20px). */
  readonly size?: 'sm' | 'md';
  readonly labels: ConfidenceBadgeLabels;
  readonly className?: string;
}

// CONF_COLORS palette (mockup §) — replaced inline HSL with Tailwind entity-token classes (P2 #807 Task 6+7+8):
//   high   green: toolkit entity (bg-entity-toolkit/15 + text-entity-toolkit-text) — text variant for AA (#1094 Real-C-E)
//   medium amber: agent entity  (bg-entity-agent/18  + text-entity-agent)
//   low    rose:  event entity  (bg-entity-event/15  + text-entity-event)
const PALETTE: Record<ConfidenceLevel, { cls: string }> = {
  high: { cls: 'bg-entity-toolkit/15 text-entity-toolkit-text' },
  medium: { cls: 'bg-entity-agent/18 text-entity-agent' },
  low: { cls: 'bg-entity-event/15 text-entity-event' },
};

const GLYPH: Record<ConfidenceLevel, string> = {
  high: '✓', // ✓
  medium: '◐', // ◐
  low: '⚠', // ⚠
};

const SIZE_CLASS: Record<NonNullable<ConfidenceBadgeProps['size']>, string> = {
  sm: 'h-4 w-4 text-[10px]',
  md: 'h-5 w-5 text-[12px]',
};

export function ConfidenceBadge({
  level,
  size = 'sm',
  labels,
  className,
}: ConfidenceBadgeProps): ReactElement {
  const palette = PALETTE[level];
  const glyph = GLYPH[level];
  const description = labels[level];

  return (
    <span
      role="img"
      aria-label={description}
      data-slot="confidence-badge"
      data-level={level}
      title={description}
      className={clsx(
        'inline-flex items-center justify-center rounded-full font-bold tabular-nums leading-none',
        SIZE_CLASS[size],
        palette.cls,
        className
      )}
    >
      {glyph}
    </span>
  );
}
