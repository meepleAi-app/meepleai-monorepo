/**
 * Library toolkit landing page — #4770: il creatore della partita gioca, collegato al suo account,
 * come nei wizard di /sessions/new (#4767). Qui i giocatori erano solo nomi.
 */
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import GameToolkitLandingPage from '../page';

const routerPush = vi.hoisted(() => vi.fn());
vi.mock('next/navigation', () => ({
  useParams: () => ({ gameId: 'game-1' }),
  useRouter: () => ({ push: routerPush }),
}));
vi.mock('sonner', () => ({ toast: { error: vi.fn(), success: vi.fn(), info: vi.fn() } }));

const currentUser = vi.hoisted(() => ({
  current: null as { id: string; displayName?: string } | null,
}));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: currentUser.current }),
}));

const store = vi.hoisted(() => ({
  createSession: vi.fn(),
  addPlayer: vi.fn(),
  startSession: vi.fn(),
  isLoading: false,
}));
vi.mock('@/lib/stores/session-store', () => ({
  useSessionStore: Object.assign(() => store, { getState: () => store }),
}));

const inputs = () => screen.getAllByPlaceholderText(/^Player \d+$/) as HTMLInputElement[];

describe('Library toolkit landing — #4770: il creatore collegato al suo account', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    currentUser.current = { id: 'user-marta', displayName: 'Marta' };
    store.createSession.mockResolvedValue('session-1');
    store.addPlayer.mockResolvedValue(undefined);
    store.startSession.mockResolvedValue(undefined);
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ id: 'game-1', title: 'Azul', minPlayers: 1, maxPlayers: 4 }),
      })
    );
  });

  async function renderPage() {
    render(<GameToolkitLandingPage />);
    await screen.findByText('Azul Toolkit');
  }

  it('svuotare il posto del creatore lo scollega: riscrivere il suo nome altrove non collega nessuno', async () => {
    await renderPage();
    expect(inputs()[0].value).toBe('Marta');

    // Il creatore scrive il proprio nome nel secondo posto e svuota il primo, poi ci scrive Bob.
    fireEvent.click(screen.getByRole('button', { name: '+ Add Player' }));
    fireEvent.change(inputs()[1], { target: { value: 'Marta' } });
    fireEvent.change(inputs()[0], { target: { value: '' } });
    fireEvent.change(inputs()[0], { target: { value: 'Bob' } });
    fireEvent.click(screen.getByRole('button', { name: /Start Azul Session/ }));
    await waitFor(() =>
      expect(routerPush).toHaveBeenCalledWith('/library/game-1/toolkit/session-1')
    );

    // Svuotare il primo posto lo ha scollegato: «Marta» riscritta nel secondo è solo un nome.
    expect(store.addPlayer.mock.calls.map(c => c[0])).toEqual([
      { displayName: 'Bob', color: 'Red' },
      { displayName: 'Marta', color: 'Blue' },
    ]);
  });

  it('il creatore lasciato al suo posto entra con il suo account, prima degli altri', async () => {
    await renderPage();
    fireEvent.click(screen.getByRole('button', { name: '+ Add Player' }));
    fireEvent.change(inputs()[1], { target: { value: 'Bob' } });
    fireEvent.click(screen.getByRole('button', { name: /Start Azul Session/ }));
    await waitFor(() => expect(routerPush).toHaveBeenCalled());

    expect(store.addPlayer.mock.calls.map(c => c[0])).toEqual([
      { displayName: 'Marta', color: 'Red', userId: 'user-marta' },
      { displayName: 'Bob', color: 'Blue' },
    ]);
  });
});
