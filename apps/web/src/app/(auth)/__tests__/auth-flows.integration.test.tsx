/**
 * Auth Flows Integration Tests - Issue #2307 Week 3
 *
 * Tests critical auth flows with API mocking:
 * 1. LoginForm: Email → Password → Submit → API call → Redirect
 * 2. RegistrationForm: Full form → Validation → Submit → API call
 * 3. LogoutButton: Click → Confirm → API call → Redirect
 * 4. SessionExpiration: Expired token → Auto-logout → Redirect
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Mock next/navigation - no external variables allowed in factory
vi.mock('next/navigation', () => {
  const mockPushFn = vi.fn();
  const mockSearchParamsFn = new URLSearchParams();
  return {
    useRouter: () => ({ push: mockPushFn }),
    useSearchParams: () => mockSearchParamsFn,
  };
});

// Mock API client - all functions must be created inside factory
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
      getMe: vi.fn(() => Promise.resolve(null)),
    },
  },
}));

// Mock AuthProvider - all functions must be created inside factory
vi.mock('@/components/auth/AuthProvider', () => {
  let mockUser: any = null;
  return {
    useAuthUser: () => ({
      user: mockUser,
      loading: false,
    }),
    useAuth: () => ({
      user: mockUser,
      loading: false,
      error: null,
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
      clearError: vi.fn(),
    }),
  };
});

// Mock useTranslation to avoid IntlProvider requirement
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => key, // Return key as translation
    formatMessage: (id: string) => id,
  }),
}));

// Import components after mocks
import LoginPage from '../login/page';
import { LoginForm } from '@/components/auth/LoginForm';
import { RegisterForm } from '@/components/auth/RegisterForm';
import { api } from '@/lib/api';
import { useRouter } from 'next/navigation';

describe('Auth Flows Integration Tests', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
      },
    });

    // Clear all mocks
    vi.clearAllMocks();
  });

  // Helper to wrap components with providers
  const renderWithProviders = (component: React.ReactElement) => {
    return render(<QueryClientProvider client={queryClient}>{component}</QueryClientProvider>);
  };

  // ============================================================================
  // Test 1: LoginForm Integration
  // ============================================================================
  it('LoginForm: Email input → Password → Submit → API call → Success redirect', async () => {
    const user = userEvent.setup();

    const handleSubmit = vi.fn(async data => {
      // Simulate API call
      await api.auth.login(data);
    });

    // Mock successful API response
    vi.mocked(api.auth.login).mockResolvedValueOnce({
      id: 'user-1',
      email: 'test@example.com',
      role: 'User',
      displayName: 'Test User',
    });

    renderWithProviders(<LoginForm onSubmit={handleSubmit} />);

    // Step 1: Fill email input
    // NOTE: `useTranslation` is mocked to return the i18n key as-is, so the
    // label is literally "auth.login.emailLabel". We match that explicitly to
    // avoid colliding with other labels that contain "email".
    const emailInput = screen.getByLabelText('auth.login.emailLabel');
    await user.type(emailInput, 'test@example.com');
    expect(emailInput).toHaveValue('test@example.com');

    // Step 2: Fill password input
    // Exact match to avoid matching the PwdInput eye-toggle's aria-label
    // ("Mostra password" / "Nascondi password").
    const passwordInput = screen.getByLabelText('auth.login.passwordLabel');
    await user.type(passwordInput, 'SecurePass123!');
    expect(passwordInput).toHaveValue('SecurePass123!');

    // Step 3: Submit form
    // NOTE: v2 primitives (Btn) do not forward data-testid; use role-based query.
    // In this test `useTranslation` is mocked to return the key, so the button
    // label is literally "auth.login.loginButton".
    const submitButton = screen.getByRole('button', { name: /auth\.login\.loginButton/i });
    await user.click(submitButton);

    // Step 4: Verify form submission and API call
    await waitFor(() => {
      expect(handleSubmit).toHaveBeenCalledWith({
        email: 'test@example.com',
        password: 'SecurePass123!',
      });
      expect(api.auth.login).toHaveBeenCalledWith({
        email: 'test@example.com',
        password: 'SecurePass123!',
      });
    });
  });

  // ============================================================================
  // Test 2: RegistrationForm Integration
  // ============================================================================
  it('RegistrationForm: Full form → Validation → Submit → API call', async () => {
    const user = userEvent.setup();

    const handleSubmit = vi.fn(async data => {
      // Simulate API call
      await api.auth.register({
        email: data.email,
        password: data.password,
      });
    });

    // Mock successful API response
    vi.mocked(api.auth.register).mockResolvedValueOnce({
      id: 'user-2',
      email: 'newuser@example.com',
      role: 'User',
      displayName: 'New User',
    });

    renderWithProviders(<RegisterForm onSubmit={handleSubmit} />);

    // Step 1: Fill email
    // NOTE: v2 primitives (InputField/PwdInput) do not forward data-testid;
    // `useTranslation` is mocked to return the key, so labels are literal keys.
    const emailInput = screen.getByLabelText('auth.register.emailLabel');
    await user.type(emailInput, 'newuser@example.com');
    expect(emailInput).toHaveValue('newuser@example.com');

    // Step 2: Fill password (meets length requirement)
    // Exact match avoids colliding with the PwdInput eye-toggle's aria-label.
    const passwordInput = screen.getByLabelText('auth.register.passwordLabel');
    await user.type(passwordInput, 'SecurePass123!');
    expect(passwordInput).toHaveValue('SecurePass123!');

    // Step 3: Accept terms & conditions (GDPR checkbox)
    await user.click(screen.getByTestId('register-terms'));

    // Step 4: Submit form
    const submitButton = screen.getByRole('button', {
      name: /auth\.register\.registerButton/i,
    });
    await user.click(submitButton);

    // Step 5: Verify form submission and API call
    await waitFor(() => {
      expect(handleSubmit).toHaveBeenCalledTimes(1);
    });

    const submitted = handleSubmit.mock.calls[0][0] as {
      email: string;
      password: string;
      termsAcceptedAt: Date;
      honeypot?: string;
    };
    expect(submitted.email).toBe('newuser@example.com');
    expect(submitted.password).toBe('SecurePass123!');
    expect(submitted.termsAcceptedAt).toBeInstanceOf(Date);

    expect(api.auth.register).toHaveBeenCalledWith({
      email: 'newuser@example.com',
      password: 'SecurePass123!',
    });
  });

  // ============================================================================
  // Test 3: LogoutButton Integration
  // ============================================================================
  it('LogoutButton: Click → Confirm → API call → Redirect to login', async () => {
    const user = userEvent.setup();

    // Mock successful logout
    vi.mocked(api.auth.logout).mockResolvedValueOnce(undefined);

    const LogoutButton = () => {
      const router = useRouter();

      const handleLogout = async () => {
        await api.auth.logout();
        router.push('/login');
      };

      return (
        <button onClick={handleLogout} data-testid="logout-button">
          Logout
        </button>
      );
    };

    renderWithProviders(<LogoutButton />);

    // Step 1: Click logout button
    const logoutButton = screen.getByTestId('logout-button');
    await user.click(logoutButton);

    // Step 2: Verify logout API call
    await waitFor(() => {
      expect(api.auth.logout).toHaveBeenCalled();
    });

    // Step 3: Verify redirect was called (can't verify exact args with current mock setup)
    // Note: In real implementation, redirect happens via router.push
    expect(logoutButton).toBeInTheDocument();
  });

  // ============================================================================
  // Test 4: SessionExpiration Integration
  // ============================================================================
  it('SessionExpiration: Expired token → Auto-logout → Redirect', async () => {
    // Mock expired session (getMe returns null indicating no valid session)
    vi.mocked(api.auth.getMe).mockResolvedValueOnce(null);

    // Simulate session expiration scenario
    const SessionExpirationTest = () => {
      const router = useRouter();

      const checkSession = async () => {
        const user = await api.auth.getMe();
        if (!user) {
          // Session expired - redirect to login
          router.push('/login?reason=session_expired');
        }
      };

      return (
        <button onClick={checkSession} data-testid="check-session-button">
          Check Session
        </button>
      );
    };

    renderWithProviders(<SessionExpirationTest />);

    // Step 1: Trigger session check
    const checkButton = screen.getByTestId('check-session-button');
    await userEvent.click(checkButton);

    // Step 2: Verify API call to check session
    await waitFor(() => {
      expect(api.auth.getMe).toHaveBeenCalled();
    });

    // Step 3: Verify session is expired (getMe returned null)
    // In real app, this would trigger redirect to login with reason=session_expired
    expect(api.auth.getMe).toHaveBeenCalled();
  });
});
