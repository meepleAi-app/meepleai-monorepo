/* eslint-disable security/detect-object-injection */
/**
 * PdfDocumentList Component - Issue #2391 Sprint 1
 *
 * Displays list of PDF documents associated with a shared game.
 * Grouped by type (Rulebook, Errata, Homerule) with version management.
 */

'use client';

import { FileText, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { type SharedGameDocument } from '@/lib/api/schemas/shared-games.schemas';

import { VersionBadge } from './VersionBadge';

export interface PdfDocumentListProps {
  documents: SharedGameDocument[];
  onSetActive: (documentId: string) => void;
  onRemove: (documentId: string) => void;
  isLoading?: boolean;
}

const DOCUMENT_TYPE_LABELS: Record<number, string> = {
  0: 'Rulebook',
  1: 'Errata',
  2: 'Homerule',
};

export function PdfDocumentList({
  documents,
  onSetActive,
  onRemove,
  isLoading = false,
}: PdfDocumentListProps) {
  // Group documents by type
  const groupedDocs = documents.reduce(
    (acc, doc) => {
      const type = doc.documentType;
      if (!acc[type]) acc[type] = [];
      acc[type].push(doc);
      return acc;
    },
    {} as Record<number, SharedGameDocument[]>
  );

  return (
    <div className="space-y-4">
      {Object.entries(groupedDocs).map(([type, docs]) => (
        <Card key={type}>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <FileText className="h-5 w-5" />
              {DOCUMENT_TYPE_LABELS[parseInt(type)]}
            </CardTitle>
            <CardDescription>
              {docs.length} {docs.length === 1 ? 'documento' : 'documenti'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {docs.map(doc => (
                <div
                  key={doc.id}
                  className="flex items-center justify-between p-3 border rounded-lg hover:bg-accent/50 transition-colors"
                >
                  <div className="flex-1 space-y-1">
                    <div className="flex items-center gap-2">
                      <VersionBadge version={doc.version} isActive={doc.isActive} />
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
                    <p className="text-xs text-muted-foreground">
                      Caricato: {new Date(doc.createdAt).toLocaleDateString('it-IT')}
                    </p>
                  </div>

                  <div className="flex gap-2">
                    {!doc.isActive && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => onSetActive(doc.id)}
                        disabled={isLoading}
                      >
                        Imposta Attiva
                      </Button>
                    )}
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => onRemove(doc.id)}
                      disabled={isLoading}
                      className="text-destructive hover:text-destructive"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      ))}

      {documents.length === 0 && (
        <Card>
          <CardContent className="py-12 text-center text-muted-foreground">
            <FileText className="h-12 w-12 mx-auto mb-4 opacity-50" />
            <p>Nessun documento associato a questo gioco</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
