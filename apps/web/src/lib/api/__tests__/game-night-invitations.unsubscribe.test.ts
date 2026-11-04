/**
 * unsubscribeFromInvitationEmails — #4846.
 *
 * The link in every email to a game night guest ends on a page that calls this: the invitation token is the
 * credential, and the answer is one of four things the page has to tell apart — done, a token nobody knows,
 * too many attempts, or a failure worth retrying.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../core/httpClient', () => ({
  getApiBase: () => 'https://api.test',
}));

import {
  InvitationFetchError,
  InvitationNotFoundError,
  unsubscribeFromInvitationEmails,
} from '../game-night-invitations';

const fetchMock = vi.fn();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('unsubscribeFromInvitationEmails', () => {
  it('POSTs to the unsubscribe route of the token, with no body and no credentials beyond the token', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));

    const result = await unsubscribeFromInvitationEmails('AbC_dEf-123');

    expect(result).toEqual({ kind: 'unsubscribed' });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('https://api.test/api/v1/game-nights/invitations/AbC_dEf-123/unsubscribe');
    expect(init.method).toBe('POST');
    expect(init.body).toBeUndefined();
  });

  it('encodes the token into the path, so a crafted one cannot reach another route', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));

    await unsubscribeFromInvitationEmails('a/../respond?x=1');

    const [url] = fetchMock.mock.calls[0] as [string];
    expect(url).toBe(
      'https://api.test/api/v1/game-nights/invitations/a%2F..%2Frespond%3Fx%3D1/unsubscribe'
    );
  });

  it('reports a rate limit with the seconds to wait when the server says them', async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 429, headers: { 'Retry-After': '30' } })
    );

    await expect(unsubscribeFromInvitationEmails('tok')).resolves.toEqual({
      kind: 'rate-limited',
      retryAfter: 30,
    });
  });

  it('reports a rate limit without a wait when the header is missing or not a number', async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 429, headers: { 'Retry-After': 'soon' } })
    );

    await expect(unsubscribeFromInvitationEmails('tok')).resolves.toEqual({
      kind: 'rate-limited',
      retryAfter: null,
    });
  });

  it('throws InvitationNotFoundError for a token nobody knows (404)', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 404 }));

    await expect(unsubscribeFromInvitationEmails('tok')).rejects.toBeInstanceOf(
      InvitationNotFoundError
    );
  });

  it('throws InvitationFetchError with the status on a server failure', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 500 }));

    await expect(unsubscribeFromInvitationEmails('tok')).rejects.toMatchObject({
      name: 'InvitationFetchError',
      status: 500,
    });
  });

  it('throws InvitationFetchError with no status when the network fails', async () => {
    fetchMock.mockRejectedValue(new TypeError('network down'));

    const error = await unsubscribeFromInvitationEmails('tok').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(InvitationFetchError);
    expect((error as InvitationFetchError).status).toBeNull();
  });
});
