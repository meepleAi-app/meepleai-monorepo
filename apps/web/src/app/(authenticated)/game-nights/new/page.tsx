/**
 * /game-nights/new — wizard route entry point.
 *
 * Issue #950 W3 Commit 3: swapped from the legacy single-form
 * GameNightForm to the 4-step wizard orchestrator.
 *
 * Spec: docs/superpowers/specs/2026-05-18-sp7-game-night-create.md §11
 * (AC-O.1, AC-O.2).
 */

import { Suspense, type ReactElement } from 'react';

import { NewGameNightContent } from './_content';

export default function NewGameNightPage(): ReactElement {
  // The wizard reads `?step=` via useSearchParams; Next.js requires the
  // calling client tree to be wrapped in Suspense for the static prerender
  // bailout. The fallback is intentionally minimal — the wizard mounts
  // within a few hundred ms once the route shell is hydrated.
  return (
    <Suspense fallback={null}>
      <NewGameNightContent />
    </Suspense>
  );
}
