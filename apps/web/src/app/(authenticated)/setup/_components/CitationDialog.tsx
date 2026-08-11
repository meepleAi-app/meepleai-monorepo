'use client';

import type { JSX } from 'react';

import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { useTranslation } from '@/hooks/useTranslation';
import type { Snippet } from '@/lib/api/schemas';

export interface CitationDialogProps {
  /** Snippets to display, or `null` when the dialog should be closed. */
  snippets: Snippet[] | null;
  onClose: () => void;
}

/**
 * References dialog for a setup-guide step. Replaces the legacy bespoke
 * fixed-overlay `CitationModal` with the design-system `Dialog` primitive.
 * Issue: fix/setup-page-redesign.
 */
export function CitationDialog({ snippets, onClose }: CitationDialogProps): JSX.Element {
  const { t } = useTranslation();
  const isOpen = snippets !== null;

  return (
    <Dialog open={isOpen} onOpenChange={next => !next && onClose()}>
      <DialogContent className="max-h-[80vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t('pages.setup.references')}</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          {(snippets ?? []).map((snippet, idx) => (
            <div key={idx} className="rounded-md border border-border bg-muted p-3">
              <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                <span className="text-sm font-medium text-foreground">
                  {t('pages.setup.citation.docLabel', { id: snippet.documentId.substring(0, 8) })}
                </span>
                <span className="text-xs text-muted-foreground">
                  {snippet.pageNumber !== null
                    ? t('pages.setup.citation.pageLabel', { n: snippet.pageNumber })
                    : t('pages.setup.citation.noPage')}
                </span>
              </div>
              <div className="text-[13px] leading-relaxed text-muted-foreground">
                {snippet.snippet}
              </div>
            </div>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}
