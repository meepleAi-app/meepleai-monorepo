import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock rules-cache
vi.mock('@/lib/game-night/rules-cache', () => ({
  getCachedAnalyses: vi.fn().mockReturnValue(null),
  cacheRulebookAnalyses: vi.fn(),
}));

// Mock sharedGamesClient
vi.mock('@/lib/api/clients/sharedGamesClient', () => ({
  createSharedGamesClient: vi.fn(() => ({
    getGameAnalysis: vi.fn().mockResolvedValue([
      {
        id: 'a1',
        sharedGameId: 'g1',
        gameTitle: 'Catan',
        summary: 'Commercia e costruisci per vincere.',
        keyMechanics: ['Scambio', 'Costruzione'],
        victoryConditions: {
          primary: 'Primi 10 punti',
          alternatives: [],
          isPointBased: true,
          targetPoints: 10,
        },
        resources: [],
        gamePhases: [
          { name: 'Setup', description: 'Posiziona esagoni', order: 1, isOptional: false },
        ],
        commonQuestions: [],
        generatedFaqs: [],
        confidenceScore: 0.9,
        version: '1',
        isActive: true,
        source: 'LLM',
        analyzedAt: '2026-01-01T00:00:00Z',
        createdBy: 'user1',
        keyConcepts: [],
        completionStatus: 'Complete',
      },
    ]),
  })),
}));

// Mock HttpClient dependency
vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({})),
}));

import { RulesContent } from '../RulesContent';

describe('RulesContent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('mostra placeholder quando gameId è null', () => {
    render(<RulesContent gameId={null} />);
    expect(screen.getByText(/seleziona un gioco/i)).toBeInTheDocument();
  });

  it('mostra loading spinner durante fetch', () => {
    render(<RulesContent gameId="g1" />);
    expect(screen.getByTestId('rules-loading')).toBeInTheDocument();
  });

  it('mostra il summary dopo il fetch', async () => {
    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Commercia e costruisci per vincere.')).toBeInTheDocument();
    });
  });

  it('mostra le meccaniche chiave', async () => {
    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Scambio')).toBeInTheDocument();
      expect(screen.getByText('Costruzione')).toBeInTheDocument();
    });
  });

  it('mostra le fasi di gioco', async () => {
    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Setup')).toBeInTheDocument();
    });
  });

  it('mostra le condizioni di vittoria', async () => {
    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText(/primi 10 punti/i)).toBeInTheDocument();
    });
  });

  it('usa la cache se disponibile senza fetching', async () => {
    const { getCachedAnalyses } = await import('@/lib/game-night/rules-cache');
    vi.mocked(getCachedAnalyses).mockReturnValue({
      analyses: [
        {
          id: 'a1',
          sharedGameId: 'g1',
          gameTitle: 'Catan',
          summary: 'Da cache.',
          keyMechanics: [],
          victoryConditions: null,
          resources: [],
          gamePhases: [],
          commonQuestions: [],
          generatedFaqs: [],
          confidenceScore: 0.9,
          version: '1',
          isActive: true,
          source: 'LLM',
          analyzedAt: '2026-01-01T00:00:00Z',
          createdBy: 'user1',
          keyConcepts: [],
          completionStatus: 'Complete',
        } as any,
      ],
      cachedAt: new Date().toISOString(),
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
      gameTitle: 'Catan',
    });

    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Da cache.')).toBeInTheDocument();
    });
  });

  it('mostra messaggio se nessuna analisi disponibile', async () => {
    // Reset cache mock to avoid bleeding from previous test
    const { getCachedAnalyses } = await import('@/lib/game-night/rules-cache');
    vi.mocked(getCachedAnalyses).mockReturnValue(null);

    const { createSharedGamesClient } = await import('@/lib/api/clients/sharedGamesClient');
    vi.mocked(createSharedGamesClient).mockReturnValue({
      getGameAnalysis: vi.fn().mockResolvedValue([]),
    } as any);

    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText(/non disponibili/i)).toBeInTheDocument();
    });
  });
});
