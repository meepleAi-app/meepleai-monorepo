/**
 * MeepleKbCard - KB Document adapter using MeepleCard
 * Issue #5001 — KB Card: azioni contestuali e visibilità condizionale
 *
 * Adapter component that wraps MeepleCard for KB document display.
 * Implements the action visibility matrix from Issue #5001:
 *
 * | Action           | Visible when          | Enabled when             |
 * |------------------|-----------------------|--------------------------|
 * | Info             | always                | always                   |
 * | Chat con doc     | kbStatus = indexed    | always                   |
 * | Re-indicizza     | isAdmin               | kbStatus ≠ processing    |
 * | Scarica          | isEditor || isAdmin   | always                   |
 * | Elimina          | isAdmin               | always                   |
 *
 * @example
 * ```tsx
 * <MeepleKbCard
 *   document={pdfDocumentDto}
 *   isAdmin={hasRole(currentUser, 'Admin')}
 *   isEditor={hasRole(currentUser, 'Editor')}
 *   onReindex={handleReindex}
 *   onDownload={handleDownload}
 *   onDelete={handleDelete}
 * />
 * ```
 */

'use client';

import {
  MeepleCard,
  MeepleCardSkeleton,
  type MeepleCardAction,
  type MeepleCardVariant,
} from '@/components/ui/data-display/meeple-card';
import { buildKbCardProps } from '@/lib/card-mappers';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

// ============================================================================
// Types
// ============================================================================

export interface MeepleKbCardProps {
  /** PDF document DTO from API */
  document: PdfDocumentDto;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Whether the current user is an admin */
  isAdmin?: boolean;
  /** Whether the current user is an editor (or admin) */
  isEditor?: boolean;
  /** Re-index callback (admin only) */
  onReindex?: (documentId: string) => void;
  /** Download callback (editor/admin) */
  onDownload?: (documentId: string) => void;
  /** Delete callback (admin only) */
  onDelete?: (documentId: string, fileName: string) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

function mapDocumentStatus(processingStatus: string): 'indexed' | 'processing' | 'failed' | 'none' {
  switch (processingStatus) {
    case 'Completed':
    case 'Ready':
      return 'indexed';
    case 'Failed':
      return 'failed';
    case 'Uploading':
    case 'Extracting':
    case 'Chunking':
    case 'Embedding':
    case 'Indexing':
      return 'processing';
    default:
      return 'none';
  }
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleKbCard({
  document,
  variant = 'grid',
  isAdmin = false,
  isEditor = false,
  onReindex,
  onDownload,
  onDelete,
  className,
}: MeepleKbCardProps) {
  const isEditorOrAdmin = isEditor || isAdmin;
  const documentStatus = mapDocumentStatus(document.processingStatus);
  const isProcessing = documentStatus === 'processing';
  const isIndexed = documentStatus === 'indexed';
  const mapperProps = buildKbCardProps(document);

  // Quick Actions Configuration
  const actions: MeepleCardAction[] = [
    // Chat con documento: visible only if indexed
    ...(isIndexed
      ? [
          {
            icon: '💬',
            label: 'Chat con documento',
            onClick: () => {
              window.location.href = `/chat/new?documentId=${document.id}`;
            },
          } satisfies MeepleCardAction,
        ]
      : []),
    // Re-indicizza: visible only for admin, disabled if currently processing
    ...(isAdmin
      ? [
          {
            icon: '🔄',
            label: isProcessing ? 'Indicizzazione già in corso' : 'Re-indicizza',
            onClick: () => onReindex?.(document.id),
            disabled: isProcessing,
          } satisfies MeepleCardAction,
        ]
      : []),
    // Scarica: visible for editor/admin
    ...(isEditorOrAdmin
      ? [
          {
            icon: '⬇️',
            label: 'Scarica',
            onClick: () => onDownload?.(document.id),
          } satisfies MeepleCardAction,
        ]
      : []),
    // Elimina: visible only for admin
    ...(isAdmin
      ? [
          {
            icon: '🗑️',
            label: 'Elimina',
            onClick: () => onDelete?.(document.id, document.fileName),
            variant: 'danger' as const,
          } satisfies MeepleCardAction,
        ]
      : []),
  ];

  const subtitle = document.documentType
    ? `${document.documentType.charAt(0).toUpperCase()}${document.documentType.slice(1)} — ${formatFileSize(document.fileSizeBytes)}`
    : formatFileSize(document.fileSizeBytes);

  return (
    <MeepleCard
      entity="kb"
      variant={variant}
      title={document.fileName}
      subtitle={subtitle}
      badge={mapperProps.badge}
      metadata={mapperProps.metadata}
      className={className}
      onClick={() => (window.location.href = `/documents/${document.id}`)}
      actions={actions.length > 0 ? actions : undefined}
      data-testid={`kb-card-${document.id}`}
    />
  );
}

/**
 * MeepleKbCard Skeleton for loading state
 */
export function MeepleKbCardSkeleton({ variant = 'grid' }: { variant?: MeepleCardVariant }) {
  return <MeepleCardSkeleton variant={variant} data-testid="kb-card-skeleton" />;
}

export default MeepleKbCard;
