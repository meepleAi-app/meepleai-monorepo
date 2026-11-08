import { describe, it, expect, vi } from 'vitest';

import { createAdminAnalyticsClient } from '../adminAnalyticsClient';

/**
 * #3836 — il client chiamava `/admin/openrouter/usage/requests`, che non esiste: il path corretto
 * e' `/admin/openrouter/requests`, un livello piu' su.
 *
 * L'errore e' comprensibile — i due endpoint vicini stanno davvero sotto `usage/`
 * (`usage/timeline`, `usage/costs`) — ed e' proprio li' che il backend non e' coerente. Il
 * risultato era un 404 e la tabella delle richieste vuota, senza che nulla lo dicesse.
 *
 * Il test asserisce la URL: e' l'unica parte che era sbagliata, e nessun controllo di tipo puo'
 * proteggerla. La forma della risposta era gia' allineata allo schema.
 */

const rispostaValida = {
  items: [],
  total: 0,
  page: 1,
  pageSize: 20,
  totalPages: 0,
};

function httpFinto() {
  const get = vi.fn().mockResolvedValue(rispostaValida);
  return {
    client: {
      get,
      post: vi.fn(),
      put: vi.fn(),
      patch: vi.fn(),
      delete: vi.fn(),
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any,
    get,
  };
}

describe('adminAnalyticsClient — richieste OpenRouter', () => {
  it('chiama /admin/openrouter/requests, non /usage/requests', async () => {
    const { client, get } = httpFinto();
    const analytics = createAdminAnalyticsClient(client);

    await analytics.getRecentRequests({ page: 1, pageSize: 20 });

    const chiamata = get.mock.calls[0]![0] as string;
    expect(chiamata).toContain('/api/v1/admin/openrouter/requests');
    expect(chiamata).not.toContain('/usage/requests');
  });

  it('conserva i parametri di query', async () => {
    const { client, get } = httpFinto();
    const analytics = createAdminAnalyticsClient(client);

    await analytics.getRecentRequests({ page: 2, pageSize: 50, successOnly: true });

    const chiamata = get.mock.calls[0]![0] as string;
    expect(chiamata).toContain('page=2');
    expect(chiamata).toContain('pageSize=50');
    expect(chiamata).toContain('successOnly=true');
  });
});
