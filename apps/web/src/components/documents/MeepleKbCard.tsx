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

import { MessageCircle, RefreshCw, Download, Trash2 } from 'lucide-react';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import type { KbIndexingStatus } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
import { getNavigationLinks } from '@/config/entity-navigation';
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

/**
 * Map PdfDocumentDto.processingStatus string to KbIndexingStatus.
 */
function mapDocumentStatus(processingStatus: string): KbIndexingStatus {
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

  // ============================================================================
  // Quick Actions Configuration
  // ============================================================================

  const entityQuickActions = [
    // Chat con documento: visible only if indexed
    {
      icon: MessageCircle,
      label: 'Chat con documento',
      onClick: () => {
        window.location.href = `/chat/new?documentId=${document.id}`;
      },
      hidden: !isIndexed,
    },
    // Re-indicizza: visible only for admin, disabled if currently processing
    {
      icon: RefreshCw,
      label: isProcessing ? 'Indicizzazione già in corso' : 'Re-indicizza',
      onClick: () => onReindex?.(document.id),
      hidden: !isAdmin,
      disabled: isProcessing,
    },
    // Scarica: visible for editor/admin
    {
      icon: Download,
      label: 'Scarica',
      onClick: () => onDownload?.(document.id),
      hidden: !isEditorOrAdmin,
    },
    // Elimina: visible only for admin
    {
      icon: Trash2,
      label: 'Elimina',
      onClick: () => onDelete?.(document.id, document.fileName),
      hidden: !isAdmin,
    },
  ];

  // ============================================================================
  // Card Data
  // ============================================================================

  const subtitle = document.documentType
    ? `${document.documentType.charAt(0).toUpperCase()}${document.documentType.slice(1)} — ${formatFileSize(document.fileSizeBytes)}`
    : formatFileSize(document.fileSizeBytes);

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <MeepleCard
      id={document.id}
      entity="kb"
      variant={variant}
      title={document.fileName}
      subtitle={subtitle}
      documentStatus={documentStatus}
      className={className}
      onClick={() => window.location.href = `/documents/${document.id}`}
      // Issue #5001: Quick actions with conditional visibility
      entityQuickActions={entityQuickActions}
      showInfoButton
      infoHref={`/documents/${document.id}`}
      infoTooltip="Vai al dettaglio"
      // Navigation footer: Game + Agent links
      navigateTo={getNavigationLinks('kb', {
        id: document.id,
        gameId: document.gameId,
      })}
      data-testid={`kb-card-${document.id}`}
    />
  );
}

/**
 * MeepleKbCard Skeleton for loading state
 */
export function MeepleKbCardSkeleton({
  variant = 'grid',
}: {
  variant?: MeepleCardVariant;
}) {
  return (
    <MeepleCard
      entity="kb"
      variant={variant}
      title=""
      loading
      data-testid="kb-card-skeleton"
    />
  );
}

// ============================================================================
// Helpers
// ============================================================================

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default MeepleKbCard;
