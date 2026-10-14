/**
 * Tests for PdfList
 * Issue #4820: Step 2 Knowledge Base & PDF Management
 */

import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';
import { PdfList } from '../PdfList';

const mockDocs: PdfDocumentDto[] = [
  {
    id: 'doc-1',
    gameId: 'game-1',
    fileName: 'Catan-Regolamento.pdf',
    filePath: '/uploads/catan.pdf',
    fileSizeBytes: 2_500_000,
    processingState: 'Ready',
    uploadedAt: '2026-02-19T10:00:00Z',
    processedAt: '2026-02-19T10:05:00Z',
    pageCount: 16,
    documentType: 'base',
    isPublic: true,
  },
  {
    id: 'doc-2',
    gameId: 'game-1',
    fileName: 'Catan-Espansione-Marinai.pdf',
    filePath: '/uploads/catan-exp.pdf',
    fileSizeBytes: 800_000,
    processingState: 'Extracting',
    uploadedAt: '2026-02-19T10:10:00Z',
    processedAt: null,
    pageCount: null,
    documentType: 'expansion',
    isPublic: false,
  },
  {
    id: 'doc-3',
    gameId: 'game-1',
    fileName: 'Catan-Errata.pdf',
    filePath: '/uploads/catan-errata.pdf',
    fileSizeBytes: 150_000,
    processingState: 'Failed',
    uploadedAt: '2026-02-19T10:12:00Z',
    processedAt: null,
    pageCount: 2,
    documentType: 'errata',
    isPublic: false,
  },
];

describe('PdfList', () => {
  it('renders all documents', () => {
    render(<PdfList documents={mockDocs} />);

    expect(screen.getByTestId('pdf-list')).toBeInTheDocument();
    expect(screen.getByTestId('pdf-item-doc-1')).toBeInTheDocument();
    expect(screen.getByTestId('pdf-item-doc-2')).toBeInTheDocument();
    expect(screen.getByTestId('pdf-item-doc-3')).toBeInTheDocument();
  });

  it('displays file names', () => {
    render(<PdfList documents={mockDocs} />);

    expect(screen.getByText('Catan-Regolamento.pdf')).toBeInTheDocument();
    expect(screen.getByText('Catan-Espansione-Marinai.pdf')).toBeInTheDocument();
    expect(screen.getByText('Catan-Errata.pdf')).toBeInTheDocument();
  });

  it('displays page count when available', () => {
    render(<PdfList documents={mockDocs} />);

    expect(screen.getByText('16 pagine')).toBeInTheDocument();
    expect(screen.getByText('2 pagine')).toBeInTheDocument();
  });

  it('displays file size', () => {
    render(<PdfList documents={mockDocs} />);

    expect(screen.getByText('2.4 MB')).toBeInTheDocument();
  });

  it('shows status badges with correct labels', () => {
    render(<PdfList documents={mockDocs} />);

    expect(screen.getByText('Pronto')).toBeInTheDocument();
    expect(screen.getByText('Estrazione')).toBeInTheDocument();
    expect(screen.getByText('Errore')).toBeInTheDocument();
  });

  it('renders loading state', () => {
    render(<PdfList documents={[]} loading />);

    expect(screen.getByTestId('pdf-list-loading')).toBeInTheDocument();
    expect(screen.getByText('Caricamento documenti...')).toBeInTheDocument();
  });

  it('renders empty state', () => {
    render(<PdfList documents={[]} />);

    expect(screen.getByTestId('pdf-list-empty')).toBeInTheDocument();
    expect(screen.getByText('Nessun PDF disponibile per questo gioco.')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<PdfList documents={mockDocs} className="custom-class" />);

    expect(screen.getByTestId('pdf-list')).toHaveClass('custom-class');
  });

  // #4356: sono stati veri del backend. Il PDF appena caricato dal wizard nasce in `Uploading`, e
  // `Indexing` è l'ultima fase prima di `Ready`: finivano su «In attesa», con l'icona ferma.
  it('shows a document still uploading or indexing as in progress', () => {
    render(
      <PdfList
        documents={[
          { ...mockDocs[1], id: 'doc-up', processingState: 'Uploading' },
          { ...mockDocs[1], id: 'doc-ix', processingState: 'Indexing' },
        ]}
      />
    );

    const uploading = screen.getByTestId('pdf-item-doc-up');
    const indexing = screen.getByTestId('pdf-item-doc-ix');
    expect(within(uploading).getByText('Caricamento')).toBeInTheDocument();
    expect(within(indexing).getByText('Indicizzazione')).toBeInTheDocument();
    for (const item of [uploading, indexing]) {
      expect(item.querySelector('.animate-spin')).not.toBeNull();
    }
  });
});
