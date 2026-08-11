/**
 * #2832: the card-suppression email opt-in toggle renders and persists via its dedicated endpoint.
 */
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockGetPreferences = vi.hoisted(() => vi.fn());
const mockUpdatePreferences = vi.hoisted(() => vi.fn());
const mockUpdateCardSuppression = vi.hoisted(() => vi.fn());
const mockUpdateSlackPreferences = vi.hoisted(() => vi.fn());
const mockUpdateQuietHours = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    notifications: {
      getPreferences: mockGetPreferences,
      updatePreferences: mockUpdatePreferences,
      updateCardSuppressionEmailPreference: mockUpdateCardSuppression,
      updateSlackPreferences: mockUpdateSlackPreferences,
      updateQuietHours: mockUpdateQuietHours,
    },
  },
}));

vi.mock('@/hooks/useToast', () => ({
  useToast: () => ({ toast: vi.fn() }),
}));

import { NotificationPreferences } from '../NotificationPreferences';

function fullPrefs(overrides: Record<string, unknown> = {}) {
  return {
    userId: 'u1',
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
    inAppOnGameNightInvitation: true,
    emailOnGameNightInvitation: true,
    pushOnGameNightInvitation: true,
    emailOnGameNightReminder: true,
    pushOnGameNightReminder: true,
    emailOnCardSuppressed: false,
    timeZone: 'UTC',
    quietHoursStart: null,
    quietHoursEnd: null,
    slackEnabled: true,
    slackOnDocumentReady: true,
    slackOnDocumentFailed: true,
    slackOnRetryAvailable: false,
    slackOnGameNightInvitation: true,
    slackOnGameNightReminder: true,
    slackOnShareRequestCreated: true,
    slackOnShareRequestApproved: true,
    slackOnBadgeEarned: true,
    ...overrides,
  };
}

describe('NotificationPreferences — card-suppression email opt-in (#2832)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPreferences.mockResolvedValue(fullPrefs());
    mockUpdatePreferences.mockResolvedValue(undefined);
    mockUpdateCardSuppression.mockResolvedValue(undefined);
    mockUpdateSlackPreferences.mockResolvedValue(undefined);
    mockUpdateQuietHours.mockResolvedValue(undefined);
  });

  it('renders the card-suppression toggle reflecting the loaded (off) state', async () => {
    render(<NotificationPreferences />);
    const toggle = await screen.findByTestId('pref-emailOnCardSuppressed');
    expect(toggle).toBeInTheDocument();
    expect(toggle).toHaveAttribute('aria-checked', 'false');
  });

  it('persists the opt-in via the dedicated endpoint on save', async () => {
    render(<NotificationPreferences />);
    const toggle = await screen.findByTestId('pref-emailOnCardSuppressed');

    fireEvent.click(toggle); // off → on
    fireEvent.click(screen.getByTestId('save-preferences'));

    await waitFor(() => expect(mockUpdateCardSuppression).toHaveBeenCalledWith(true));
  });
});
