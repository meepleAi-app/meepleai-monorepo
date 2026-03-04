/**
 * KB Utilities - Shared formatting helpers for Knowledge Base displays
 * Extracted from meeple-info-card.tsx for reuse in KbDrawerSheet and GameBackContent.
 */

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
}

export const documentTypeLabels: Record<string, string> = {
  base: 'Regolamento',
  expansion: 'Espansione',
  errata: 'Errata',
  homerule: 'Regole Casa',
};

export const documentTypeColors: Record<string, string> = {
  base: 'bg-[hsla(25,95%,38%,0.1)] text-[hsl(25,95%,38%)]',
  expansion: 'bg-[hsla(168,76%,42%,0.1)] text-[hsl(168,76%,42%)]',
  errata: 'bg-[hsla(262,83%,62%,0.1)] text-[hsl(262,83%,62%)]',
  homerule: 'bg-[hsla(210,90%,50%,0.1)] text-[hsl(210,90%,50%)]',
};

export const statusIndicators: Record<string, { label: string; color: string; dot: string }> = {
  Pending: { label: 'In attesa', color: 'text-amber-600', dot: 'bg-amber-500' },
  pending: { label: 'In attesa', color: 'text-amber-600', dot: 'bg-amber-500' },
  Processing: { label: 'Elaborazione...', color: 'text-blue-600', dot: 'bg-blue-500' },
  processing: { label: 'Elaborazione...', color: 'text-blue-600', dot: 'bg-blue-500' },
  Ready: { label: 'Completato', color: 'text-green-600', dot: 'bg-green-500' },
  completed: { label: 'Completato', color: 'text-green-600', dot: 'bg-green-500' },
  Failed: { label: 'Errore', color: 'text-red-600', dot: 'bg-red-500' },
  failed: { label: 'Errore', color: 'text-red-600', dot: 'bg-red-500' },
};

export function getStatusIndicator(status: string) {
  // eslint-disable-next-line security/detect-object-injection -- status is from server enum
  return statusIndicators[status] ?? statusIndicators.Pending;
}

/**
 * Normalize document status from either processingState (PascalCase) or processingStatus (lowercase).
 * Returns a canonical status string usable with statusIndicators/getStatusIndicator.
 */
export function getDocumentStatus(doc: {
  processingState?: string;
  processingStatus?: string;
}): string {
  return doc.processingState || doc.processingStatus || 'Pending';
}

/**
 * Check if a document is ready/completed.
 */
export function isDocumentReady(doc: {
  processingState?: string;
  processingStatus?: string;
}): boolean {
  return doc.processingState === 'Ready' || doc.processingStatus === 'completed';
}
