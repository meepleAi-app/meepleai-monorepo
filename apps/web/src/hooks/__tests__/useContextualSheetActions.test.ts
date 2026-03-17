// apps/web/src/hooks/__tests__/useContextualSheetActions.test.ts
import { renderHook } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/components/layout/LayoutProvider', () => ({
  useLayout: vi.fn(() => ({ context: 'default', setContext: vi.fn() })),
}));

import { usePathname } from 'next/navigation';
import { useContextualSheetActions } from '../useContextualSheetActions';

const mockUsePathname = vi.mocked(usePathname);

describe('useContextualSheetActions', () => {
  it('returns game actions on game detail page', () => {
    mockUsePathname.mockReturnValue('/library/games/catan-123');
    const { result } = renderHook(() => useContextualSheetActions());
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('faq');
    expect(ids).toContain('rules');
    expect(ids).toContain('reviews');
    expect(ids).toContain('strategy');
    expect(ids).toContain('new-session');
  });

  it('returns session actions on session page', () => {
    mockUsePathname.mockReturnValue('/sessions/abc-123');
    const { result } = renderHook(() => useContextualSheetActions());
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('add-note');
    expect(ids).toContain('scoreboard');
  });

  it('returns dashboard actions on /dashboard', () => {
    mockUsePathname.mockReturnValue('/dashboard');
    const { result } = renderHook(() => useContextualSheetActions());
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('quick-start');
    expect(ids).toContain('recent-games');
  });

  it('returns dashboard actions on root', () => {
    mockUsePathname.mockReturnValue('/');
    const { result } = renderHook(() => useContextualSheetActions());
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('quick-start');
  });

  it('returns chat actions with settings', () => {
    mockUsePathname.mockReturnValue('/chat/conv-789');
    const { result } = renderHook(() => useContextualSheetActions());
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('new-topic');
    expect(ids).toContain('history');
    expect(ids).toContain('settings');
  });

  it('returns library actions on library page', () => {
    mockUsePathname.mockReturnValue('/library');
    const { result } = renderHook(() => useContextualSheetActions());
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('add-game');
  });

  it('each action has required fields', () => {
    mockUsePathname.mockReturnValue('/library/games/catan-123');
    const { result } = renderHook(() => useContextualSheetActions());
    for (const action of result.current) {
      expect(action).toHaveProperty('id');
      expect(action).toHaveProperty('label');
      expect(action).toHaveProperty('icon');
      expect(action).toHaveProperty('onClick');
    }
  });

  it('returns empty for catalog page', () => {
    mockUsePathname.mockReturnValue('/games');
    const { result } = renderHook(() => useContextualSheetActions());
    expect(result.current).toEqual([]);
  });
});
