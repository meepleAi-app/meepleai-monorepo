import { describe, it, expect } from 'vitest';
import { buildKbCardProps } from '../kb-card-mapper';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

const baseDoc: PdfDocumentDto = {
  id: 'doc-1',
  gameId: 'game-1',
  fileName: 'regolamento.pdf',
  filePath: '/uploads/regolamento.pdf',
  fileSizeBytes: 1024000,
  processingStatus: 'Completed',
  uploadedAt: '2024-01-01T00:00:00Z',
  processedAt: '2024-01-01T01:00:00Z',
  pageCount: 42,
  documentType: 'base',
  isPublic: false,
  processingState: 'Completed',
  progressPercentage: 100,
  retryCount: 0,
  maxRetries: 3,
  canRetry: false,
  errorCategory: null,
  processingError: null,
  documentCategory: 'Rulebook',
  baseDocumentId: null,
  isActiveForRag: true,
  hasAcceptedDisclaimer: false,
  versionLabel: null,
};

describe('buildKbCardProps', () => {
  it('passes pageCount to props', () => {
    const props = buildKbCardProps(baseDoc);
    expect(props.pageCount).toBe(42);
  });

  it('passes undefined when pageCount is null', () => {
    const props = buildKbCardProps({ ...baseDoc, pageCount: null });
    expect(props.pageCount).toBeUndefined();
  });

  it('sets identityChip1 to PDF', () => {
    const props = buildKbCardProps(baseDoc);
    expect(props.identityChip1).toBe('PDF');
  });

  it('builds stateLabel for Completed state', () => {
    const props = buildKbCardProps(baseDoc);
    expect(props.stateLabel).toEqual({ text: 'Indicizzato', variant: 'success' });
  });

  it('builds stateLabel for Failed state', () => {
    const props = buildKbCardProps({ ...baseDoc, processingState: 'Failed' });
    expect(props.stateLabel).toEqual({ text: 'Errore', variant: 'error' });
  });

  it('builds stateLabel for Indexing state', () => {
    const props = buildKbCardProps({ ...baseDoc, processingState: 'Indexing' });
    expect(props.stateLabel).toEqual({ text: 'In Elaborazione', variant: 'warning' });
  });

  it('builds stateLabel for Pending state', () => {
    const props = buildKbCardProps({ ...baseDoc, processingState: 'Pending' });
    expect(props.stateLabel).toEqual({ text: 'In Attesa', variant: 'info' });
  });
});
