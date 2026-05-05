/**
 * AuthModal Tests (auth-flow v2)
 *
 * Covers the v2 refactor:
 * - Segmented-control tab switcher (role="tablist" / role="tab")
 * - Default login mode + register mode via `defaultMode` prop
 * - Close behavior (Escape via AccessibleModal)
 *
 * External dependencies (useAuth, api client, next/navigation, analytics)
 * are mocked so tests focus on AuthModal UI wiring.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

// --- Translation mock (must be defined before AuthModal import) -------------
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

// --- Router mock -------------------------------------------------------------
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn().mockResolvedValue(undefined),
    refresh: vi.fn(),
  }),
}));

// --- Auth hook mock ---------------------------------------------------------
// NOTE: Use vi.hoisted so the stable reference is set up BEFORE the mock
// factory runs. Stable identities prevent the AuthModal reset effect from
// re-firing on every render (which would clobber `activeTab` back to
// `defaultMode` between clicks and state updates).
const { mockAuth } = vi.hoisted(() => ({
  mockAuth: {
    register: vi.fn().mockResolvedValue({
      id: 'u1',
      email: 'test@example.com',
      role: 'User',
    }),
    loadCurrentUser: vi.fn().mockResolvedValue(undefined),
    clearError: vi.fn(),
  },
}));
vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => mockAuth,
}));

// --- API client mock --------------------------------------------------------
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      login: vi.fn().mockResolvedValue({ user: null, requiresTwoFactor: false }),
      verify2FALogin: vi.fn(),
    },
  },
}));

// --- Analytics / logger mocks ----------------------------------------------
vi.mock('@/lib/analytics/flywheel-events', () => ({
  trackSignUp: vi.fn(),
}));

vi.mock('@/lib/logger', () => ({
  logger: {
    error: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
  },
}));

// Import AFTER mocks are registered
import { AuthModal } from '../AuthModal';

describe('AuthModal (v2 segmented tabs)', () => {
  it('renders login tab by default', () => {
    render(<AuthModal isOpen onClose={vi.fn()} />);
    expect(screen.getByTestId('login-form')).toBeInTheDocument();
  });

  it('renders register tab when defaultMode=register', () => {
    render(<AuthModal isOpen onClose={vi.fn()} defaultMode="register" />);
    expect(screen.getByTestId('register-form')).toBeInTheDocument();
  });

  it('switches between tabs via segmented control', () => {
    render(<AuthModal isOpen onClose={vi.fn()} />);

    // Verify both tabs exist with correct role + accessible name (mocked i18n keys)
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(2);
    expect(tabs[0]).toHaveTextContent(/auth\.modal\.loginTab/);
    expect(tabs[1]).toHaveTextContent(/auth\.modal\.registerTab/);

    // Click register tab via data-testid (stable cross-locale)
    fireEvent.click(screen.getByTestId('auth-tab-register'));
    expect(screen.getByTestId('register-form')).toBeInTheDocument();
    expect(screen.getByTestId('auth-tab-register')).toHaveAttribute('aria-selected', 'true');

    // Click login tab
    fireEvent.click(screen.getByTestId('auth-tab-login'));
    expect(screen.getByTestId('login-form')).toBeInTheDocument();
    expect(screen.getByTestId('auth-tab-login')).toHaveAttribute('aria-selected', 'true');
  });

  it('calls onClose when Escape is pressed (AccessibleModal behavior)', () => {
    const onClose = vi.fn();
    render(<AuthModal isOpen onClose={onClose} />);

    // Radix Dialog listens on document for Escape
    fireEvent.keyDown(document.body, { key: 'Escape', code: 'Escape' });
    expect(onClose).toHaveBeenCalled();
  });
});
