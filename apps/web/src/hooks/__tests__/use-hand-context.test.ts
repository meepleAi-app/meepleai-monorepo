import { renderHook, act } from '@testing-library/react';

import { useHandContext, type HandCard, type HandContextType } from '../use-hand-context';

const mockCard = (id: string, entity: string = 'game'): HandCard => ({
  id,
  entity: entity as HandCard['entity'],
  title: `Card ${id}`,
  href: `/library/${id}`,
});

describe('useHandContext', () => {
  beforeEach(() => {
    // Reset the store between tests
    const { result } = renderHook(() => useHandContext());
    act(() => result.current.clear());
  });

  it('should start with empty hand', () => {
    const { result } = renderHook(() => useHandContext());
    expect(result.current.cards).toHaveLength(0);
    expect(result.current.focusedIdx).toBe(-1);
    expect(result.current.handContext).toBe('library');
  });

  it('should add a card and auto-focus it', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => result.current.addCard(mockCard('1')));
    expect(result.current.cards).toHaveLength(1);
    expect(result.current.focusedIdx).toBe(0);
  });

  it('should not add duplicate cards', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.addCard(mockCard('1'));
    });
    expect(result.current.cards).toHaveLength(1);
  });

  it('should limit hand to 7 cards', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      for (let i = 0; i < 10; i++) {
        result.current.addCard(mockCard(`${i}`));
      }
    });
    expect(result.current.cards.length).toBeLessThanOrEqual(7);
  });

  it('should remove a card by id', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.addCard(mockCard('2'));
      result.current.removeCard('1');
    });
    expect(result.current.cards).toHaveLength(1);
    expect(result.current.cards[0].id).toBe('2');
  });

  it('should focus a card by index', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.addCard(mockCard('2'));
      result.current.focusCard(1);
    });
    expect(result.current.focusedIdx).toBe(1);
  });

  it('should swipe to next/previous card', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.addCard(mockCard('2'));
      result.current.addCard(mockCard('3'));
      result.current.focusCard(0);
    });
    act(() => result.current.swipeNext());
    expect(result.current.focusedIdx).toBe(1);
    act(() => result.current.swipePrev());
    expect(result.current.focusedIdx).toBe(0);
  });

  it('should clamp swipe at boundaries', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.focusCard(0);
    });
    act(() => result.current.swipePrev());
    expect(result.current.focusedIdx).toBe(0);
    act(() => result.current.swipeNext());
    expect(result.current.focusedIdx).toBe(0); // only 1 card
  });

  it('should set hand context type', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => result.current.setHandContext('sessions'));
    expect(result.current.handContext).toBe('sessions');
  });

  it('should clear all cards', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.addCard(mockCard('2'));
      result.current.clear();
    });
    expect(result.current.cards).toHaveLength(0);
    expect(result.current.focusedIdx).toBe(-1);
  });
});
