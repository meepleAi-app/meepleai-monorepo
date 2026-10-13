/**
 * KnowledgeBaseTab Component (Issue #4229)
 *
 * Displays the Knowledge Base documents indexed for a game.
 *
 * Issue #4138: this used to fetch the game's AGENTS and then, for each one, call
 * `api.agents.getDocuments(agent.id)` on `/agents/{id}/documents`. Two things were wrong with
 * that:
 *
 *   - the route is NOT MOUNTED on the backend (marked in #4139 after enumerating every mapped
 *     `/agents` path), so the per-agent query could only ever fail — which the tab reported as
 *     "No Documents Indexed Yet", indistinguishable from an empty KB;
 *   - and documents belong to a GAME, not to an agent. With one system agent, keying the KB by
 *     agent has nothing left to key on.
 *
 * It now reads `useDocumentsByGame`, i.e. the per-game listing that already existed.
 *
 * That also fixes a defect in the card: it showed `document.gameName` as the document title, so
 * every row displayed the same text (the game's name) instead of the file's. `PdfDocumentDto`
 * carries `fileName`.
 */

'use client';

import { Database, FileText, Loader2 } from 'lucide-react';

import { PdfIndexingStatus } from '@/components/admin/shared-games/PdfIndexingStatus';
import { PdfStatusBadge } from '@/components/pdf';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { useDocumentsByGame } from '@/hooks/queries/useDocumentsByGame';
import { useEmbeddingStatus } from '@/hooks/useEmbeddingStatus';
import type { EmbeddingStatus } from '@/lib/api/schemas/knowledge-base.schemas';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';
import type { PdfState } from '@/types/pdf';

// ============================================================================
// Types
// ============================================================================

export interface KnowledgeBaseTabProps {
  /** Game ID whose indexed documents to list */
  gameId: string;
}

// `PdfDocumentDto.documentType` is a string enum, unlike the numeric 0-2 of the retired
// per-agent DTO.
const DOCUMENT_TYPE_LABELS: Record<string, string> = {
  base: 'Rulebook',
  expansion: 'Expansion',
  errata: 'Errata',
  homerule: 'Homerule',
};

const DOCUMENT_TYPE_VARIANTS: Record<string, 'default' | 'secondary' | 'outline'> = {
  base: 'default',
  expansion: 'outline',
  errata: 'secondary',
  homerule: 'outline',
};

/** Map backend EmbeddingStatus to frontend PdfState */
function mapEmbeddingStatusToPdfState(status: EmbeddingStatus): PdfState {
  const mapping: Record<EmbeddingStatus, PdfState> = {
    Pending: 'pending',
    Extracting: 'extracting',
    Chunking: 'chunking',
    Embedding: 'embedding',
    Completed: 'ready',
    Failed: 'failed',
  };
  return mapping[status];
}

export function KnowledgeBaseTab({ gameId }: KnowledgeBaseTabProps) {
  // Fetch embedding status for the game's KB
  const { data: embeddingData } = useEmbeddingStatus(gameId);
  const gamePdfState: PdfState = embeddingData
    ? mapEmbeddingStatusToPdfState(embeddingData.status)
    : 'pending';

  const { data: documents, isLoading, error } = useDocumentsByGame({ gameId });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        <span className="ml-3 text-muted-foreground">Loading Knowledge Base...</span>
      </div>
    );
  }

  // A failed fetch must NOT render as "no documents": that is exactly how the retired per-agent
  // query hid an unmounted route for months.
  if (error) {
    return (
      <Alert variant="destructive">
        <AlertDescription>Failed to load documents: {error.message}</AlertDescription>
      </Alert>
    );
  }

  if (!documents || documents.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <Database className="h-12 w-12 text-muted-foreground opacity-50 mb-4" />
        <h3 className="text-lg font-semibold mb-2">No Documents Indexed Yet</h3>
        <p className="text-sm text-muted-foreground max-w-md">
          Documents will appear here once they have been uploaded and indexed in the Knowledge Base.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold">Knowledge Base Documents</h3>
          <p className="text-sm text-muted-foreground">
            {documents.length} document{documents.length !== 1 ? 's' : ''} indexed
          </p>
        </div>
      </div>

      {/* Documents List */}
      <div className="grid gap-4 md:grid-cols-2">
        {documents.map(doc => (
          <DocumentCard key={doc.id} document={doc} pdfState={gamePdfState} />
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Document Card Component
// ============================================================================

interface DocumentCardProps {
  document: PdfDocumentDto;
  /** PDF indexing state from game-level embedding status */
  pdfState: PdfState;
}

function DocumentCard({ document, pdfState }: DocumentCardProps) {
  const typeLabel = DOCUMENT_TYPE_LABELS[document.documentType] || 'Unknown';
  const typeVariant = DOCUMENT_TYPE_VARIANTS[document.documentType] || 'secondary';

  return (
    <Card>
      <CardContent className="pt-6">
        <div className="space-y-3">
          {/* Header */}
          <div className="flex items-start justify-between gap-2">
            <div className="flex items-start gap-3 flex-1 min-w-0">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted flex-shrink-0">
                <FileText className="h-5 w-5 text-muted-foreground" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="font-medium text-sm truncate" title={document.fileName}>
                  {document.fileName}
                </p>
                {document.pageCount !== null && (
                  <p className="text-xs text-muted-foreground truncate">
                    {document.pageCount} page{document.pageCount !== 1 ? 's' : ''}
                  </p>
                )}
              </div>
            </div>
            <div className="flex gap-2 flex-shrink-0">
              <PdfStatusBadge state={pdfState} variant="compact" />
              <Badge variant={typeVariant}>{typeLabel}</Badge>
            </div>
          </div>

          {/* Indexing Status */}
          <PdfIndexingStatus pdfId={document.id} compact />

          {/* Metadata */}
          <div className="flex flex-wrap gap-2">
            {document.isPublic && (
              <Badge variant="outline" className="text-xs">
                Public
              </Badge>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
