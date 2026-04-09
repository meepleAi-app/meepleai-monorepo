'use client';

/**
 * Dashboard Client — thin wrapper around the V2 Gaming Hub.
 *
 * Phase 5 (Desktop UX Redesign) removed the legacy Bento grid and the
 * feature flag that gated V2. Dashboard V2 is now the only experience.
 * Canonical Bento widgets still live under `./widgets/` for any future
 * reuse and are covered by their own tests.
 */

import { DashboardClientV2 } from './v2';

export function DashboardClient() {
  return <DashboardClientV2 />;
}
