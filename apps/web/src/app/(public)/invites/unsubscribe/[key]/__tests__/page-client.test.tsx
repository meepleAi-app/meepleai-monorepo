/**
 * @vitest-environment jsdom
 *
 * /invites/unsubscribe/[key] — #4846.
 *
 * The link is in every email to a game night guest. Mail scanners and link previews open links before the person
 * does, so the page must not unsubscribe on load: it asks, and the tap on the button is what counts.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import itMessages from '@/locales/it.json';
import enMessages from '@/locales/en.json';

const unsubscribeMock = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/game-night-invitations', async () => {
  const actual = await vi.importActual<typeof import('@/lib/api/game-night-invitations')>(
    '@/lib/api/game-night-invitations'
  );
  return { ...actual, unsubscribeFromInvitationEmails: unsubscribeMock };
});
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, vars?: Record<string, unknown>) =>
      vars ? `${key}:${JSON.stringify(vars)}` : key,
  }),
}));

import { InvitationFetchError, InvitationNotFoundError } from '@/lib/api/game-night-invitations';

import { UnsubscribePageClient } from '../page-client';

const P = 'pages.invites.unsubscribe';

beforeEach(() => {
  unsubscribeMock.mockReset();
});

describe('UnsubscribePageClient', () => {
  it('asks first, and does not unsubscribe just because the page was opened', () => {
    render(<UnsubscribePageClient unsubscribeKey="key-1" />);

    expect(screen.getByRole('heading', { name: `${P}.heading` })).toBeTruthy();
    expect(screen.getByRole('button', { name: `${P}.confirmCta` })).toBeTruthy();
    expect(unsubscribeMock).not.toHaveBeenCalled();
  });

  it('unsubscribes on the tap, with the token of the link, and says it is done', async () => {
    unsubscribeMock.mockResolvedValue({ kind: 'unsubscribed' });
    const user = userEvent.setup();
    render(<UnsubscribePageClient unsubscribeKey="key-1" />);

    await user.click(screen.getByRole('button', { name: `${P}.confirmCta` }));

    await waitFor(() => expect(screen.getByText(`${P}.done.heading`)).toBeTruthy());
    expect(unsubscribeMock).toHaveBeenCalledTimes(1);
    expect(unsubscribeMock).toHaveBeenCalledWith('key-1');
    expect(screen.queryByRole('button', { name: `${P}.confirmCta` })).toBeNull();
  });

  it('does not send twice when the button is tapped again while the first request is in flight', async () => {
    let resolve!: (value: { kind: 'unsubscribed' }) => void;
    unsubscribeMock.mockReturnValue(
      new Promise(r => {
        resolve = r;
      })
    );
    const user = userEvent.setup();
    render(<UnsubscribePageClient unsubscribeKey="key-1" />);
    const button = screen.getByRole('button', { name: `${P}.confirmCta` });

    await user.click(button);
    await user.click(screen.getByRole('button', { name: `${P}.submitting` }));
    resolve({ kind: 'unsubscribed' });

    await waitFor(() => expect(screen.getByText(`${P}.done.heading`)).toBeTruthy());
    expect(unsubscribeMock).toHaveBeenCalledTimes(1);
  });

  it('moves focus to the outcome when the button goes away, so a keyboard user is not dropped on the body', async () => {
    unsubscribeMock.mockResolvedValue({ kind: 'unsubscribed' });
    const user = userEvent.setup();
    render(<UnsubscribePageClient unsubscribeKey="key-1" />);

    await user.click(screen.getByRole('button', { name: `${P}.confirmCta` }));

    const heading = await screen.findByRole('heading', { name: `${P}.done.heading` });
    await waitFor(() => expect(document.activeElement).toBe(heading));
  });

  it('tells the person the link is not valid when nobody holds the key', async () => {
    unsubscribeMock.mockRejectedValue(new InvitationNotFoundError('key-1'));
    const user = userEvent.setup();
    render(<UnsubscribePageClient unsubscribeKey="key-1" />);

    await user.click(screen.getByRole('button', { name: `${P}.confirmCta` }));

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain(`${P}.invalid.heading`);
    await waitFor(() => expect(document.activeElement?.textContent).toBe(`${P}.invalid.heading`));
    expect(screen.queryByRole('button', { name: `${P}.confirmCta` })).toBeNull();
  });

  it('says how long to wait after a rate limit, and lets the person try again', async () => {
    unsubscribeMock
      .mockResolvedValueOnce({ kind: 'rate-limited', retryAfter: 30 })
      .mockResolvedValueOnce({ kind: 'unsubscribed' });
    const user = userEvent.setup();
    render(<UnsubscribePageClient unsubscribeKey="key-1" />);

    await user.click(screen.getByRole('button', { name: `${P}.confirmCta` }));

    const notice = await screen.findByRole('status');
    expect(notice.textContent).toContain(`${P}.rateLimited`);
    expect(notice.textContent).toContain('"seconds":30');

    await user.click(screen.getByRole('button', { name: `${P}.confirmCta` }));
    await waitFor(() => expect(screen.getByText(`${P}.done.heading`)).toBeTruthy());
  });

  it('keeps the button on a failure worth retrying, and a retry that works ends in done', async () => {
    unsubscribeMock
      .mockRejectedValueOnce(new InvitationFetchError(500, 'boom'))
      .mockResolvedValueOnce({ kind: 'unsubscribed' });
    const user = userEvent.setup();
    render(<UnsubscribePageClient unsubscribeKey="key-1" />);

    await user.click(screen.getByRole('button', { name: `${P}.confirmCta` }));
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain(`${P}.error`);

    await user.click(screen.getByRole('button', { name: `${P}.retryCta` }));

    await waitFor(() => expect(screen.getByText(`${P}.done.heading`)).toBeTruthy());
    expect(unsubscribeMock).toHaveBeenCalledTimes(2);
  });
});

describe('the words of the page', () => {
  const KEYS = [
    'heading',
    'intro',
    'confirmCta',
    'submitting',
    'done.heading',
    'done.body',
    'invalid.heading',
    'invalid.body',
    'rateLimited',
    'error',
    'retryCta',
  ];

  const at = (messages: unknown, path: string): unknown =>
    path
      .split('.')
      .reduce<unknown>(
        (node, key) =>
          node && typeof node === 'object' ? (node as Record<string, unknown>)[key] : undefined,
        messages
      );

  it.each([
    ['it', itMessages],
    ['en', enMessages],
  ])('exist in %s, none empty', (_name, messages) => {
    for (const key of KEYS) {
      const value = at(messages, `pages.invites.unsubscribe.${key}`);
      expect(typeof value, `${key}`).toBe('string');
      expect((value as string).trim().length, `${key} is empty`).toBeGreaterThan(0);
    }
  });

  it('the rate limit message carries the seconds, in both languages', () => {
    expect(at(itMessages, 'pages.invites.unsubscribe.rateLimited')).toContain('{seconds}');
    expect(at(enMessages, 'pages.invites.unsubscribe.rateLimited')).toContain('{seconds}');
  });
});
