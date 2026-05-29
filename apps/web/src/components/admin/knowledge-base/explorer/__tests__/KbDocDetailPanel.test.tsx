import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { KbDocDetailPanel } from '../KbDocDetailPanel';
import type {
  KbDocDetail,
  KbDocEnvelope,
  KbChunkSummary,
} from '@/lib/api/schemas/kb-chunks.schemas';

// ── next/navigation mock (mutable per-test via mockSearchParams) ──────────────
let mockSearchParams: URLSearchParams = new URLSearchParams('');

vi.mock('next/navigation', () => ({
  useSearchParams: () => mockSearchParams,
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/admin/knowledge-base',
}));

// ── next/link mock (KbDocDetailTabs renders Link) ─────────────────────────────
vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    ...rest
  }: {
    href: string;
    children: ReactNode;
    [k: string]: unknown;
  }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

// ── admin-kb-ingestion mock (IngestionPanel uses fetchKbDocIngestionLog) ───────
vi.mock('@/lib/api/admin-kb-ingestion', () => ({
  fetchKbDocIngestionLog: vi.fn().mockResolvedValue(null),
  retryIngestionJob: vi.fn().mockResolvedValue(undefined),
}));

// ── admin-kb-used-by mock (UsedByPanel uses fetchKbDocConsumingAgents) ─────────
vi.mock('@/lib/api/admin-kb-used-by', () => ({
  fetchKbDocConsumingAgents: vi.fn().mockResolvedValue([]),
}));

const mockUseKbDocDetail = vi.fn();
const mockUseKbChunksList = vi.fn();

vi.mock('@/hooks/queries/useKbDocDetail', () => ({
  useKbDocDetail: (options: unknown) => mockUseKbDocDetail(options),
}));
vi.mock('@/hooks/queries/useKbChunksList', () => ({
  useKbChunksList: (options: unknown) => mockUseKbChunksList(options),
}));

// ── QueryClientProvider wrapper (needed for IngestionPanel tab) ───────────────
function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

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
    mockSearchParams = new URLSearchParams(''); // default: overview tab
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

  // ── Tab routing tests ─────────────────────────────────────────────────────

  it('renders Overview content (hero + chunks) when no ?tab param', () => {
    // mockSearchParams defaults to '' (overview) from beforeEach
    mockUseKbDocDetail.mockReturnValue({ data: readyEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({
      data: { pages: [{ items: [chunk1], nextCursor: null, totalCount: 1 }] },
      hasNextPage: false,
      isFetchingNextPage: false,
      fetchNextPage: vi.fn(),
    });
    render(<KbDocDetailPanel docId="doc-1" />, { wrapper: makeWrapper() });
    // Hero heading should be visible (overview content)
    expect(screen.getByRole('heading', { name: /Wingspan-Oceania-EN\.pdf/ })).toBeInTheDocument();
    // Tabs nav should be present
    expect(screen.getByRole('navigation', { name: /sezione documento/i })).toBeInTheDocument();
    // IngestionPanel should NOT be rendered
    expect(screen.queryByTestId('ingestion-panel-empty')).not.toBeInTheDocument();
    expect(screen.queryByTestId('ingestion-panel-loading')).not.toBeInTheDocument();
  });

  it('renders IngestionPanel when ?tab=ingestion', async () => {
    mockSearchParams = new URLSearchParams('tab=ingestion');
    mockUseKbDocDetail.mockReturnValue({ data: readyEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({
      data: undefined,
      hasNextPage: false,
      isFetchingNextPage: false,
      fetchNextPage: vi.fn(),
    });
    render(<KbDocDetailPanel docId="doc-1" />, { wrapper: makeWrapper() });
    // Tabs nav should be present
    expect(screen.getByRole('navigation', { name: /sezione documento/i })).toBeInTheDocument();
    // IngestionPanel empty state should appear (fetchKbDocIngestionLog resolves null)
    await waitFor(() => expect(screen.getByTestId('ingestion-panel-empty')).toBeInTheDocument());
    // Overview hero should NOT be visible
    expect(
      screen.queryByRole('heading', { name: /Wingspan-Oceania-EN\.pdf/ })
    ).not.toBeInTheDocument();
  });

  it('renders UsedByPanel when ?tab=used-by', async () => {
    mockSearchParams = new URLSearchParams('tab=used-by');
    mockUseKbDocDetail.mockReturnValue({ data: readyEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({
      data: undefined,
      hasNextPage: false,
      isFetchingNextPage: false,
      fetchNextPage: vi.fn(),
    });
    render(<KbDocDetailPanel docId="doc-1" />, { wrapper: makeWrapper() });
    // Empty state appears because fetchKbDocConsumingAgents mock resolves [].
    await waitFor(() => expect(screen.getByTestId('used-by-empty')).toBeInTheDocument());
    // Overview hero must NOT render when used-by is active.
    expect(
      screen.queryByRole('heading', { name: /Wingspan-Oceania-EN\.pdf/ })
    ).not.toBeInTheDocument();
  });

  it('renders UsedByPanel even when document is locked (independent of doc readiness)', async () => {
    mockSearchParams = new URLSearchParams('tab=used-by');
    mockUseKbDocDetail.mockReturnValue({ data: lockedEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({ data: undefined, hasNextPage: false });
    render(<KbDocDetailPanel docId="doc-1" />, { wrapper: makeWrapper() });
    // Used-by tab renders (empty state mock resolves []), NOT the locked banner.
    await waitFor(() => expect(screen.getByTestId('used-by-empty')).toBeInTheDocument());
    expect(screen.queryByText(/in elaborazione/i)).not.toBeInTheDocument();
  });

  it('still shows the locked banner when activeTab is overview', () => {
    mockSearchParams = new URLSearchParams(''); // default: overview
    mockUseKbDocDetail.mockReturnValue({ data: lockedEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({ data: undefined, hasNextPage: false });
    render(<KbDocDetailPanel docId="doc-1" />, { wrapper: makeWrapper() });
    expect(screen.getByText(/in elaborazione/i)).toBeInTheDocument();
    expect(screen.queryByTestId('used-by-empty')).not.toBeInTheDocument();
  });
});
