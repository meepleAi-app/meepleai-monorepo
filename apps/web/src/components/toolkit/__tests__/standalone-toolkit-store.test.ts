import { describe, it, expect, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useStandaloneToolkitStore } from '@/lib/stores/standalone-toolkit-store';

beforeEach(() => {
  useStandaloneToolkitStore.setState({
    decks: {},
    counters: [],
    randomizer: { originalItems: [], remainingItems: [], lastExtracted: null },
  });
});

describe('CardDeck', () => {
  it('initDeck creates a shuffled draw pile', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.initDeck('d1', 'Scala 40', ['A', 'B', 'C'], false));
    expect(result.current.decks['d1'].drawPile).toHaveLength(3);
    expect(result.current.decks['d1'].discardPile).toHaveLength(0);
  });

  it('drawCard removes a card from drawPile', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.initDeck('d1', 'Test', ['A', 'B', 'C'], false));
    let card: string | null = null;
    act(() => {
      card = result.current.drawCard('d1');
    });
    expect(card).not.toBeNull();
    expect(result.current.decks['d1'].drawPile).toHaveLength(2);
  });

  it('undoDraw restores state within 30s window', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.initDeck('d1', 'Test', ['A', 'B', 'C'], false));
    act(() => result.current.drawCard('d1'));
    let success = false;
    act(() => {
      success = result.current.undoDraw('d1');
    });
    expect(success).toBe(true);
    expect(result.current.decks['d1'].drawPile).toHaveLength(3);
  });

  it('undoDraw fails after 30s', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.initDeck('d1', 'Test', ['A', 'B', 'C'], false));
    act(() => result.current.drawCard('d1'));
    // Manually expire the undo window
    act(() => {
      useStandaloneToolkitStore.setState(s => ({
        decks: {
          ...s.decks,
          d1: { ...s.decks['d1'], undoExpiry: Date.now() - 1 },
        },
      }));
    });
    let success = true;
    act(() => {
      success = result.current.undoDraw('d1');
    });
    expect(success).toBe(false);
    expect(result.current.decks['d1'].drawPile).toHaveLength(2);
  });
});

describe('Counter', () => {
  it('initCounters creates counters with initialValue', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.initCounters([{ id: 'c1', name: 'Points', initialValue: 10 }]));
    expect(result.current.counters[0].value).toBe(10);
  });

  it('incrementCounter increases value', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.initCounters([{ id: 'c1', name: 'P', initialValue: 0 }]));
    act(() => result.current.incrementCounter('c1'));
    expect(result.current.counters[0].value).toBe(1);
  });

  it('incrementCounter respects max', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.initCounters([{ id: 'c1', name: 'P', initialValue: 9, max: 10 }]));
    act(() => result.current.incrementCounter('c1', 5));
    expect(result.current.counters[0].value).toBe(10);
  });

  it('decrementCounter respects min', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.initCounters([{ id: 'c1', name: 'P', initialValue: 1, min: 0 }]));
    act(() => result.current.decrementCounter('c1', 5));
    expect(result.current.counters[0].value).toBe(0);
  });

  it('resetCounter restores initialValue', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.initCounters([{ id: 'c1', name: 'P', initialValue: 5 }]));
    act(() => result.current.incrementCounter('c1', 3));
    act(() => result.current.resetCounter('c1'));
    expect(result.current.counters[0].value).toBe(5);
  });
});

describe('Randomizer', () => {
  it('setRandomizerItems sets items and remaining', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.setRandomizerItems(['A', 'B', 'C']));
    expect(result.current.randomizer.remainingItems).toHaveLength(3);
  });

  it('extractRandom removes item from remaining (sampling without replacement)', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.setRandomizerItems(['A', 'B', 'C']));
    act(() => result.current.extractRandom());
    expect(result.current.randomizer.remainingItems).toHaveLength(2);
  });

  it('extractRandom returns null when pool is empty', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.setRandomizerItems([]));
    let extracted: string | null = 'not-null';
    act(() => {
      extracted = result.current.extractRandom();
    });
    expect(extracted).toBeNull();
  });

  it('resetRandomizer restores all items', () => {
    const { result } = renderHook(() => useStandaloneToolkitStore());
    act(() => result.current.setRandomizerItems(['A', 'B', 'C']));
    act(() => result.current.extractRandom());
    act(() => result.current.resetRandomizer());
    expect(result.current.randomizer.remainingItems).toHaveLength(3);
  });
});
