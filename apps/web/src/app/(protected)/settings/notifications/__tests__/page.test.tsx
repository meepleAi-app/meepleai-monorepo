/**
 * Tests for Notification Settings Page (Issue #4220)
 */

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import NotificationSettingsPage from '../page';

// Mock useToast
vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({
    toast: vi.fn(),
  }),
}));

global.fetch = vi.fn();

describe('NotificationSettingsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders loading state initially', () => {
    (global.fetch as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise(() => {}) // Never resolves
    );

    const { container } = render(<NotificationSettingsPage />);

    // Loader2 SVG has aria-hidden="true", so query it via class name
    const loader = container.querySelector('.animate-spin');
    expect(loader).toBeInTheDocument();
  });

  it('loads and displays notification preferences', async () => {
    const mockPrefs = {
      userId: 'user-123',
      emailOnDocumentReady: true,
      emailOnDocumentFailed: true,
      emailOnRetryAvailable: false,
      pushOnDocumentReady: false,
      pushOnDocumentFailed: false,
      pushOnRetryAvailable: false,
      inAppOnDocumentReady: true,
      inAppOnDocumentFailed: true,
      inAppOnRetryAvailable: true,
    };

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => mockPrefs,
    });

    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByText('Notification Settings')).toBeInTheDocument();
    });

    expect(screen.getByText('Document Ready')).toBeInTheDocument();
    expect(screen.getByText('Processing Failed')).toBeInTheDocument();
    expect(screen.getByText('Retry Available')).toBeInTheDocument();
  });

  it('saves preferences when Save Changes is clicked', async () => {
    const mockPrefs = {
      userId: 'user-123',
      emailOnDocumentReady: true,
      emailOnDocumentFailed: false,
      emailOnRetryAvailable: false,
      pushOnDocumentReady: false,
      pushOnDocumentFailed: false,
      pushOnRetryAvailable: false,
      inAppOnDocumentReady: true,
      inAppOnDocumentFailed: false,
      inAppOnRetryAvailable: false,
    };

    (global.fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockPrefs,
      })
      .mockResolvedValueOnce({
        ok: true,
      });

    const user = userEvent.setup();
    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByText('Notification Settings')).toBeInTheDocument();
    });

    const saveButton = screen.getByRole('button', { name: /Save Changes/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        '/api/v1/notifications/preferences',
        expect.objectContaining({
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(mockPrefs),
        })
      );
    });
  });

  it('toggles email preference for document ready', async () => {
    const mockPrefs = {
      userId: 'user-123',
      emailOnDocumentReady: false,
      emailOnDocumentFailed: false,
      emailOnRetryAvailable: false,
      pushOnDocumentReady: false,
      pushOnDocumentFailed: false,
      pushOnRetryAvailable: false,
      inAppOnDocumentReady: false,
      inAppOnDocumentFailed: false,
      inAppOnRetryAvailable: false,
    };

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => ({ ...mockPrefs }),
    });

    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByText('Document Ready')).toBeInTheDocument();
    });

    // Find the first switch (email for document ready)
    const switches = screen.getAllByRole('switch');
    const emailReadySwitch = switches[0];

    expect(emailReadySwitch).toHaveAttribute('aria-checked', 'false');

    fireEvent.click(emailReadySwitch);

    await waitFor(() => {
      expect(emailReadySwitch).toHaveAttribute('aria-checked', 'true');
    });
  });
});
