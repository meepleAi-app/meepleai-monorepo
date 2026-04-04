import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { ProfileStep } from '../ProfileStep';

const mockUpdateProfile = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      updateProfile: mockUpdateProfile,
    },
  },
  ApiError: class ApiError extends Error {
    constructor(message: string) {
      super(message);
      this.name = 'ApiError';
    }
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('ProfileStep', () => {
  const user = userEvent.setup();
  const onComplete = vi.fn();
  const onSkip = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders display name input', () => {
    renderWithQuery(<ProfileStep onComplete={onComplete} onSkip={onSkip} />);

    expect(screen.getByText('Set Up Your Profile')).toBeInTheDocument();
    expect(screen.getByLabelText(/Display Name/)).toBeInTheDocument();
  });

  it('calls onSkip when skip button clicked', async () => {
    renderWithQuery(<ProfileStep onComplete={onComplete} onSkip={onSkip} />);

    await user.click(screen.getByTestId('profile-skip'));

    expect(onSkip).toHaveBeenCalled();
  });

  it('calls onSkip when submitting with empty name', async () => {
    renderWithQuery(<ProfileStep onComplete={onComplete} onSkip={onSkip} />);

    await user.click(screen.getByRole('button', { name: /save profile/i }));

    expect(onSkip).toHaveBeenCalled();
    expect(mockUpdateProfile).not.toHaveBeenCalled();
  });

  it('calls updateProfile and onComplete on successful submit', async () => {
    mockUpdateProfile.mockResolvedValueOnce({});

    renderWithQuery(<ProfileStep onComplete={onComplete} onSkip={onSkip} />);

    await user.type(screen.getByLabelText(/Display Name/), 'TestUser');
    await user.click(screen.getByRole('button', { name: /save profile/i }));

    await waitFor(() => {
      expect(mockUpdateProfile).toHaveBeenCalledWith({ displayName: 'TestUser' });
    });

    await waitFor(() => {
      expect(onComplete).toHaveBeenCalled();
    });
  });

  it('shows error on API failure', async () => {
    mockUpdateProfile.mockRejectedValueOnce(new Error('Server error'));

    renderWithQuery(<ProfileStep onComplete={onComplete} onSkip={onSkip} />);

    await user.type(screen.getByLabelText(/Display Name/), 'TestUser');
    await user.click(screen.getByRole('button', { name: /save profile/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });
});
