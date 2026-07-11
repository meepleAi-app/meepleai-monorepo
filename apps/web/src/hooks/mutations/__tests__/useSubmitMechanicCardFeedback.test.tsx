/** @vitest-environment jsdom */
import type { JSX, ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { api, NotFoundError, RateLimitError } from '@/lib/api';

import { useSubmitMechanicCardFeedback } from '../useSubmitMechanicCardFeedback';

function makeWrapper(qc: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }): JSX.Element {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

const CARD_ID = '11111111-1111-4111-8111-111111111111';
const CLAIM_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

describe('useSubmitMechanicCardFeedback (ME-M3.1 #533)', () => {
  it('posts a positive vote (errorType null) via the client', async () => {
    const spy = vi
      .spyOn(api.sharedGames, 'submitMechanicCardFeedback')
      .mockResolvedValue(undefined);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useSubmitMechanicCardFeedback(), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync({
        cardId: CARD_ID,
        body: {
          claimId: CLAIM_ID,
          isPositive: true,
          errorType: null,
          description: null,
          suggestedCitation: null,
        },
      });
    });

    expect(spy).toHaveBeenCalledWith(CARD_ID, {
      claimId: CLAIM_ID,
      isPositive: true,
      errorType: null,
      description: null,
      suggestedCitation: null,
    });
    spy.mockRestore();
  });

  it('posts a negative report with type + description', async () => {
    const spy = vi
      .spyOn(api.sharedGames, 'submitMechanicCardFeedback')
      .mockResolvedValue(undefined);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useSubmitMechanicCardFeedback(), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync({
        cardId: CARD_ID,
        body: {
          claimId: CLAIM_ID,
          isPositive: false,
          errorType: 'factual',
          description: 'This is wrong.',
          suggestedCitation: 'p. 12',
        },
      });
    });

    expect(spy).toHaveBeenCalledWith(CARD_ID, {
      claimId: CLAIM_ID,
      isPositive: false,
      errorType: 'factual',
      description: 'This is wrong.',
      suggestedCitation: 'p. 12',
    });
    spy.mockRestore();
  });

  it('surfaces a RateLimitError (429) on the mutation', async () => {
    const spy = vi
      .spyOn(api.sharedGames, 'submitMechanicCardFeedback')
      .mockRejectedValue(new RateLimitError({ message: 'rate limited', endpoint: '/feedback' }));
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useSubmitMechanicCardFeedback(), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      try {
        await result.current.mutateAsync({
          cardId: CARD_ID,
          body: {
            claimId: CLAIM_ID,
            isPositive: true,
            errorType: null,
            description: null,
            suggestedCitation: null,
          },
        });
      } catch {
        /* expected */
      }
    });

    await waitFor(() => expect(result.current.error).toBeInstanceOf(RateLimitError));
    spy.mockRestore();
  });

  it('surfaces a NotFoundError (404) on the mutation', async () => {
    const spy = vi
      .spyOn(api.sharedGames, 'submitMechanicCardFeedback')
      .mockRejectedValue(new NotFoundError({ message: 'gone', endpoint: '/feedback' }));
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useSubmitMechanicCardFeedback(), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      try {
        await result.current.mutateAsync({
          cardId: CARD_ID,
          body: {
            claimId: CLAIM_ID,
            isPositive: true,
            errorType: null,
            description: null,
            suggestedCitation: null,
          },
        });
      } catch {
        /* expected */
      }
    });

    await waitFor(() => expect(result.current.error).toBeInstanceOf(NotFoundError));
    spy.mockRestore();
  });
});
