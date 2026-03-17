/**
 * Register Page Content Tests (invite-only registration feature)
 *
 * 1. Shows register form (AuthModal) when public registration is enabled
 * 2. Shows RequestAccessForm when public registration is disabled
 */

import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// ────────────────────────────────────────────────────────────────────────────
// Mocks
// ────────────────────────────────────────────────────────────────────────────

const mockGetRegistrationMode = vi.fn();

vi.mock('@/lib/api/context', () => ({
  useApiClient: vi.fn(),
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, defaultMessage?: string) => defaultMessage ?? key,
    locale: 'en',
    formatMessage: vi.fn(),
    formatNumber: vi.fn(),
    formatDate: vi.fn(),
    formatTime: vi.fn(),
    formatRelativeTime: vi.fn(),
  }),
}));

// Mock next/navigation (required by RegisterPageContent)
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() }),
  useSearchParams: () => ({ get: vi.fn().mockReturnValue(null) }),
  usePathname: () => '/register',
}));

// Mock AuthModal so we don't render its full internals
vi.mock('@/components/auth', () => ({
  AuthModal: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="auth-modal">AuthModal</div> : null,
}));

// Mock RequestAccessForm to keep the test focused on the page's branching logic
vi.mock('@/components/auth/RequestAccessForm', () => ({
  RequestAccessForm: () => <div data-testid="request-access-form">RequestAccessForm</div>,
}));

// Mock AuthLayout to render children transparently
vi.mock('@/components/layouts', () => ({
  AuthLayout: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

// ────────────────────────────────────────────────────────────────────────────
// Import component under test after mocks
// ────────────────────────────────────────────────────────────────────────────

import { useApiClient } from '@/lib/api/context';
import { RegisterPageContent } from '@/app/(auth)/register/_content';

const mockUseApiClient = vi.mocked(useApiClient);

// ────────────────────────────────────────────────────────────────────────────
// Test suite
// ────────────────────────────────────────────────────────────────────────────

describe('RegisterPageContent', () => {
  beforeEach(() => {
    mockGetRegistrationMode.mockReset();
    mockUseApiClient.mockReturnValue({
      accessRequests: { getRegistrationMode: mockGetRegistrationMode },
    } as any);
  });

  it('shows AuthModal (register form) when public registration is enabled', async () => {
    mockGetRegistrationMode.mockResolvedValueOnce({ publicRegistrationEnabled: true });

    render(<RegisterPageContent />);

    await waitFor(() => {
      expect(screen.getByTestId('auth-modal')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('request-access-form')).not.toBeInTheDocument();
  });

  it('shows RequestAccessForm when public registration is disabled', async () => {
    mockGetRegistrationMode.mockResolvedValueOnce({ publicRegistrationEnabled: false });

    render(<RegisterPageContent />);

    await waitFor(() => {
      expect(screen.getByTestId('request-access-form')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('auth-modal')).not.toBeInTheDocument();
  });

  it('defaults to RequestAccessForm (invite-only) when API call fails', async () => {
    mockGetRegistrationMode.mockRejectedValueOnce(new Error('Network error'));

    render(<RegisterPageContent />);

    await waitFor(() => {
      expect(screen.getByTestId('request-access-form')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('auth-modal')).not.toBeInTheDocument();
  });
});
