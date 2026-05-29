import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IngestionHero } from '../IngestionHero';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

function sample(opts: Partial<IngestionLog> = {}): IngestionLog {
  return {
    id: '00000000-0000-0000-0000-000000000099',
    pdfDocumentId: '00000000-0000-0000-0000-000000000088',
    pdfFileName: 'rulebook.pdf',
    userId: '00000000-0000-0000-0000-000000000077',
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
    ...opts,
  };
}

describe('IngestionHero', () => {
  it('shows the file name', () => {
    render(<IngestionHero log={sample()} chunkCount={42} pageCount={10} />);
    expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
  });

  it('shows status chip', () => {
    render(<IngestionHero log={sample({ status: 'Failed' })} chunkCount={0} pageCount={0} />);
    expect(screen.getByText(/failed/i)).toBeInTheDocument();
  });

  it('shows retry counter when retryCount > 0', () => {
    render(
      <IngestionHero log={sample({ retryCount: 2, maxRetries: 3 })} chunkCount={0} pageCount={0} />
    );
    expect(screen.getByText(/retry 2\/3/i)).toBeInTheDocument();
  });

  it('hides retry counter when retryCount === 0', () => {
    render(<IngestionHero log={sample({ retryCount: 0 })} chunkCount={0} pageCount={0} />);
    expect(screen.queryByText(/retry/i)).not.toBeInTheDocument();
  });

  it('renders 4 KPI cards: Progress, Chunks, Pages, Cost', () => {
    render(<IngestionHero log={sample()} chunkCount={400} pageCount={80} />);
    expect(screen.getByText(/progresso/i)).toBeInTheDocument();
    expect(screen.getByText(/chunks/i)).toBeInTheDocument();
    expect(screen.getByText(/pages/i)).toBeInTheDocument();
    expect(screen.getByText(/cost/i)).toBeInTheDocument();
  });
});
