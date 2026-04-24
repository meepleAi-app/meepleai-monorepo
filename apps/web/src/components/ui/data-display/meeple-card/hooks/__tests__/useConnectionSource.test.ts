import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useConnectionSource } from '../useConnectionSource';
import { __resetDevWarnDedup } from '../devWarn';

describe('useConnectionSource', () => {
  beforeEach(() => {
    __resetDevWarnDedup();
    vi.restoreAllMocks();
  });

  it('A1: returns source=null for empty props', () => {
    const { result } = renderHook(() => useConnectionSource({}));
    expect(result.current.source).toBeNull();
    expect(result.current.items).toEqual([]);
  });

  it('A2: connections=[] → source=connections, items=[]', () => {
    const { result } = renderHook(() => useConnectionSource({ connections: [] }));
    expect(result.current.source).toBe('connections');
    expect(result.current.items).toEqual([]);
  });

  it('A3: connections with items', () => {
    const cs = [{ entityType: 'session' as const, count: 1 }];
    const { result } = renderHook(() => useConnectionSource({ connections: cs }));
    expect(result.current.source).toBe('connections');
    expect(result.current.items).toEqual(cs);
  });

  it('A4: navItems=[] → source=null', () => {
    const { result } = renderHook(() => useConnectionSource({ navItems: [] }));
    expect(result.current.source).toBeNull();
  });

  it('A5: navItems with items → source=navItems', () => {
    const { result } = renderHook(() =>
      useConnectionSource({ navItems: [{ label: 'x', entity: 'session', icon: null }] })
    );
    expect(result.current.source).toBe('navItems');
  });

  it('A6: manaPips → source=manaPips', () => {
    const { result } = renderHook(() =>
      useConnectionSource({ manaPips: [{ state: 'ok' } as any] })
    );
    expect(result.current.source).toBe('manaPips');
  });

  it('A7: connections + navItems → connections wins + warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const { result } = renderHook(() =>
      useConnectionSource({
        connections: [{ entityType: 'kb', count: 1 }],
        navItems: [{ label: 'y', entity: 'kb', icon: null }],
      })
    );
    expect(result.current.source).toBe('connections');
    expect(warn).toHaveBeenCalledWith(expect.stringMatching(/dual source|mix/i));
  });
});
