/**
 * #4190 — ogni operazione che cambia l'identità svuota la requestCache.
 *
 * La cache deduplica le GET per 100 ms dopo la risposta, con una chiave che non dipende dai cookie
 * di sessione: un `/auth/me` anonimo (un 401 diventa `null`) poteva essere servito a chi aveva
 * appena fatto login, e uno autenticato a chi aveva appena fatto logout.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { createAuthClient } from '../clients/authClient';
import type { HttpClient } from '../core/httpClient';
import { globalRequestCache } from '../core/requestCache';

const USER = { id: 'u1', email: 'u@example.com', displayName: 'U', role: 'user' };

describe("authClient svuota la requestCache quando cambia l'identità (#4190)", () => {
  let http: { post: ReturnType<typeof vi.fn>; get: ReturnType<typeof vi.fn> };
  let clear: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    http = { post: vi.fn(), get: vi.fn() };
    clear = vi.spyOn(globalRequestCache, 'clear');
  });

  afterEach(() => {
    clear.mockRestore();
  });

  const client = () => createAuthClient({ httpClient: http as unknown as HttpClient });

  it.each([
    [
      'login',
      () => client().login({ email: USER.email, password: 'x' }),
      { user: USER, requiresTwoFactor: false },
    ],
    ['verify2FALogin', () => client().verify2FALogin('tmp', '123456'), { user: USER }],
    [
      'register',
      () => client().register({ email: USER.email, password: 'x', termsAccepted: true }),
      { user: USER },
    ],
    ['logout', () => client().logout(), { ok: true }],
  ] as const)('%s clears it after the call succeeds', async (_name, call, response) => {
    http.post.mockResolvedValue(response);

    await call();

    expect(clear).toHaveBeenCalledTimes(1);
    expect(clear.mock.invocationCallOrder[0]).toBeGreaterThan(
      http.post.mock.invocationCallOrder[0]
    );
  });

  it('a failed login leaves it alone: the identity did not change', async () => {
    http.post.mockRejectedValue(new Error('401'));

    await expect(client().login({ email: USER.email, password: 'x' })).rejects.toThrow();

    expect(clear).not.toHaveBeenCalled();
  });
});
