/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useNavBreadcrumb } from '../useNavBreadcrumb';
import type { HandCard } from '@/lib/stores/card-hand-store';

// Mock token imports
vi.mock('@/components/ui/data-display/meeple-card/tokens', () => ({
  entityHsl: (entity: string) => {
    const colors: Record<string, string> = {
      game: 'hsl(25,95%,45%)',
      player: 'hsl(262,83%,58%)',
      session: 'hsl(240,60%,55%)',
    };
    return colors[entity] || 'hsl(0,0%,50%)';
  },
}));

// Mock next/navigation
let mockPathname = '/dashboard';
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname,
}));

// Mock card-hand-store with mutable state
const mockCards: HandCard[] = [];
vi.mock('@/lib/stores/card-hand-store', async () => {
  const actual = await vi.importActual<typeof import('@/lib/stores/card-hand-store')>(
    '@/lib/stores/card-hand-store'
  );
  return {
    ...actual,
    useCardHand: (selector?: (state: any) => any) => {
      const state = { cards: mockCards };
      return selector ? selector(state) : state.cards;
    },
  };
});

beforeEach(() => {
  mockCards.length = 0;
  mockPathname = '/dashboard';
});

describe('useNavBreadcrumb', () => {
  it('restituisce segmento per route nota', () => {
    mockPathname = '/library';
    const { result } = renderHook(() => useNavBreadcrumb());
    expect(result.current).toHaveLength(1);
    expect(result.current[0].label).toBe('Libreria');
    expect(result.current[0].href).toBe('/library');
    expect(result.current[0].entityType).toBeUndefined();
  });

  it('restituisce segmento entity da card in mano', () => {
    mockPathname = '/games/abc';
    mockCards.push({
      id: 'game:abc',
      entityType: 'game',
      entityId: 'abc',
      label: 'Catan',
      href: '/games/abc',
      pinned: false,
      addedAt: Date.now(),
    });
    const { result } = renderHook(() => useNavBreadcrumb());
    const catanSegment = result.current.find(s => s.label === 'Catan');
    expect(catanSegment).toBeDefined();
    expect(catanSegment?.entityType).toBe('game');
    expect(catanSegment?.color).toBe('hsl(25,95%,45%)');
  });

  it('lista vuota per route sconosciuta senza card', () => {
    mockPathname = '/unknown/path';
    const { result } = renderHook(() => useNavBreadcrumb());
    expect(result.current).toHaveLength(0);
  });

  it('dashboard restituisce "Dashboard"', () => {
    mockPathname = '/dashboard';
    const { result } = renderHook(() => useNavBreadcrumb());
    expect(result.current).toHaveLength(1);
    expect(result.current[0].label).toBe('Dashboard');
    expect(result.current[0].href).toBe('/dashboard');
  });

  it('sessions restituisce "Sessioni"', () => {
    mockPathname = '/sessions';
    const { result } = renderHook(() => useNavBreadcrumb());
    expect(result.current).toHaveLength(1);
    expect(result.current[0].label).toBe('Sessioni');
    expect(result.current[0].href).toBe('/sessions');
  });

  it('agenti restituisce "Agenti"', () => {
    mockPathname = '/agents';
    const { result } = renderHook(() => useNavBreadcrumb());
    expect(result.current).toHaveLength(1);
    expect(result.current[0].label).toBe('Agenti');
  });

  it('toolkit restituisce "Toolkit"', () => {
    mockPathname = '/toolkit';
    const { result } = renderHook(() => useNavBreadcrumb());
    expect(result.current).toHaveLength(1);
    expect(result.current[0].label).toBe('Toolkit');
  });

  it('chat restituisce "Chat"', () => {
    mockPathname = '/chat';
    const { result } = renderHook(() => useNavBreadcrumb());
    expect(result.current).toHaveLength(1);
    expect(result.current[0].label).toBe('Chat');
  });

  it('multi-livello con card e route nota', () => {
    mockPathname = '/games/abc/sessions/xyz';
    mockCards.push({
      id: 'game:abc',
      entityType: 'game',
      entityId: 'abc',
      label: 'Catan',
      href: '/games/abc',
      pinned: false,
      addedAt: Date.now(),
    });
    const { result } = renderHook(() => useNavBreadcrumb());
    expect(result.current.length).toBeGreaterThan(0);
    expect(result.current.some(s => s.label === 'Catan')).toBe(true);
  });

  it('esclude duplicati con lo stesso href', () => {
    mockPathname = '/games/abc';
    mockCards.push({
      id: 'game:abc',
      entityType: 'game',
      entityId: 'abc',
      label: 'Catan',
      href: '/games/abc',
      pinned: false,
      addedAt: Date.now(),
    });
    const { result } = renderHook(() => useNavBreadcrumb());
    const uniqueHrefs = new Set(result.current.map(s => s.href));
    expect(uniqueHrefs.size).toBe(result.current.length);
  });

  it('ordina segmenti in ordine di accumulo percorso', () => {
    mockPathname = '/games/abc/sessions';
    mockCards.push({
      id: 'game:abc',
      entityType: 'game',
      entityId: 'abc',
      label: 'Catan',
      href: '/games/abc',
      pinned: false,
      addedAt: Date.now(),
    });
    const { result } = renderHook(() => useNavBreadcrumb());
    // /games/abc should come before /games/abc/sessions
    const catanIndex = result.current.findIndex(s => s.label === 'Catan');
    expect(catanIndex).toBeLessThan(result.current.length - 1);
  });
});
