/**
 * Tests for useMeepleCardActions (catalog context).
 *
 * S3 (library-to-game epic): direct add + auto-navigate + "Vai al gioco"
 * action when the game is already in library.
 */
import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useMeepleCardActions } from '../useMeepleCardActions';

// ============================================================================
// Mocks
// ============================================================================

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();
vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
    error: (...args: unknown[]) => mockToastError(...args),
  },
}));

const mockAddMutate = vi.fn();
const mockCurrentUser = { data: null as { id: string; role: string } | null };
const mockLibraryStatus = { data: null as { inLibrary: boolean; isFavorite: boolean } | null };

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockCurrentUser,
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useAddGameToLibrary: () => ({ mutate: mockAddMutate }),
  useGameInLibraryStatus: () => mockLibraryStatus,
  useRemoveGameFromLibrary: () => ({ mutate: vi.fn() }),
}));

// ============================================================================
// Tests
// ============================================================================

const GAME_ID = '00000000-0000-4000-8000-000000000001';

describe('useMeepleCardActions — catalog context (S3)', () => {
  beforeEach(() => {
    mockPush.mockReset();
    mockAddMutate.mockReset();
    mockToastSuccess.mockReset();
    mockToastError.mockReset();
    mockCurrentUser.data = null;
    mockLibraryStatus.data = null;
  });

  it('shows Add action disabled for guest user', () => {
    mockCurrentUser.data = null;
    const { result } = renderHook(() => useMeepleCardActions('game', GAME_ID, 'catalog'));
    const add = result.current.find(a => a.label === 'Aggiungi a Libreria');
    expect(add).toBeDefined();
    expect(add!.disabled).toBe(true);
    expect(add!.hidden).toBeFalsy();
    expect(add!.disabledTooltip).toMatch(/accedi/i);
  });

  it('shows Add action enabled for authenticated user not in library', () => {
    mockCurrentUser.data = { id: 'user-1', role: 'user' };
    mockLibraryStatus.data = { inLibrary: false, isFavorite: false };
    const { result } = renderHook(() => useMeepleCardActions('game', GAME_ID, 'catalog'));
    const add = result.current.find(a => a.label === 'Aggiungi a Libreria');
    expect(add).toBeDefined();
    expect(add!.disabled).toBeFalsy();
    expect(add!.hidden).toBe(false);
  });

  it('hides Add and shows Go-to-game when game is already in library', () => {
    mockCurrentUser.data = { id: 'user-1', role: 'user' };
    mockLibraryStatus.data = { inLibrary: true, isFavorite: false };
    const { result } = renderHook(() => useMeepleCardActions('game', GAME_ID, 'catalog'));
    const add = result.current.find(a => a.label === 'Aggiungi a Libreria');
    const goTo = result.current.find(a => a.label === 'Vai al gioco');
    expect(add!.hidden).toBe(true);
    expect(goTo).toBeDefined();
    expect(goTo!.hidden).toBe(false);
  });

  it('clicking Add triggers direct mutate + toast + router.push on success', () => {
    mockCurrentUser.data = { id: 'user-1', role: 'user' };
    mockLibraryStatus.data = { inLibrary: false, isFavorite: false };
    mockAddMutate.mockImplementation((_vars, opts) => {
      opts?.onSuccess?.();
    });

    const { result } = renderHook(() => useMeepleCardActions('game', GAME_ID, 'catalog'));
    const add = result.current.find(a => a.label === 'Aggiungi a Libreria')!;
    add.onClick?.();

    expect(mockAddMutate).toHaveBeenCalledWith(
      { gameId: GAME_ID },
      expect.objectContaining({ onSuccess: expect.any(Function), onError: expect.any(Function) })
    );
    expect(mockToastSuccess).toHaveBeenCalledWith('Aggiunto alla libreria');
    expect(mockPush).toHaveBeenCalledWith(`/library/games/${GAME_ID}`);
  });

  it('clicking Add triggers error toast on failure', () => {
    mockCurrentUser.data = { id: 'user-1', role: 'user' };
    mockLibraryStatus.data = { inLibrary: false, isFavorite: false };
    mockAddMutate.mockImplementation((_vars, opts) => {
      opts?.onError?.(new Error('Quota piena'));
    });

    const { result } = renderHook(() => useMeepleCardActions('game', GAME_ID, 'catalog'));
    const add = result.current.find(a => a.label === 'Aggiungi a Libreria')!;
    add.onClick?.();

    expect(mockToastError).toHaveBeenCalledWith('Quota piena');
    expect(mockPush).not.toHaveBeenCalled();
  });

  it('clicking Go-to-game navigates without mutate', () => {
    mockCurrentUser.data = { id: 'user-1', role: 'user' };
    mockLibraryStatus.data = { inLibrary: true, isFavorite: false };
    const { result } = renderHook(() => useMeepleCardActions('game', GAME_ID, 'catalog'));
    const goTo = result.current.find(a => a.label === 'Vai al gioco')!;
    goTo.onClick?.();

    expect(mockPush).toHaveBeenCalledWith(`/library/games/${GAME_ID}`);
    expect(mockAddMutate).not.toHaveBeenCalled();
  });

  it('onAddToLibrary callback overrides direct mutate (wizard path)', () => {
    mockCurrentUser.data = { id: 'user-1', role: 'user' };
    mockLibraryStatus.data = { inLibrary: false, isFavorite: false };
    const onAddToLibrary = vi.fn();
    const { result } = renderHook(() =>
      useMeepleCardActions('game', GAME_ID, 'catalog', { onAddToLibrary })
    );
    const add = result.current.find(a => a.label === 'Aggiungi a Libreria')!;
    add.onClick?.();

    expect(onAddToLibrary).toHaveBeenCalledOnce();
    expect(mockAddMutate).not.toHaveBeenCalled();
    expect(mockPush).not.toHaveBeenCalled();
  });
});
