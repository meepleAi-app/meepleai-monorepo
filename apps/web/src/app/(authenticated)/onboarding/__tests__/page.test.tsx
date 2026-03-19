/**
 * Onboarding Page Tests
 *
 * Tests:
 * 1. Renders form with pre-filled read-only email
 * 2. Requires display name (submit without name should not call API)
 * 3. Submits profile with correct object shape and completes onboarding
 * 4. Redirects if already onboarded
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// ─── Router mocks ─────────────────────────────────────────────────────────────

const mockPush = vi.hoisted(() => vi.fn());
const mockReplace = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush, replace: mockReplace }),
}));

// ─── API mocks ────────────────────────────────────────────────────────────────

const mockUpdateProfile = vi.hoisted(() => vi.fn());
const mockCompleteOnboarding = vi.hoisted(() => vi.fn());
const mockUploadAvatar = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  createApiClient: () => ({
    auth: {
      updateProfile: mockUpdateProfile,
      completeOnboarding: mockCompleteOnboarding,
      uploadAvatar: mockUploadAvatar,
    },
  }),
}));

// ─── useAuth mock ─────────────────────────────────────────────────────────────

const mockUseAuth = vi.hoisted(() => vi.fn());

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: mockUseAuth,
}));

// ─── next/image mock ──────────────────────────────────────────────────────────

vi.mock('next/image', () => ({
  default: ({ src, alt }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} />
  ),
}));

// ─── Subject under test ───────────────────────────────────────────────────────

import OnboardingPage from '../page';

// ─── Helpers ──────────────────────────────────────────────────────────────────

const defaultUser = {
  id: 'user-1',
  email: 'test@example.com',
  displayName: null,
  role: 'User',
  onboardingCompleted: false,
};

function setupAuth(overrides: Partial<typeof defaultUser> = {}, loading = false) {
  mockUseAuth.mockReturnValue({
    user: { ...defaultUser, ...overrides },
    loading,
  });
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('OnboardingPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUpdateProfile.mockResolvedValue({ ok: true });
    mockCompleteOnboarding.mockResolvedValue({ ok: true });
    mockUploadAvatar.mockResolvedValue({ avatarUrl: 'https://example.com/avatar.jpg' });
  });

  it('renders form with pre-filled read-only email', () => {
    setupAuth();
    render(<OnboardingPage />);

    expect(screen.getByText('Benvenuto!')).toBeInTheDocument();
    expect(screen.getByLabelText('Display Name')).toBeInTheDocument();

    const emailInput = screen.getByLabelText('Email') as HTMLInputElement;
    expect(emailInput.value).toBe('test@example.com');
    expect(emailInput.disabled).toBe(true);
  });

  it('does not call API when display name is empty', async () => {
    setupAuth();
    const user = userEvent.setup();
    render(<OnboardingPage />);

    const submitButton = screen.getByRole('button', { name: /Entra/i });
    await user.click(submitButton);

    expect(mockUpdateProfile).not.toHaveBeenCalled();
    expect(mockCompleteOnboarding).not.toHaveBeenCalled();
  });

  it('does not call API when display name is too short (1 char)', async () => {
    setupAuth();
    const user = userEvent.setup();
    render(<OnboardingPage />);

    await user.type(screen.getByLabelText('Display Name'), 'A');
    await user.click(screen.getByRole('button', { name: /Entra/i }));

    expect(mockUpdateProfile).not.toHaveBeenCalled();
    expect(mockCompleteOnboarding).not.toHaveBeenCalled();
  });

  it('submits profile with correct object shape and completes onboarding', async () => {
    setupAuth();
    const user = userEvent.setup();
    render(<OnboardingPage />);

    await user.type(screen.getByLabelText('Display Name'), 'Mario Rossi');
    await user.click(screen.getByRole('button', { name: /Entra/i }));

    await waitFor(() => {
      expect(mockUpdateProfile).toHaveBeenCalledWith({ displayName: 'Mario Rossi' });
    });
    expect(mockCompleteOnboarding).toHaveBeenCalledWith(false);
    expect(mockPush).toHaveBeenCalledWith('/dashboard');
  });

  it('trims whitespace before submitting', async () => {
    setupAuth();
    const user = userEvent.setup();
    render(<OnboardingPage />);

    await user.type(screen.getByLabelText('Display Name'), '  Mario  ');
    await user.click(screen.getByRole('button', { name: /Entra/i }));

    await waitFor(() => {
      expect(mockUpdateProfile).toHaveBeenCalledWith({ displayName: 'Mario' });
    });
  });

  it('shows error message when API call fails', async () => {
    setupAuth();
    mockUpdateProfile.mockRejectedValue(new Error('Errore di rete'));
    const user = userEvent.setup();
    render(<OnboardingPage />);

    await user.type(screen.getByLabelText('Display Name'), 'Mario Rossi');
    await user.click(screen.getByRole('button', { name: /Entra/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Errore di rete');
    });
    expect(mockPush).not.toHaveBeenCalled();
  });

  it('redirects if already onboarded', async () => {
    setupAuth({ onboardingCompleted: true });
    render(<OnboardingPage />);

    await waitFor(() => {
      expect(mockReplace).toHaveBeenCalledWith('/dashboard');
    });
  });

  it('shows loading spinner while auth is loading', () => {
    mockUseAuth.mockReturnValue({ user: null, loading: true });
    render(<OnboardingPage />);

    // Should render spinner, not form
    expect(screen.queryByText('Benvenuto!')).not.toBeInTheDocument();
    // Loader2 renders as an svg with animate-spin
    const spinner = document.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('returns null when user is null and not loading', () => {
    mockUseAuth.mockReturnValue({ user: null, loading: false });
    const { container } = render(<OnboardingPage />);
    expect(container.firstChild).toBeNull();
  });
});
