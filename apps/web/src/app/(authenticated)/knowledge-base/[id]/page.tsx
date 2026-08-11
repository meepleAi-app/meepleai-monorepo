'use client';

import { use, useEffect, useMemo, useState } from 'react';

import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';

import {
  ChunkSearchBox,
  KbChunkListPanel,
  KbChunkPreview,
  KbHeader,
  type KbChunkPreviewState,
} from '@/components/features/knowledge-base';
import { HubPageContainer } from '@/components/layout/PageContainer';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import {
  useKbChunk,
  useKbChunks,
  useKbDocument,
  useSearchKbChunks,
} from '@/hooks/queries/use-kb-detail';
import { useRecentsStore } from '@/stores/use-recents';

export default function KnowledgeBaseDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id: documentId } = use(params);

  const documentQuery = useKbDocument(documentId);
  const chunksQuery = useKbChunks(documentId, { limit: 200 });

  const [activeChunkId, setActiveChunkId] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');

  const chunkPreviewQuery = useKbChunk(documentId, activeChunkId ?? '');
  const chunkSearchQuery = useSearchKbChunks({ docId: documentId, query: searchQuery });

  // Recents push when the document arrives.
  useEffect(() => {
    if (documentQuery.data) {
      useRecentsStore.getState().push({
        id: documentId,
        entity: 'kb',
        title: documentQuery.data.title,
        href: `/knowledge-base/${documentId}`,
      });
    }
  }, [documentId, documentQuery.data]);

  // Selection autopilot: pick the first chunk once the list lands so the
  // preview pane has something to render without a manual click.
  const chunks = useMemo(() => chunksQuery.data?.items ?? [], [chunksQuery.data]);
  useEffect(() => {
    if (activeChunkId === null && chunks.length > 0) {
      setActiveChunkId(chunks[0].id);
    }
  }, [activeChunkId, chunks]);

  // Filter the chunks list by search matches when a debounced query is active.
  const filteredChunks = useMemo(() => {
    if (searchQuery.trim().length === 0) return chunks;
    const matches = chunkSearchQuery.data?.matches ?? [];
    if (matches.length === 0) return [];
    const matchedIds = new Set(matches.map(m => m.chunkId));
    return chunks.filter(c => matchedIds.has(c.id));
  }, [chunks, chunkSearchQuery.data, searchQuery]);

  const previewState: KbChunkPreviewState = useMemo(() => {
    if (activeChunkId === null) return { kind: 'empty' };
    if (chunkPreviewQuery.isLoading) return { kind: 'loading' };
    if (chunkPreviewQuery.isError) {
      return {
        kind: 'error',
        message: 'Errore caricamento chunk. Riprova tra qualche istante.',
      };
    }
    if (chunkPreviewQuery.data) return { kind: 'ready', chunk: chunkPreviewQuery.data };
    return { kind: 'empty' };
  }, [
    activeChunkId,
    chunkPreviewQuery.data,
    chunkPreviewQuery.isError,
    chunkPreviewQuery.isLoading,
  ]);

  // Document FSM
  if (documentQuery.isLoading) {
    return (
      <div className="min-h-screen bg-background py-8 px-4">
        <HubPageContainer className="p-0">
          <Skeleton className="h-8 w-48 mb-6" />
          <Skeleton className="h-[400px] w-full" />
        </HubPageContainer>
      </div>
    );
  }

  if (documentQuery.isError || !documentQuery.data) {
    return (
      <div className="min-h-screen bg-background py-8 px-4">
        <HubPageContainer className="p-0">
          <Alert variant="destructive">
            <AlertDescription>Documento non trovato o non accessibile.</AlertDescription>
          </Alert>
          <Button asChild className="mt-4">
            <Link href="/library">
              <ArrowLeft className="mr-2 h-4 w-4" /> Torna alla Libreria
            </Link>
          </Button>
        </HubPageContainer>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen flex-col bg-background">
      <div className="border-b border-border bg-card px-4 py-3">
        <Button asChild variant="ghost" size="sm" className="font-nunito">
          <Link href="/library">
            <ArrowLeft className="mr-2 h-4 w-4" /> Torna alla Libreria
          </Link>
        </Button>
      </div>

      <div
        data-slot="kb-detail-split-view"
        className="grid flex-1 grid-cols-1 gap-0 lg:grid-cols-[minmax(320px,28rem)_1fr]"
      >
        {/* Sticky sx column — header, search, chunks list */}
        <aside className="flex max-h-[calc(100vh-3.5rem)] flex-col border-r border-border lg:sticky lg:top-0">
          <KbHeader document={documentQuery.data} />
          <div className="border-b border-border bg-card p-3">
            <ChunkSearchBox onCommit={setSearchQuery} />
          </div>
          <div className="flex-1 overflow-hidden">
            {chunksQuery.isLoading ? (
              <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
                Caricamento chunk…
              </div>
            ) : chunksQuery.isError ? (
              <div
                role="alert"
                className="flex h-full items-center justify-center text-sm text-destructive"
              >
                Errore caricamento chunk.
              </div>
            ) : (
              <KbChunkListPanel
                chunks={filteredChunks}
                activeChunkId={activeChunkId}
                onSelect={setActiveChunkId}
                emptyLabel={
                  searchQuery.trim().length > 0
                    ? 'Nessun chunk corrisponde alla ricerca.'
                    : 'Nessun chunk disponibile.'
                }
              />
            )}
          </div>
        </aside>

        {/* dx column — preview */}
        <main className="flex max-h-[calc(100vh-3.5rem)] flex-col">
          <KbChunkPreview state={previewState} />
        </main>
      </div>
    </div>
  );
}
