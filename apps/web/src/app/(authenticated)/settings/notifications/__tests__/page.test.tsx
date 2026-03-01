/**
 * Tests for Notification Settings Page (Issue #4220, #4416)
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

// Mock usePushNotifications
const mockPush = {
  isSubscribed: false,
  isSupported: true,
  isLoading: false,
  subscribe: vi.fn(),
  unsubscribe: vi.fn(),
};

vi.mock('@/hooks/usePushNotifications', () => ({
  usePushNotifications: () => mockPush,
}));

global.fetch = vi.fn();

describe('NotificationSettingsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPush.isSubscribed = false;
    mockPush.isSupported = true;
    mockPush.isLoading = false;
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
      hasPushSubscription: false,
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
      hasPushSubscription: false,
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
      hasPushSubscription: false,
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

  it('shows push subscription card when push is supported', async () => {
    const mockPrefs = {
      userId: 'user-123',
      emailOnDocumentReady: true,
      emailOnDocumentFailed: true,
      emailOnRetryAvailable: false,
      pushOnDocumentReady: true,
      pushOnDocumentFailed: true,
      pushOnRetryAvailable: false,
      inAppOnDocumentReady: true,
      inAppOnDocumentFailed: true,
      inAppOnRetryAvailable: true,
      hasPushSubscription: false,
    };

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => mockPrefs,
    });

    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByText('Browser Push Notifications')).toBeInTheDocument();
    });

    expect(screen.getByRole('button', { name: 'Enable' })).toBeInTheDocument();
  });

  it('shows Disable button when push is subscribed', async () => {
    mockPush.isSubscribed = true;

    const mockPrefs = {
      userId: 'user-123',
      emailOnDocumentReady: true,
      emailOnDocumentFailed: true,
      emailOnRetryAvailable: false,
      pushOnDocumentReady: true,
      pushOnDocumentFailed: true,
      pushOnRetryAvailable: false,
      inAppOnDocumentReady: true,
      inAppOnDocumentFailed: true,
      inAppOnRetryAvailable: true,
      hasPushSubscription: true,
    };

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => mockPrefs,
    });

    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Disable' })).toBeInTheDocument();
    });
  });

  it('push toggles are disabled when not subscribed', async () => {
    mockPush.isSubscribed = false;

    const mockPrefs = {
      userId: 'user-123',
      emailOnDocumentReady: true,
      emailOnDocumentFailed: true,
      emailOnRetryAvailable: false,
      pushOnDocumentReady: true,
      pushOnDocumentFailed: true,
      pushOnRetryAvailable: false,
      inAppOnDocumentReady: true,
      inAppOnDocumentFailed: true,
      inAppOnRetryAvailable: true,
      hasPushSubscription: false,
    };

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => mockPrefs,
    });

    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByText('Document Ready')).toBeInTheDocument();
    });

    const switches = screen.getAllByRole('switch');
    // Push switches (indices 1, 4, 7 — second in each section)
    expect(switches[1]).toBeDisabled();
    expect(switches[4]).toBeDisabled();
    expect(switches[7]).toBeDisabled();
  });

  it('calls subscribe when Enable button is clicked', async () => {
    mockPush.isSubscribed = false;

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
      hasPushSubscription: false,
    };

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => mockPrefs,
    });

    const user = userEvent.setup();
    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Enable' })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: 'Enable' }));

    expect(mockPush.subscribe).toHaveBeenCalledTimes(1);
  });

  it('calls unsubscribe when Disable button is clicked', async () => {
    mockPush.isSubscribed = true;

    const mockPrefs = {
      userId: 'user-123',
      emailOnDocumentReady: true,
      emailOnDocumentFailed: true,
      emailOnRetryAvailable: false,
      pushOnDocumentReady: true,
      pushOnDocumentFailed: true,
      pushOnRetryAvailable: false,
      inAppOnDocumentReady: true,
      inAppOnDocumentFailed: true,
      inAppOnRetryAvailable: true,
      hasPushSubscription: true,
    };

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => mockPrefs,
    });

    const user = userEvent.setup();
    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Disable' })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: 'Disable' }));

    expect(mockPush.unsubscribe).toHaveBeenCalledTimes(1);
  });

  it('shows Send Test button when push is subscribed', async () => {
    mockPush.isSubscribed = true;

    const mockPrefs = {
      userId: 'user-123',
      emailOnDocumentReady: true,
      emailOnDocumentFailed: true,
      emailOnRetryAvailable: false,
      pushOnDocumentReady: true,
      pushOnDocumentFailed: true,
      pushOnRetryAvailable: false,
      inAppOnDocumentReady: true,
      inAppOnDocumentFailed: true,
      inAppOnRetryAvailable: true,
      hasPushSubscription: true,
    };

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => mockPrefs,
    });

    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('test-push-button')).toBeInTheDocument();
    });
  });

  it('hides Send Test button when not subscribed', async () => {
    mockPush.isSubscribed = false;

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
      hasPushSubscription: false,
    };

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => mockPrefs,
    });

    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByText('Document Ready')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('test-push-button')).not.toBeInTheDocument();
  });

  it('sends test push notification on button click', async () => {
    mockPush.isSubscribed = true;

    const mockPrefs = {
      userId: 'user-123',
      emailOnDocumentReady: true,
      emailOnDocumentFailed: true,
      emailOnRetryAvailable: false,
      pushOnDocumentReady: true,
      pushOnDocumentFailed: true,
      pushOnRetryAvailable: false,
      inAppOnDocumentReady: true,
      inAppOnDocumentFailed: true,
      inAppOnRetryAvailable: true,
      hasPushSubscription: true,
    };

    (global.fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockPrefs,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ message: 'Test notification sent' }),
      });

    const user = userEvent.setup();
    render(<NotificationSettingsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('test-push-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('test-push-button'));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        '/api/v1/notifications/push/test',
        expect.objectContaining({ method: 'POST' })
      );
    });
  });
});
