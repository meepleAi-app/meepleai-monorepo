/**
 * KbCardStatusRow Tests - Issue #5197
 *
 * Tests for KbCardStatusRow component (Issue #5192):
 * - Renders filename, status badge, progress bar, retry button
 * - Status badge reflects processingState correctly
 * - Progress bar only visible when processing
 * - Retry button only visible when failed and retries remain
 *
 * Pattern: Vitest + React Testing Library
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { KbCardStatusRow } from '../KbCardStatusRow';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

// ============================================================================
// Test helpers
// ============================================================================

function makeDoc(overrides: Partial<PdfDocumentDto> = {}): PdfDocumentDto {
  return {
    id: 'doc-001',
    gameId: 'game-001',
    fileName: 'rulebook.pdf',
    filePath: '/uploads/rulebook.pdf',
    fileSizeBytes: 1024 * 1024,
    processingStatus: 'Processing',
    uploadedAt: '2026-01-01T00:00:00.000Z',
    processedAt: null,
    pageCount: null,
    documentType: 'base',
    isPublic: false,
    processingState: 'Pending',
    progressPercentage: 0,
    retryCount: 0,
    maxRetries: 3,
    ...overrides,
  };
}

// ============================================================================
// Tests
// ============================================================================

describe('KbCardStatusRow - Issue #5197', () => {
  // --------------------------------------------------------------------------
  // Rendering
  // --------------------------------------------------------------------------

  it('renders filename', () => {
    render(<KbCardStatusRow document={makeDoc({ fileName: 'my-rulebook.pdf' })} />);
    expect(screen.getByTestId('kb-card-status-row-filename')).toHaveTextContent('my-rulebook.pdf');
  });

  it('renders with correct data-testid based on document id', () => {
    render(<KbCardStatusRow document={makeDoc({ id: 'abc-123' })} />);
    expect(screen.getByTestId('kb-card-status-row-abc-123')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Status badge — processingState mapping
  // --------------------------------------------------------------------------

  it('shows indexed badge for Ready state', () => {
    render(<KbCardStatusRow document={makeDoc({ processingState: 'Ready' })} />);
    const row = screen.getByTestId('kb-card-status-row-doc-001');
    expect(row).toBeInTheDocument();
    // DocumentStatusBadge uses label "Indicizzata" for indexed status
    expect(screen.getByText('Indicizzata')).toBeInTheDocument();
  });

  it('shows processing badge for Extracting state', () => {
    render(
      <KbCardStatusRow
        document={makeDoc({ processingState: 'Extracting', progressPercentage: 30 })}
      />
    );
    expect(screen.getByTestId('kb-card-status-row-progress')).toBeInTheDocument();
  });

  it('shows processing badge for Uploading state', () => {
    render(
      <KbCardStatusRow
        document={makeDoc({ processingState: 'Uploading', progressPercentage: 10 })}
      />
    );
    expect(screen.getByTestId('kb-card-status-row-progress')).toBeInTheDocument();
  });

  it('shows failed badge for Failed state', () => {
    render(
      <KbCardStatusRow
        document={makeDoc({ processingState: 'Failed', retryCount: 0, maxRetries: 3 })}
      />
    );
    // Should show retry button since retries remain
    expect(screen.getByTestId('kb-card-status-row-retry')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Progress bar
  // --------------------------------------------------------------------------

  it('shows progress bar when processing', () => {
    render(
      <KbCardStatusRow
        document={makeDoc({ processingState: 'Indexing', progressPercentage: 55 })}
      />
    );
    const bar = screen.getByTestId('kb-card-status-row-progress');
    expect(bar).toBeInTheDocument();
    expect(bar).toHaveAttribute('aria-valuenow', '55');
    expect(bar).toHaveAttribute('aria-valuemin', '0');
    expect(bar).toHaveAttribute('aria-valuemax', '100');
  });

  it('hides progress bar when Ready', () => {
    render(
      <KbCardStatusRow document={makeDoc({ processingState: 'Ready', progressPercentage: 100 })} />
    );
    expect(screen.queryByTestId('kb-card-status-row-progress')).not.toBeInTheDocument();
  });

  it('hides progress bar when Failed', () => {
    render(
      <KbCardStatusRow document={makeDoc({ processingState: 'Failed', progressPercentage: 42 })} />
    );
    expect(screen.queryByTestId('kb-card-status-row-progress')).not.toBeInTheDocument();
  });

  it('hides progress bar when Pending', () => {
    render(<KbCardStatusRow document={makeDoc({ processingState: 'Pending' })} />);
    expect(screen.queryByTestId('kb-card-status-row-progress')).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Retry button
  // --------------------------------------------------------------------------

  it('shows retry button when failed and retries remain', () => {
    render(
      <KbCardStatusRow
        document={makeDoc({ processingState: 'Failed', retryCount: 1, maxRetries: 3 })}
      />
    );
    expect(screen.getByTestId('kb-card-status-row-retry')).toBeInTheDocument();
  });

  it('hides retry button when retries exhausted', () => {
    render(
      <KbCardStatusRow
        document={makeDoc({ processingState: 'Failed', retryCount: 3, maxRetries: 3 })}
      />
    );
    expect(screen.queryByTestId('kb-card-status-row-retry')).not.toBeInTheDocument();
  });

  it('hides retry button when not failed', () => {
    render(
      <KbCardStatusRow
        document={makeDoc({ processingState: 'Ready', retryCount: 0, maxRetries: 3 })}
      />
    );
    expect(screen.queryByTestId('kb-card-status-row-retry')).not.toBeInTheDocument();
  });

  it('calls onRetry with document id when clicked', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(
      <KbCardStatusRow
        document={makeDoc({
          id: 'doc-retry',
          processingState: 'Failed',
          retryCount: 0,
          maxRetries: 3,
        })}
        onRetry={onRetry}
      />
    );
    await user.click(screen.getByTestId('kb-card-status-row-retry'));
    expect(onRetry).toHaveBeenCalledOnce();
    expect(onRetry).toHaveBeenCalledWith('doc-retry');
  });

  it('disables retry button when isRetrying=true', () => {
    render(
      <KbCardStatusRow
        document={makeDoc({ processingState: 'Failed', retryCount: 0, maxRetries: 3 })}
        isRetrying={true}
      />
    );
    expect(screen.getByTestId('kb-card-status-row-retry')).toBeDisabled();
  });

  // --------------------------------------------------------------------------
  // Retry count display
  // --------------------------------------------------------------------------

  it('shows retry count when retryCount > 0', () => {
    render(
      <KbCardStatusRow
        document={makeDoc({ processingState: 'Failed', retryCount: 2, maxRetries: 3 })}
      />
    );
    expect(screen.getByTestId('kb-card-status-row-retry-count')).toHaveTextContent('Tentativo 2/3');
  });

  it('hides retry count when retryCount = 0', () => {
    render(<KbCardStatusRow document={makeDoc({ processingState: 'Pending', retryCount: 0 })} />);
    expect(screen.queryByTestId('kb-card-status-row-retry-count')).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Custom className
  // --------------------------------------------------------------------------

  it('applies custom className', () => {
    const { container } = render(
      <KbCardStatusRow document={makeDoc()} className="my-custom-class" />
    );
    expect(container.firstChild).toHaveClass('my-custom-class');
  });
});
