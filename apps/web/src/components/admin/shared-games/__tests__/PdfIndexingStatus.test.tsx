import { screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import type { ProcessingProgress } from '@/lib/api';

const { getProcessingProgress } = vi.hoisted(() => ({ getProcessingProgress: vi.fn() }));

vi.mock('@/lib/api', () => ({ api: { pdf: { getProcessingProgress } } }));

import { PdfIndexingStatus } from '../PdfIndexingStatus';

const PDF_ID = 'cc216014-19a3-4a59-bdba-d1219c18e422';

// #5106: GET /pdfs/{id}/progress as the API sends it — a ProcessingProgress, derived from the PDF's state when none is
// recorded. The component used to read `status` and `progress`, which the response does not have.
const progress = (overrides: Partial<ProcessingProgress>): ProcessingProgress => ({
  currentStep: 'Uploading',
  percentComplete: 0,
  elapsedTime: '00:00:00',
  estimatedTimeRemaining: null,
  pagesProcessed: 0,
  totalPages: 0,
  startedAt: '2026-11-20T10:00:00Z',
  completedAt: null,
  errorMessage: null,
  ...overrides,
});

describe('PdfIndexingStatus (#5106)', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    getProcessingProgress.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('mostra RAG Ready per un PDF pronto, e smette di chiedere', async () => {
    getProcessingProgress.mockResolvedValue(
      progress({
        currentStep: 'Completed',
        percentComplete: 100,
        completedAt: '2026-11-20T10:04:00Z',
      })
    );
    const onComplete = vi.fn();

    renderWithQuery(<PdfIndexingStatus pdfId={PDF_ID} compact onComplete={onComplete} />);

    expect(await screen.findByText('RAG Ready')).toBeInTheDocument();
    expect(getProcessingProgress).toHaveBeenCalledWith(PDF_ID);
    expect(onComplete).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(10_000);
    expect(getProcessingProgress).toHaveBeenCalledTimes(1);
  });

  it('mostra il passo in corso, e continua a chiedere', async () => {
    getProcessingProgress.mockResolvedValue(
      progress({ currentStep: 'Chunking', percentComplete: 40 })
    );

    renderWithQuery(<PdfIndexingStatus pdfId={PDF_ID} compact />);

    expect(await screen.findByText('Suddiviso')).toBeInTheDocument();
    // percentComplete is the overall percentage: the bar shows it as it is.
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '40');
    await vi.advanceTimersByTimeAsync(3_100);
    await waitFor(() => expect(getProcessingProgress.mock.calls.length).toBeGreaterThanOrEqual(2));
  });

  it("mostra l'errore di un PDF fallito", async () => {
    getProcessingProgress.mockResolvedValue(
      progress({ currentStep: 'Failed', errorMessage: 'Extraction failed: no text layer' })
    );
    const onError = vi.fn();

    renderWithQuery(<PdfIndexingStatus pdfId={PDF_ID} compact onError={onError} />);

    expect(await screen.findByText('Extraction failed: no text layer')).toBeInTheDocument();
    expect(screen.getByText('Errore')).toBeInTheDocument();
    expect(onError).toHaveBeenCalledWith('Extraction failed: no text layer');
  });

  it('senza risposta mostra che lo stato non è disponibile', async () => {
    getProcessingProgress.mockResolvedValue(null);

    renderWithQuery(<PdfIndexingStatus pdfId={PDF_ID} compact />);

    expect(await screen.findByText('Errore nel recupero dello stato')).toBeInTheDocument();
  });
});
