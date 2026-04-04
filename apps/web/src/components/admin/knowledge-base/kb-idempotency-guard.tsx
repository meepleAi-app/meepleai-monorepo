'use client';

/**
 * KbIdempotencyGuard
 * Shows a warning banner when a game already has an indexed KB (with auto-backup).
 * Prevents accidental re-processing of already indexed games.
 */

import { useQuery } from '@tanstack/react-query';
import { AlertTriangleIcon, ArchiveIcon, ExternalLinkIcon } from 'lucide-react';
import Link from 'next/link';

import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

interface KbIdempotencyGuardProps {
  gameId?: string;
}

export function KbIdempotencyGuard({ gameId }: KbIdempotencyGuardProps) {
  const { data } = useQuery({
    queryKey: ['admin', 'kb-game-statuses'],
    queryFn: () => adminClient.getGameKbStatuses(),
    staleTime: 60_000,
    enabled: !!gameId,
  });

  if (!gameId || !data) return null;

  const gameStatus = data.items.find(item => item.gameId.toLowerCase() === gameId.toLowerCase());

  if (!gameStatus || gameStatus.kbStatus === 'none') return null;

  const isComplete = gameStatus.kbStatus === 'complete';
  const lastIndexed = gameStatus.latestIndexedAt
    ? new Date(gameStatus.latestIndexedAt).toLocaleDateString('it-IT', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      })
    : null;

  return (
    <div
      className={`rounded-xl border p-4 flex items-start gap-3 ${
        isComplete
          ? 'bg-amber-50 border-amber-200 dark:bg-amber-900/20 dark:border-amber-800'
          : 'bg-blue-50 border-blue-200 dark:bg-blue-900/20 dark:border-blue-800'
      }`}
    >
      <AlertTriangleIcon
        className={`h-5 w-5 mt-0.5 flex-shrink-0 ${
          isComplete ? 'text-amber-600 dark:text-amber-400' : 'text-blue-600 dark:text-blue-400'
        }`}
      />
      <div className="flex-1">
        <p
          className={`text-sm font-semibold ${
            isComplete ? 'text-amber-700 dark:text-amber-300' : 'text-blue-700 dark:text-blue-300'
          }`}
        >
          {isComplete
            ? 'Questo gioco ha già una Knowledge Base completa'
            : 'Questo gioco ha una Knowledge Base parziale'}
        </p>
        <p
          className={`text-xs mt-1 ${
            isComplete ? 'text-amber-600 dark:text-amber-400' : 'text-blue-600 dark:text-blue-400'
          }`}
        >
          {gameStatus.documentCount} {gameStatus.documentCount === 1 ? 'documento' : 'documenti'}{' '}
          indicizzati · {gameStatus.totalChunks.toLocaleString('it-IT')} chunks
          {lastIndexed && ` · ultimo aggiornamento ${lastIndexed}`}
        </p>
        {gameStatus.hasAutoBackup && (
          <div className="flex items-center gap-2 mt-2">
            <Link
              href={`/admin/knowledge-base/snapshots`}
              className={`inline-flex items-center gap-1.5 text-xs font-medium ${
                isComplete
                  ? 'text-amber-700 dark:text-amber-300 hover:text-amber-800 dark:hover:text-amber-200'
                  : 'text-blue-700 dark:text-blue-300 hover:text-blue-800 dark:hover:text-blue-200'
              }`}
            >
              <ArchiveIcon className="h-3.5 w-3.5" />
              Ripristina da snapshot
              <ExternalLinkIcon className="h-3 w-3" />
            </Link>
            <span
              className={`text-xs ${
                isComplete
                  ? 'text-amber-500 dark:text-amber-500'
                  : 'text-blue-500 dark:text-blue-500'
              }`}
            >
              (evita di rielaborare i PDF)
            </span>
          </div>
        )}
      </div>
    </div>
  );
}
