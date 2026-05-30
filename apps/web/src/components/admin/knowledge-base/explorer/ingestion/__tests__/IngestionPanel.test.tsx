import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { IngestionPanel } from '../IngestionPanel';
import * as ingestApi from '@/lib/api/admin-kb-ingestion';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';
import type { ReactNode } from 'react';

function wrap() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

function buildLog(): IngestionLog {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    pdfDocumentId: '00000000-0000-0000-0000-000000000002',
    pdfFileName: 'doc.pdf',
    userId: '00000000-0000-0000-0000-000000000003',
    status: 'Completed',
    priority: 0,
    currentStep: null,
    createdAt: '2026-05-29T10:00:00.000Z',
    startedAt: '2026-05-29T10:00:01.000Z',
    completedAt: '2026-05-29T10:00:11.000Z',
    errorMessage: null,
    retryCount: 0,
    maxRetries: 3,
    canRetry: false,
    steps: [],
  };
}

describe('IngestionPanel', () => {
  it('shows skeleton while loading', () => {
    vi.spyOn(ingestApi, 'fetchKbDocIngestionLog').mockImplementation(
      () => new Promise(() => undefined)
    );
    render(<IngestionPanel docId="xxx" chunkCount={0} pageCount={0} />, { wrapper: wrap() });
    expect(screen.getByTestId('ingestion-panel-loading')).toBeInTheDocument();
  });

  it('shows empty state when backend returns null', async () => {
    vi.spyOn(ingestApi, 'fetchKbDocIngestionLog').mockResolvedValue(null);
    render(<IngestionPanel docId="xxx" chunkCount={0} pageCount={0} />, { wrapper: wrap() });
    await waitFor(() => expect(screen.getByTestId('ingestion-panel-empty')).toBeInTheDocument());
  });

  it('renders hero + timeline + log + actions when data present', async () => {
    vi.spyOn(ingestApi, 'fetchKbDocIngestionLog').mockResolvedValue(buildLog());
    render(<IngestionPanel docId="xxx" chunkCount={10} pageCount={4} />, { wrapper: wrap() });
    await waitFor(() => expect(screen.getByText('doc.pdf')).toBeInTheDocument());
    expect(screen.getByTestId('ingestion-step-upload')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /download/i })).toBeInTheDocument();
  });
});
