/**
 * EmailVerificationBanner — F1 #1974 regression tests.
 *
 * Contract:
 *   - Hidden when query is loading
 *   - Hidden when no user
 *   - Hidden when `emailVerified === true`
 *   - Hidden when `emailVerified === undefined` (legacy payload, OAuth)
 *   - Shown when `emailVerified === false`
 *   - Dismissible per-session via sessionStorage; stays hidden after dismiss
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, it, expect, vi } from 'vitest';

import type { AuthUser } from '@/types/auth';

import { EmailVerificationBanner } from '../EmailVerificationBanner';

// ─── Mocks ──────────────────────────────────────────────────────────────────

const mockUseCurrentUser = vi.fn();
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));

const baseMessages = {
  'auth.emailVerification.banner.title': 'Verifica la tua email',
  'auth.emailVerification.banner.description':
    'Conferma la tua email per sbloccare tutte le funzionalità di MeepleAI.',
  'auth.emailVerification.banner.cta': 'Verifica ora',
  'auth.emailVerification.banner.dismissAriaLabel': 'Chiudi banner',
};

function renderBanner() {
  return render(
    <IntlProvider locale="it" messages={baseMessages}>
      <EmailVerificationBanner />
    </IntlProvider>
  );
}

const fakeUser = (overrides: Partial<AuthUser> = {}): AuthUser => ({
  id: '00000000-0000-0000-0000-000000000001',
  email: 'test@example.com',
  role: 'User',
  ...overrides,
});

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
});

describe('EmailVerificationBanner (F1 #1974)', () => {
  it('renders null while useCurrentUser is loading', () => {
    mockUseCurrentUser.mockReturnValue({ data: undefined, isLoading: true });
    const { container } = renderBanner();
    expect(container.firstChild).toBeNull();
  });

  it('renders null when there is no current user', () => {
    mockUseCurrentUser.mockReturnValue({ data: null, isLoading: false });
    const { container } = renderBanner();
    expect(container.firstChild).toBeNull();
  });

  it('renders null when emailVerified is true', () => {
    mockUseCurrentUser.mockReturnValue({
      data: fakeUser({ emailVerified: true }),
      isLoading: false,
    });
    const { container } = renderBanner();
    expect(container.firstChild).toBeNull();
  });

  it('renders null when emailVerified is undefined (legacy payload / OAuth)', () => {
    mockUseCurrentUser.mockReturnValue({
      data: fakeUser({ emailVerified: undefined }),
      isLoading: false,
    });
    const { container } = renderBanner();
    expect(container.firstChild).toBeNull();
  });

  it('renders the banner when emailVerified is explicitly false', () => {
    mockUseCurrentUser.mockReturnValue({
      data: fakeUser({ emailVerified: false }),
      isLoading: false,
    });
    renderBanner();
    expect(screen.getByTestId('email-verification-banner')).toBeInTheDocument();
    expect(screen.getByText('Verifica la tua email')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /verifica ora/i })).toHaveAttribute(
      'href',
      '/email-verification'
    );
  });

  it('hides the banner after the dismiss button is clicked', () => {
    mockUseCurrentUser.mockReturnValue({
      data: fakeUser({ emailVerified: false }),
      isLoading: false,
    });
    renderBanner();
    expect(screen.getByTestId('email-verification-banner')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('email-verification-banner-dismiss'));
    expect(screen.queryByTestId('email-verification-banner')).toBeNull();
  });

  it('stays dismissed across remounts in the same session (sessionStorage)', () => {
    mockUseCurrentUser.mockReturnValue({
      data: fakeUser({ emailVerified: false }),
      isLoading: false,
    });
    const { unmount } = renderBanner();
    fireEvent.click(screen.getByTestId('email-verification-banner-dismiss'));
    expect(screen.queryByTestId('email-verification-banner')).toBeNull();
    unmount();

    // Re-mount within the same "session" — the dismissal must persist.
    renderBanner();
    expect(screen.queryByTestId('email-verification-banner')).toBeNull();
  });
});
