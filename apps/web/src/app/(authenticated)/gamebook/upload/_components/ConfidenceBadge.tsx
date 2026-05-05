/**
 * Gamebook — ConfidenceBadge component (Sprint 1, Task 1.8)
 *
 * Renders a colour-coded badge for the SmolDocling average confidence score.
 *   >= 0.80 → green  (high)
 *   >= 0.50 → amber  (medium)
 *   <  0.50 → red    (low)
 *   null    → grey   (not yet available)
 */

'use client';

import type { JSX } from 'react';

export interface ConfidenceBadgeProps {
  /** 0–1 confidence score from SmolDocling, or null if not yet available */
  confidence: number | null;
}

function getVariant(confidence: number | null): {
  label: string;
  className: string;
} {
  if (confidence === null) {
    return {
      label: '—',
      className: 'bg-muted text-muted-foreground',
    };
  }

  const pct = Math.round(confidence * 100);
  const label = `${pct}%`;

  if (confidence >= 0.8) {
    return {
      label,
      className: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
    };
  }
  if (confidence >= 0.5) {
    return {
      label,
      className: 'bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200',
    };
  }
  return { label, className: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200' };
}

/**
 * Displays a confidence percentage badge with colour coding.
 *
 * @example
 * <ConfidenceBadge confidence={0.85} />  // → "85%" green
 * <ConfidenceBadge confidence={null} />  // → "—" grey
 */
export function ConfidenceBadge({ confidence }: ConfidenceBadgeProps): JSX.Element {
  const { label, className } = getVariant(confidence);

  return (
    <span
      data-testid="confidence-badge"
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${className}`}
      aria-label={confidence !== null ? `Confidence: ${label}` : 'Confidence not available'}
    >
      {label}
    </span>
  );
}
