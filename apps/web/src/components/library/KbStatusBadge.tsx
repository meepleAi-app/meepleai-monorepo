/**
 * KbStatusBadge — Knowledge Base indexing status overlay for game cards.
 *
 * Issue #4946: Show PDF indexing progress in wizard and on game card.
 *
 * Displays a small badge indicating whether a game's PDF has been indexed.
 * Polls the backend every 3 s (via usePdfProcessingStatus) and stops when
 * the status is terminal (indexed | failed).
 *
 * Usage:
 * ```tsx
 * <div className="relative">
 *   <PrivateGameCard game={game} ... />
 *   <KbStatusBadge gameId={game.id} />
 * </div>
 * ```
 */

'use client';

import { CheckCircle2, Loader2, XCircle } from 'lucide-react';
import Link from 'next/link';

import { usePdfProcessingStatus } from '@/hooks/queries/usePdfProcessingStatus';
import { useTranslation } from '@/hooks/useTranslation';

interface KbStatusBadgeProps {
  /** UUID of the private game */
  gameId: string;
}

export function KbStatusBadge({ gameId }: KbStatusBadgeProps) {
  const { t } = useTranslation();
  const { data } = usePdfProcessingStatus(gameId);

  if (!data) return null;

  const { status } = data;

  if (status === 'pending' || status === 'processing') {
    return (
      <div
        className={[
          'absolute bottom-2 left-2 right-2',
          'flex items-center gap-1.5 rounded-md px-2.5 py-1.5',
          'bg-amber-500/20 text-amber-700 dark:text-amber-400',
          'border border-amber-500/30 text-xs font-medium',
        ].join(' ')}
        data-testid={`kb-badge-${gameId}`}
        aria-label={t('kbStatus.processing')}
      >
        <Loader2 className="h-3 w-3 animate-spin flex-shrink-0" />
        <span className="truncate">{t('kbStatus.processing')}</span>
      </div>
    );
  }

  if (status === 'indexed') {
    return (
      <div
        className={[
          'absolute bottom-2 left-2 right-2',
          'flex items-center gap-1.5 rounded-md px-2.5 py-1.5',
          'bg-teal-500/20 text-teal-700 dark:text-teal-400',
          'border border-teal-500/30 text-xs font-medium',
        ].join(' ')}
        data-testid={`kb-badge-${gameId}`}
        aria-label={t('kbStatus.indexed')}
      >
        <CheckCircle2 className="h-3 w-3 flex-shrink-0" />
        <span className="flex-1 truncate">{t('kbStatus.indexed')}</span>
        <Link
          href={`/library/games/${gameId}/agent`}
          className="ml-auto underline underline-offset-2 hover:text-teal-600"
          onClick={e => e.stopPropagation()}
          aria-label={t('kbStatus.createAgent')}
        >
          {t('kbStatus.createAgent')} →
        </Link>
      </div>
    );
  }

  if (status === 'failed') {
    return (
      <div
        className={[
          'absolute bottom-2 left-2 right-2',
          'flex items-center gap-1.5 rounded-md px-2.5 py-1.5',
          'bg-red-500/20 text-red-700 dark:text-red-400',
          'border border-red-500/30 text-xs font-medium',
        ].join(' ')}
        data-testid={`kb-badge-${gameId}`}
        aria-label={t('kbStatus.failed')}
      >
        <XCircle className="h-3 w-3 flex-shrink-0" />
        <span className="flex-1 truncate">{t('kbStatus.failed')}</span>
        <span className="ml-auto text-muted-foreground">{t('kbStatus.retry')}</span>
      </div>
    );
  }

  return null;
}
