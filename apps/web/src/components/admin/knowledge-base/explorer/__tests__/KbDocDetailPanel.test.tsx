import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { KbDocDetailPanel } from '../KbDocDetailPanel';
import type {
  KbDocDetail,
  KbDocEnvelope,
  KbChunkSummary,
} from '@/lib/api/schemas/kb-chunks.schemas';

const mockUseKbDocDetail = vi.fn();
const mockUseKbChunksList = vi.fn();

vi.mock('@/hooks/queries/useKbDocDetail', () => ({
  useKbDocDetail: (options: unknown) => mockUseKbDocDetail(options),
}));
vi.mock('@/hooks/queries/useKbChunksList', () => ({
  useKbChunksList: (options: unknown) => mockUseKbChunksList(options),
}));

const readyDoc: KbDocDetail = {
  id: 'doc-1',
  title: 'Wingspan-Oceania-EN.pdf',
  docType: 'rulebook',
  gameId: 'g-1',
  gameName: 'Wingspan',
  uploaderName: 'Aaron',
  uploadedAt: '2024-12-08T14:22:00Z',
  lastIngestedAt: '2024-12-08T14:22:30Z',
  processingStatus: 'ready',
  chunkCount: 412,
  pageCount: 42,
  language: 'en',
  tags: [],
};

const readyEnvelope: KbDocEnvelope = { status: 'ready', doc: readyDoc };
const lockedEnvelope: KbDocEnvelope = {
  status: 'locked',
  processingStatus: 'processing',
  doc: null,
};

const chunk1: KbChunkSummary = {
  id: 'c-1',
  position: 12,
  headingPath: ['Setup', 'Predator activation'],
  snippet: 'Quando più uccelli predatori sono attivati nello stesso turno…',
  pageNumber: 22,
  vectorId: 'v-1',
};
const chunk2: KbChunkSummary = {
  id: 'c-2',
  position: 13,
  headingPath: ['Power'],
  snippet: 'Power di tipo predator si attiva su trigger when activated…',
  pageNumber: 14,
  vectorId: 'v-2',
};

describe('KbDocDetailPanel', () => {
  beforeEach(() => {
    mockUseKbDocDetail.mockReset();
    mockUseKbChunksList.mockReset();
  });

  it('renders the empty placeholder when docId is null', () => {
    mockUseKbDocDetail.mockReturnValue({ data: null, isLoading: false });
    mockUseKbChunksList.mockReturnValue({ data: undefined, hasNextPage: false });
    render(<KbDocDetailPanel docId={null} />);
    expect(screen.getByText(/seleziona un documento/i)).toBeInTheDocument();
  });

  it('renders a loading skeleton while doc detail is loading', () => {
    mockUseKbDocDetail.mockReturnValue({ data: undefined, isLoading: true });
    mockUseKbChunksList.mockReturnValue({ data: undefined, hasNextPage: false });
    render(<KbDocDetailPanel docId="doc-1" />);
    expect(screen.getByTestId('kb-doc-detail-loading')).toBeInTheDocument();
  });

  it('renders the in-progress banner for a locked envelope', () => {
    mockUseKbDocDetail.mockReturnValue({ data: lockedEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({ data: undefined, hasNextPage: false });
    render(<KbDocDetailPanel docId="doc-1" />);
    expect(screen.getByText(/in elaborazione/i)).toBeInTheDocument();
    expect(screen.queryByText(chunk1.snippet)).not.toBeInTheDocument();
  });

  it('renders the ready hero with title, gameName, language and counts', () => {
    mockUseKbDocDetail.mockReturnValue({ data: readyEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({
      data: { pages: [{ items: [chunk1, chunk2], nextCursor: null, totalCount: 2 }] },
      hasNextPage: false,
      isFetchingNextPage: false,
      fetchNextPage: vi.fn(),
    });
    render(<KbDocDetailPanel docId="doc-1" />);

    expect(screen.getByRole('heading', { name: /Wingspan-Oceania-EN\.pdf/ })).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('en')).toBeInTheDocument();
    expect(screen.getByText('rulebook')).toBeInTheDocument();
    expect(screen.getByText(/412/)).toBeInTheDocument();
    expect(screen.getByText(/42/)).toBeInTheDocument();
  });

  it('renders chunks with position, pageNumber and snippet', () => {
    mockUseKbDocDetail.mockReturnValue({ data: readyEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({
      data: { pages: [{ items: [chunk1, chunk2], nextCursor: null, totalCount: 2 }] },
      hasNextPage: false,
      isFetchingNextPage: false,
      fetchNextPage: vi.fn(),
    });
    render(<KbDocDetailPanel docId="doc-1" />);
    expect(screen.getByText(/c-0012/)).toBeInTheDocument();
    expect(screen.getByText(/p\. 22/)).toBeInTheDocument();
    expect(screen.getByText(/Quando più uccelli/)).toBeInTheDocument();
    expect(screen.getByText('Setup › Predator activation')).toBeInTheDocument();
  });

  it('shows "Carica altri" button when hasNextPage is true and calls fetchNextPage', () => {
    const fetchNextPage = vi.fn();
    mockUseKbDocDetail.mockReturnValue({ data: readyEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({
      data: { pages: [{ items: [chunk1], nextCursor: 'cur-1', totalCount: 50 }] },
      hasNextPage: true,
      isFetchingNextPage: false,
      fetchNextPage,
    });
    render(<KbDocDetailPanel docId="doc-1" />);
    fireEvent.click(screen.getByRole('button', { name: /carica altri/i }));
    expect(fetchNextPage).toHaveBeenCalled();
  });

  it('disables docDetail and chunks queries when docId is null', () => {
    mockUseKbDocDetail.mockReturnValue({ data: null, isLoading: false });
    mockUseKbChunksList.mockReturnValue({ data: undefined, hasNextPage: false });
    render(<KbDocDetailPanel docId={null} />);
    expect(mockUseKbDocDetail).toHaveBeenCalledWith(expect.objectContaining({ enabled: false }));
    expect(mockUseKbChunksList).toHaveBeenCalledWith(expect.objectContaining({ enabled: false }));
  });
});
