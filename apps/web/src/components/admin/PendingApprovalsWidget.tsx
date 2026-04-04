'use client';

import { useState, useEffect, useCallback } from 'react';

import { Check, X, Eye, Clock } from 'lucide-react';
import Link from 'next/link';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface PendingApprovalsWidgetProps {
  /**
   * Optional CSS class name
   */
  className?: string;
  /**
   * Test ID for testing
   */
  'data-testid'?: string;
  /**
   * Maximum number of items to display
   * @default 3
   */
  limit?: number;
}

// ============================================================================
// Component
// ============================================================================

/**
 * PendingApprovalsWidget - Display pending game approvals with quick actions
 *
 * @see Issue #2789 - [Admin Dashboard] Pending Approvals Widget
 */
export function PendingApprovalsWidget({
  className,
  'data-testid': testId = 'pending-approvals-widget',
  limit = 3,
}: PendingApprovalsWidgetProps) {
  // ============================================================================
  // State
  // ============================================================================

  const [games, setGames] = useState<SharedGame[]>([]);
  const [total, setTotal] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [processingIds, setProcessingIds] = useState<Set<string>>(new Set());

  // ============================================================================
  // Data Fetching
  // ============================================================================

  const fetchPendingApprovals = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const data = await api.sharedGames.getPendingApprovals({
        pageNumber: 1,
        pageSize: limit,
      });

      setGames(data.items);
      setTotal(data.total);
    } catch (err) {
      logger.error('Failed to fetch pending approvals:', err);
      setError('Errore nel caricamento delle approvazioni in attesa');
      toast.error('Impossibile caricare le approvazioni');
    } finally {
      setIsLoading(false);
    }
  }, [limit]);

  useEffect(() => {
    fetchPendingApprovals();
  }, [fetchPendingApprovals]);

  // ============================================================================
  // Actions
  // ============================================================================

  const handleApprove = async (gameId: string, gameTitle: string) => {
    setProcessingIds(prev => new Set(prev).add(gameId));

    try {
      await api.sharedGames.approvePublication(gameId);

      // Optimistic update
      setGames(prev => prev.filter(g => g.id !== gameId));
      setTotal(prev => Math.max(0, prev - 1));

      toast.success(`"${gameTitle}" approvato con successo`);
    } catch (err) {
      logger.error('Failed to approve game:', err);
      toast.error("Errore nell'approvazione del gioco");

      // Refetch on error to restore state
      await fetchPendingApprovals();
    } finally {
      setProcessingIds(prev => {
        const next = new Set(prev);
        next.delete(gameId);
        return next;
      });
    }
  };

  const handleReject = async (gameId: string, gameTitle: string) => {
    setProcessingIds(prev => new Set(prev).add(gameId));

    try {
      await api.sharedGames.rejectPublication(gameId, "Rifiutato dall'amministratore");

      // Optimistic update
      setGames(prev => prev.filter(g => g.id !== gameId));
      setTotal(prev => Math.max(0, prev - 1));

      toast.success(`"${gameTitle}" rifiutato`);
    } catch (err) {
      logger.error('Failed to reject game:', err);
      toast.error('Errore nel rifiuto del gioco');

      // Refetch on error to restore state
      await fetchPendingApprovals();
    } finally {
      setProcessingIds(prev => {
        const next = new Set(prev);
        next.delete(gameId);
        return next;
      });
    }
  };

  // ============================================================================
  // Helpers
  // ============================================================================

  const formatRelativeTime = (dateString: string): string => {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 60) {
      return `${diffMins}m fa`;
    } else if (diffHours < 24) {
      return `${diffHours}h fa`;
    } else if (diffDays === 1) {
      return 'ieri';
    } else if (diffDays < 7) {
      return `${diffDays}g fa`;
    } else {
      return date.toLocaleDateString('it-IT', { day: '2-digit', month: '2-digit' });
    }
  };

  // ============================================================================
  // Render: Loading State
  // ============================================================================

  if (isLoading) {
    return (
      <Card className={className} data-testid={testId}>
        <CardHeader className="pb-3">
          <div className="flex items-center justify-between">
            <CardTitle className="text-base font-semibold" data-testid="widget-title">
              In Attesa di Approvazione
            </CardTitle>
            <Skeleton className="h-5 w-8" />
          </div>
        </CardHeader>
        <CardContent>
          <div className="space-y-3" data-testid="widget-skeleton">
            {Array.from({ length: limit }).map((_, i) => (
              <Skeleton key={i} className="h-20" />
            ))}
          </div>
        </CardContent>
      </Card>
    );
  }

  // ============================================================================
  // Render: Error State
  // ============================================================================

  if (error) {
    return (
      <Card className={className} data-testid={testId}>
        <CardHeader className="pb-3">
          <CardTitle className="text-base font-semibold">In Attesa di Approvazione</CardTitle>
        </CardHeader>
        <CardContent>
          <div
            className="p-4 bg-red-50 text-red-700 rounded-lg text-sm"
            role="alert"
            data-testid="widget-error"
          >
            {error}
          </div>
        </CardContent>
      </Card>
    );
  }

  // ============================================================================
  // Render: Main Content
  // ============================================================================

  return (
    <Card className={className} data-testid={testId}>
      {/* Header */}
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base font-semibold">In Attesa di Approvazione</CardTitle>
          <div className="flex items-center gap-2">
            {total > 0 && (
              <Badge variant="outline" className="bg-amber-50 text-amber-700 border-amber-200">
                {total}
              </Badge>
            )}
            {total > limit && (
              <Link
                href="/admin/shared-games?status=pending"
                className="text-xs text-blue-600 hover:text-blue-700 hover:underline"
                data-testid="view-all-link"
              >
                Vedi tutti
              </Link>
            )}
          </div>
        </div>
      </CardHeader>

      {/* Content */}
      <CardContent>
        {games.length === 0 ? (
          // Empty State
          <div className="py-8 text-center text-muted-foreground" data-testid="empty-state">
            <Clock className="h-10 w-10 mx-auto mb-2 text-muted-foreground/50" aria-hidden="true" />
            <p className="text-sm" data-testid="empty-state-message">
              Nessun gioco in attesa di approvazione
            </p>
          </div>
        ) : (
          // Games List
          <div className="space-y-3" role="list">
            {games.map(game => {
              const isProcessing = processingIds.has(game.id);

              return (
                <div
                  key={game.id}
                  role="listitem"
                  className={cn(
                    'p-3 border border-border/50 dark:border-border/30 rounded-lg',
                    'transition-all duration-200',
                    'hover:border-amber-300 hover:bg-amber-50/30',
                    isProcessing && 'opacity-50 pointer-events-none'
                  )}
                  data-testid={`approval-item-${game.id}`}
                >
                  {/* Game Info */}
                  <div className="mb-2">
                    <h4 className="font-medium text-sm text-foreground truncate" title={game.title}>
                      {game.title}
                    </h4>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      {formatRelativeTime(game.createdAt)}
                    </p>
                  </div>

                  {/* Actions */}
                  <div className="flex items-center gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => handleApprove(game.id, game.title)}
                      disabled={isProcessing}
                      className={cn(
                        'flex-1 border-green-200 text-green-700',
                        'hover:bg-green-50 hover:border-green-300'
                      )}
                      data-testid={`approve-btn-${game.id}`}
                    >
                      <Check className="h-3.5 w-3.5 mr-1" aria-hidden="true" />
                      Approva
                    </Button>

                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => handleReject(game.id, game.title)}
                      disabled={isProcessing}
                      className={cn(
                        'flex-1 border-red-200 text-red-700',
                        'hover:bg-red-50 hover:border-red-300'
                      )}
                      data-testid={`reject-btn-${game.id}`}
                    >
                      <X className="h-3.5 w-3.5 mr-1" aria-hidden="true" />
                      Rifiuta
                    </Button>

                    <Link
                      href={`/admin/shared-games/${game.id}`}
                      className={cn(
                        'inline-flex items-center justify-center',
                        'h-8 px-3 rounded-md border border-border/50 dark:border-border/70',
                        'text-sm text-foreground',
                        'hover:bg-muted/50 dark:hover:bg-muted/30 hover:border-border',
                        'transition-colors'
                      )}
                      data-testid={`preview-btn-${game.id}`}
                    >
                      <Eye className="h-3.5 w-3.5" aria-hidden="true" />
                      <span className="sr-only">Visualizza dettagli</span>
                    </Link>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
