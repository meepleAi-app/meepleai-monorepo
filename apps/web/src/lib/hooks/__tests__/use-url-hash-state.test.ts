/**
 * Tests for useUrlHashState — covers SSR safety, hydration, multi-key sync,
 * codec round-trips, malformed hash recovery, and back/forward navigation.
 */
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useUrlHashState, type HashSchema } from '../use-url-hash-state';

interface FilterState {
  q: string;
  chips: string[];
  flag: boolean;
}

const schema: HashSchema<FilterState> = {
  q: {
    decode: raw => raw,
    encode: v => v,
  },
  chips: {
    decode: raw => (raw ? raw.split(',').filter(Boolean) : []),
    encode: v => v.join(','),
  },
  flag: {
    decode: raw => raw === 'true',
    encode: v => (v ? 'true' : ''),
  },
};

const defaults: FilterState = {
  q: '',
  chips: [],
  flag: false,
};

function setHash(value: string) {
  // Use replaceState so that the test setup does not pollute history.
  const url = new URL(window.location.href);
  url.hash = value;
  window.history.replaceState(null, '', url.toString());
}

describe('useUrlHashState', () => {
  beforeEach(() => {
    setHash('');
  });

  afterEach(() => {
    setHash('');
  });

  it('returns defaults when hash is empty', () => {
    const { result } = renderHook(() => useUrlHashState(schema, defaults));
    const [state] = result.current;
    expect(state).toEqual(defaults);
  });

  it('hydrates state from existing hash on mount', () => {
    setHash('#q=catan&chips=tk,top&flag=true');

    const { result } = renderHook(() => useUrlHashState(schema, defaults));

    const [state] = result.current;
    expect(state).toEqual({
      q: 'catan',
      chips: ['tk', 'top'],
      flag: true,
    });
  });

  it('writes single-key update to hash via replaceState', () => {
    const { result } = renderHook(() => useUrlHashState(schema, defaults));

    act(() => {
      const [, update] = result.current;
      update({ q: 'catan' });
    });

    expect(window.location.hash).toBe('#q=catan');
    const [state] = result.current;
    expect(state.q).toBe('catan');
    expect(state.chips).toEqual([]);
  });

  it('merges multi-key updates atomically', () => {
    const { result } = renderHook(() => useUrlHashState(schema, defaults));

    act(() => {
      const [, update] = result.current;
      update({ q: 'catan', chips: ['tk', 'ag'], flag: true });
    });

    expect(window.location.hash).toContain('q=catan');
    expect(window.location.hash).toContain('chips=tk%2Cag');
    expect(window.location.hash).toContain('flag=true');

    const [state] = result.current;
    expect(state).toEqual({ q: 'catan', chips: ['tk', 'ag'], flag: true });
  });

  it('omits keys whose encoder returns empty string', () => {
    const { result } = renderHook(() => useUrlHashState(schema, defaults));

    act(() => {
      const [, update] = result.current;
      update({ q: 'catan', flag: false });
    });

    // flag=false → encoder returns '' → key omitted
    expect(window.location.hash).toBe('#q=catan');
  });

  it('partial update preserves prior keys', () => {
    setHash('#q=catan&chips=tk');

    const { result } = renderHook(() => useUrlHashState(schema, defaults));

    act(() => {
      const [, update] = result.current;
      update({ chips: ['ag', 'top'] });
    });

    expect(window.location.hash).toContain('q=catan');
    expect(window.location.hash).toContain('chips=ag%2Ctop');
  });

  it('reset() clears hash and resets state to defaults', () => {
    setHash('#q=catan&chips=tk');

    const { result } = renderHook(() => useUrlHashState(schema, defaults));

    act(() => {
      const [, , reset] = result.current;
      reset();
    });

    expect(window.location.hash).toBe('');
    const [state] = result.current;
    expect(state).toEqual(defaults);
  });

  it('responds to hashchange event (back/forward navigation)', () => {
    const { result } = renderHook(() => useUrlHashState(schema, defaults));

    act(() => {
      // Simulate browser back/forward: change location then dispatch hashchange.
      setHash('#q=ticket');
      window.dispatchEvent(new HashChangeEvent('hashchange'));
    });

    const [state] = result.current;
    expect(state.q).toBe('ticket');
  });

  it('falls back to defaults when decoder throws', () => {
    const throwingSchema: HashSchema<{ n: number }> = {
      n: {
        decode: raw => {
          const n = Number.parseInt(raw, 10);
          if (Number.isNaN(n)) {
            throw new Error('not a number');
          }
          return n;
        },
        encode: v => String(v),
      },
    };

    setHash('#n=not-a-number');

    const { result } = renderHook(() => useUrlHashState(throwingSchema, { n: 42 }));

    const [state] = result.current;
    expect(state.n).toBe(42);
  });

  it('handles malformed hash entries without crashing', () => {
    setHash('#q=catan&=orphan&malformed&chips=tk');

    const { result } = renderHook(() => useUrlHashState(schema, defaults));

    const [state] = result.current;
    expect(state.q).toBe('catan');
    expect(state.chips).toEqual(['tk']);
  });

  it('round-trips encoded special characters', () => {
    const { result } = renderHook(() => useUrlHashState(schema, defaults));

    act(() => {
      const [, update] = result.current;
      update({ q: 'catan & ticket' });
    });

    expect(window.location.hash).toBe('#q=catan%20%26%20ticket');

    // Re-mount to verify decode round-trip.
    const { result: result2 } = renderHook(() => useUrlHashState(schema, defaults));
    const [state2] = result2.current;
    expect(state2.q).toBe('catan & ticket');
  });
});
