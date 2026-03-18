/**
 * Game Rules Tab Component
 *
 * Displays PDF rulebooks and document management with processing state badges.
 * Uses the 7-state PdfProcessingState for granular status display.
 *
 * Issue M3: Connect KB documents to game detail
 */

'use client';

import React from 'react';

import {
  FileText,
  Download,
  Eye,
  CheckCircle,
  Clock,
  XCircle,
  AlertCircle,
  Upload,
  Loader2,
} from 'lucide-react';
import Link from 'next/link';

import { EmptyStateCard } from '@/components/features/common/EmptyStateCard';
import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

// ============================================================================
// Types
// ============================================================================

interface GameRulesTabProps {
  gameId: string;
  documents: PdfDocumentDto[];
  isLoading?: boolean;
}

// Processing states ordered by pipeline progression
const ACTIVE_STATES = ['Uploading', 'Extracting', 'Chunking', 'Embedding', 'Indexing'];

// ============================================================================
// Helpers
// ============================================================================

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function getProcessingStateIcon(state: string) {
  const stateLower = state.toLowerCase();
  if (stateLower === 'ready') {
    return <CheckCircle className="h-4 w-4 text-green-500" />;
  }
  if (stateLower === 'failed') {
    return <XCircle className="h-4 w-4 text-red-500" />;
  }
  if (stateLower === 'pending') {
    return <Clock className="h-4 w-4 text-yellow-500" />;
  }
  // Active states: Uploading, Extracting, Chunking, Embedding, Indexing
  return <Loader2 className="h-4 w-4 text-blue-500 animate-spin" />;
}

function getProcessingStateBadge(state: string) {
  const isActive = ACTIVE_STATES.some(s => s.toLowerCase() === state.toLowerCase());

  if (state.toLowerCase() === 'ready') {
    return (
      <Badge variant="default" className="bg-green-600">
        Pronto
      </Badge>
    );
  }
  if (state.toLowerCase() === 'failed') {
    return <Badge variant="destructive">Fallito</Badge>;
  }
  if (state.toLowerCase() === 'pending') {
    return <Badge variant="secondary">In attesa</Badge>;
  }
  if (isActive) {
    return (
      <Badge variant="default" className="bg-blue-600 animate-pulse">
        {state}
      </Badge>
    );
  }
  return <Badge variant="outline">{state}</Badge>;
}

// ============================================================================
// Component
// ============================================================================

export function GameRulesTab({ gameId, documents = [], isLoading = false }: GameRulesTabProps) {
  if (isLoading) {
    return (
      <div className="space-y-6">
        <Card>
          <CardHeader>
            <Skeleton className="h-6 w-48" />
            <Skeleton className="h-4 w-64 mt-2" />
          </CardHeader>
          <CardContent className="space-y-3">
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
          </CardContent>
        </Card>
      </div>
    );
  }

  // Group documents by processing state
  const readyDocuments = documents.filter(
    doc =>
      (doc.processingState ?? doc.processingStatus).toLowerCase() === 'ready' ||
      (doc.processingState ?? doc.processingStatus).toLowerCase() === 'completed'
  );
  const activeDocuments = documents.filter(doc => {
    const state = (doc.processingState ?? doc.processingStatus).toLowerCase();
    return ACTIVE_STATES.some(s => s.toLowerCase() === state) || state === 'pending';
  });
  const failedDocuments = documents.filter(
    doc => (doc.processingState ?? doc.processingStatus).toLowerCase() === 'failed'
  );

  if (documents.length === 0) {
    return (
      <EmptyStateCard
        title="Nessun documento"
        description="Carica le regole del gioco per consultarle con l'assistente AI"
        ctaLabel="Carica Regolamento"
        onCtaClick={() => {
          // Navigate to upload — for now link to admin upload
          window.location.href = `/admin/knowledge-base?gameId=${gameId}`;
        }}
        icon={FileText}
        entityColor="25 95% 45%" // orange for game-related
      />
    );
  }

  return (
    <div className="space-y-6">
      {/* Upload button */}
      <div className="flex justify-end">
        <Button asChild variant="outline" size="sm">
          <Link href={`/admin/knowledge-base?gameId=${gameId}`}>
            <Upload className="h-4 w-4 mr-2" />
            Carica Documento
          </Link>
        </Button>
      </div>

      {/* Ready Documents */}
      {readyDocuments.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              <span>Regolamenti Disponibili</span>
              <Badge variant="secondary">{readyDocuments.length}</Badge>
            </CardTitle>
            <CardDescription>Documenti pronti per la consultazione AI</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {readyDocuments.map(doc => (
                <div
                  key={doc.id}
                  className="flex items-center justify-between p-4 border border-border/50 dark:border-border/30 rounded-lg hover:bg-muted/50 dark:hover:bg-muted/30 transition-colors"
                >
                  <div className="flex items-center gap-3 flex-1 min-w-0">
                    {getProcessingStateIcon(doc.processingState ?? doc.processingStatus)}
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <p className="font-medium truncate">{doc.fileName}</p>
                        {getProcessingStateBadge(doc.processingState ?? doc.processingStatus)}
                      </div>
                      <div className="flex items-center gap-3 text-xs text-muted-foreground mt-1">
                        <span>{formatFileSize(doc.fileSizeBytes)}</span>
                        {doc.pageCount && (
                          <>
                            <span>&middot;</span>
                            <span>{doc.pageCount} pagine</span>
                          </>
                        )}
                        <span>&middot;</span>
                        <span>
                          Caricato il {new Date(doc.uploadedAt).toLocaleDateString('it-IT')}
                        </span>
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 ml-4">
                    <Button size="sm" variant="outline" disabled>
                      <Eye className="h-4 w-4 mr-2" />
                      Vedi
                    </Button>
                    <Button size="sm" variant="outline" disabled>
                      <Download className="h-4 w-4 mr-2" />
                      Scarica
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Processing Documents */}
      {activeDocuments.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              <span>In Elaborazione</span>
              <Badge variant="secondary">{activeDocuments.length}</Badge>
            </CardTitle>
            <CardDescription>Documenti in fase di processing</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {activeDocuments.map(doc => {
                const state = doc.processingState ?? doc.processingStatus;
                return (
                  <div
                    key={doc.id}
                    className="flex items-center justify-between p-4 border border-blue-200 dark:border-blue-800 rounded-lg bg-blue-50/50 dark:bg-blue-900/10"
                  >
                    <div className="flex items-center gap-3 flex-1 min-w-0">
                      {getProcessingStateIcon(state)}
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2">
                          <p className="font-medium truncate">{doc.fileName}</p>
                          {getProcessingStateBadge(state)}
                        </div>
                        <div className="flex items-center gap-3 text-xs text-muted-foreground mt-1">
                          <span>{formatFileSize(doc.fileSizeBytes)}</span>
                          <span>&middot;</span>
                          <span>
                            Caricato il {new Date(doc.uploadedAt).toLocaleDateString('it-IT')}
                          </span>
                          {doc.progressPercentage > 0 && (
                            <>
                              <span>&middot;</span>
                              <span>{doc.progressPercentage}%</span>
                            </>
                          )}
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Failed Documents */}
      {failedDocuments.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              <span>Falliti</span>
              <Badge variant="destructive">{failedDocuments.length}</Badge>
            </CardTitle>
            <CardDescription>Documenti con errori di elaborazione</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {failedDocuments.map(doc => (
                <div
                  key={doc.id}
                  className="flex items-center justify-between p-4 border border-red-200 dark:border-red-700 rounded-lg bg-red-50/50 dark:bg-red-900/10"
                >
                  <div className="flex items-center gap-3 flex-1 min-w-0">
                    {getProcessingStateIcon('failed')}
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <p className="font-medium truncate">{doc.fileName}</p>
                        {getProcessingStateBadge('failed')}
                      </div>
                      <div className="flex items-center gap-3 text-xs text-muted-foreground mt-1">
                        <span>{formatFileSize(doc.fileSizeBytes)}</span>
                        <span>&middot;</span>
                        <span>
                          Caricato il {new Date(doc.uploadedAt).toLocaleDateString('it-IT')}
                        </span>
                        {doc.processingError && (
                          <>
                            <span>&middot;</span>
                            <span className="text-red-500 truncate max-w-[200px]">
                              {doc.processingError}
                            </span>
                          </>
                        )}
                      </div>
                    </div>
                  </div>
                  {doc.canRetry && (
                    <Button size="sm" variant="outline" disabled>
                      <AlertCircle className="h-4 w-4 mr-2" />
                      Riprova
                    </Button>
                  )}
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
