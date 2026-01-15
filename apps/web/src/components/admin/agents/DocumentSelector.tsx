/**
 * DocumentSelector Component - Issue #2399
 *
 * Multi-select component for choosing documents for agent knowledge base.
 * Fetches available documents from SharedGameCatalog and allows selection.
 */

'use client';

import * as React from 'react';

import { FileText, Search, Check, Loader2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import type { SharedGameDocument } from '@/lib/api/schemas/shared-games.schemas';
import { cn } from '@/lib/utils';

export interface DocumentSelectorProps {
  /** Currently selected document IDs */
  selectedIds: string[];
  /** Available documents to select from */
  availableDocuments: SharedGameDocument[];
  /** Callback when selection changes */
  onSelectionChange: (documentIds: string[]) => void;
  /** Whether the selector is in loading state */
  isLoading?: boolean;
  /** Whether the selector is disabled */
  disabled?: boolean;
  /** Maximum number of documents that can be selected */
  maxSelections?: number;
  /** Optional className for styling */
  className?: string;
}

const DOCUMENT_TYPE_LABELS: Record<number, { label: string; color: string }> = {
  0: { label: 'Rulebook', color: 'bg-blue-100 text-blue-800 border-blue-300' },
  1: { label: 'Errata', color: 'bg-orange-100 text-orange-800 border-orange-300' },
  2: { label: 'Homerule', color: 'bg-green-100 text-green-800 border-green-300' },
};

export function DocumentSelector({
  selectedIds,
  availableDocuments,
  onSelectionChange,
  isLoading = false,
  disabled = false,
  maxSelections = 50,
  className,
}: DocumentSelectorProps) {
  const [searchTerm, setSearchTerm] = React.useState('');

  // Filter documents based on search term
  const filteredDocuments = React.useMemo(() => {
    if (!searchTerm.trim()) return availableDocuments;

    const term = searchTerm.toLowerCase();
    return availableDocuments.filter(doc => {
      const typeLabel = DOCUMENT_TYPE_LABELS[doc.documentType]?.label.toLowerCase() ?? '';
      const tags = doc.tags?.join(' ').toLowerCase() ?? '';
      const version = doc.version.toLowerCase();

      return typeLabel.includes(term) || tags.includes(term) || version.includes(term);
    });
  }, [availableDocuments, searchTerm]);

  // Group documents by type
  const groupedDocuments = React.useMemo(() => {
    return filteredDocuments.reduce(
      (acc, doc) => {
        const type = doc.documentType;
        if (!acc[type]) acc[type] = [];
        acc[type].push(doc);
        return acc;
      },
      {} as Record<number, SharedGameDocument[]>
    );
  }, [filteredDocuments]);

  const handleToggleDocument = (documentId: string) => {
    if (disabled) return;

    const isSelected = selectedIds.includes(documentId);
    if (isSelected) {
      onSelectionChange(selectedIds.filter(id => id !== documentId));
    } else if (selectedIds.length < maxSelections) {
      onSelectionChange([...selectedIds, documentId]);
    }
  };

  const handleSelectAll = () => {
    if (disabled) return;
    const allIds = filteredDocuments.map(doc => doc.id).slice(0, maxSelections);
    onSelectionChange(allIds);
  };

  const handleClearAll = () => {
    if (disabled) return;
    onSelectionChange([]);
  };

  if (isLoading) {
    return (
      <Card className={className}>
        <CardContent className="py-12 flex flex-col items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground mb-4" />
          <p className="text-muted-foreground">Caricamento documenti...</p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={className}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <FileText className="h-5 w-5" />
              Seleziona Documenti
            </CardTitle>
            <CardDescription>
              {selectedIds.length} di {maxSelections} documenti selezionati
            </CardDescription>
          </div>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={handleSelectAll}
              disabled={disabled || filteredDocuments.length === 0}
            >
              Seleziona Tutti
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={handleClearAll}
              disabled={disabled || selectedIds.length === 0}
            >
              Deseleziona Tutti
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Search Input */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Cerca documenti per tipo, tag o versione..."
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
            className="pl-10"
            disabled={disabled}
          />
        </div>

        {/* Document List */}
        <ScrollArea className="h-[400px] rounded-md border p-4">
          {filteredDocuments.length === 0 ? (
            <div className="py-8 text-center text-muted-foreground">
              <FileText className="h-10 w-10 mx-auto mb-4 opacity-50" />
              <p>Nessun documento trovato</p>
            </div>
          ) : (
            <div className="space-y-6">
              {Object.entries(groupedDocuments).map(([type, docs]) => {
                const typeNum = parseInt(type);
                const typeConfig = DOCUMENT_TYPE_LABELS[typeNum];

                return (
                  <div key={type} className="space-y-3">
                    <div className="flex items-center gap-2">
                      <Badge variant="outline" className={typeConfig?.color}>
                        {typeConfig?.label ?? `Type ${type}`}
                      </Badge>
                      <span className="text-xs text-muted-foreground">
                        ({docs.length} {docs.length === 1 ? 'documento' : 'documenti'})
                      </span>
                    </div>

                    <div className="space-y-2 pl-2">
                      {docs.map(doc => {
                        const isSelected = selectedIds.includes(doc.id);

                        return (
                          <div
                            key={doc.id}
                            className={cn(
                              'flex items-center gap-3 p-3 rounded-lg border transition-colors cursor-pointer',
                              isSelected ? 'bg-primary/10 border-primary' : 'hover:bg-accent/50',
                              disabled && 'opacity-50 cursor-not-allowed'
                            )}
                            onClick={() => handleToggleDocument(doc.id)}
                            onKeyDown={e => {
                              if (e.key === 'Enter' || e.key === ' ') {
                                e.preventDefault();
                                handleToggleDocument(doc.id);
                              }
                            }}
                            role="checkbox"
                            aria-checked={isSelected}
                            tabIndex={disabled ? -1 : 0}
                          >
                            <Checkbox
                              checked={isSelected}
                              disabled={disabled}
                              className="pointer-events-none"
                            />

                            <div className="flex-1 min-w-0">
                              <div className="flex items-center gap-2 flex-wrap">
                                <Badge variant="secondary" className="text-xs">
                                  v{doc.version}
                                </Badge>
                                {doc.isActive && (
                                  <Badge variant="default" className="text-xs">
                                    Attivo
                                  </Badge>
                                )}
                                {doc.tags?.map(tag => (
                                  <Badge key={tag} variant="outline" className="text-xs">
                                    {tag}
                                  </Badge>
                                ))}
                              </div>
                              <p className="text-xs text-muted-foreground mt-1">
                                Creato: {new Date(doc.createdAt).toLocaleDateString('it-IT')}
                              </p>
                            </div>

                            {isSelected && <Check className="h-4 w-4 text-primary flex-shrink-0" />}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
