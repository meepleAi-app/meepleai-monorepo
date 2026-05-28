import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { KbExplorer } from '../KbExplorer';
import type { GameKbStatusItem } from '@/lib/api/schemas/admin-knowledge-base.schemas';

// next/navigation
const mockReplace = vi.fn();
const mockSearchParams = vi.fn();
const mockPathname = vi.fn();
vi.mock('next/navigation', () => ({
  useSearchParams: () => mockSearchParams(),
  useRouter: () => ({ replace: mockReplace }),
  usePathname: () => mockPathname(),
}));

// adminClient
const mockGetGameKbStatuses = vi.fn();
vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({ getGameKbStatuses: mockGetGameKbStatuses }),
}));
vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: class {},
}));

// child hooks
vi.mock('@/hooks/queries/useGameDocuments', () => ({
  useKbGameDocuments: () => ({ data: [], isLoading: false }),
}));
vi.mock('@/hooks/queries/useKbDocDetail', () => ({
  useKbDocDetail: () => ({ data: null, isLoading: false }),
}));
vi.mock('@/hooks/queries/useKbChunksList', () => ({
  useKbChunksList: () => ({ data: undefined, hasNextPage: false }),
}));

const games: GameKbStatusItem[] = [
  {
    gameId: 'g-1',
    gameName: 'Wingspan',
    kbStatus: 'complete',
    documentCount: 6,
    totalChunks: 412,
    latestIndexedAt: null,
    hasAutoBackup: true,
  },
];

function wrap(children: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('KbExplorer', () => {
  beforeEach(() => {
    mockReplace.mockReset();
    mockSearchParams.mockReturnValue(new URLSearchParams());
    mockPathname.mockReturnValue('/admin/knowledge-base');
    mockGetGameKbStatuses.mockReset();
  });

  it('shows loading state while top-level games are loading', () => {
    mockGetGameKbStatuses.mockImplementation(() => new Promise(() => {}));
    render(wrap(<KbExplorer />));
    expect(screen.getByTestId('kb-explorer-loading')).toBeInTheDocument();
  });

  it('renders the tree once games load and the empty doc panel by default', async () => {
    mockGetGameKbStatuses.mockResolvedValue({ items: games });
    render(wrap(<KbExplorer />));
    expect(await screen.findByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByTestId('kb-doc-detail-empty')).toBeInTheDocument();
  });

  it('hydrates selectedDocId from ?doc= query param', async () => {
    mockSearchParams.mockReturnValue(new URLSearchParams({ doc: 'd-42' }));
    mockGetGameKbStatuses.mockResolvedValue({ items: games });
    render(wrap(<KbExplorer />));
    await screen.findByText('Wingspan');
    expect(screen.queryByTestId('kb-doc-detail-empty')).not.toBeInTheDocument();
  });

  it('updates ?doc= via router.replace when a doc leaf is clicked', async () => {
    mockGetGameKbStatuses.mockResolvedValue({ items: games });
    render(wrap(<KbExplorer />));
    await screen.findByText('Wingspan');
    fireEvent.click(screen.getByRole('treeitem', { name: /Wingspan/ }));
    expect(mockReplace).not.toHaveBeenCalled();
  });
});
