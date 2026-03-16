'use client';

import { useQuery } from '@tanstack/react-query';
import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { FileTextIcon, CheckCircleIcon, BoxIcon, ArrowRightIcon } from 'lucide-react';
import Link from 'next/link';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B';
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${sizes[i]}`;
}

export function RecentPdfsWidget() {
  // Filter by processingState='Ready' (fully indexed).
  // Note: PdfListItem has TWO status fields:
  //   - processingState: state-machine value used for filtering (Ready, Pending, Processing, Failed)
  //   - processingStatus: display label for the current pipeline step (Extracting, Chunking, etc.)
  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'hub', 'recent-pdfs'],
    queryFn: () =>
      adminClient.getAllPdfs({
        state: 'Ready',
        sortBy: 'processedAt',
        sortOrder: 'desc',
        pageSize: 5,
      }),
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const pdfs = data?.items ?? [];

  return (
    <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center justify-between text-base">
          <div className="flex items-center gap-2">
            <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-green-500 to-emerald-600 flex items-center justify-center">
              <CheckCircleIcon className="h-4 w-4 text-white" />
            </div>
            <span>Ultimi PDF Elaborati</span>
          </div>
          <Link
            href="/admin/knowledge-base/documents"
            className="text-xs text-amber-600 hover:text-amber-700 dark:text-amber-400 flex items-center gap-1"
            aria-label="Tutti i documenti"
          >
            Tutti <ArrowRightIcon className="h-3 w-3" />
          </Link>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        {isLoading && (
          <div className="space-y-2">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full rounded-lg" />
            ))}
          </div>
        )}

        {!isLoading && pdfs.length === 0 && (
          <p className="text-sm text-muted-foreground text-center py-4">
            Nessun documento elaborato
          </p>
        )}

        {pdfs.map(pdf => (
          <div
            key={pdf.id}
            className="flex items-center gap-3 p-2.5 rounded-lg bg-slate-50/50 dark:bg-zinc-900/50 border border-slate-200/30 dark:border-zinc-700/30"
          >
            <FileTextIcon className="h-4 w-4 text-green-600 dark:text-green-400 shrink-0" />
            <div className="min-w-0 flex-1">
              <p className="text-sm font-medium text-slate-900 dark:text-zinc-100 truncate">
                {pdf.fileName}
              </p>
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                {pdf.gameTitle && (
                  <>
                    <span>{pdf.gameTitle}</span>
                    <span>·</span>
                  </>
                )}
                <span>{formatFileSize(pdf.fileSizeBytes)}</span>
                <span>·</span>
                <span className="flex items-center gap-0.5">
                  <BoxIcon className="h-3 w-3" />
                  {pdf.chunkCount} chunks
                </span>
              </div>
            </div>
            {pdf.processedAt && (
              <span className="text-xs text-muted-foreground shrink-0">
                {formatDistanceToNow(new Date(pdf.processedAt), { addSuffix: true, locale: it })}
              </span>
            )}
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
