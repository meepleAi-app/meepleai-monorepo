/**
 * RequestAccessForm Component Tests
 *
 * Tests for the invite-only registration access request form:
 * 1. Renders email input and submit button
 * 2. Shows validation error for empty/invalid email
 * 3. Shows success message after submission
 * 4. Disables form during submission
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// ────────────────────────────────────────────────────────────────────────────
// Hoisted mocks (must be declared before vi.mock calls)
// ────────────────────────────────────────────────────────────────────────────

const mockRequestAccess = vi.fn();

// Mock useApiClient — hoisted so the factory can reference mockRequestAccess
vi.mock('@/lib/api/context', () => ({
  useApiClient: vi.fn(),
}));

// Mock useTranslation — return the key as the translation (fallback behaviour)
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

// ────────────────────────────────────────────────────────────────────────────
// Import the component after mocks are in place
// ────────────────────────────────────────────────────────────────────────────

import { useApiClient } from '@/lib/api/context';
import { RequestAccessForm } from '@/components/auth/RequestAccessForm';

const mockUseApiClient = vi.mocked(useApiClient);

// ────────────────────────────────────────────────────────────────────────────
// Test suite
// ────────────────────────────────────────────────────────────────────────────

describe('RequestAccessForm', () => {
  beforeEach(() => {
    mockRequestAccess.mockReset();
    mockUseApiClient.mockReturnValue({
      accessRequests: { requestAccess: mockRequestAccess },
    } as any);
  });

  it('renders email input and submit button', () => {
    render(<RequestAccessForm />);

    expect(screen.getByTestId('request-access-email')).toBeInTheDocument();
    expect(screen.getByTestId('request-access-submit')).toBeInTheDocument();
    // Submit button should be enabled initially
    expect(screen.getByTestId('request-access-submit')).not.toBeDisabled();
  });

  it('shows validation error for empty or invalid email', async () => {
    const user = userEvent.setup();
    render(<RequestAccessForm />);

    // Submit without entering an email
    await user.click(screen.getByTestId('request-access-submit'));

    await waitFor(() => {
      expect(screen.getByText(/email is required/i)).toBeInTheDocument();
    });

    // Enter an invalid email
    await user.clear(screen.getByTestId('request-access-email'));
    await user.type(screen.getByTestId('request-access-email'), 'not-an-email');
    await user.click(screen.getByTestId('request-access-submit'));

    await waitFor(() => {
      expect(screen.getByText(/valid email/i)).toBeInTheDocument();
    });

    // API should NOT have been called
    expect(mockRequestAccess).not.toHaveBeenCalled();
  });

  it('shows success message after successful submission', async () => {
    mockRequestAccess.mockResolvedValueOnce({ message: 'Request received.' });

    const user = userEvent.setup();
    render(<RequestAccessForm />);

    await user.type(screen.getByTestId('request-access-email'), 'test@example.com');
    await user.click(screen.getByTestId('request-access-submit'));

    await waitFor(() => {
      expect(screen.getByTestId('request-access-success')).toBeInTheDocument();
    });

    expect(mockRequestAccess).toHaveBeenCalledWith('test@example.com');
    // Form should no longer be visible
    expect(screen.queryByTestId('request-access-form')).not.toBeInTheDocument();
  });

  it('shows success message even when the API throws (enumeration protection)', async () => {
    mockRequestAccess.mockRejectedValueOnce(new Error('Network error'));

    const user = userEvent.setup();
    render(<RequestAccessForm />);

    await user.type(screen.getByTestId('request-access-email'), 'test@example.com');
    await user.click(screen.getByTestId('request-access-submit'));

    await waitFor(() => {
      expect(screen.getByTestId('request-access-success')).toBeInTheDocument();
    });
  });

  it('disables email input and button during submission', async () => {
    // Make the API call hang so we can observe the loading state
    let resolveFn!: () => void;
    mockRequestAccess.mockReturnValueOnce(
      new Promise<{ message: string }>(resolve => {
        resolveFn = () => resolve({ message: 'ok' });
      })
    );

    const user = userEvent.setup();
    render(<RequestAccessForm />);

    await user.type(screen.getByTestId('request-access-email'), 'test@example.com');
    await user.click(screen.getByTestId('request-access-submit'));

    // While the request is in-flight, input and button should be disabled
    await waitFor(() => {
      expect(screen.getByTestId('request-access-email')).toBeDisabled();
      expect(screen.getByTestId('request-access-submit')).toBeDisabled();
    });

    // Resolve the request
    resolveFn();

    await waitFor(() => {
      expect(screen.getByTestId('request-access-success')).toBeInTheDocument();
    });
  });
});
