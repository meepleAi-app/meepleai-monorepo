'use client';

import { useState } from 'react';

import { toast } from 'sonner';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { useIndexerVersions } from '@/hooks/queries/useIndexerVersions';
import { useReindexDoc } from '@/hooks/queries/useKbDocActions';

export interface KbReindexDropdownProps {
  readonly docId: string;
  readonly processingStatus: 'queued' | 'processing' | 'ready' | 'failed';
}

/** Shared focus-visible ring classes applied to every bare button (a11y). */
const FOCUS_RING =
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2';

/**
 * KbReindexDropdown — split-button per il re-index versionato (Issue #1673).
 *
 * Layout:
 *   [ ⟳ Re-index ] [ ▾ ]
 *
 * Comportamento:
 *   - Click sul body → reindex con default server (`indexerVersion` omesso).
 *   - Click sul caret → menu con versioni selectable; selezione → reindex con versione esplicita.
 *   - Entrambi i bottoni si disabilitano quando il documento è in pipeline.
 */
export function KbReindexDropdown({ docId, processingStatus }: KbReindexDropdownProps) {
  const reindex = useReindexDoc(docId);
  const versionsQuery = useIndexerVersions();
  const [menuOpen, setMenuOpen] = useState(false);

  const disabled =
    processingStatus === 'processing' || processingStatus === 'queued' || reindex.isPending;

  const runReindex = (indexerVersion?: string) => {
    const payload = indexerVersion ? { indexerVersion } : undefined;
    reindex.mutate(payload, {
      onSuccess: () =>
        toast.success(indexerVersion ? `Re-index avviato (${indexerVersion})` : 'Re-index avviato'),
      onError: (err: Error) => toast.error(`Re-index fallito: ${err.message}`),
    });
  };

  return (
    <div className="inline-flex">
      <button
        type="button"
        onClick={() => runReindex()}
        disabled={disabled}
        className={`rounded-l-md border border-r-0 border-border px-3 py-1.5 text-xs font-medium hover:bg-muted/70 disabled:cursor-not-allowed disabled:opacity-50 ${FOCUS_RING}`}
        aria-label="⟳ Re-index"
      >
        ⟳ Re-index
      </button>
      <DropdownMenu open={menuOpen} onOpenChange={setMenuOpen}>
        <DropdownMenuTrigger asChild>
          <button
            type="button"
            disabled={disabled || versionsQuery.isLoading}
            className={`rounded-r-md border border-border px-2 py-1.5 text-xs font-medium hover:bg-muted/70 disabled:cursor-not-allowed disabled:opacity-50 ${FOCUS_RING}`}
            aria-label="Scegli versione"
          >
            ▾
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {(versionsQuery.data ?? []).map(v => (
            <DropdownMenuItem key={v.version} onSelect={() => runReindex(v.version)}>
              {v.displayName}
              {v.isCurrent ? ' · default' : ''}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
