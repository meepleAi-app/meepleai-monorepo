/**
 * DocumentPicker Component - Issue #2415
 *
 * Multi-select document picker with search, pagination, and hover preview.
 * Used in Knowledge Base for selecting documents to add to the collection.
 */

'use client';

import * as React from 'react';
import { useMemo, useState } from 'react';

import { FileText, Loader2, Search } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { HoverCard, HoverCardContent, HoverCardTrigger } from '@/components/ui/overlays/hover-card';
import { Input } from '@/components/ui/primitives/input';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { cn } from '@/lib/utils';

// ========== Types ==========

export interface DocumentMetadata {
  id: string;
  title: string;
  documentType: 'Rulebook' | 'Errata' | 'Homerule';
  version: string;
  pageCount?: number;
  fileSize?: string;
  uploadedAt: string;
  uploadedBy: string;
  tags: string[];
  gameName?: string;
  isActive?: boolean;
}

export interface DocumentPickerProps {
  /** Currently selected document IDs */
  selectedIds: string[];
  /** Available documents to select from */
  availableDocuments: DocumentMetadata[];
  /** Callback when selection changes */
  onSelectionChange: (documentIds: string[]) => void;
  /** Whether the picker is in loading state */
  isLoading?: boolean;
  /** Whether the picker is disabled */
  disabled?: boolean;
  /** Maximum number of documents that can be selected */
  maxSelections?: number;
  /** Number of documents per page */
  pageSize?: number;
  /** Optional className for styling */
  className?: string;
}

// ========== Constants ==========

const DOCUMENT_TYPE_COLORS: Record<string, string> = {
  Rulebook: 'bg-blue-100 text-blue-800 border-blue-300',
  Errata: 'bg-orange-100 text-orange-800 border-orange-300',
  Homerule: 'bg-green-100 text-green-800 border-green-300',
};

const DEFAULT_PAGE_SIZE = 20;

// ========== Main Component ==========

export function DocumentPicker({
  selectedIds,
  availableDocuments,
  onSelectionChange,
  isLoading = false,
  disabled = false,
  maxSelections,
  pageSize = DEFAULT_PAGE_SIZE,
  className,
}: DocumentPickerProps): React.JSX.Element {
  const [searchTerm, setSearchTerm] = useState('');
  const [currentPage, setCurrentPage] = useState(1);

  // Filter documents by search term
  const filteredDocuments = useMemo(() => {
    if (!searchTerm) return availableDocuments;

    const lowerSearch = searchTerm.toLowerCase();
    return availableDocuments.filter(
      doc =>
        doc.title.toLowerCase().includes(lowerSearch) ||
        doc.gameName?.toLowerCase().includes(lowerSearch) ||
        doc.tags.some(tag => tag.toLowerCase().includes(lowerSearch))
    );
  }, [availableDocuments, searchTerm]);

  // Paginate filtered documents
  const totalPages = Math.ceil(filteredDocuments.length / pageSize);
  const paginatedDocuments = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    const end = start + pageSize;
    return filteredDocuments.slice(start, end);
  }, [filteredDocuments, currentPage, pageSize]);

  // Reset to page 1 when search changes
  React.useEffect(() => {
    setCurrentPage(1);
  }, [searchTerm]);

  // ========== Handlers ==========

  const handleSelectAll = () => {
    const allIds = filteredDocuments.map(doc => doc.id);
    if (maxSelections && allIds.length > maxSelections) {
      onSelectionChange(allIds.slice(0, maxSelections));
    } else {
      onSelectionChange(allIds);
    }
  };

  const handleClearAll = () => {
    onSelectionChange([]);
  };

  const handleToggleDocument = (documentId: string) => {
    const isSelected = selectedIds.includes(documentId);

    if (isSelected) {
      onSelectionChange(selectedIds.filter(id => id !== documentId));
    } else {
      if (maxSelections && selectedIds.length >= maxSelections) {
        return; // Cannot select more
      }
      onSelectionChange([...selectedIds, documentId]);
    }
  };

  const handlePageChange = (page: number) => {
    setCurrentPage(page);
  };

  // ========== Computed Values ==========

  const selectedCount = selectedIds.length;
  const canSelectMore = !maxSelections || selectedCount < maxSelections;

  // ========== Render ==========

  return (
    <Card className={cn('w-full', className)}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <FileText className="h-5 w-5" />
              Select Documents
            </CardTitle>
            <CardDescription>
              {maxSelections
                ? `Select up to ${maxSelections} documents (${selectedCount} selected)`
                : `${selectedCount} documents selected`}
            </CardDescription>
          </div>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleSelectAll}
              disabled={disabled || isLoading || !canSelectMore}
            >
              Select All
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={handleClearAll}
              disabled={disabled || isLoading || selectedCount === 0}
            >
              Clear All
            </Button>
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Search Input */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            type="text"
            placeholder="Search by title, game, or tag..."
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
            disabled={disabled || isLoading}
            className="pl-10"
          />
        </div>

        {/* Document List */}
        {isLoading ? (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        ) : paginatedDocuments.length === 0 ? (
          <div className="py-8 text-center text-muted-foreground">
            {searchTerm ? 'No documents match your search.' : 'No documents available.'}
          </div>
        ) : (
          <>
            <ScrollArea className="h-[400px] pr-4">
              <div className="space-y-2">
                {paginatedDocuments.map(document => (
                  <DocumentListItem
                    key={document.id}
                    document={document}
                    isSelected={selectedIds.includes(document.id)}
                    onToggle={handleToggleDocument}
                    disabled={disabled || (!selectedIds.includes(document.id) && !canSelectMore)}
                  />
                ))}
              </div>
            </ScrollArea>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between border-t pt-4">
                <div className="text-sm text-muted-foreground">
                  Page {currentPage} of {totalPages}
                </div>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handlePageChange(currentPage - 1)}
                    disabled={currentPage === 1 || disabled}
                  >
                    Previous
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handlePageChange(currentPage + 1)}
                    disabled={currentPage === totalPages || disabled}
                  >
                    Next
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

// ========== Document List Item with Hover Preview ==========

interface DocumentListItemProps {
  document: DocumentMetadata;
  isSelected: boolean;
  onToggle: (id: string) => void;
  disabled?: boolean;
}

function DocumentListItem({
  document,
  isSelected,
  onToggle,
  disabled = false,
}: DocumentListItemProps): React.JSX.Element {
  return (
    <HoverCard openDelay={300}>
      <HoverCardTrigger asChild>
        <div
          role="checkbox"
          aria-checked={isSelected}
          tabIndex={disabled ? -1 : 0}
          className={cn(
            'flex items-start gap-3 rounded-lg border p-3 transition-colors',
            'hover:bg-accent',
            isSelected && 'border-primary bg-primary/5',
            disabled && 'cursor-not-allowed opacity-50',
            !disabled && 'cursor-pointer'
          )}
          onClick={() => !disabled && onToggle(document.id)}
          onKeyDown={e => {
            if (!disabled && (e.key === 'Enter' || e.key === ' ')) {
              e.preventDefault();
              onToggle(document.id);
            }
          }}
        >
          <Checkbox
            checked={isSelected}
            disabled={disabled}
            className="mt-0.5"
            onClick={e => e.stopPropagation()}
          />

          <div className="flex-1 space-y-1">
            <div className="flex items-center gap-2">
              <span className="font-medium">{document.title}</span>
              <Badge
                variant="outline"
                className={cn('text-xs', DOCUMENT_TYPE_COLORS[document.documentType])}
              >
                {document.documentType}
              </Badge>
              {document.isActive === false && (
                <Badge variant="outline" className="text-xs text-muted-foreground">
                  Inactive
                </Badge>
              )}
            </div>

            {document.gameName && (
              <div className="text-sm text-muted-foreground">{document.gameName}</div>
            )}

            {document.tags.length > 0 && (
              <div className="flex flex-wrap gap-1">
                {document.tags.slice(0, 3).map(tag => (
                  <Badge key={tag} variant="secondary" className="text-xs">
                    {tag}
                  </Badge>
                ))}
                {document.tags.length > 3 && (
                  <Badge variant="secondary" className="text-xs">
                    +{document.tags.length - 3} more
                  </Badge>
                )}
              </div>
            )}
          </div>

          <FileText className="h-5 w-5 text-muted-foreground" />
        </div>
      </HoverCardTrigger>

      {/* Hover Preview Card */}
      <HoverCardContent className="w-80">
        <div className="space-y-3">
          <div>
            <h4 className="font-semibold">{document.title}</h4>
            {document.gameName && (
              <p className="text-sm text-muted-foreground">{document.gameName}</p>
            )}
          </div>

          <div className="grid grid-cols-2 gap-2 text-sm">
            <div>
              <span className="font-medium">Type:</span>{' '}
              <span className="text-muted-foreground">{document.documentType}</span>
            </div>
            <div>
              <span className="font-medium">Version:</span>{' '}
              <span className="text-muted-foreground">{document.version}</span>
            </div>
            {document.pageCount && (
              <div>
                <span className="font-medium">Pages:</span>{' '}
                <span className="text-muted-foreground">{document.pageCount}</span>
              </div>
            )}
            {document.fileSize && (
              <div>
                <span className="font-medium">Size:</span>{' '}
                <span className="text-muted-foreground">{document.fileSize}</span>
              </div>
            )}
          </div>

          <div className="text-xs text-muted-foreground">
            Uploaded by {document.uploadedBy} on{' '}
            {new Date(document.uploadedAt).toLocaleDateString()}
          </div>

          {document.tags.length > 0 && (
            <div>
              <div className="mb-1 text-xs font-medium">Tags:</div>
              <div className="flex flex-wrap gap-1">
                {document.tags.map(tag => (
                  <Badge key={tag} variant="secondary" className="text-xs">
                    {tag}
                  </Badge>
                ))}
              </div>
            </div>
          )}
        </div>
      </HoverCardContent>
    </HoverCard>
  );
}
