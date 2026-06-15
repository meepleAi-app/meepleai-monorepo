import { describe, expect, it } from 'vitest';

import { mapConnectionState } from '../map-connection-state';

describe('mapConnectionState', () => {
  it("returns undefined for 'connecting' (hide pip on initial connect)", () => {
    expect(mapConnectionState('connecting')).toBeUndefined();
  });

  it("returns 'connected' for 'connected'", () => {
    expect(mapConnectionState('connected')).toBe('connected');
  });

  it("returns 'reconnecting' for 'reconnecting'", () => {
    expect(mapConnectionState('reconnecting')).toBe('reconnecting');
  });

  it("returns 'reconnecting' for 'degraded-polling' (amber fallback)", () => {
    expect(mapConnectionState('degraded-polling')).toBe('reconnecting');
  });

  it("returns 'failed' for 'failed'", () => {
    expect(mapConnectionState('failed')).toBe('failed');
  });

  it('throws on unknown SSE state (defense-in-depth)', () => {
    expect(() => mapConnectionState('unknown' as never)).toThrow(/unhandled SseConnectionState/);
  });
});
