/**
 * #4190 — dopo la registrazione dal form, AuthProvider vede l'utente senza ricaricare la pagina.
 *
 * Come per il login, il form usava l'hook omonimo `@/hooks/useAuth`, con uno stato suo, e
 * AuthProvider restava fermo all'anonimo letto al montaggio di /register. Questo test NON fa il
 * mock di AuthProvider né di `@/hooks/useAuth`.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn().mockResolvedValue(undefined), refresh: vi.fn() }),
}));

const api = vi.hoisted(() => ({
  session: { loggedIn: false },
  getMe: vi.fn(),
  register: vi.fn(),
}));
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getMe: (...a: unknown[]) => api.getMe(...a),
      register: (...a: unknown[]) => api.register(...a),
      login: vi.fn(),
      logout: vi.fn(),
    },
  },
}));

vi.mock('@/lib/api/context', () => ({
  useApiClient: () => ({
    accessRequests: {
      getRegistrationMode: vi
        .fn()
        .mockResolvedValue({ publicRegistrationEnabled: true, oauthEnabled: false }),
      requestAccess: vi.fn(),
    },
  }),
}));

vi.mock('@/lib/logger', () => ({
  logger: { error: vi.fn(), info: vi.fn(), warn: vi.fn() },
}));

vi.mock('@/components/auth/oauth-url', () => ({
  buildOAuthUrl: (provider: string) => `https://api.test/oauth/${provider}`,
}));

vi.mock('@/lib/analytics/flywheel-events', () => ({
  trackSignUp: vi.fn(),
}));

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { AuthProvider, useAuth } from '@/components/auth/AuthProvider';

import { RegisterPageContent } from '../_content';

const USER = { id: 'user-2', email: 'new@example.com', displayName: 'New', role: 'user' };

function AuthProbe() {
  const { user } = useAuth();
  return <span data-testid="auth-probe">{user?.email ?? 'anonymous'}</span>;
}

beforeEach(() => {
  vi.clearAllMocks();
  api.session.loggedIn = false;
  api.getMe.mockImplementation(async () => (api.session.loggedIn ? USER : null));
  api.register.mockImplementation(async () => {
    api.session.loggedIn = true;
    return USER;
  });
});

describe('RegisterPageContent dentro il vero AuthProvider (#4190)', () => {
  it('after registering AuthProvider exposes the new user, without remounting', async () => {
    render(
      <QueryClientProvider client={createTestQueryClient()}>
        <AuthProvider>
          <RegisterPageContent />
          <AuthProbe />
        </AuthProvider>
      </QueryClientProvider>
    );
    await screen.findByTestId('register-form');
    expect(screen.getByTestId('auth-probe')).toHaveTextContent('anonymous');

    fireEvent.change(screen.getByLabelText(/auth\.register\.emailLabel/i), {
      target: { value: USER.email },
    });
    fireEvent.change(screen.getByLabelText(/auth\.register\.passwordLabel/i), {
      target: { value: 'StrongPassword1' },
    });
    fireEvent.click(screen.getByTestId('register-terms'));
    fireEvent.submit(screen.getByTestId('register-form'));

    await waitFor(() => expect(screen.getByTestId('auth-probe')).toHaveTextContent(USER.email));
    expect(api.register).toHaveBeenCalledWith(
      expect.objectContaining({ email: USER.email, termsAccepted: true })
    );
  });
});
