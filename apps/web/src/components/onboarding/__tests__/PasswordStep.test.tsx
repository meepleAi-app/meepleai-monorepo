import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { PasswordStep } from '../PasswordStep';

const mockAcceptInvitation = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    invitations: {
      acceptInvitation: mockAcceptInvitation,
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

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: ({
      children,
      className,
      ...props
    }: {
      children?: React.ReactNode;
      className?: string;
      [key: string]: unknown;
    }) => (
      <div className={className} {...props}>
        {children}
      </div>
    ),
  },
}));

describe('PasswordStep', () => {
  const user = userEvent.setup();
  const onComplete = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders password and confirm password fields', () => {
    renderWithQuery(<PasswordStep token="test-token" onComplete={onComplete} />);

    expect(screen.getByText('Create Your Password')).toBeInTheDocument();
    expect(screen.getByLabelText(/^Password/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Confirm Password/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create account/i })).toBeInTheDocument();
  });

  it('shows password strength meter when password entered', async () => {
    renderWithQuery(<PasswordStep token="test-token" onComplete={onComplete} />);

    await user.type(screen.getByLabelText(/^Password/), 'abc');

    expect(screen.getByTestId('password-strength')).toBeInTheDocument();
    expect(screen.getByText('Weak')).toBeInTheDocument();
  });

  it('shows validation rules when password entered', async () => {
    renderWithQuery(<PasswordStep token="test-token" onComplete={onComplete} />);

    await user.type(screen.getByLabelText(/^Password/), 'Test1');

    const rules = screen.getByTestId('password-rules');
    expect(rules).toBeInTheDocument();
    expect(screen.getByText('At least 8 characters')).toBeInTheDocument();
    expect(screen.getByText('At least 1 uppercase letter')).toBeInTheDocument();
    expect(screen.getByText('At least 1 lowercase letter')).toBeInTheDocument();
    expect(screen.getByText('At least 1 number')).toBeInTheDocument();
  });

  it('shows strong strength for valid password', async () => {
    renderWithQuery(<PasswordStep token="test-token" onComplete={onComplete} />);

    await user.type(screen.getByLabelText(/^Password/), 'StrongPass1');

    expect(screen.getByText('Strong')).toBeInTheDocument();
  });

  it('shows password mismatch error', async () => {
    renderWithQuery(<PasswordStep token="test-token" onComplete={onComplete} />);

    await user.type(screen.getByLabelText(/^Password/), 'StrongPass1');
    await user.type(screen.getByLabelText(/Confirm Password/), 'Different1');

    expect(screen.getByText('Passwords do not match')).toBeInTheDocument();
  });

  it('submit button is disabled until password is valid and matching', () => {
    renderWithQuery(<PasswordStep token="test-token" onComplete={onComplete} />);

    expect(screen.getByRole('button', { name: /create account/i })).toBeDisabled();
  });

  it('calls acceptInvitation and onComplete on successful submit', async () => {
    mockAcceptInvitation.mockResolvedValueOnce({
      user: { id: '1', email: 'test@test.com' },
      sessionToken: 'tok',
      expiresAt: '2026-12-31',
    });

    renderWithQuery(<PasswordStep token="test-token" onComplete={onComplete} />);

    await user.type(screen.getByLabelText(/^Password/), 'StrongPass1');
    await user.type(screen.getByLabelText(/Confirm Password/), 'StrongPass1');
    await user.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => {
      expect(mockAcceptInvitation).toHaveBeenCalledWith('test-token', 'StrongPass1', 'StrongPass1');
    });

    await waitFor(() => {
      expect(onComplete).toHaveBeenCalled();
    });
  });

  it('shows error on API failure', async () => {
    mockAcceptInvitation.mockRejectedValueOnce(new Error('Token expired'));

    renderWithQuery(<PasswordStep token="test-token" onComplete={onComplete} />);

    await user.type(screen.getByLabelText(/^Password/), 'StrongPass1');
    await user.type(screen.getByLabelText(/Confirm Password/), 'StrongPass1');
    await user.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    expect(onComplete).not.toHaveBeenCalled();
  });
});
