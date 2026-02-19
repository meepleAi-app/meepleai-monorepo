/**
 * useNavigationTrail Hook - Unit Tests
 *
 * @see Issue #4705 - Integration Testing
 * @see Issue #4704 - Connected Navigation + Breadcrumb Trail
 */

import { renderHook, act } from '@testing-library/react';

import { useNavigationTrail, setNavigationHighlight } from '../use-navigation-trail';
import type { BreadcrumbStep } from '../use-navigation-trail';

// Mock sessionStorage
const mockStorage: Record<string, string> = {};

beforeEach(() => {
  Object.keys(mockStorage).forEach(key => delete mockStorage[key]);
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation((key: string) => mockStorage[key] ?? null);
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation((key: string, value: string) => {
    mockStorage[key] = value;
  });
  vi.spyOn(Storage.prototype, 'removeItem').mockImplementation((key: string) => {
    delete mockStorage[key];
  });
});

afterEach(() => {
  vi.restoreAllMocks();
});

const STEP_GAME: BreadcrumbStep = { entity: 'game', label: 'Catan', href: '/games/1' };
const STEP_AGENT: BreadcrumbStep = { entity: 'agent', label: 'RulesMaster', href: '/agents/1' };
const STEP_CHAT: BreadcrumbStep = { entity: 'chatSession', label: 'Rules Q&A', href: '/chat/1' };

describe('useNavigationTrail', () => {
  it('returns empty trail initially', () => {
    const { result } = renderHook(() => useNavigationTrail());
    expect(result.current.trail).toEqual([]);
  });

  it('push adds a step to the trail', () => {
    const { result } = renderHook(() => useNavigationTrail());
    act(() => {
      result.current.push(STEP_GAME);
    });
    // Re-read after event dispatch
    expect(result.current.trail).toHaveLength(1);
    expect(result.current.trail[0]).toEqual(STEP_GAME);
  });

  it('push avoids duplicating the last step', () => {
    const { result } = renderHook(() => useNavigationTrail());
    act(() => {
      result.current.push(STEP_GAME);
    });
    act(() => {
      result.current.push(STEP_GAME);
    });
    expect(result.current.trail).toHaveLength(1);
  });

  it('push truncates trail when navigating to existing step', () => {
    const { result } = renderHook(() => useNavigationTrail());
    act(() => {
      result.current.push(STEP_GAME);
    });
    act(() => {
      result.current.push(STEP_AGENT);
    });
    act(() => {
      result.current.push(STEP_CHAT);
    });
    expect(result.current.trail).toHaveLength(3);

    // Navigate back to STEP_GAME (already in trail)
    act(() => {
      result.current.push(STEP_GAME);
    });
    expect(result.current.trail).toHaveLength(1);
    expect(result.current.trail[0]).toEqual(STEP_GAME);
  });

  it('navigateTo truncates trail to given index', () => {
    const { result } = renderHook(() => useNavigationTrail());
    act(() => {
      result.current.push(STEP_GAME);
    });
    act(() => {
      result.current.push(STEP_AGENT);
    });
    act(() => {
      result.current.push(STEP_CHAT);
    });
    act(() => {
      result.current.navigateTo(0);
    });
    expect(result.current.trail).toHaveLength(1);
    expect(result.current.trail[0]).toEqual(STEP_GAME);
  });

  it('clear empties the trail', () => {
    const { result } = renderHook(() => useNavigationTrail());
    act(() => {
      result.current.push(STEP_GAME);
    });
    act(() => {
      result.current.push(STEP_AGENT);
    });
    act(() => {
      result.current.clear();
    });
    expect(result.current.trail).toEqual([]);
  });

  it('persists trail to sessionStorage', () => {
    const { result } = renderHook(() => useNavigationTrail());
    act(() => {
      result.current.push(STEP_GAME);
    });
    expect(mockStorage['meeple-nav-trail']).toBeDefined();
    const stored = JSON.parse(mockStorage['meeple-nav-trail']);
    expect(stored).toHaveLength(1);
    expect(stored[0].href).toBe('/games/1');
  });

  it('navigateTo ignores out-of-range indices', () => {
    const { result } = renderHook(() => useNavigationTrail());
    act(() => {
      result.current.push(STEP_GAME);
    });
    act(() => {
      result.current.navigateTo(5);
    });
    expect(result.current.trail).toHaveLength(1);
    act(() => {
      result.current.navigateTo(-1);
    });
    expect(result.current.trail).toHaveLength(1);
  });
});

describe('setNavigationHighlight', () => {
  it('writes entity to sessionStorage', () => {
    setNavigationHighlight('agent');
    expect(mockStorage['meeple-nav-highlight']).toBe('agent');
  });
});
