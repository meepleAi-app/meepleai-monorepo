/**
 * #4190, passo 2 — una fonte unica per l'utente corrente.
 *
 * AuthProvider teneva l'utente in uno `useState` suo, letto al primo montaggio, mentre
 * `useCurrentUser` lo teneva nella query `['user','current']`. Erano due verità: il passo 1
 * (#4208) ha riallineato login e registrazione, ma qualunque altro cambio (un logout in un'altra
 * scheda visto al ritorno sulla finestra, un `invalidate`) aggiornava una sola delle due.
 * Ora AuthProvider legge dalla query, e login, registrazione e logout scrivono lì.
 */

import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { userKeys } from '@/hooks/queries/useCurrentUser';

const api = vi.hoisted(() => ({
  getMe: vi.fn(),
  login: vi.fn(),
  logout: vi.fn(),
  register: vi.fn(),
}));
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getMe: (...a: unknown[]) => api.getMe(...a),
      login: (...a: unknown[]) => api.login(...a),
      logout: (...a: unknown[]) => api.logout(...a),
      register: (...a: unknown[]) => api.register(...a),
    },
  },
}));
vi.mock('@/lib/logger', () => ({ logger: { error: vi.fn(), info: vi.fn(), warn: vi.fn() } }));

import { AuthProvider, useAuth } from '../AuthProvider';

const USER = { id: 'user-1', email: 'user@example.com', displayName: 'User', role: 'user' };

let auth: ReturnType<typeof useAuth> | null = null;
function Probe() {
  auth = useAuth();
  return <span data-testid="probe">{auth.user?.email ?? 'anonymous'}</span>;
}

function renderWithClient(client: QueryClient) {
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <Probe />
      </AuthProvider>
    </QueryClientProvider>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  auth = null;
});

describe('AuthProvider — fonte unica (#4190)', () => {
  it('segue la query dell’utente corrente quando cambia fuori da AuthProvider', async () => {
    api.getMe.mockResolvedValue(USER);
    const client = createTestQueryClient();
    renderWithClient(client);
    await waitFor(() => expect(screen.getByTestId('probe')).toHaveTextContent(USER.email));

    // Un logout visto altrove (un'altra scheda, un invalidate): la query diventa null.
    act(() => {
      client.setQueryData(userKeys.current(), null);
    });

    await waitFor(() => expect(screen.getByTestId('probe')).toHaveTextContent('anonymous'));
  });

  it('il login scrive l’utente nella query, senza aspettare un altro giro', async () => {
    api.getMe.mockResolvedValue(null);
    api.login.mockResolvedValue({ requiresTwoFactor: false, user: USER });
    const client = createTestQueryClient();
    renderWithClient(client);
    await waitFor(() => expect(auth).not.toBeNull());

    await act(async () => {
      await auth!.login(USER.email, 'StrongPassword1');
    });

    expect(client.getQueryData(userKeys.current())).toEqual(USER);
    expect(screen.getByTestId('probe')).toHaveTextContent(USER.email);
  });

  it('il logout scrive null nella query', async () => {
    api.getMe.mockResolvedValue(USER);
    api.logout.mockResolvedValue(undefined);
    const client = createTestQueryClient();
    renderWithClient(client);
    await waitFor(() => expect(screen.getByTestId('probe')).toHaveTextContent(USER.email));

    await act(async () => {
      await auth!.logout();
    });

    expect(client.getQueryData(userKeys.current())).toBeNull();
    expect(screen.getByTestId('probe')).toHaveTextContent('anonymous');
  });
});
