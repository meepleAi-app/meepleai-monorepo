/* eslint-disable security/detect-object-injection */
/**
 * SelectedDocumentsList Component - Issue #2399
 *
 * Displays the list of documents currently selected for an agent's knowledge base.
 * Shows document details including game name, type, version, and tags.
 */

'use client';

import * as React from 'react';

import { FileText, X, Loader2, AlertCircle, CheckCircle } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ScrollArea } from '@/components/ui/scroll-area';
import type { SelectedDocumentDto } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

export interface SelectedDocumentsListProps {
  /** Currently selected documents with metadata */
  documents: SelectedDocumentDto[];
  /** Callback to remove a document from selection */
  onRemove?: (documentId: string) => void;
  /** Whether the list is in loading state */
  isLoading?: boolean;
  /** Whether the list is in saving state */
  isSaving?: boolean;
  /** Whether the user can modify the selection */
  canEdit?: boolean;
  /** Optional className for styling */
  className?: string;
}

const DOCUMENT_TYPE_LABELS: Record<number, { label: string; color: string }> = {
  0: { label: 'Rulebook', color: 'bg-blue-100 text-blue-800 border-blue-300' },
  1: { label: 'Errata', color: 'bg-orange-100 text-orange-800 border-orange-300' },
  2: { label: 'Homerule', color: 'bg-green-100 text-green-800 border-green-300' },
};

export function SelectedDocumentsList({
  documents,
  onRemove,
  isLoading = false,
  isSaving = false,
  canEdit = true,
  className,
}: SelectedDocumentsListProps) {
  // Group documents by game
  const groupedDocuments = React.useMemo(() => {
    return documents.reduce(
      (acc, doc) => {
        const gameKey = doc.gameName ?? 'Unknown Game';
        if (!acc[gameKey]) {
          acc[gameKey] = {
            gameId: doc.sharedGameId,
            documents: [],
          };
        }
        acc[gameKey].documents.push(doc);
        return acc;
      },
      {} as Record<string, { gameId: string; documents: SelectedDocumentDto[] }>
    );
  }, [documents]);

  if (isLoading) {
    return (
      <Card className={className}>
        <CardContent className="py-12 flex flex-col items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground mb-4" />
          <p className="text-muted-foreground">Caricamento documenti selezionati...</p>
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
              <CheckCircle className="h-5 w-5 text-green-500" />
              Documenti Selezionati
            </CardTitle>
            <CardDescription>
              {documents.length} {documents.length === 1 ? 'documento' : 'documenti'} nella
              knowledge base
            </CardDescription>
          </div>
          {isSaving && (
            <div className="flex items-center gap-2 text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span className="text-sm">Salvataggio...</span>
            </div>
          )}
        </div>
      </CardHeader>
      <CardContent>
        {documents.length === 0 ? (
          <div className="py-8 text-center text-muted-foreground">
            <AlertCircle className="h-10 w-10 mx-auto mb-4 opacity-50" />
            <p className="font-medium">Nessun documento selezionato</p>
            <p className="text-sm mt-2">
              Seleziona i documenti dalla lista per aggiungerli alla knowledge base dell&apos;agente
            </p>
          </div>
        ) : (
          <ScrollArea className="h-[300px]">
            <div className="space-y-6 pr-4">
              {Object.entries(groupedDocuments).map(([gameName, { documents: gameDocs }]) => (
                <div key={gameName} className="space-y-3">
                  <div className="flex items-center gap-2 sticky top-0 bg-background py-1">
                    <FileText className="h-4 w-4 text-muted-foreground" />
                    <span className="font-medium text-sm">{gameName}</span>
                    <span className="text-xs text-muted-foreground">
                      ({gameDocs.length} {gameDocs.length === 1 ? 'doc' : 'docs'})
                    </span>
                  </div>

                  <div className="space-y-2 pl-6">
                    {gameDocs.map(doc => {
                      const typeConfig = DOCUMENT_TYPE_LABELS[doc.documentType];

                      return (
                        <div
                          key={doc.id}
                          className={cn(
                            'flex items-center gap-3 p-3 rounded-lg border',
                            'bg-accent/20 hover:bg-accent/40 transition-colors'
                          )}
                        >
                          <div className="flex-1 min-w-0 space-y-1">
                            <div className="flex items-center gap-2 flex-wrap">
                              <Badge variant="outline" className={cn('text-xs', typeConfig?.color)}>
                                {typeConfig?.label ?? `Type ${doc.documentType}`}
                              </Badge>
                              <Badge variant="secondary" className="text-xs">
                                v{doc.version}
                              </Badge>
                              {doc.isActive && (
                                <Badge variant="default" className="text-xs bg-green-600">
                                  Attivo
                                </Badge>
                              )}
                              {!doc.isActive && (
                                <Badge variant="outline" className="text-xs text-amber-600">
                                  Inattivo
                                </Badge>
                              )}
                            </div>

                            {doc.tags && doc.tags.length > 0 && (
                              <div className="flex gap-1 flex-wrap">
                                {doc.tags.map(tag => (
                                  <Badge key={tag} variant="outline" className="text-xs">
                                    {tag}
                                  </Badge>
                                ))}
                              </div>
                            )}
                          </div>

                          {canEdit && onRemove && (
                            <Button
                              type="button"
                              variant="ghost"
                              size="icon"
                              onClick={() => onRemove(doc.id)}
                              disabled={isSaving}
                              className="flex-shrink-0 hover:bg-destructive/20 hover:text-destructive"
                              aria-label={`Rimuovi documento ${typeConfig?.label ?? ''} v${doc.version}`}
                            >
                              <X className="h-4 w-4" />
                            </Button>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          </ScrollArea>
        )}
      </CardContent>
    </Card>
  );
}
