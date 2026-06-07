/**
 * StatePreviewToggle — floating dev-only control for state simulation.
 *
 * Asse B WP5 T5 (Issue #1897). Renders null in production (zero DOM impact).
 * In dev, provides a 5-option select bound to the current pageId's preview state.
 */

'use client';

import { useStatePreview } from './use-state-preview';

import type { StateKind } from './state-preview-types';

const STATES: readonly StateKind[] = ['default', 'empty', 'loading', 'error', 'offline'] as const;

interface StatePreviewToggleProps {
  pageId: string;
}

export function StatePreviewToggle({ pageId }: StatePreviewToggleProps) {
  // Production guard: render nothing — zero DOM, zero a11y surface, zero cost.
  if (process.env.NODE_ENV === 'production') return null;

  return <StatePreviewToggleImpl pageId={pageId} />;
}

function StatePreviewToggleImpl({ pageId }: StatePreviewToggleProps) {
  const { state, setStateFor } = useStatePreview(pageId);
  const labelId = `state-preview-label-${pageId}`;
  const selectId = `state-preview-select-${pageId}`;

  return (
    <div
      data-testid="state-preview-toggle"
      className="fixed bottom-4 right-4 z-[9999] rounded-md bg-background p-2 shadow-lg border border-border"
    >
      <label
        id={labelId}
        htmlFor={selectId}
        className="block text-xs uppercase text-muted-foreground"
      >
        Preview State
      </label>
      <select
        id={selectId}
        aria-labelledby={labelId}
        value={state}
        onChange={e => setStateFor(pageId, e.target.value as StateKind)}
        className="w-full bg-background text-foreground border border-border rounded"
      >
        {STATES.map(s => (
          <option key={s} value={s}>
            {s}
          </option>
        ))}
      </select>
    </div>
  );
}
