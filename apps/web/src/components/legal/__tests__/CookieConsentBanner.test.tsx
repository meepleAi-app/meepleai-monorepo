/**
 * CookieConsentBanner Component Tests
 *
 * Tests cookie consent UI: rendering, checkbox behavior, consent persistence.
 */

import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, beforeEach, vi } from 'vitest';

const mockT = vi.fn((key: string) => {
  const translations: Record<string, string> = {
    'legal.cookieBanner.title': 'This site uses cookies',
    'legal.cookieBanner.description': 'We use essential technical cookies.',
    'legal.cookieBanner.readPolicy': 'Read Cookie Policy',
    'legal.cookieBanner.ariaLabel': 'Cookie consent',
    'legal.cookieBanner.essential': 'Essential',
    'legal.cookieBanner.essentialAlways': 'always active',
    'legal.cookieBanner.essentialAriaLabel': 'Essential cookies (always active)',
    'legal.cookieBanner.analytics': 'Analytics',
    'legal.cookieBanner.analyticsDescription': 'help us understand usage',
    'legal.cookieBanner.analyticsAriaLabel': 'Analytics cookies',
    'legal.cookieBanner.functional': 'Functional',
    'legal.cookieBanner.functionalDescription': 'remember your preferences',
    'legal.cookieBanner.functionalAriaLabel': 'Functional cookies',
    'legal.cookieBanner.customize': 'Customize',
    'legal.cookieBanner.essentialOnly': 'Essential only',
    'legal.cookieBanner.acceptAll': 'Accept all',
    'legal.cookieBanner.back': 'Back',
    'legal.cookieBanner.savePreferences': 'Save preferences',
  };
  return translations[key] ?? key;
});

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: mockT,
    locale: 'en',
    formatMessage: vi.fn(),
    formatNumber: vi.fn(),
    formatDate: vi.fn(),
    formatTime: vi.fn(),
    formatRelativeTime: vi.fn(),
  }),
}));

// Must import after mock
import { CookieConsentBanner } from '../CookieConsentBanner';

const CONSENT_KEY = 'meepleai-cookie-consent';

describe('CookieConsentBanner', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('renders banner when no consent stored', () => {
    render(<CookieConsentBanner />);
    expect(screen.getByTestId('cookie-consent-banner')).toBeInTheDocument();
    expect(screen.getByText('This site uses cookies')).toBeInTheDocument();
  });

  it('does not render when consent already stored', () => {
    localStorage.setItem(
      CONSENT_KEY,
      JSON.stringify({
        version: '1.0',
        essential: true,
        analytics: false,
        functional: false,
        timestamp: new Date().toISOString(),
      })
    );
    render(<CookieConsentBanner />);
    expect(screen.queryByTestId('cookie-consent-banner')).not.toBeInTheDocument();
  });

  it('hides banner after clicking "Accept all"', async () => {
    const user = userEvent.setup();
    render(<CookieConsentBanner />);

    await user.click(screen.getByText('Accept all'));
    expect(screen.queryByTestId('cookie-consent-banner')).not.toBeInTheDocument();

    const stored = JSON.parse(localStorage.getItem(CONSENT_KEY)!);
    expect(stored.analytics).toBe(true);
    expect(stored.functional).toBe(true);
  });

  it('hides banner after clicking "Essential only"', async () => {
    const user = userEvent.setup();
    render(<CookieConsentBanner />);

    await user.click(screen.getByText('Essential only'));
    expect(screen.queryByTestId('cookie-consent-banner')).not.toBeInTheDocument();

    const stored = JSON.parse(localStorage.getItem(CONSENT_KEY)!);
    expect(stored.analytics).toBe(false);
    expect(stored.functional).toBe(false);
  });

  it('shows detail checkboxes on "Customize" click', async () => {
    const user = userEvent.setup();
    render(<CookieConsentBanner />);

    await user.click(screen.getByText('Customize'));
    expect(screen.getByLabelText('Analytics cookies')).toBeInTheDocument();
    expect(screen.getByLabelText('Functional cookies')).toBeInTheDocument();
  });

  it('checkboxes use local state and do NOT persist until Save', async () => {
    const user = userEvent.setup();
    render(<CookieConsentBanner />);

    await user.click(screen.getByText('Customize'));

    // Toggle analytics checkbox
    const analyticsCheckbox = screen.getByLabelText('Analytics cookies');
    await user.click(analyticsCheckbox);
    expect(analyticsCheckbox).toBeChecked();

    // Banner should still be visible — not persisted yet
    expect(screen.getByTestId('cookie-consent-banner')).toBeInTheDocument();
    expect(localStorage.getItem(CONSENT_KEY)).toBeNull();
  });

  it('saves preferences on "Save preferences" with local state', async () => {
    const user = userEvent.setup();
    render(<CookieConsentBanner />);

    await user.click(screen.getByText('Customize'));

    // Enable analytics, leave functional unchecked
    await user.click(screen.getByLabelText('Analytics cookies'));
    await user.click(screen.getByText('Save preferences'));

    expect(screen.queryByTestId('cookie-consent-banner')).not.toBeInTheDocument();

    const stored = JSON.parse(localStorage.getItem(CONSENT_KEY)!);
    expect(stored.analytics).toBe(true);
    expect(stored.functional).toBe(false);
  });

  it('has correct aria attributes', () => {
    render(<CookieConsentBanner />);
    const dialog = screen.getByTestId('cookie-consent-banner');
    expect(dialog).toHaveAttribute('role', 'dialog');
    expect(dialog).toHaveAttribute('aria-label', 'Cookie consent');
  });

  it('uses i18n keys for all strings', () => {
    render(<CookieConsentBanner />);
    // Verify t() was called with expected keys
    expect(mockT).toHaveBeenCalledWith('legal.cookieBanner.title');
    expect(mockT).toHaveBeenCalledWith('legal.cookieBanner.description');
    expect(mockT).toHaveBeenCalledWith('legal.cookieBanner.readPolicy');
  });
});
