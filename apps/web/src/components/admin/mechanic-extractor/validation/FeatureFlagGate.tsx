/**
 * Mechanic Extractor — AI Comprehension Validation feature flag gate (ADR-051
 * Sprint 2 / Task 25).
 *
 * Wraps a subtree so it only renders when the
 * `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED` env flag is `'true'`. Use this
 * inline at *embedded* call sites (e.g. an action button surfaced from
 * another page that lives outside the dedicated /mechanic-extractor route
 * tree); for full-page gates prefer `if (!isMechanicValidationEnabled())
 * notFound();` at the top of the page so the route 404s rather than rendering
 * an empty shell.
 */

'use client';

import { type ReactNode } from 'react';

import { isMechanicValidationEnabled } from '@/lib/feature-flags/mechanic-validation';

export interface FeatureFlagGateProps {
  children: ReactNode;
  /**
   * Optional fallback rendered when the flag is off. Defaults to `null` so
   * the section disappears entirely from the layout.
   */
  fallback?: ReactNode;
}

export function FeatureFlagGate({ children, fallback = null }: FeatureFlagGateProps): ReactNode {
  if (!isMechanicValidationEnabled()) return fallback;
  return children;
}
