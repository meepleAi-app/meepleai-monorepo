import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

// ============================================================================
// PDF Document Helpers (Issue #5195)
// ============================================================================

/** Map PdfProcessingState string → KbDocumentPreview status */
export function mapProcessingStateToStatus(
  state: string
): 'processing' | 'indexed' | 'failed' | 'none' {
  switch (state) {
    case 'Ready':
      return 'indexed';
    case 'Failed':
      return 'failed';
    case 'Extracting':
    case 'Chunking':
    case 'Embedding':
    case 'Indexing':
    case 'Uploading':
      return 'processing';
    default:
      return 'none';
  }
}

/** Map raw JSON object from /api/v1/games/{id}/pdfs → PdfDocumentDto */
export function mapRawToPdfDocumentDto(raw: Record<string, unknown>): PdfDocumentDto {
  return {
    id: String(raw.id ?? ''),
    gameId: String(raw.gameId ?? ''),
    fileName: String(raw.fileName ?? raw.name ?? ''),
    filePath: String(raw.filePath ?? ''),
    fileSizeBytes: Number(raw.fileSizeBytes ?? 0),
    processingStatus: String(raw.processingStatus ?? 'Pending'),
    uploadedAt: String(raw.uploadedAt ?? new Date().toISOString()),
    processedAt: raw.processedAt != null ? String(raw.processedAt) : null,
    pageCount: raw.pageCount != null ? Number(raw.pageCount) : null,
    documentType: (raw.documentType as 'base' | 'expansion' | 'errata' | 'homerule') ?? 'base',
    isPublic: Boolean(raw.isPublic ?? false),
    processingState: String(raw.processingState ?? 'Pending'),
    progressPercentage: Number(raw.progressPercentage ?? 0),
    retryCount: Number(raw.retryCount ?? 0),
    maxRetries: Number(raw.maxRetries ?? 3),
    // Issue #5183: retry eligibility + error categorization
    canRetry: Boolean(raw.canRetry ?? false),
    errorCategory: raw.errorCategory != null ? String(raw.errorCategory) : null,
    processingError: raw.processingError != null ? String(raw.processingError) : null,
    documentCategory:
      (raw.documentCategory as
        | 'Rulebook'
        | 'Expansion'
        | 'Errata'
        | 'QuickStart'
        | 'Reference'
        | 'PlayerAid'
        | 'Other') ?? 'Rulebook',
    baseDocumentId: raw.baseDocumentId != null ? String(raw.baseDocumentId) : null,
    isActiveForRag: Boolean(raw.isActiveForRag ?? true),
    hasAcceptedDisclaimer: Boolean(raw.hasAcceptedDisclaimer ?? false),
    versionLabel: raw.versionLabel != null ? String(raw.versionLabel) : null,
  };
}
