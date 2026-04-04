/**
 * MeepleKbCard Tests
 * Issue #5001 — KB Card: azioni contestuali e visibilità condizionale
 *
 * Tests the action visibility matrix:
 * - Info: always visible
 * - Chat con documento: visible only if kbStatus = indexed
 * - Re-indicizza: visible only for admin, disabled if processing
 * - Scarica: visible only for editor/admin
 * - Elimina: visible only for admin
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { MeepleKbCard, MeepleKbCardSkeleton } from '../MeepleKbCard';

import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

// ============================================================================
// Mock Data
// ============================================================================

const mockIndexedDocument: PdfDocumentDto = {
  id: 'doc-123',
  gameId: 'game-456',
  fileName: 'Twilight Imperium Rulebook.pdf',
  filePath: '/uploads/ti4.pdf',
  fileSizeBytes: 2048000,
  processingStatus: 'Completed',
  uploadedAt: '2024-01-01T00:00:00Z',
  processedAt: '2024-01-01T01:00:00Z',
  pageCount: 84,
  documentType: 'base',
  isPublic: true,
};

const mockProcessingDocument: PdfDocumentDto = {
  ...mockIndexedDocument,
  id: 'doc-789',
  fileName: 'Expansion Rules.pdf',
  processingStatus: 'Extracting',
  processedAt: null,
};

const mockFailedDocument: PdfDocumentDto = {
  ...mockIndexedDocument,
  id: 'doc-fail',
  fileName: 'Failed Document.pdf',
  processingStatus: 'Failed',
  processedAt: null,
};

const mockNoneDocument: PdfDocumentDto = {
  ...mockIndexedDocument,
  id: 'doc-none',
  fileName: 'Unprocessed.pdf',
  processingStatus: 'Pending',
  processedAt: null,
};

// ============================================================================
// Tests
// ============================================================================

describe('MeepleKbCard', () => {
  // --------------------------------------------------------------------------
  // Basic rendering
  // --------------------------------------------------------------------------

  it('renders document file name as title', () => {
    render(<MeepleKbCard document={mockIndexedDocument} />);
    expect(screen.getByText('Twilight Imperium Rulebook.pdf')).toBeInTheDocument();
  });

  it('renders with correct data-testid', () => {
    render(<MeepleKbCard document={mockIndexedDocument} />);
    expect(screen.getByTestId('kb-card-doc-123')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // AC: DocumentStatusBadge
  // --------------------------------------------------------------------------

  it('renders indexed status badge for completed document', () => {
    render(<MeepleKbCard document={mockIndexedDocument} />);
    expect(screen.getByTestId('kb-status-indexed')).toBeInTheDocument();
  });

  it('renders processing status badge for in-progress document', () => {
    render(<MeepleKbCard document={mockProcessingDocument} />);
    expect(screen.getByTestId('kb-status-processing')).toBeInTheDocument();
  });

  it('renders failed status badge for failed document', () => {
    render(<MeepleKbCard document={mockFailedDocument} />);
    expect(screen.getByTestId('kb-status-failed')).toBeInTheDocument();
  });

  it('renders none status badge for pending document', () => {
    render(<MeepleKbCard document={mockNoneDocument} />);
    expect(screen.getByTestId('kb-status-none')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // AC: Chat con documento — visible only if indexed
  // --------------------------------------------------------------------------

  it('Chat action is visible for indexed document', () => {
    render(<MeepleKbCard document={mockIndexedDocument} />);
    expect(screen.getByRole('button', { name: 'Chat con documento' })).toBeInTheDocument();
  });

  it('Chat action is not visible for processing document', () => {
    render(<MeepleKbCard document={mockProcessingDocument} />);
    expect(screen.queryByRole('button', { name: 'Chat con documento' })).not.toBeInTheDocument();
  });

  it('Chat action is not visible for failed document', () => {
    render(<MeepleKbCard document={mockFailedDocument} />);
    expect(screen.queryByRole('button', { name: 'Chat con documento' })).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // AC: Re-indicizza — visible only for admin, disabled if processing
  // --------------------------------------------------------------------------

  it('Re-indicizza not shown for non-admin user', () => {
    render(<MeepleKbCard document={mockIndexedDocument} isAdmin={false} />);
    expect(screen.queryByRole('button', { name: 'Re-indicizza' })).not.toBeInTheDocument();
  });

  it('Re-indicizza shown for admin on indexed document', () => {
    render(<MeepleKbCard document={mockIndexedDocument} isAdmin={true} />);
    expect(screen.getByRole('button', { name: 'Re-indicizza' })).toBeInTheDocument();
  });

  it('Re-indicizza is disabled for admin on processing document with tooltip label', () => {
    render(<MeepleKbCard document={mockProcessingDocument} isAdmin={true} />);
    const button = screen.getByRole('button', { name: 'Indicizzazione già in corso' });
    expect(button).toBeInTheDocument();
    expect(button).toHaveAttribute('aria-disabled', 'true');
  });

  it('Re-indicizza calls onReindex with document id when clicked', async () => {
    const user = userEvent.setup();
    const onReindex = vi.fn();

    render(
      <MeepleKbCard
        document={mockIndexedDocument}
        isAdmin={true}
        onReindex={onReindex}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Re-indicizza' }));
    expect(onReindex).toHaveBeenCalledWith('doc-123');
  });

  // --------------------------------------------------------------------------
  // AC: Scarica — visible only for editor/admin
  // --------------------------------------------------------------------------

  it('Scarica not shown for non-editor non-admin user', () => {
    render(<MeepleKbCard document={mockIndexedDocument} isAdmin={false} isEditor={false} />);
    expect(screen.queryByRole('button', { name: 'Scarica' })).not.toBeInTheDocument();
  });

  it('Scarica shown for editor', () => {
    render(<MeepleKbCard document={mockIndexedDocument} isEditor={true} isAdmin={false} />);
    expect(screen.getByRole('button', { name: 'Scarica' })).toBeInTheDocument();
  });

  it('Scarica shown for admin', () => {
    render(<MeepleKbCard document={mockIndexedDocument} isAdmin={true} isEditor={false} />);
    expect(screen.getByRole('button', { name: 'Scarica' })).toBeInTheDocument();
  });

  it('Scarica calls onDownload with document id when clicked', async () => {
    const user = userEvent.setup();
    const onDownload = vi.fn();

    render(
      <MeepleKbCard
        document={mockIndexedDocument}
        isAdmin={true}
        onDownload={onDownload}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Scarica' }));
    expect(onDownload).toHaveBeenCalledWith('doc-123');
  });

  // --------------------------------------------------------------------------
  // AC: Elimina — visible only for admin
  // --------------------------------------------------------------------------

  it('Elimina not shown for non-admin user', () => {
    render(<MeepleKbCard document={mockIndexedDocument} isAdmin={false} />);
    expect(screen.queryByRole('button', { name: 'Elimina' })).not.toBeInTheDocument();
  });

  it('Elimina shown for admin', () => {
    render(<MeepleKbCard document={mockIndexedDocument} isAdmin={true} />);
    expect(screen.getByRole('button', { name: 'Elimina' })).toBeInTheDocument();
  });

  it('Elimina calls onDelete with document id and file name when clicked', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();

    render(
      <MeepleKbCard
        document={mockIndexedDocument}
        isAdmin={true}
        onDelete={onDelete}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Elimina' }));
    expect(onDelete).toHaveBeenCalledWith('doc-123', 'Twilight Imperium Rulebook.pdf');
  });

  // --------------------------------------------------------------------------
  // AC: No Configura/Edit action
  // --------------------------------------------------------------------------

  it('does not render Configura action', () => {
    render(<MeepleKbCard document={mockIndexedDocument} isAdmin={true} />);
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Unauthenticated / no-role user
  // --------------------------------------------------------------------------

  it('unauthenticated user sees no admin/editor actions', () => {
    render(<MeepleKbCard document={mockIndexedDocument} />);
    expect(screen.queryByRole('button', { name: 'Re-indicizza' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Scarica' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Elimina' })).not.toBeInTheDocument();
  });

  it('unauthenticated user can still see Chat for indexed document', () => {
    render(<MeepleKbCard document={mockIndexedDocument} />);
    expect(screen.getByRole('button', { name: 'Chat con documento' })).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Skeleton
  // --------------------------------------------------------------------------

  it('renders skeleton with correct testid', () => {
    render(<MeepleKbCardSkeleton />);
    // MeepleCard in loading mode hardcodes data-testid="meeple-card-skeleton"
    expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
  });
});
