/* eslint-disable security/detect-object-injection */
/**
 * SelectedDocuments Component - Issue #2416
 *
 * Displays selected documents with removal and drag-and-drop reordering.
 * Features: removable badges, drag-and-drop ordering, max limit alerts, selection statistics.
 */

'use client';

import * as React from 'react';
import { useCallback, useMemo, useState } from 'react';

import {
  closestCenter,
  DndContext,
  DragEndEvent,
  DragOverlay,
  DragStartEvent,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { AlertTriangle, FileText, GripVertical, Loader2, X } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ScrollArea } from '@/components/ui/scroll-area';
import { cn } from '@/lib/utils';

// ========== Types ==========

export interface SelectedDocument {
  id: string;
  title: string;
  documentType: 'Rulebook' | 'Errata' | 'Homerule';
  version: string;
  gameName?: string;
  tags: string[];
}

export interface SelectedDocumentsProps {
  /** Currently selected documents in display order */
  documents: SelectedDocument[];
  /** Callback when documents are reordered or removed */
  onDocumentsChange: (documents: SelectedDocument[]) => void;
  /** Maximum number of documents allowed */
  maxDocuments?: number;
  /** Whether the component is in loading state */
  isLoading?: boolean;
  /** Whether the component is disabled (no interactions) */
  disabled?: boolean;
  /** Optional className for styling */
  className?: string;
}

// ========== Constants ==========

const DOCUMENT_TYPE_COLORS: Record<string, string> = {
  Rulebook: 'bg-blue-100 text-blue-800 border-blue-300 hover:bg-blue-200',
  Errata: 'bg-orange-100 text-orange-800 border-orange-300 hover:bg-orange-200',
  Homerule: 'bg-green-100 text-green-800 border-green-300 hover:bg-green-200',
};

const DEFAULT_MAX_DOCUMENTS = 50;

// ========== Main Component ==========

export function SelectedDocuments({
  documents,
  onDocumentsChange,
  maxDocuments = DEFAULT_MAX_DOCUMENTS,
  isLoading = false,
  disabled = false,
  className,
}: SelectedDocumentsProps): React.JSX.Element {
  const [activeId, setActiveId] = useState<string | null>(null);

  // DnD sensors configuration
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 8, // 8px before drag starts
      },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    })
  );

  // ========== Handlers ==========

  const handleDragStart = useCallback((event: DragStartEvent) => {
    setActiveId(event.active.id as string);
  }, []);

  const handleDragEnd = useCallback(
    (event: DragEndEvent) => {
      const { active, over } = event;
      setActiveId(null);

      if (over && active.id !== over.id) {
        const oldIndex = documents.findIndex(doc => doc.id === active.id);
        const newIndex = documents.findIndex(doc => doc.id === over.id);

        if (oldIndex !== -1 && newIndex !== -1) {
          const newOrder = arrayMove(documents, oldIndex, newIndex);
          onDocumentsChange(newOrder);
        }
      }
    },
    [documents, onDocumentsChange]
  );

  const handleRemoveDocument = useCallback(
    (documentId: string) => {
      const newDocuments = documents.filter(doc => doc.id !== documentId);
      onDocumentsChange(newDocuments);
    },
    [documents, onDocumentsChange]
  );

  // ========== Computed Values ==========

  const documentCount = documents.length;
  const isAtLimit = documentCount >= maxDocuments;
  const isNearLimit = documentCount >= maxDocuments * 0.8;

  const activeDocument = useMemo(
    () => documents.find(doc => doc.id === activeId),
    [documents, activeId]
  );

  // Statistics
  const statistics = useMemo(() => {
    const byType: Record<string, number> = {};
    const byGame: Record<string, number> = {};

    documents.forEach(doc => {
      byType[doc.documentType] = (byType[doc.documentType] || 0) + 1;
      if (doc.gameName) {
        byGame[doc.gameName] = (byGame[doc.gameName] || 0) + 1;
      }
    });

    return { byType, byGame, uniqueGames: Object.keys(byGame).length };
  }, [documents]);

  // ========== Render ==========

  if (isLoading) {
    return (
      <Card className={className}>
        <CardContent className="flex flex-col items-center justify-center py-12">
          <Loader2 className="mb-4 h-8 w-8 animate-spin text-muted-foreground" />
          <p className="text-muted-foreground">Loading selected documents...</p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={cn('w-full', className)}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <FileText className="h-5 w-5" />
              Selected Documents
            </CardTitle>
            <CardDescription>
              {documentCount} of {maxDocuments} documents selected
              {statistics.uniqueGames > 0 && ` from ${statistics.uniqueGames} games`}
            </CardDescription>
          </div>

          {/* Statistics Badges */}
          {documentCount > 0 && (
            <div className="flex gap-1">
              {Object.entries(statistics.byType).map(([type, count]) => (
                <Badge
                  key={type}
                  variant="outline"
                  className={cn('text-xs', DOCUMENT_TYPE_COLORS[type])}
                >
                  {count} {type}
                </Badge>
              ))}
            </div>
          )}
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Limit Alert */}
        {isAtLimit && (
          <Alert variant="destructive">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>Maximum limit reached</AlertTitle>
            <AlertDescription>
              You have reached the maximum of {maxDocuments} documents. Remove some documents to add
              new ones.
            </AlertDescription>
          </Alert>
        )}

        {isNearLimit && !isAtLimit && (
          <Alert>
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>Approaching limit</AlertTitle>
            <AlertDescription>
              You have selected {documentCount} of {maxDocuments} documents (
              {Math.round((documentCount / maxDocuments) * 100)}%).
            </AlertDescription>
          </Alert>
        )}

        {/* Document List with DnD */}
        {documentCount === 0 ? (
          <div className="py-8 text-center text-muted-foreground">
            <FileText className="mx-auto mb-4 h-10 w-10 opacity-50" />
            <p className="font-medium">No documents selected</p>
            <p className="mt-2 text-sm">Select documents from the picker to add them here.</p>
          </div>
        ) : (
          <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragStart={handleDragStart}
            onDragEnd={handleDragEnd}
          >
            <ScrollArea className="h-[350px]">
              <SortableContext
                items={documents.map(doc => doc.id)}
                strategy={verticalListSortingStrategy}
              >
                <div className="space-y-2 pr-4">
                  {documents.map((document, index) => (
                    <SortableDocumentItem
                      key={document.id}
                      document={document}
                      index={index}
                      onRemove={handleRemoveDocument}
                      disabled={disabled}
                    />
                  ))}
                </div>
              </SortableContext>
            </ScrollArea>

            {/* Drag Overlay */}
            <DragOverlay>
              {activeDocument ? (
                <DocumentItemContent
                  document={activeDocument}
                  index={documents.findIndex(d => d.id === activeDocument.id)}
                  isDragging
                />
              ) : null}
            </DragOverlay>
          </DndContext>
        )}

        {/* Clear All Button */}
        {documentCount > 0 && !disabled && (
          <div className="flex justify-end border-t pt-4">
            <Button
              variant="outline"
              size="sm"
              onClick={() => onDocumentsChange([])}
              className="text-destructive hover:bg-destructive/10 hover:text-destructive"
            >
              Clear All ({documentCount})
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ========== Sortable Document Item ==========

interface SortableDocumentItemProps {
  document: SelectedDocument;
  index: number;
  onRemove: (id: string) => void;
  disabled?: boolean;
}

function SortableDocumentItem({
  document,
  index,
  onRemove,
  disabled = false,
}: SortableDocumentItemProps): React.JSX.Element {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: document.id,
    disabled,
  });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  return (
    <div ref={setNodeRef} style={style} className={cn(isDragging && 'opacity-50')} {...attributes}>
      <DocumentItemContent
        document={document}
        index={index}
        onRemove={onRemove}
        disabled={disabled}
        dragHandleProps={listeners}
      />
    </div>
  );
}

// ========== Document Item Content ==========

interface DocumentItemContentProps {
  document: SelectedDocument;
  index: number;
  onRemove?: (id: string) => void;
  disabled?: boolean;
  isDragging?: boolean;
  dragHandleProps?: Record<string, unknown>;
}

function DocumentItemContent({
  document,
  index,
  onRemove,
  disabled = false,
  isDragging = false,
  dragHandleProps,
}: DocumentItemContentProps): React.JSX.Element {
  const typeColor = DOCUMENT_TYPE_COLORS[document.documentType] ?? '';

  return (
    <div
      className={cn(
        'flex items-center gap-3 rounded-lg border bg-background p-3 transition-colors',
        'hover:bg-accent/50',
        isDragging && 'shadow-lg ring-2 ring-primary'
      )}
    >
      {/* Drag Handle */}
      <button
        type="button"
        className={cn(
          'flex cursor-grab items-center justify-center rounded p-1',
          'text-muted-foreground hover:bg-accent hover:text-foreground',
          'focus:outline-none focus-visible:ring-2 focus-visible:ring-primary',
          disabled && 'cursor-not-allowed opacity-50'
        )}
        aria-label={`Drag to reorder ${document.title}`}
        disabled={disabled}
        {...dragHandleProps}
      >
        <GripVertical className="h-4 w-4" />
      </button>

      {/* Position indicator */}
      <span className="flex h-6 w-6 items-center justify-center rounded-full bg-muted text-xs font-medium">
        {index + 1}
      </span>

      {/* Document info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-medium truncate">{document.title}</span>
          <Badge variant="outline" className={cn('text-xs', typeColor)}>
            {document.documentType}
          </Badge>
          <Badge variant="secondary" className="text-xs">
            v{document.version}
          </Badge>
        </div>

        {document.gameName && (
          <p className="text-sm text-muted-foreground truncate">{document.gameName}</p>
        )}

        {document.tags.length > 0 && (
          <div className="mt-1 flex gap-1 flex-wrap">
            {document.tags.slice(0, 3).map(tag => (
              <Badge key={tag} variant="outline" className="text-xs">
                {tag}
              </Badge>
            ))}
            {document.tags.length > 3 && (
              <Badge variant="outline" className="text-xs text-muted-foreground">
                +{document.tags.length - 3}
              </Badge>
            )}
          </div>
        )}
      </div>

      {/* Remove button */}
      {onRemove && !disabled && (
        <Button
          type="button"
          variant="ghost"
          size="icon"
          onClick={() => onRemove(document.id)}
          className="flex-shrink-0 hover:bg-destructive/10 hover:text-destructive"
          aria-label={`Remove ${document.title}`}
        >
          <X className="h-4 w-4" />
        </Button>
      )}
    </div>
  );
}
