/**
 * #4190 — dopo il reset della password, il login automatico aggiorna AuthProvider.
 *
 * La pagina fa login da sola (`api.auth.login`) e poi naviga lato client verso /chat. Senza
 * aggiornare AuthProvider, che ha letto l'utente da anonimo al montaggio, `/chat` partiva con
 * `user` nullo: le liste delle chat restavano vuote. Questo test NON fa il mock di AuthProvider.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn().mockResolvedValue(undefined), refresh: vi.fn() }),
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

const api = vi.hoisted(() => ({
  session: { loggedIn: false },
  getMe: vi.fn(),
  login: vi.fn(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getMe: (...a: unknown[]) => api.getMe(...a),
      login: (...a: unknown[]) => api.login(...a),
      verifyResetToken: vi.fn().mockResolvedValue(undefined),
      confirmPasswordReset: vi.fn().mockResolvedValue({ message: 'ok' }),
      requestPasswordReset: vi.fn(),
      logout: vi.fn(),
      register: vi.fn(),
    },
  },
  ApiError: class ApiError extends Error {},
}));

vi.mock('@/lib/logger', () => ({
  logger: { error: vi.fn(), info: vi.fn(), warn: vi.fn() },
}));

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { AuthProvider, useAuth } from '@/components/auth/AuthProvider';

import { ResetPasswordPageContent } from '../_content';

const USER = { id: 'user-3', email: 'reset@example.com', displayName: 'Reset', role: 'user' };

function AuthProbe() {
  const { user } = useAuth();
  return <span data-testid="auth-probe">{user?.email ?? 'anonymous'}</span>;
}

beforeEach(() => {
  vi.clearAllMocks();
  api.session.loggedIn = false;
  api.getMe.mockImplementation(async () => (api.session.loggedIn ? USER : null));
  api.login.mockImplementation(async () => {
    api.session.loggedIn = true;
    return { requiresTwoFactor: false, user: USER };
  });
});

describe('ResetPasswordPageContent dentro il vero AuthProvider (#4190)', () => {
  it('after the automatic login AuthProvider exposes the user, before navigating', async () => {
    render(
      <QueryClientProvider client={createTestQueryClient()}>
        <AuthProvider>
          <ResetPasswordPageContent token="tok-4190" />
          <AuthProbe />
        </AuthProvider>
      </QueryClientProvider>
    );

    const pwd = await screen.findByLabelText('auth.resetPassword.passwordLabel');
    const confirm = screen.getByLabelText('auth.resetPassword.confirmPasswordLabel');
    const submit = await screen.findByTestId('reset-password-confirm');
    await waitFor(() => expect(submit).not.toHaveAttribute('aria-busy', 'true'));
    expect(screen.getByTestId('auth-probe')).toHaveTextContent('anonymous');

    fireEvent.change(pwd, { target: { value: 'StrongPassword1' } });
    fireEvent.change(confirm, { target: { value: 'StrongPassword1' } });
    await waitFor(() => expect(submit).not.toBeDisabled());
    fireEvent.submit(submit.closest('form')!);

    await waitFor(() => expect(api.login).toHaveBeenCalled());
    await waitFor(() => expect(screen.getByTestId('auth-probe')).toHaveTextContent(USER.email));
  });
});
