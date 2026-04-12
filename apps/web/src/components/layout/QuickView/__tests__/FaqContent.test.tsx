import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock rules-cache
vi.mock('@/lib/game-night/rules-cache', () => ({
  getCachedAnalyses: vi.fn().mockReturnValue(null),
  cacheRulebookAnalyses: vi.fn(),
}));

const mockFaqs = [
  {
    question: 'Quanti punti servono per vincere?',
    answer: 'Servono 10 punti vittoria.',
    sourceSection: 'Vittoria',
    confidence: 0.95,
    tags: ['punti', 'vittoria'],
  },
  {
    question: 'Posso costruire strade ovunque?',
    answer: 'No, solo adiacenti alle tue strutture.',
    sourceSection: 'Costruzione',
    confidence: 0.9,
    tags: ['strade'],
  },
];

// Mock sharedGamesClient
vi.mock('@/lib/api/clients/sharedGamesClient', () => ({
  createSharedGamesClient: vi.fn(() => ({
    getGameAnalysis: vi.fn().mockResolvedValue([
      {
        id: 'a1',
        sharedGameId: 'g1',
        gameTitle: 'Catan',
        summary: 'Commercia e costruisci per vincere.',
        keyMechanics: [],
        victoryConditions: null,
        resources: [],
        gamePhases: [],
        commonQuestions: ['Come si gioca il primo turno?'],
        generatedFaqs: mockFaqs,
        confidenceScore: 0.9,
        version: 1,
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

import { FaqContent } from '../FaqContent';

describe('FaqContent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('mostra placeholder quando gameId è null', () => {
    render(<FaqContent gameId={null} />);
    expect(screen.getByText(/seleziona un gioco/i)).toBeInTheDocument();
  });

  it('mostra loading spinner durante fetch', () => {
    render(<FaqContent gameId="g1" />);
    expect(screen.getByTestId('faq-loading')).toBeInTheDocument();
  });

  it('mostra le FAQ generate dopo il fetch', async () => {
    render(<FaqContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Quanti punti servono per vincere?')).toBeInTheDocument();
      expect(screen.getByText('Servono 10 punti vittoria.')).toBeInTheDocument();
    });
  });

  it('mostra tutte le FAQ', async () => {
    render(<FaqContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Posso costruire strade ovunque?')).toBeInTheDocument();
      expect(screen.getByText('No, solo adiacenti alle tue strutture.')).toBeInTheDocument();
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
          generatedFaqs: [
            {
              question: 'FAQ da cache?',
              answer: 'Sì, dalla cache.',
              sourceSection: 'Cache',
              confidence: 0.8,
              tags: [],
            },
          ],
          confidenceScore: 0.9,
          version: 1,
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

    render(<FaqContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('FAQ da cache?')).toBeInTheDocument();
    });
  });

  it('mostra messaggio se nessuna FAQ disponibile', async () => {
    const { getCachedAnalyses } = await import('@/lib/game-night/rules-cache');
    vi.mocked(getCachedAnalyses).mockReturnValue(null);

    const { createSharedGamesClient } = await import('@/lib/api/clients/sharedGamesClient');
    vi.mocked(createSharedGamesClient).mockReturnValue({
      getGameAnalysis: vi.fn().mockResolvedValue([]),
    } as any);

    render(<FaqContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText(/non disponibili/i)).toBeInTheDocument();
    });
  });
});
