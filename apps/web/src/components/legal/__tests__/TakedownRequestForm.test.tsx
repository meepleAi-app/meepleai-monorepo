/** @vitest-environment jsdom */
import { render, screen, fireEvent, waitFor, type RenderResult } from '@testing-library/react';
import type { ReactElement } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { LegalLocaleProvider } from '@/components/legal/LegalLocaleToggle';

import { TakedownRequestForm, TAKEDOWN_EMAIL, buildTakedownBody } from '../TakedownRequestForm';

// The form is mounted inside LegalLocaleProvider in production (LegalPageLayout
// footerSlot), which supplies both the IT/EN locale context and the IntlProvider.
function renderForm(ui: ReactElement): RenderResult {
  return render(<LegalLocaleProvider>{ui}</LegalLocaleProvider>);
}

function fillTextField(label: RegExp, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

const VALID = {
  name: 'Jane Doe',
  email: 'jane@example.com',
  work: 'Catan Rulebook',
  cardUrl: 'https://meepleai.com/games/abc/card',
  description: 'Reproduces protected text verbatim.',
};

/** Fill every required field + confirm checkbox with valid values. */
function fillAllValid() {
  fillTextField(/full name/i, VALID.name);
  fillTextField(/email/i, VALID.email);
  fillTextField(/copyrighted work/i, VALID.work);
  fillTextField(/url of the card/i, VALID.cardUrl);
  fillTextField(/description of the problem/i, VALID.description);
  fireEvent.click(screen.getByRole('checkbox'));
}

describe('TakedownRequestForm', () => {
  const originalLocation = window.location;
  const originalClipboard = navigator.clipboard;

  beforeEach(() => {
    // Force EN so the label regexes above match (default locale is IT).
    window.localStorage.setItem('meepleai-legal-locale', 'en');
    vi.restoreAllMocks();
  });

  afterEach(() => {
    window.localStorage.clear();
    // Restore the objects we replaced per-test.
    Object.defineProperty(window, 'location', {
      value: originalLocation,
      configurable: true,
      writable: true,
    });
    Object.defineProperty(navigator, 'clipboard', {
      value: originalClipboard,
      configurable: true,
    });
  });

  /** Replace window.location so `location.href = mailto` is observable, not a real nav. */
  function stubLocation(): { current: string } {
    const ref = { current: '' };
    delete (window as unknown as { location?: unknown }).location;
    (window as unknown as { location: { href: string } }).location = {
      get href() {
        return ref.current;
      },
      set href(v: string) {
        ref.current = v;
      },
    };
    return ref;
  }

  it('renders all required fields and both action buttons', async () => {
    renderForm(<TakedownRequestForm />);
    // Wait for the locale to sync to EN (useEffect on mount).
    await screen.findByRole('button', { name: /open pre-filled email/i });

    expect(screen.getByLabelText(/full name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/copyrighted work/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/url of the card/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/description of the problem/i)).toBeInTheDocument();
    expect(screen.getByRole('checkbox')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /open pre-filled email/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /copy request to clipboard/i })).toBeInTheDocument();
  });

  it('blocks submit and shows errors when required fields are empty', async () => {
    renderForm(<TakedownRequestForm />);
    await screen.findByRole('button', { name: /open pre-filled email/i });

    const loc = stubLocation();
    fireEvent.click(screen.getByTestId('takedown-submit'));

    // Error summary announced
    expect(await screen.findByTestId('takedown-error-summary')).toBeInTheDocument();
    // One inline error per required text field + confirmation → multiple alerts
    const alerts = screen.getAllByRole('alert');
    expect(alerts.length).toBeGreaterThan(1);
    // Never navigated to a mailto because validation failed
    expect(loc.current).toBe('');
  });

  it('marks invalid email with an aria-invalid state and a specific error', async () => {
    renderForm(<TakedownRequestForm />);
    await screen.findByRole('button', { name: /open pre-filled email/i });

    fillTextField(/full name/i, VALID.name);
    fillTextField(/email/i, 'not-an-email');
    fillTextField(/copyrighted work/i, VALID.work);
    fillTextField(/url of the card/i, VALID.cardUrl);
    fillTextField(/description of the problem/i, VALID.description);
    fireEvent.click(screen.getByRole('checkbox'));

    stubLocation();
    fireEvent.click(screen.getByTestId('takedown-submit'));

    const emailInput = screen.getByLabelText(/email/i);
    await waitFor(() => expect(emailInput).toHaveAttribute('aria-invalid', 'true'));
    expect(screen.getByText(/valid email address/i)).toBeInTheDocument();
  });

  it('opens a pre-filled mailto with the field values when valid', async () => {
    renderForm(<TakedownRequestForm />);
    await screen.findByRole('button', { name: /open pre-filled email/i });

    const loc = stubLocation();
    fillAllValid();
    fireEvent.click(screen.getByTestId('takedown-submit'));

    expect(loc.current).toContain(`mailto:${TAKEDOWN_EMAIL}`);
    const decoded = decodeURIComponent(loc.current);
    // Subject carries the work title
    expect(decoded).toContain('Takedown request — Catan Rulebook');
    // Body carries the reporter details
    expect(decoded).toContain(VALID.name);
    expect(decoded).toContain(VALID.email);
    expect(decoded).toContain(VALID.cardUrl);
    expect(decoded).toContain(VALID.description);
  });

  it('copies the structured request to the clipboard when valid', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText },
      configurable: true,
    });

    renderForm(<TakedownRequestForm />);
    await screen.findByRole('button', { name: /copy request to clipboard/i });

    fillAllValid();
    fireEvent.click(screen.getByTestId('takedown-copy'));

    await waitFor(() => expect(writeText).toHaveBeenCalledTimes(1));
    const copiedText = writeText.mock.calls[0][0] as string;
    expect(copiedText).toContain('Takedown request — Catan Rulebook');
    expect(copiedText).toContain(VALID.name);
    expect(copiedText).toContain(VALID.cardUrl);
    // Button flips to the "Copied!" confirmation state
    expect(await screen.findByRole('button', { name: /copied/i })).toBeInTheDocument();
  });

  it('does not copy when validation fails', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText },
      configurable: true,
    });

    renderForm(<TakedownRequestForm />);
    await screen.findByRole('button', { name: /copy request to clipboard/i });

    fireEvent.click(screen.getByTestId('takedown-copy'));

    expect(await screen.findByTestId('takedown-error-summary')).toBeInTheDocument();
    expect(writeText).not.toHaveBeenCalled();
  });

  it('renders a direct mailto link to takedown@meepleai.app', async () => {
    renderForm(<TakedownRequestForm />);
    await screen.findByRole('button', { name: /open pre-filled email/i });

    const mailLink = screen.getByRole('link', { name: TAKEDOWN_EMAIL });
    expect(mailLink).toHaveAttribute('href', `mailto:${TAKEDOWN_EMAIL}`);
  });
});

describe('buildTakedownBody', () => {
  it('lays out all fields plus the good-faith statement in order', () => {
    const body = buildTakedownBody(
      {
        name: 'Jane',
        email: 'jane@example.com',
        work: 'Catan',
        cardUrl: 'https://x/card',
        description: 'text copied',
        confirmed: true,
      },
      {
        name: 'Name',
        email: 'Email',
        work: 'Work',
        cardUrl: 'Card URL',
        description: 'Description',
        confirm: 'I confirm.',
      }
    );

    expect(body).toContain('Name: Jane');
    expect(body).toContain('Email: jane@example.com');
    expect(body).toContain('Work: Catan');
    expect(body).toContain('Card URL: https://x/card');
    expect(body).toContain('Description:');
    expect(body).toContain('text copied');
    expect(body.trimEnd().endsWith('I confirm.')).toBe(true);
  });
});
