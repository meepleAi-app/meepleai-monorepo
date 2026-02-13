/**
 * KnowledgeBaseTab Component (Issue #4229)
 *
 * Displays Knowledge Base documents for a SharedGame.
 * Shows indexed PDFs from all agents associated with the game.
 *
 * Features:
 * - Fetches agents for the game
 * - Loads KB documents for each agent
 * - Displays documents with indexing status
 * - Empty state when no documents indexed
 */

'use client';

import { useQueries } from '@tanstack/react-query';
import { Database, FileText, Loader2 } from 'lucide-react';

import { PdfIndexingStatus } from '@/components/admin/shared-games/PdfIndexingStatus';
import { PdfStatusBadge } from '@/components/pdf';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { agentDocumentsKeys } from '@/hooks/queries/useAgentDocuments';
import { useGameAgents } from '@/hooks/queries/useGameAgents';
import { api } from '@/lib/api';
import type { AgentDocumentsDto, SelectedDocumentDto } from '@/lib/api/schemas';
import type { PdfState } from '@/types/pdf';

// ============================================================================
// Types
// ============================================================================

export interface KnowledgeBaseTabProps {
  /** Game ID to fetch agents and documents for */
  gameId: string;
}

// Document type mapping
const DOCUMENT_TYPE_LABELS: Record<number, string> = {
  0: 'Rulebook',
  1: 'Errata',
  2: 'Homerule',
};

const DOCUMENT_TYPE_VARIANTS: Record<number, 'default' | 'secondary' | 'outline'> = {
  0: 'default',
  1: 'secondary',
  2: 'outline',
};

// ============================================================================
// Component
// ============================================================================

export function KnowledgeBaseTab({ gameId }: KnowledgeBaseTabProps) {
  // Fetch agents for the game
  const {
    data: agents,
    isLoading: agentsLoading,
    error: agentsError,
  } = useGameAgents({ gameId });

  // Fetch documents for each agent using useQueries (fixes Rules of Hooks violation)
  const documentsQueries = useQueries({
    queries:
      agents?.map(agent => ({
        queryKey: agentDocumentsKeys.byAgent(agent.id),
        queryFn: async (): Promise<AgentDocumentsDto | null> => api.agents.getDocuments(agent.id),
        enabled: !!agent.id,
        staleTime: 30_000,
        gcTime: 5 * 60_000,
      })) || [],
  });

  // Aggregate all documents from all agents
  const allDocuments: Array<SelectedDocumentDto & { agentName: string }> =
    documentsQueries.flatMap((query, index) => {
      if (!query.data?.documents || !agents) {
        return [];
      }
      const agent = agents.at(index);
      const agentName = agent?.name || 'Unknown Agent';
      return query.data.documents.map(doc => ({
        ...doc,
        agentName,
      }));
    });

  // Loading state
  if (agentsLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        <span className="ml-3 text-muted-foreground">Loading Knowledge Base...</span>
      </div>
    );
  }

  // Error state
  if (agentsError) {
    return (
      <Alert variant="destructive">
        <AlertDescription>
          Failed to load agents: {agentsError.message}
        </AlertDescription>
      </Alert>
    );
  }

  // Empty state - no agents
  if (!agents || agents.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <Database className="h-12 w-12 text-muted-foreground opacity-50 mb-4" />
        <h3 className="text-lg font-semibold mb-2">No Agents Available</h3>
        <p className="text-sm text-muted-foreground max-w-md">
          This game doesn't have any AI agents configured yet.
        </p>
      </div>
    );
  }

  // Empty state - no documents
  if (allDocuments.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <FileText className="h-12 w-12 text-muted-foreground opacity-50 mb-4" />
        <h3 className="text-lg font-semibold mb-2">No Documents Indexed Yet</h3>
        <p className="text-sm text-muted-foreground max-w-md">
          Documents will appear here once they have been uploaded and indexed in the Knowledge
          Base.
        </p>
      </div>
    );
  }

  // Documents display
  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold">Knowledge Base Documents</h3>
          <p className="text-sm text-muted-foreground">
            {allDocuments.length} document{allDocuments.length !== 1 ? 's' : ''} indexed across{' '}
            {agents.length} agent{agents.length !== 1 ? 's' : ''}
          </p>
        </div>
      </div>

      {/* Documents List */}
      <div className="grid gap-4 md:grid-cols-2">
        {allDocuments.map(doc => (
          <DocumentCard key={doc.id} document={doc} />
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Document Card Component
// ============================================================================

interface DocumentCardProps {
  document: SelectedDocumentDto & { agentName: string };
}

function DocumentCard({ document }: DocumentCardProps) {
  const typeLabel = DOCUMENT_TYPE_LABELS[document.documentType] || 'Unknown';
  const typeVariant = DOCUMENT_TYPE_VARIANTS[document.documentType] || 'secondary';

  // Map document state to PdfState (simplified for now - can be enhanced with real data)
  const pdfState: PdfState = 'ready'; // TODO: Get actual state from API

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
                <p className="font-medium text-sm truncate" title={document.gameName || 'Untitled'}>
                  {document.gameName || 'Untitled Document'}
                </p>
                <p className="text-xs text-muted-foreground truncate">
                  Agent: {document.agentName}
                </p>
              </div>
            </div>
            <div className="flex gap-2 flex-shrink-0">
              {/* New: PdfStatusBadge (Issue #4217) */}
              <PdfStatusBadge state={pdfState} variant="compact" />
              <Badge variant={typeVariant}>
                {typeLabel}
              </Badge>
            </div>
          </div>

          {/* Indexing Status */}
          <PdfIndexingStatus pdfId={document.pdfDocumentId} compact />

          {/* Metadata */}
          <div className="flex flex-wrap gap-2">
            {document.version && (
              <Badge variant="outline" className="text-xs">
                v{document.version}
              </Badge>
            )}
            {document.tags.map(tag => (
              <Badge key={tag} variant="secondary" className="text-xs">
                {tag}
              </Badge>
            ))}
            {document.isActive && (
              <Badge variant="default" className="text-xs bg-green-600 hover:bg-green-700">
                Active
              </Badge>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
