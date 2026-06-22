'use client';

import type { JSX } from 'react';

import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { useDocumentEmbeddingsMeta } from '@/hooks/admin/use-document-embeddings-meta';
import { getDocumentChunksExportUrl } from '@/lib/api/admin-kb-embeddings';
import { isNotFoundError } from '@/lib/api/core/errors';

import { EmbeddingsMetaStrip, type EmbeddingsMetaState } from './embeddings-meta-strip';
import { EmbeddingsSearchPanel } from './embeddings-search-panel';

/**
 * Issue #1674: drawer side-right that opens above KbDocDetailPanel.
 * Displays embeddings meta + scoped semantic search + export footer.
 *
 * Security: NO raw vector values exposed. See spec §7.
 */

export interface DocumentEmbeddingsDrawerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  docId: string | null;
  docFileName: string | null;
}

export function DocumentEmbeddingsDrawer({
  open,
  onOpenChange,
  docId,
  docFileName,
}: DocumentEmbeddingsDrawerProps): JSX.Element | null {
  const metaQuery = useDocumentEmbeddingsMeta(docId, open);

  if (!docId || !docFileName) {
    return null;
  }

  const exportHref = getDocumentChunksExportUrl(docId);

  const metaState: EmbeddingsMetaState = metaQuery.isPending
    ? { status: 'loading' }
    : metaQuery.isError
      ? isNotFoundError(metaQuery.error)
        ? { status: 'not-indexed' }
        : { status: 'error', message: metaQuery.error.message }
      : metaQuery.data
        ? { status: 'success', data: metaQuery.data }
        : { status: 'loading' };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="flex w-[720px] flex-col gap-0 p-0 sm:max-w-[720px]">
        <SheetHeader className="border-b border-border px-6 py-4">
          <SheetTitle className="text-base font-semibold">Embeddings · {docFileName}</SheetTitle>
        </SheetHeader>

        <div className="flex-1 space-y-4 overflow-y-auto px-6 py-4">
          <EmbeddingsMetaStrip state={metaState} />
          {metaState.status === 'success' ? <EmbeddingsSearchPanel docId={docId} /> : null}
        </div>

        <div className="flex justify-between gap-3 border-t border-border px-6 py-4">
          <Button asChild variant="outline" disabled={metaState.status !== 'success'}>
            <a href={exportHref} download={`${docId}-chunks.json`}>
              ⤓ Export chunks JSON
            </a>
          </Button>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Chiudi
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}
