import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

import { processingStateToLabel } from './shared-utils';

/**
 * Mappa un PdfDocumentDto alle props SymbolStrip di MeepleCard.
 */
export function buildKbCardProps(document: PdfDocumentDto): Partial<MeepleCardProps> {
  return {
    pageCount: document.pageCount ?? undefined,
    identityChip1: 'PDF',
    stateLabel: processingStateToLabel(document.processingState),
  };
}
