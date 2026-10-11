/**
 * #4190 — dopo il login dal form, AuthProvider vede l'utente senza ricaricare la pagina.
 *
 * Il form importava un altro hook omonimo, `@/hooks/useAuth`, con uno stato suo: il suo
 * `loadCurrentUser()` aggiornava il form e non AuthProvider. AuthProvider legge l'utente solo al
 * primo montaggio, cioè su /login da anonimo, e la navigazione dopo il login è lato client: `user`
 * restava null. Ne seguivano le liste delle chat vuote (`useChatSessions` ha `enabled: !!userId`),
 * l'avviso di sessione in scadenza mai mostrato, la UI admin nascosta.
 *
 * Questo test NON fa il mock di AuthProvider né di `@/hooks/useAuth`: è proprio il mock che
 * nascondeva il difetto.
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn().mockResolvedValue(undefined), refresh: vi.fn() }),
}));

const api = vi.hoisted(() => ({
  // La sessione esiste solo dopo il login: '/auth/me' risponde come il backend.
  session: { loggedIn: false },
  getMe: vi.fn(),
  login: vi.fn(),
  verify2FALogin: vi.fn(),
}));
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getMe: (...a: unknown[]) => api.getMe(...a),
      login: (...a: unknown[]) => api.login(...a),
      verify2FALogin: (...a: unknown[]) => api.verify2FALogin(...a),
      logout: vi.fn(),
      register: vi.fn(),
    },
  },
}));

vi.mock('@/lib/logger', () => ({
  logger: { error: vi.fn(), info: vi.fn(), warn: vi.fn() },
}));

vi.mock('@/components/auth/oauth-url', () => ({
  buildOAuthUrl: (provider: string) => `https://api.test/oauth/${provider}`,
}));

import { AuthProvider, useAuth } from '@/components/auth/AuthProvider';

import { LoginPageContent } from '../_content';

const USER = {
  id: 'user-1',
  email: 'user@example.com',
  displayName: 'User',
  role: 'user',
};

function AuthProbe() {
  const { user } = useAuth();
  return <span data-testid="auth-probe">{user?.email ?? 'anonymous'}</span>;
}

function renderLoginInsideProvider() {
  render(
    <AuthProvider>
      <LoginPageContent />
      <AuthProbe />
    </AuthProvider>
  );
}

function fillAndSubmit() {
  fireEvent.change(screen.getByLabelText(/auth\.login\.emailLabel/i), {
    target: { value: USER.email },
  });
  fireEvent.change(screen.getByLabelText(/auth\.login\.passwordLabel/i), {
    target: { value: 'StrongPassword1' },
  });
  fireEvent.submit(screen.getByTestId('login-form'));
}

beforeEach(() => {
  vi.clearAllMocks();
  api.session.loggedIn = false;
  api.getMe.mockImplementation(async () => (api.session.loggedIn ? USER : null));
});

async function settledAnonymous() {
  await waitFor(() => expect(api.getMe).toHaveBeenCalled());
  expect(screen.getByTestId('auth-probe')).toHaveTextContent('anonymous');
}

describe('LoginPageContent dentro il vero AuthProvider (#4190)', () => {
  it('after a password login AuthProvider exposes the user, without remounting', async () => {
    api.login.mockImplementation(async () => {
      api.session.loggedIn = true;
      return { requiresTwoFactor: false, user: USER };
    });
    renderLoginInsideProvider();
    await settledAnonymous();

    fillAndSubmit();

    await waitFor(() => expect(screen.getByTestId('auth-probe')).toHaveTextContent(USER.email));
  });

  it('after the 2FA step AuthProvider exposes the user', async () => {
    api.login.mockResolvedValue({ requiresTwoFactor: true, tempSessionToken: 'tmp-token' });
    api.verify2FALogin.mockImplementation(async () => {
      api.session.loggedIn = true;
      return USER;
    });
    renderLoginInsideProvider();
    await settledAnonymous();

    fillAndSubmit();
    const code = await screen.findByLabelText(/code|codice/i);
    fireEvent.change(code, { target: { value: '123456' } });
    fireEvent.submit(code.closest('form')!);

    await waitFor(() => expect(screen.getByTestId('auth-probe')).toHaveTextContent(USER.email));
  });
});
