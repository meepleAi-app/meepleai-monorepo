import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import CookieSettingsPage from '../page';
import { CONSENT_KEY, CONSENT_VERSION, type CookieConsent } from '@/lib/cookie-consent';

// Toast is a visual side-effect; assert it was called, don't render it.
vi.mock('sonner', () => ({
  toast: { success: vi.fn() },
}));

// Mirror the test pattern used by existing locale-aware pages in this repo:
// useTranslation returns t(key) => key so assertions hit the key paths directly.
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('CookieSettingsPage', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('mounts with both toggles off when no consent is stored', async () => {
    await act(async () => {
      render(<CookieSettingsPage />);
    });
    const analytics = screen.getByRole('switch', {
      name: 'pages.cookieSettings.categories.analytics.label',
    });
    const functional = screen.getByRole('switch', {
      name: 'pages.cookieSettings.categories.functional.label',
    });
    expect(analytics).toHaveAttribute('aria-checked', 'false');
    expect(functional).toHaveAttribute('aria-checked', 'false');
  });

  it('mounts with toggles reflecting stored consent', async () => {
    const stored: CookieConsent = {
      version: CONSENT_VERSION,
      essential: true,
      analytics: true,
      functional: false,
      timestamp: '2026-04-24T10:00:00.000Z',
    };
    localStorage.setItem(CONSENT_KEY, JSON.stringify(stored));

    await act(async () => {
      render(<CookieSettingsPage />);
    });

    expect(
      screen.getByRole('switch', { name: 'pages.cookieSettings.categories.analytics.label' })
    ).toHaveAttribute('aria-checked', 'true');
    expect(
      screen.getByRole('switch', { name: 'pages.cookieSettings.categories.functional.label' })
    ).toHaveAttribute('aria-checked', 'false');
  });

  it('toggling analytics updates the control state before saving', async () => {
    const user = userEvent.setup();
    await act(async () => {
      render(<CookieSettingsPage />);
    });

    const analytics = screen.getByRole('switch', {
      name: 'pages.cookieSettings.categories.analytics.label',
    });
    await user.click(analytics);
    expect(analytics).toHaveAttribute('aria-checked', 'true');
  });

  it('Save writes the correct shape to localStorage and emits cookie-consent-updated', async () => {
    const user = userEvent.setup();
    const dispatchSpy = vi.spyOn(window, 'dispatchEvent');

    await act(async () => {
      render(<CookieSettingsPage />);
    });

    await user.click(
      screen.getByRole('switch', { name: 'pages.cookieSettings.categories.analytics.label' })
    );
    await user.click(screen.getByRole('button', { name: 'pages.cookieSettings.actions.save' }));

    const raw = localStorage.getItem(CONSENT_KEY);
    expect(raw).not.toBeNull();
    const parsed = JSON.parse(raw!) as CookieConsent;
    expect(parsed.version).toBe(CONSENT_VERSION);
    expect(parsed.essential).toBe(true);
    expect(parsed.analytics).toBe(true);
    expect(parsed.functional).toBe(false);

    const events = dispatchSpy.mock.calls
      .map(([e]) => e as Event)
      .filter(e => e.type === 'cookie-consent-updated');
    expect(events.length).toBeGreaterThanOrEqual(1);
  });

  it('Accept all sets both analytics and functional to true', async () => {
    const user = userEvent.setup();
    await act(async () => {
      render(<CookieSettingsPage />);
    });

    await user.click(
      screen.getByRole('button', { name: 'pages.cookieSettings.actions.acceptAll' })
    );

    const parsed = JSON.parse(localStorage.getItem(CONSENT_KEY)!) as CookieConsent;
    expect(parsed.analytics).toBe(true);
    expect(parsed.functional).toBe(true);
  });

  it('Reject optional sets both analytics and functional to false', async () => {
    const user = userEvent.setup();
    await act(async () => {
      render(<CookieSettingsPage />);
    });

    await user.click(
      screen.getByRole('switch', { name: 'pages.cookieSettings.categories.analytics.label' })
    );
    await user.click(
      screen.getByRole('button', { name: 'pages.cookieSettings.actions.rejectAll' })
    );

    const parsed = JSON.parse(localStorage.getItem(CONSENT_KEY)!) as CookieConsent;
    expect(parsed.analytics).toBe(false);
    expect(parsed.functional).toBe(false);
  });

  it('essential toggle is always checked and disabled', async () => {
    await act(async () => {
      render(<CookieSettingsPage />);
    });

    const essential = screen.getByRole('switch', {
      name: 'pages.cookieSettings.categories.essential.label',
    });
    expect(essential).toHaveAttribute('aria-checked', 'true');
    expect(essential).toBeDisabled();
  });
});
