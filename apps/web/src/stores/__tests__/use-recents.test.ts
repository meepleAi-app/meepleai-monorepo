// apps/web/src/stores/__tests__/use-recents.test.ts
import { act } from 'react';
import { describe, it, expect, beforeEach } from 'vitest';
import { useRecentsStore } from '../use-recents';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

function makeRecent(id: string, entity: MeepleEntityType = 'game') {
  return { id, entity, title: `Title ${id}`, href: `/games/${id}` };
}

describe('useRecentsStore', () => {
  beforeEach(() => {
    act(() => useRecentsStore.getState().clear());
    sessionStorage.clear();
  });

  it('starts empty', () => {
    expect(useRecentsStore.getState().items).toEqual([]);
  });

  it('push adds an item with visitedAt timestamp', () => {
    act(() => useRecentsStore.getState().push(makeRecent('g1')));
    const items = useRecentsStore.getState().items;
    expect(items).toHaveLength(1);
    expect(items[0].id).toBe('g1');
    expect(items[0].visitedAt).toBeGreaterThan(0);
  });

  it('push promotes existing item to front', () => {
    act(() => {
      useRecentsStore.getState().push(makeRecent('g1'));
      useRecentsStore.getState().push(makeRecent('g2'));
      useRecentsStore.getState().push(makeRecent('g1')); // re-visit
    });
    const items = useRecentsStore.getState().items;
    expect(items).toHaveLength(2);
    expect(items[0].id).toBe('g1'); // most recent first
  });

  it('evicts oldest when exceeding max 4', () => {
    act(() => {
      useRecentsStore.getState().push(makeRecent('g1'));
      useRecentsStore.getState().push(makeRecent('g2'));
      useRecentsStore.getState().push(makeRecent('g3'));
      useRecentsStore.getState().push(makeRecent('g4'));
      useRecentsStore.getState().push(makeRecent('g5'));
    });
    const items = useRecentsStore.getState().items;
    expect(items).toHaveLength(4);
    expect(items.find(i => i.id === 'g1')).toBeUndefined(); // evicted
  });

  it('remove deletes by id', () => {
    act(() => {
      useRecentsStore.getState().push(makeRecent('g1'));
      useRecentsStore.getState().push(makeRecent('g2'));
      useRecentsStore.getState().remove('g1');
    });
    expect(useRecentsStore.getState().items).toHaveLength(1);
  });

  it('clear empties the list', () => {
    act(() => {
      useRecentsStore.getState().push(makeRecent('g1'));
      useRecentsStore.getState().clear();
    });
    expect(useRecentsStore.getState().items).toEqual([]);
  });

  it('persists to sessionStorage', () => {
    act(() => useRecentsStore.getState().push(makeRecent('g1')));
    const stored = sessionStorage.getItem('meeple-recents');
    expect(stored).toBeTruthy();
    expect(JSON.parse(stored!)).toHaveLength(1);
  });
});
