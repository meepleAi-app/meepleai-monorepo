import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock CitationPdfTab to avoid cascading imports of react-pdf + api.knowledgeBase
// (CitationModal lazy-mounts it only when user clicks PDF tab — not exercised here)
vi.mock('../CitationPdfTab', () => ({
  CitationPdfTab: () => null,
}));

vi.mock('@/lib/api/clients/chatClient', () => ({
  qaStream: vi.fn(),
}));

import { qaStream } from '@/lib/api/clients/chatClient';
import { GameChatTabV2 } from '../GameChatTabV2';

async function* mockStream(events: Array<{ type: number; data: unknown }>) {
  for (const e of events) yield e;
}

const sampleCitation = {
  documentId: 'd1',
  pageNumber: 12,
  snippet: 'ogni potere…',
  relevanceScore: 0.95,
  copyrightTier: 'full' as const,
};

const happyEvents = [
  { type: 7, data: 'Sì, ' },
  { type: 7, data: 'ogni potere si attiva ogni volta.' },
  { type: 4, data: { confidence: 0.92, Citations: [sampleCitation] } },
];

const lowConfEvents = [
  { type: 7, data: 'Non sono certo, probabilmente si rimescolano gli scarti.' },
  { type: 4, data: { confidence: 0.42, Citations: [{ ...sampleCitation, pageNumber: 6, snippet: 'fine mazzo', documentId: 'd2' }] } },
];

const oocEvents = [
  { type: 7, data: 'Non ho informazioni su Tainted Grail.' },
  { type: 4, data: { confidence: 0.0, Citations: [] } },
];

describe('GameChatTabV2', () => {
  beforeEach(() => {
    vi.mocked(qaStream).mockReset();
  });

  it('renders empty state with input bar and suggested prompts', () => {
    render(<GameChatTabV2 gameId="wingspan" />);
    expect(screen.getByPlaceholderText(/scrivi/i)).toBeInTheDocument();
  });

  it('happy path: ask → user bubble + agent bubble + citation chip + alta badge', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    render(<GameChatTabV2 gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => expect(screen.getByText(/Sì, ogni potere/)).toBeInTheDocument());
    expect(screen.getByText('q?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /p\. ?12/ })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'alta');
  });

  it('low confidence path: shows disclaimer + bassa badge', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(lowConfEvents) as any);
    render(<GameChatTabV2 gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'edge?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => expect(screen.getByText(/non sono certo/i)).toBeInTheDocument());
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'bassa');
  });

  it('out-of-context path: shows action pills', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(oocEvents) as any);
    render(<GameChatTabV2 gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'tg?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => expect(screen.getByRole('button', { name: /cambia gioco/i })).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /cerca un agente/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /resta/i })).toBeInTheDocument();
  });

  it('citation chip click opens CitationModal', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    render(<GameChatTabV2 gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => screen.getByRole('button', { name: /p\. ?12/ }));
    fireEvent.click(screen.getByRole('button', { name: /p\. ?12/ }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    // KB button hidden until G4 → onOpenInKb undefined
    expect(screen.queryByRole('button', { name: /apri nella kb/i })).not.toBeInTheDocument();
  });
});
