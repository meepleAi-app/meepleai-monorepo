/**
 * ScoringPanelEmpty — shared fallback when `ScoringPanelData` is null or its
 * variant payload has no rows. Composed by `ScoringPanelRenderer` (T6) and
 * exported via the barrel so individual variant panels (T2-T5) DO NOT
 * re-implement an empty branch.
 *
 * Token discipline (CLAUDE.md § Token Canonicalization, DS-15 error level):
 * - Surfaces: `bg-card` / `bg-muted` / `border-border`. No raw HSL.
 *
 * Issue #2373 — sub-issue G5a of epic #2354 (T6).
 */

import type { ReactElement } from 'react';

export interface ScoringPanelEmptyLabels {
  readonly title: string;
  readonly message: string;
  readonly trophyAriaLabel: string;
}

export interface ScoringPanelEmptyProps {
  readonly labels: ScoringPanelEmptyLabels;
  readonly className?: string;
}

/**
 * Inline trophy SVG — same path as `RankingPanel.TrophyIcon` (T3). Inlined
 * to avoid a runtime `lucide-react` dep for a single glyph that already
 * exists elsewhere on this branch.
 */
function TrophyIcon({ label }: { readonly label: string }): ReactElement {
  return (
    <svg
      role="img"
      aria-label={label}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className="h-6 w-6 text-muted-foreground"
    >
      <path d="M6 9H4.5a2.5 2.5 0 0 1 0-5H6" />
      <path d="M18 9h1.5a2.5 2.5 0 0 0 0-5H18" />
      <path d="M4 22h16" />
      <path d="M10 14.66V17c0 .55-.47.98-.97 1.21C7.85 18.75 7 20.24 7 22" />
      <path d="M14 14.66V17c0 .55.47.98.97 1.21C16.15 18.75 17 20.24 17 22" />
      <path d="M18 2H6v7a6 6 0 0 0 12 0V2Z" />
    </svg>
  );
}

export function ScoringPanelEmpty({ labels, className }: ScoringPanelEmptyProps): ReactElement {
  return (
    <section
      data-testid="scoring-panel-empty"
      aria-label={labels.title}
      className={`flex flex-col items-center gap-2 rounded-lg border border-border bg-card px-4 py-6 text-center ${className ?? ''}`}
    >
      <TrophyIcon label={labels.trophyAriaLabel} />
      <h3 className="text-sm font-semibold text-foreground">{labels.title}</h3>
      <p className="text-sm text-muted-foreground">{labels.message}</p>
    </section>
  );
}
