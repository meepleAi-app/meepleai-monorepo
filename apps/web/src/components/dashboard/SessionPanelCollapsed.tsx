'use client';

import { useSessionSlot } from './useSessionSlot';

/**
 * Collapsed session panel shown when CardStack is at 56px.
 *
 * Renders a session icon with a pulsing live dot overlay.
 */
export function SessionPanelCollapsed() {
  const { isVisible } = useSessionSlot();

  if (!isVisible) return null;

  return (
    <div
      data-testid="session-panel-collapsed"
      className="relative flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-500/15 text-lg"
    >
      <span aria-hidden="true">⏳</span>
      <span
        className="live-dot absolute -top-0.5 -right-0.5 h-2.5 w-2.5 rounded-full bg-green-500 ring-2 ring-background"
        aria-label="Live session"
      />
    </div>
  );
}
