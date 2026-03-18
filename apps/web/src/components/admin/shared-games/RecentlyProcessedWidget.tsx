/**
 * RecentlyProcessedWidget — Collapsible card showing the 10 most recent PDFs
 * processed across all shared games. Polls every 15 seconds.
 *
 * Collapse state is persisted in localStorage.
 */

'use client';

import { useCallback, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow } from 'date-fns';
import { it as itLocale } from 'date-fns/locale';
import {
  ChevronDownIcon,
  ChevronUpIcon,
  ExternalLinkIcon,
  FileTextIcon,
  Loader2Icon,
  RefreshCwIcon,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { api } from '@/lib/api';
import { type RecentlyProcessedDocument } from '@/lib/api/clients/sharedGamesClient';

// ─── localStorage key ────────────────────────────────────────────────────────

const COLLAPSE_KEY = 'admin:recentPdfs:collapsed';

function readCollapsed(): boolean {
  if (typeof window === 'undefined') return false;
  return localStorage.getItem(COLLAPSE_KEY) === 'true';
}

// ─── Status helpers ──────────────────────────────────────────────────────────

interface StatusConfig {
  label: string;
  variant: 'default' | 'destructive' | 'secondary';
  spinning: boolean;
}

function getStatusConfig(state: string): StatusConfig {
  if (state === 'Ready') return { label: 'Indicizzato', variant: 'default', spinning: false };
  if (state === 'Failed') return { label: 'Fallito', variant: 'destructive', spinning: false };
  return { label: 'Elaborazione', variant: 'secondary', spinning: true };
}

// ─── Component ───────────────────────────────────────────────────────────────

export function RecentlyProcessedWidget() {
  const [collapsed, setCollapsed] = useState(readCollapsed);
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'shared-games', 'recently-processed'],
    queryFn: () => api.sharedGames.getRecentlyProcessed(10),
    refetchInterval: 15_000,
  });

  const retryMutation = useMutation({
    mutationFn: (jobId: string) => api.admin.retryJob(jobId),
    onSuccess: () => {
      toast.success('Riprocessamento avviato');
      queryClient.invalidateQueries({
        queryKey: ['admin', 'shared-games', 'recently-processed'],
      });
    },
    onError: () => {
      toast.error('Errore durante il riprocessamento');
    },
  });

  const toggleCollapse = useCallback(() => {
    setCollapsed(prev => {
      const next = !prev;
      localStorage.setItem(COLLAPSE_KEY, String(next));
      return next;
    });
  }, []);

  const documents = data ?? [];

  // Hide entirely when no documents and not loading
  if (!isLoading && documents.length === 0) {
    return null;
  }

  return (
    <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center justify-between text-base">
          <div className="flex items-center gap-2">
            <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center">
              <FileTextIcon className="h-4 w-4 text-white" />
            </div>
            <span className="font-quicksand">Ultimi PDF Elaborati</span>
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={toggleCollapse}
            aria-label={collapsed ? 'Espandi' : 'Comprimi'}
          >
            {collapsed ? (
              <ChevronDownIcon className="h-4 w-4" />
            ) : (
              <ChevronUpIcon className="h-4 w-4" />
            )}
          </Button>
        </CardTitle>
      </CardHeader>

      {!collapsed && (
        <CardContent className="pt-0">
          <div className="overflow-x-auto">
            <table className="w-full text-sm" role="table">
              <thead>
                <tr className="text-left text-xs text-muted-foreground border-b border-slate-200/50 dark:border-zinc-700/50">
                  <th className="pb-2 font-medium">PDF</th>
                  <th className="pb-2 font-medium">Gioco</th>
                  <th className="pb-2 font-medium">Stato</th>
                  <th className="pb-2 font-medium">Tempo</th>
                  <th className="pb-2 font-medium text-right">Azione</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200/30 dark:divide-zinc-700/30">
                {documents.map(doc => (
                  <DocumentRow
                    key={doc.pdfDocumentId}
                    doc={doc}
                    onRetry={retryMutation.mutate}
                    retryingJobId={
                      retryMutation.isPending ? (retryMutation.variables as string) : null
                    }
                  />
                ))}
              </tbody>
            </table>
          </div>

          {/* Footer link */}
          <div className="mt-4 text-center">
            <Link
              href="/admin/knowledge-base/documents"
              className="text-xs text-amber-600 hover:text-amber-700 dark:text-amber-400 inline-flex items-center gap-1"
            >
              Vedi tutti <ExternalLinkIcon className="h-3 w-3" />
            </Link>
          </div>
        </CardContent>
      )}
    </Card>
  );
}

// ─── Row sub-component ───────────────────────────────────────────────────────

interface DocumentRowProps {
  doc: RecentlyProcessedDocument;
  onRetry: (jobId: string) => void;
  retryingJobId: string | null;
}

function DocumentRow({ doc, onRetry, retryingJobId }: DocumentRowProps) {
  const status = getStatusConfig(doc.processingState);

  return (
    <tr className="hover:bg-slate-50/50 dark:hover:bg-zinc-900/30">
      {/* PDF */}
      <td className="py-2 pr-3">
        <div className="flex items-center gap-2 min-w-0">
          <FileTextIcon className="h-4 w-4 text-slate-500 shrink-0" />
          <span className="truncate max-w-[180px]" title={doc.fileName}>
            {doc.fileName}
          </span>
        </div>
      </td>

      {/* Gioco */}
      <td className="py-2 pr-3">
        <Link
          href={`/admin/shared-games/${doc.sharedGameId}`}
          className="flex items-center gap-2 text-amber-600 hover:text-amber-700 dark:text-amber-400 min-w-0"
        >
          {doc.thumbnailUrl ? (
            <Image
              src={doc.thumbnailUrl}
              alt=""
              width={24}
              height={24}
              className="rounded shrink-0"
            />
          ) : null}
          <span className="truncate max-w-[140px]">{doc.gameName}</span>
        </Link>
      </td>

      {/* Stato */}
      <td className="py-2 pr-3">
        <Badge variant={status.variant} className="gap-1">
          {status.spinning && <Loader2Icon className="h-3 w-3 animate-spin" />}
          {status.label}
        </Badge>
      </td>

      {/* Tempo */}
      <td className="py-2 pr-3 text-xs text-muted-foreground whitespace-nowrap">
        {formatDistanceToNow(new Date(doc.timestamp), { addSuffix: true, locale: itLocale })}
      </td>

      {/* Azione */}
      <td className="py-2 text-right">
        <ActionCell doc={doc} onRetry={onRetry} retryingJobId={retryingJobId} />
      </td>
    </tr>
  );
}

// ─── Action cell ─────────────────────────────────────────────────────────────

interface ActionCellProps {
  doc: RecentlyProcessedDocument;
  onRetry: (jobId: string) => void;
  retryingJobId: string | null;
}

function ActionCell({ doc, onRetry, retryingJobId }: ActionCellProps) {
  if (doc.processingState === 'Ready') {
    return (
      <Button variant="ghost" size="sm" asChild>
        <Link href={`/admin/shared-games/${doc.sharedGameId}`}>Vai al gioco</Link>
      </Button>
    );
  }

  if (doc.processingState === 'Failed' && doc.canRetry && doc.jobId) {
    const isRetrying = retryingJobId === doc.jobId;
    return (
      <Button variant="outline" size="sm" onClick={() => onRetry(doc.jobId!)} disabled={isRetrying}>
        {isRetrying ? (
          <Loader2Icon className="h-3 w-3 animate-spin mr-1" />
        ) : (
          <RefreshCwIcon className="h-3 w-3 mr-1" />
        )}
        Riprova
      </Button>
    );
  }

  // Processing or Failed without retry
  return (
    <Button variant="ghost" size="sm" asChild>
      <Link href="/admin/knowledge-base/documents">Vai alla coda</Link>
    </Button>
  );
}
