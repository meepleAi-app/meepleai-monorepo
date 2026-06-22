/**
 * mapConnectionState — wire G4 LiveTopBar connection pip (Issue #2355).
 *
 * Maps the 5-value SSE connection state from `useSessionLiveStream` to the
 * 3-value pip enum exposed by `LiveTopBar`. The `connecting` initial state
 * returns `undefined` to keep the pip hidden until the first connection
 * attempt resolves — avoids a flash of amber on every mount.
 *
 * | SSE state          | Pip state      | Visual           |
 * |--------------------|----------------|------------------|
 * | `connecting`       | undefined      | (hidden)         |
 * | `connected`        | 'connected'    | emerald          |
 * | `reconnecting`     | 'reconnecting' | amber            |
 * | `degraded-polling` | 'reconnecting' | amber (fallback) |
 * | `failed`           | 'failed'       | destructive      |
 *
 * @see apps/web/src/lib/session-live/use-session-live-stream.ts (SseConnectionState)
 * @see apps/web/src/components/features/session-live/LiveTopBar.tsx (LiveTopBarConnectionState)
 * @see Issue #2355 (G4 wiring), parent #2352 (G4 primitive shipped)
 */

import type { LiveTopBarConnectionState } from '@/components/features/session-live';

type SseConnectionState =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'degraded-polling'
  | 'failed';

export function mapConnectionState(
  state: SseConnectionState
): LiveTopBarConnectionState | undefined {
  switch (state) {
    case 'connecting':
      return undefined;
    case 'connected':
      return 'connected';
    case 'reconnecting':
    case 'degraded-polling':
      return 'reconnecting';
    case 'failed':
      return 'failed';
    default:
      return assertNever(state);
  }
}

function assertNever(value: never): never {
  throw new Error(`mapConnectionState: unhandled SseConnectionState "${String(value)}".`);
}
