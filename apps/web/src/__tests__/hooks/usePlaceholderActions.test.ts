import { act, renderHook } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { usePlaceholderActions } from '@/hooks/usePlaceholderActions';
import type { HandCard } from '@/stores/use-card-hand';

const placeholderCard: HandCard = {
  id: 'action-search-agent',
  entity: 'agent',
  title: 'Cerca Agente',
  href: '#action-search-agent',
  isPlaceholder: true,
  placeholderAction: 'search-agent',
};

const regularCard: HandCard = {
  id: 'regular-game-1',
  entity: 'game',
  title: 'Twilight Imperium',
  href: '/games/twilight-imperium',
};

describe('usePlaceholderActions', () => {
  it('activeSheet is null initially', () => {
    const { result } = renderHook(() => usePlaceholderActions());
    expect(result.current.activeSheet).toBeNull();
  });

  it('handleCardClick returns true and sets activeSheet for a placeholder card', () => {
    const { result } = renderHook(() => usePlaceholderActions());

    let returned: boolean = false;
    act(() => {
      returned = result.current.handleCardClick(placeholderCard);
    });

    expect(returned).toBe(true);
    expect(result.current.activeSheet).toBe('search-agent');
  });

  it('handleCardClick returns false for a regular card without isPlaceholder', () => {
    const { result } = renderHook(() => usePlaceholderActions());

    let returned: boolean = true;
    act(() => {
      returned = result.current.handleCardClick(regularCard);
    });

    expect(returned).toBe(false);
    expect(result.current.activeSheet).toBeNull();
  });

  it('handleCardClick returns false for a card with isPlaceholder=false', () => {
    const { result } = renderHook(() => usePlaceholderActions());

    const nonPlaceholder: HandCard = {
      id: 'section-home',
      entity: 'custom',
      title: 'Home',
      href: '/dashboard',
      isPlaceholder: false,
    };

    let returned: boolean = true;
    act(() => {
      returned = result.current.handleCardClick(nonPlaceholder);
    });

    expect(returned).toBe(false);
    expect(result.current.activeSheet).toBeNull();
  });

  it('closeSheet resets activeSheet to null', () => {
    const { result } = renderHook(() => usePlaceholderActions());

    act(() => {
      result.current.handleCardClick(placeholderCard);
    });
    expect(result.current.activeSheet).toBe('search-agent');

    act(() => {
      result.current.closeSheet();
    });
    expect(result.current.activeSheet).toBeNull();
  });

  it('handleCardClick updates activeSheet when a different placeholder card is clicked', () => {
    const { result } = renderHook(() => usePlaceholderActions());

    const toolkitCard: HandCard = {
      id: 'action-toolkit',
      entity: 'toolkit',
      title: 'Toolkit',
      href: '#action-toolkit',
      isPlaceholder: true,
      placeholderAction: 'toolkit',
    };

    act(() => {
      result.current.handleCardClick(placeholderCard);
    });
    expect(result.current.activeSheet).toBe('search-agent');

    act(() => {
      result.current.handleCardClick(toolkitCard);
    });
    expect(result.current.activeSheet).toBe('toolkit');
  });
});
