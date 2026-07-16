/**
 * #2994: quiet-hours (ADR-076) + Slack channel toggles render and round-trip through their
 * dedicated save endpoints.
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

// Stable toast reference: the component's load effect depends on `toast`, and the real hook returns
// a useCallback-stable function. A fresh vi.fn() per render would re-trigger the fetch and clobber edits.
const mockToast = vi.hoisted(() => vi.fn());
vi.mock('@/hooks/useToast', () => ({
  useToast: () => ({ toast: mockToast }),
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

describe('NotificationPreferences — Slack toggles (#2994)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPreferences.mockResolvedValue(fullPrefs());
    mockUpdatePreferences.mockResolvedValue(undefined);
    mockUpdateCardSuppression.mockResolvedValue(undefined);
    mockUpdateSlackPreferences.mockResolvedValue(undefined);
    mockUpdateQuietHours.mockResolvedValue(undefined);
  });

  it('renders the Slack master toggle reflecting the loaded (on) state', async () => {
    render(<NotificationPreferences />);
    const toggle = await screen.findByTestId('pref-slackEnabled');
    expect(toggle).toBeInTheDocument();
    expect(toggle).toHaveAttribute('aria-checked', 'true');
  });

  it('persists a per-type Slack toggle change via the dedicated endpoint on save', async () => {
    render(<NotificationPreferences />);
    const toggle = await screen.findByTestId('pref-slackOnBadgeEarned');
    expect(toggle).toHaveAttribute('aria-checked', 'true');

    fireEvent.click(toggle); // on → off
    fireEvent.click(screen.getByTestId('save-preferences'));

    await waitFor(() =>
      expect(mockUpdateSlackPreferences).toHaveBeenCalledWith(
        expect.objectContaining({ slackOnBadgeEarned: false, slackEnabled: true })
      )
    );
  });
});

describe('NotificationPreferences — quiet hours (#2995)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUpdatePreferences.mockResolvedValue(undefined);
    mockUpdateCardSuppression.mockResolvedValue(undefined);
    mockUpdateSlackPreferences.mockResolvedValue(undefined);
    mockUpdateQuietHours.mockResolvedValue(undefined);
  });

  it('renders disabled by default with the time inputs hidden', async () => {
    mockGetPreferences.mockResolvedValue(fullPrefs());
    render(<NotificationPreferences />);

    const toggle = await screen.findByTestId('pref-quietHoursEnabled');
    expect(toggle).toHaveAttribute('aria-checked', 'false');
    expect(screen.queryByTestId('pref-quietHoursStart')).not.toBeInTheDocument();
  });

  it('enabling reveals the time inputs and saves the default 22:00 → 08:00 window', async () => {
    mockGetPreferences.mockResolvedValue(fullPrefs());
    render(<NotificationPreferences />);

    const toggle = await screen.findByTestId('pref-quietHoursEnabled');
    fireEvent.click(toggle); // off → on

    expect(await screen.findByTestId('pref-quietHoursStart')).toHaveValue('22:00');
    expect(screen.getByTestId('pref-quietHoursEnd')).toHaveValue('08:00');

    fireEvent.click(screen.getByTestId('save-preferences'));

    await waitFor(() =>
      expect(mockUpdateQuietHours).toHaveBeenCalledWith({
        timeZone: 'UTC',
        quietHoursStart: '22:00',
        quietHoursEnd: '08:00',
      })
    );
  });

  it('hydrates an existing window and persists an edited start time', async () => {
    mockGetPreferences.mockResolvedValue(
      fullPrefs({ timeZone: 'Europe/Rome', quietHoursStart: '23:00', quietHoursEnd: '07:00' })
    );
    render(<NotificationPreferences />);

    const enableToggle = await screen.findByTestId('pref-quietHoursEnabled');
    expect(enableToggle).toHaveAttribute('aria-checked', 'true');

    const start = screen.getByTestId('pref-quietHoursStart');
    expect(start).toHaveValue('23:00');

    fireEvent.change(start, { target: { value: '22:30' } });
    fireEvent.click(screen.getByTestId('save-preferences'));

    await waitFor(() =>
      expect(mockUpdateQuietHours).toHaveBeenCalledWith({
        timeZone: 'Europe/Rome',
        quietHoursStart: '22:30',
        quietHoursEnd: '07:00',
      })
    );
  });

  it('disabling an existing window persists null bounds', async () => {
    mockGetPreferences.mockResolvedValue(
      fullPrefs({ timeZone: 'Europe/Rome', quietHoursStart: '23:00', quietHoursEnd: '07:00' })
    );
    render(<NotificationPreferences />);

    const toggle = await screen.findByTestId('pref-quietHoursEnabled');
    fireEvent.click(toggle); // on → off

    fireEvent.click(screen.getByTestId('save-preferences'));

    await waitFor(() =>
      expect(mockUpdateQuietHours).toHaveBeenCalledWith({
        timeZone: 'Europe/Rome',
        quietHoursStart: null,
        quietHoursEnd: null,
      })
    );
  });
});
