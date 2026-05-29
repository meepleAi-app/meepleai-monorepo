import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { IngestionActions } from '../IngestionActions';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

function sampleLog(opts: Partial<IngestionLog> = {}): IngestionLog {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    pdfDocumentId: '00000000-0000-0000-0000-000000000002',
    pdfFileName: 'doc.pdf',
    userId: '00000000-0000-0000-0000-000000000003',
    status: 'Completed',
    priority: 0,
    currentStep: null,
    createdAt: new Date().toISOString(),
    startedAt: null,
    completedAt: null,
    errorMessage: null,
    retryCount: 0,
    maxRetries: 3,
    canRetry: false,
    steps: [],
    ...opts,
  };
}

describe('IngestionActions', () => {
  it('shows Download log and Copy job ID always', () => {
    render(<IngestionActions log={sampleLog()} onRetry={vi.fn()} />);
    expect(screen.getByRole('button', { name: /download/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /copy job id/i })).toBeInTheDocument();
  });

  it('hides Re-enqueue when canRetry is false', () => {
    render(<IngestionActions log={sampleLog({ canRetry: false })} onRetry={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /re-enqueue/i })).not.toBeInTheDocument();
  });

  it('shows Re-enqueue when canRetry is true', () => {
    render(<IngestionActions log={sampleLog({ canRetry: true })} onRetry={vi.fn()} />);
    expect(screen.getByRole('button', { name: /re-enqueue/i })).toBeInTheDocument();
  });

  it('calls onRetry with jobId when Re-enqueue clicked', () => {
    const onRetry = vi.fn();
    const log = sampleLog({ canRetry: true });
    render(<IngestionActions log={log} onRetry={onRetry} />);
    fireEvent.click(screen.getByRole('button', { name: /re-enqueue/i }));
    expect(onRetry).toHaveBeenCalledWith(log.id);
  });

  it('writes job ID to clipboard when Copy clicked', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, writable: true });
    const log = sampleLog();
    render(<IngestionActions log={log} onRetry={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: /copy job id/i }));
    expect(writeText).toHaveBeenCalledWith(log.id);
  });
});
