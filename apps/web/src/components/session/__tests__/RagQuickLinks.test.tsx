/**
 * RagQuickLinks — S4 RAG quick links pills + detail BottomSheet
 */
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockApi = vi.hoisted(() => ({
  knowledgeBase: {
    getQuickLinks: vi.fn(),
  },
}));

vi.mock('@/lib/api', () => ({ api: mockApi }));

const mockOpenChat = vi.hoisted(() => vi.fn());
vi.mock('@/hooks/useChatPanel', () => ({
  useChatPanel: () => ({ open: mockOpenChat }),
}));

import { RagQuickLinks } from '../RagQuickLinks';

const LINKS = [
  { id: '1', title: 'Regole base', snippet: 'Il gioco si svolge in turni...' },
  { id: '2', title: 'Punteggio', snippet: 'I punti si calcolano così...' },
];

beforeEach(() => {
  vi.clearAllMocks();
  mockApi.knowledgeBase.getQuickLinks.mockResolvedValue(LINKS);
});

describe('RagQuickLinks', () => {
  it('non renderizza nulla senza gameId', () => {
    const { container } = render(<RagQuickLinks gameId={undefined} />);
    expect(container.firstChild).toBeNull();
  });

  it('mostra le pill dopo il caricamento', async () => {
    render(<RagQuickLinks gameId="game-abc" />);
    await waitFor(() => {
      expect(screen.getByText('Regole base')).toBeInTheDocument();
      expect(screen.getByText('Punteggio')).toBeInTheDocument();
    });
  });

  it('non mostra più di 4 pill', async () => {
    const manyLinks = Array.from({ length: 6 }, (_, i) => ({
      id: String(i),
      title: `Link ${i}`,
      snippet: `Snippet ${i}`,
    }));
    mockApi.knowledgeBase.getQuickLinks.mockResolvedValue(manyLinks);
    render(<RagQuickLinks gameId="game-abc" />);
    await waitFor(() => {
      expect(screen.getAllByRole('button')).toHaveLength(4);
    });
  });

  it('apre il BottomSheet con snippet al click', async () => {
    render(<RagQuickLinks gameId="game-abc" />);
    await waitFor(() => screen.getByText('Regole base'));
    fireEvent.click(screen.getByText('Regole base'));
    expect(screen.getByText('Il gioco si svolge in turni...')).toBeInTheDocument();
  });

  it("il tasto Chiedi all'agente nel BottomSheet chiama openChat", async () => {
    render(<RagQuickLinks gameId="game-abc" />);
    await waitFor(() => screen.getByText('Regole base'));
    fireEvent.click(screen.getByText('Regole base'));
    fireEvent.click(screen.getByText("Chiedi all'agente"));
    expect(mockOpenChat).toHaveBeenCalled();
  });

  it('non mostra nulla se la lista è vuota', async () => {
    mockApi.knowledgeBase.getQuickLinks.mockResolvedValue([]);
    const { container } = render(<RagQuickLinks gameId="game-abc" />);
    await waitFor(() => {
      expect(container.querySelector('[data-testid="rag-quick-links"]')).toBeNull();
    });
  });
});
