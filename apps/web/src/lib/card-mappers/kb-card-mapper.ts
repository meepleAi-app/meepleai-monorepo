import type { MeepleCardMetadata, MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

import { processingStateToLabel } from './shared-utils';

/**
 * Mappa un PdfDocumentDto alle props di MeepleCard.
 */
export function buildKbCardProps(document: PdfDocumentDto): Partial<MeepleCardProps> {
  const stateInfo = processingStateToLabel(document.processingState);
  const metadata: MeepleCardMetadata[] = [{ label: 'PDF' }];
  if (document.pageCount) metadata.push({ label: `${document.pageCount}p` });

  return {
    badge: stateInfo.text,
    metadata,
  };
}
