/**
 * ChatNavigationContext — il link dal thread al suo gioco (#4244).
 *
 * `/library/{id}` è la pagina di un gioco del catalogo in libreria: per l'id di un gioco privato
 * risponde «non trovato». Un thread su un gioco privato porta alla pagina del gioco privato.
 */

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

vi.mock('@/lib/api', () => ({
  api: { chat: { getThreadById: vi.fn() } },
}));

import { api } from '@/lib/api';

import { ChatNavigationContext } from '../ChatNavigationContext';

const getThreadById = api.chat.getThreadById as Mock;

const GAME_ID = '2b3c4d5e-6f7a-4b2c-8d3e-4f5a6b7c8d9e';

function thread(extra: Record<string, unknown>) {
  return { id: 'thread-1', gameId: GAME_ID, agentId: null, ...extra };
}

describe('ChatNavigationContext — game link', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('links a thread on a private game to the page of the private game', async () => {
    getThreadById.mockResolvedValue(thread({ privateGameId: GAME_ID }));

    render(<ChatNavigationContext threadId="thread-1" />);

    const link = await screen.findByRole('link', { name: 'common.entity.game' });
    expect(link).toHaveAttribute('href', `/library/private/${GAME_ID}`);
  });

  // Il controllo: per un gioco del catalogo il link resta quello di sempre.
  it('links a thread on a catalog game to the library page of the game', async () => {
    getThreadById.mockResolvedValue(thread({ privateGameId: null }));

    render(<ChatNavigationContext threadId="thread-1" />);

    const link = await screen.findByRole('link', { name: 'common.entity.game' });
    expect(link).toHaveAttribute('href', `/library/${GAME_ID}`);
  });
});
