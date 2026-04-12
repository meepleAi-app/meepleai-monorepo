import { describe, it, expect, beforeEach } from 'vitest';
import {
  createRequestInspectorStore,
  generateId,
} from '@/dev-tools/panel/stores/requestInspectorStore';
import type { InspectorEntry } from '@/dev-tools/types';

function makeEntry(overrides: Partial<InspectorEntry> = {}): InspectorEntry {
  return {
    id: generateId(),
    timestamp: Date.now(),
    method: 'GET',
    url: 'https://example.com/api/test',
    status: 200,
    durationMs: 42,
    isMock: false,
    ...overrides,
  };
}

describe('requestInspectorStore', () => {
  let store: ReturnType<typeof createRequestInspectorStore>;

  beforeEach(() => {
    store = createRequestInspectorStore();
  });

  it('initializes with empty entries', () => {
    expect(store.getState().entries).toHaveLength(0);
  });

  it('records entries newest-first', () => {
    const first = makeEntry({ url: 'https://example.com/first' });
    const second = makeEntry({ url: 'https://example.com/second' });

    store.getState().record(first);
    store.getState().record(second);

    const entries = store.getState().entries;
    expect(entries[0].url).toBe('https://example.com/second');
    expect(entries[1].url).toBe('https://example.com/first');
  });

  it('caps ring buffer at 50 entries, dropping oldest', () => {
    // Add 55 entries
    for (let i = 0; i < 55; i++) {
      store.getState().record(makeEntry({ url: `https://example.com/item-${i}` }));
    }

    const entries = store.getState().entries;
    expect(entries).toHaveLength(50);

    // Newest should be at index 0 (item-54), oldest dropped are item-0 through item-4
    expect(entries[0].url).toBe('https://example.com/item-54');
    expect(entries[49].url).toBe('https://example.com/item-5');
  });

  it('generates unique IDs for each entry', () => {
    const ids = new Set<string>();
    for (let i = 0; i < 10; i++) {
      ids.add(generateId());
    }
    expect(ids.size).toBe(10);
  });

  it('clear empties the entries array', () => {
    store.getState().record(makeEntry());
    store.getState().record(makeEntry());
    expect(store.getState().entries).toHaveLength(2);

    store.getState().clear();
    expect(store.getState().entries).toHaveLength(0);
  });
});
