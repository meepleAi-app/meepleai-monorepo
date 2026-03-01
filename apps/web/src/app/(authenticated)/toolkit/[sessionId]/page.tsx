/**
 * Active Session Page — Tool Rail Layout (Issues #4973, #4974, #4975, #4977, #4976)
 *
 * Wires all 4 base toolkit tools + custom GameToolkit tools.
 * Active tool persisted in URL query param `?tool=<toolId>` (Issue #4974).
 */

import { Suspense } from 'react';

import { ActiveSessionPageContent } from './_content';

export default function ActiveSessionPage() {
  return (
    <Suspense>
      <ActiveSessionPageContent />
    </Suspense>
  );
}
