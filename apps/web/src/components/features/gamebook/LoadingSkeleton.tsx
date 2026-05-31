'use client';

import { type ReactElement } from 'react';

import { LABELS } from './TranslateViewer.steps';

import type { UiStep } from './TranslateViewer.steps';

export interface LoadingSkeletonProps {
  /**
   * Current UI step from deriveUiStep().
   * Determines the displayed label and test ID.
   */
  uiStep: UiStep;
}

/**
 * LoadingSkeleton component
 *
 * Renders a 4-line skeleton animation (label + 3 bars) with accessibility attributes.
 * Per DEC-5: Uses Tailwind motion-safe:animate-pulse for reduced-motion fallback.
 *
 * Structure:
 * - Container: role="status" + aria-busy="true" + aria-live="polite" for SR announcement
 * - Label: text-sm text-muted-foreground with step label from LABELS[uiStep]
 * - 3 skeleton bars: h-4 rounded bg-muted/50 motion-safe:animate-pulse
 *   - Bar 1: w-full (100%)
 *   - Bar 2: w-[92%]
 *   - Bar 3: w-[78%]
 */
export function LoadingSkeleton({ uiStep }: LoadingSkeletonProps): ReactElement {
  return (
    <div
      role="status"
      aria-busy="true"
      aria-live="polite"
      className="space-y-2 max-w-[65ch]"
      data-testid={`translate-skeleton-${uiStep}`}
    >
      <p className="text-sm text-muted-foreground" data-testid="translate-step-label">
        {LABELS[uiStep]}
      </p>
      <div className="h-4 w-full rounded bg-muted/50 motion-safe:animate-pulse" />
      <div className="h-4 w-[92%] rounded bg-muted/50 motion-safe:animate-pulse" />
      <div className="h-4 w-[78%] rounded bg-muted/50 motion-safe:animate-pulse" />
    </div>
  );
}
