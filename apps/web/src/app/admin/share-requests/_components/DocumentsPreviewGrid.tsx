import { Checkbox } from '@/components/ui';
import { FileText, FileImage, File as FileIcon } from 'lucide-react';
import type { DocumentPreviewDto } from '@/lib/api/schemas/admin-share-requests.schemas';

/**
 * Documents Preview Grid Component
 *
 * Displays attached documents in a grid with optional selection checkboxes.
 *
 * Features:
 * - Document thumbnails (if available)
 * - File name, type, and size
 * - Optional checkbox selection
 * - Visual icons based on content type
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface DocumentsPreviewGridProps {
  documents: DocumentPreviewDto[];
  selectedIds?: string[];
  onSelectionChange?: (selectedIds: string[]) => void;
  selectable?: boolean;
  className?: string;
}

export function DocumentsPreviewGrid({
  documents,
  selectedIds = [],
  onSelectionChange,
  selectable = false,
  className,
}: DocumentsPreviewGridProps){
  const handleToggle = (documentId: string) => {
    if (!onSelectionChange) return;

    const isSelected = selectedIds.includes(documentId);
    if (isSelected) {
      onSelectionChange(selectedIds.filter((id) => id !== documentId));
    } else {
      onSelectionChange([...selectedIds, documentId]);
    }
  };

  const handleToggleAll = () => {
    if (!onSelectionChange) return;

    if (selectedIds.length === documents.length) {
      // All selected -> deselect all
      onSelectionChange([]);
    } else {
      // Some or none selected -> select all
      onSelectionChange(documents.map((doc) => doc.documentId));
    }
  };

  if (documents.length === 0) {
    return (
      <div className={`text-center py-8 text-muted-foreground ${className ?? ''}`}>
        <FileText className="h-12 w-12 mx-auto mb-2 opacity-50" />
        <p>No documents attached</p>
      </div>
    );
  }

  return (
    <div className={`space-y-3 ${className ?? ''}`}>
      {/* Select All Checkbox (only if selectable) */}
      {selectable && documents.length > 1 && (
        <div className="flex items-center gap-2 pb-2 border-b">
          <Checkbox
            id="select-all"
            checked={selectedIds.length === documents.length}
            onCheckedChange={handleToggleAll}
          />
          <label htmlFor="select-all" className="text-sm font-medium cursor-pointer">
            Select All ({selectedIds.length}/{documents.length})
          </label>
        </div>
      )}

      {/* Document Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        {documents.map((doc) => {
          const isSelected = selectedIds.includes(doc.documentId);
          const icon = getDocumentIcon(doc.contentType);

          return (
            <div
              key={doc.documentId}
              className={`flex items-start gap-3 p-3 rounded-lg border transition-colors ${
                selectable ? 'cursor-pointer hover:bg-accent' : ''
              } ${isSelected ? 'border-primary bg-accent' : ''}`}
              onClick={selectable ? () => handleToggle(doc.documentId) : undefined}
            >
              {selectable && (
                <Checkbox
                  checked={isSelected}
                  onCheckedChange={() => handleToggle(doc.documentId)}
                  className="mt-1"
                />
              )}

              {/* Preview or Icon */}
              <div className="flex-shrink-0">
                {doc.previewUrl ? (
                  <img
                    src={doc.previewUrl}
                    alt={doc.fileName}
                    className="h-16 w-16 rounded object-cover border"
                  />
                ) : (
                  <div className="h-16 w-16 rounded border bg-muted flex items-center justify-center">
                    {icon}
                  </div>
                )}
              </div>

              {/* File Info */}
              <div className="flex-1 min-w-0">
                <p className="font-medium text-sm truncate" title={doc.fileName}>
                  {doc.fileName}
                </p>
                <p className="text-xs text-muted-foreground">
                  {formatFileSize(doc.fileSize)}
                  {doc.pageCount && ` • ${doc.pageCount} pages`}
                </p>
                <p className="text-xs text-muted-foreground">{getContentTypeLabel(doc.contentType)}</p>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function getDocumentIcon(contentType: string) {
  if (contentType.startsWith('image/')) {
    return <FileImage className="h-8 w-8 text-muted-foreground" />;
  }
  if (contentType === 'application/pdf') {
    return <FileText className="h-8 w-8 text-red-500" />;
  }
  return <FileIcon className="h-8 w-8 text-muted-foreground" />;
}

function getContentTypeLabel(contentType: string): string {
  if (contentType.startsWith('image/')) {
    return contentType.replace('image/', '').toUpperCase() + ' Image';
  }
  if (contentType === 'application/pdf') {
    return 'PDF Document';
  }
  return contentType;
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
