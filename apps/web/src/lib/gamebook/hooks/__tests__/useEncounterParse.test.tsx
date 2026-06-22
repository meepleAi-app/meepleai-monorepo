import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import * as encounterApi from '@/lib/api/gamebook-encounter';

import { useEncounterParse } from '../useEncounterParse';

vi.mock('@/lib/api/gamebook-encounter', async importOriginal => {
  const actual = await importOriginal<typeof encounterApi>();
  return { ...actual, parseEncounter: vi.fn() };
});

const CAMPAIGN_ID = '11111111-1111-4111-a111-111111111111';
const PHOTO_ID = '22222222-2222-4222-a222-222222222222';
const BOOK_ID = '44444444-4444-4444-a444-444444444444';

const fakeCheatsheet: encounterApi.EncounterCheatsheet = {
  enemies: [
    {
      name: 'Goblin Scout (×3)',
      icon: '⚔️',
      paragraphMarker: '§218',
      hp: '8',
      atk: '+3',
      def: '12',
      mov: '5',
    },
  ],
  options: [{ label: '⚔️ Attacca subito', diceRoll: null, outcome: '→ §223' }],
  conditions: { win: 'KO tutti i Goblin', loss: 'Party a 0 HP' },
  confidence: { enemies: 0.94, options: 0.9, conditions: 0.88 },
};

function makeWrapper(qc: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

describe('useEncounterParse', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls parseEncounter with the route ids + mutation variables and returns the cheatsheet', async () => {
    vi.mocked(encounterApi.parseEncounter).mockResolvedValueOnce(fakeCheatsheet);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });

    const { result } = renderHook(() => useEncounterParse(CAMPAIGN_ID, PHOTO_ID), {
      wrapper: makeWrapper(qc),
    });

    result.current.mutate({ paragraphNumber: 218, gameBookId: BOOK_ID });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(encounterApi.parseEncounter).toHaveBeenCalledWith(CAMPAIGN_ID, PHOTO_ID, {
      paragraphNumber: 218,
      gameBookId: BOOK_ID,
    });
    expect(result.current.data?.enemies[0].name).toBe('Goblin Scout (×3)');
  });

  it('surfaces an EncounterParseError (status 409) as the mutation error', async () => {
    vi.mocked(encounterApi.parseEncounter).mockRejectedValueOnce(
      new encounterApi.EncounterParseError(409, 'parse failed')
    );
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });

    const { result } = renderHook(() => useEncounterParse(CAMPAIGN_ID, PHOTO_ID), {
      wrapper: makeWrapper(qc),
    });

    result.current.mutate({ paragraphNumber: 218, gameBookId: BOOK_ID });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(encounterApi.EncounterParseError);
    expect((result.current.error as encounterApi.EncounterParseError).status).toBe(409);
  });
});
