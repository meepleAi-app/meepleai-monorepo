/**
 * DocumentSourceSelector - Select which documents to use as sources in chat
 *
 * Issue #2051: Multi-document source filtering
 *
 * Features:
 * - Multi-select dropdown for available PDF documents
 * - Default: "All documents" (null selection)
 * - Document type badges (base, expansion, errata, homerule)
 * - Compact design for chat input area
 * - Disabled when no game selected or loading
 */

import React from 'react';
import { ChevronDown, FileText, Check } from 'lucide-react';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';

export interface DocumentSource {
  id: string;
  fileName: string;
  documentType: 'base' | 'expansion' | 'errata' | 'homerule';
  uploadedAt: string;
  pageCount?: number | null;
}

interface DocumentSourceSelectorProps {
  /** Available documents for the current game */
  documents: DocumentSource[];
  /** Selected document IDs (null = all) */
  selectedIds: string[] | null;
  /** Callback when selection changes */
  onSelectionChange: (ids: string[] | null) => void;
  /** Disabled state */
  disabled?: boolean;
}

const DOCUMENT_TYPE_LABELS: Record<DocumentSource['documentType'], string> = {
  base: 'Base',
  expansion: 'Espansione',
  errata: 'Errata',
  homerule: 'Casa',
};

const DOCUMENT_TYPE_COLORS: Record<DocumentSource['documentType'], string> = {
  base: 'bg-blue-100 text-blue-800',
  expansion: 'bg-purple-100 text-purple-800',
  errata: 'bg-orange-100 text-orange-800',
  homerule: 'bg-green-100 text-green-800',
};

export function DocumentSourceSelector({
  documents,
  selectedIds,
  onSelectionChange,
  disabled = false,
}: DocumentSourceSelectorProps) {
  const allSelected = selectedIds === null;
  const selectedCount = selectedIds?.length ?? documents.length;

  const handleToggleAll = () => {
    onSelectionChange(allSelected ? [] : null);
  };

  const handleToggleDocument = (docId: string) => {
    if (allSelected) {
      // If "all" selected, clicking one document deselects all others except this one
      onSelectionChange([docId]);
    } else {
      const currentIds = selectedIds ?? [];
      const isSelected = currentIds.includes(docId);

      if (isSelected) {
        const newIds = currentIds.filter(id => id !== docId);
        // If deselecting last item, reset to "all"
        onSelectionChange(newIds.length === 0 ? null : newIds);
      } else {
        const newIds = [...currentIds, docId];
        // If all documents are now selected, reset to "all"
        onSelectionChange(newIds.length === documents.length ? null : newIds);
      }
    }
  };

  // Display label
  const getLabel = () => {
    if (documents.length === 0) return 'Nessun documento';
    if (allSelected) return 'Tutti i documenti';
    if (selectedCount === 0) return 'Seleziona documenti';
    if (selectedCount === 1) {
      const doc = documents.find(d => selectedIds?.includes(d.id));
      return doc ? doc.fileName : '1 documento';
    }
    return `${selectedCount} documenti`;
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          disabled={disabled || documents.length === 0}
          className="w-full justify-between text-sm h-9"
        >
          <span className="flex items-center gap-2">
            <FileText className="h-4 w-4" />
            {getLabel()}
          </span>
          <ChevronDown className="h-4 w-4 opacity-50" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-[300px]" align="start">
        <DropdownMenuLabel>Fonti documenti</DropdownMenuLabel>
        <DropdownMenuSeparator />

        {/* "All documents" option */}
        <DropdownMenuCheckboxItem checked={allSelected} onCheckedChange={handleToggleAll}>
          <div className="flex items-center gap-2">
            Tutti i documenti
            {allSelected && <Check className="h-4 w-4 ml-auto" />}
          </div>
        </DropdownMenuCheckboxItem>

        {documents.length > 0 && <DropdownMenuSeparator />}

        {/* Individual documents */}
        {documents.map(doc => {
          const isSelected = allSelected || selectedIds?.includes(doc.id) || false;

          return (
            <DropdownMenuCheckboxItem
              key={doc.id}
              checked={isSelected}
              onCheckedChange={() => handleToggleDocument(doc.id)}
            >
              <div className="flex flex-col gap-1 w-full">
                <div className="flex items-center gap-2">
                  <span className="text-sm truncate flex-1">{doc.fileName}</span>
                  {isSelected && <Check className="h-4 w-4 text-primary" />}
                </div>
                <div className="flex items-center gap-2">
                  <Badge
                    variant="secondary"
                    className={`text-xs ${DOCUMENT_TYPE_COLORS[doc.documentType]}`}
                  >
                    {DOCUMENT_TYPE_LABELS[doc.documentType]}
                  </Badge>
                  {doc.pageCount && (
                    <span className="text-xs text-muted-foreground">{doc.pageCount} pagine</span>
                  )}
                </div>
              </div>
            </DropdownMenuCheckboxItem>
          );
        })}

        {documents.length === 0 && (
          <div className="px-2 py-4 text-sm text-muted-foreground text-center">
            Nessun documento disponibile per questo gioco
          </div>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
