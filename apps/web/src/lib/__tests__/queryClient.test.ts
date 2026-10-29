/**
 * #4735: una mutazione non si ripete da sola.
 *
 * Il server può aver eseguito la scrittura anche quando la risposta è un errore (rete caduta dopo
 * l'esecuzione, 502/503, un passo che lancia dopo il commit). Rispedirla crea un duplicato: è la forma di
 * #4692 (serata doppia) e #4720 (partita doppia). Si prova col client di produzione, non con uno di test.
 */
import { MutationObserver } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';

import { getQueryClient } from '../queryClient';

describe('getQueryClient', () => {
  it('does not resend a mutation that failed', async () => {
    const mutationFn = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    const observer = new MutationObserver(getQueryClient(), { mutationFn });

    await expect(observer.mutate(undefined)).rejects.toThrow('Failed to fetch');

    expect(mutationFn).toHaveBeenCalledTimes(1);
  });

  it('still resends a mutation that asks for it', async () => {
    const mutationFn = vi
      .fn()
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockResolvedValueOnce('done');
    const observer = new MutationObserver(getQueryClient(), {
      mutationFn,
      retry: 1,
      retryDelay: 0,
    });

    await expect(observer.mutate(undefined)).resolves.toBe('done');

    expect(mutationFn).toHaveBeenCalledTimes(2);
  });

  it('keeps retrying a failed query once', async () => {
    const queryFn = vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValueOnce('ok');

    const result = await getQueryClient().fetchQuery({
      queryKey: ['query-client-retry-test'],
      queryFn,
      retryDelay: 0,
    });

    expect(result).toBe('ok');
    expect(queryFn).toHaveBeenCalledTimes(2);
  });
});
