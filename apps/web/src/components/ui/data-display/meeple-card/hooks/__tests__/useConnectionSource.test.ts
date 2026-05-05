import { describe, it, expect } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useConnectionSource } from '../useConnectionSource';

describe('useConnectionSource', () => {
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

  it('A6: manaPips → source=manaPips', () => {
    const { result } = renderHook(() =>
      useConnectionSource({ manaPips: [{ state: 'ok' } as any] })
    );
    expect(result.current.source).toBe('manaPips');
  });
});
