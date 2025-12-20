/**
 * Collection Source Filter Component
 *
 * Multi-select filter for choosing which document collections to query in chat.
 * Shows document type badges and allows filtering by specific documents.
 * Defaults to "All documents" when none selected.
 *
 * Issue #2051: Multi-document upload frontend components
 */

'use client';

import * as React from 'react';

import { Check } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { cn } from '@/lib/utils';

import { DocumentBadge, type DocumentType } from './DocumentBadge';

export interface DocumentCollection {
  id: string;
  name: string;
  documentType: DocumentType;
  documentCount: number;
}

export interface CollectionSourceFilterProps {
  collections: DocumentCollection[];
  selectedDocIds: string[];
  onChange: (selectedIds: string[]) => void;
  className?: string;
}

export function CollectionSourceFilter({
  collections,
  selectedDocIds,
  onChange,
  className,
}: CollectionSourceFilterProps) {
  const allSelected = selectedDocIds.length === 0 || selectedDocIds.length === collections.length;
  const _someSelected = selectedDocIds.length > 0 && selectedDocIds.length < collections.length;

  const handleToggleAll = React.useCallback(() => {
    if (allSelected) {
      // Deselect all (will show "All documents")
      onChange([]);
    } else {
      // Select all
      onChange(collections.map(c => c.id));
    }
  }, [allSelected, collections, onChange]);

  const handleToggleDocument = React.useCallback(
    (docId: string) => {
      if (selectedDocIds.includes(docId)) {
        // Remove from selection
        const updated = selectedDocIds.filter(id => id !== docId);
        onChange(updated);
      } else {
        // Add to selection
        onChange([...selectedDocIds, docId]);
      }
    },
    [selectedDocIds, onChange]
  );

  const getButtonLabel = React.useCallback(() => {
    if (allSelected) {
      return 'All documents';
    }

    if (selectedDocIds.length === 1) {
      const doc = collections.find(c => c.id === selectedDocIds[0]);
      return doc?.name || '1 document';
    }

    return `${selectedDocIds.length} documents`;
  }, [allSelected, selectedDocIds, collections]);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          className={cn('justify-between', className)}
          aria-label="Filter by document source"
        >
          <span className="flex items-center gap-2">{getButtonLabel()}</span>
          <span className="ml-2 text-muted-foreground">▼</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-80">
        {/* Select All Option */}
        <DropdownMenuItem
          className="cursor-pointer"
          onSelect={e => {
            e.preventDefault();
            handleToggleAll();
          }}
        >
          <div className="flex items-center gap-2 w-full">
            <Checkbox
              checked={allSelected}
              onCheckedChange={handleToggleAll}
              aria-label="Select all documents"
            />
            <span className="font-medium flex-1">All documents</span>
            {allSelected && <Check className="h-4 w-4 text-primary" />}
          </div>
        </DropdownMenuItem>

        {collections.length > 0 && <DropdownMenuSeparator />}

        {/* Individual Document Options */}
        {collections.map(collection => {
          const isSelected = selectedDocIds.includes(collection.id);

          return (
            <DropdownMenuItem
              key={collection.id}
              className="cursor-pointer"
              onSelect={e => {
                e.preventDefault();
                handleToggleDocument(collection.id);
              }}
            >
              <div className="flex items-center gap-2 w-full">
                <Checkbox
                  checked={isSelected}
                  onCheckedChange={() => handleToggleDocument(collection.id)}
                  aria-label={`Toggle ${collection.name}`}
                />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="font-medium truncate" title={collection.name}>
                      {collection.name}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <DocumentBadge type={collection.documentType} />
                    <span className="text-xs text-muted-foreground">
                      {collection.documentCount} {collection.documentCount === 1 ? 'doc' : 'docs'}
                    </span>
                  </div>
                </div>
                {isSelected && <Check className="h-4 w-4 text-primary flex-shrink-0" />}
              </div>
            </DropdownMenuItem>
          );
        })}

        {collections.length === 0 && (
          <DropdownMenuItem disabled>
            <span className="text-sm text-muted-foreground">No collections available</span>
          </DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
