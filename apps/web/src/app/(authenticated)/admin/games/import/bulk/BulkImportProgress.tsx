'use client';

/**
 * BulkImportProgress - Real-time SSE Progress Tracking
 * Issue #4174: Frontend - Bulk Import Progress (SSE)
 *
 * Features:
 * - SSE EventSource connection to backend progress endpoint
 * - Progress bar (completed / total)
 * - Real-time stats: total, completed, failed, current game
 * - Rolling last 5 successes
 * - Cancel import button
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import {
  CheckCircle,
  XCircle,
  Loader2,
  Ban,
  BarChart3,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface BulkImportProgressEvent {
  type: 'progress' | 'complete' | 'error' | 'cancelled';
  data: {
    total: number;
    completed: number;
    failed: number;
    currentGame?: string;
    currentBggId?: number;
    message?: string;
    errors?: Array<{ bggId?: number; gameName?: string; reason: string }>;
  };
}

export type ImportStatus = 'connecting' | 'in_progress' | 'complete' | 'error' | 'cancelled';

export interface BulkImportProgressProps {
  /** The import job ID from the backend */
  jobId: string;
  /** Called when import completes or errors */
  onComplete?: (result: { total: number; completed: number; failed: number }) => void;
  /** Called when user cancels */
  onCancel?: () => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Hook: useBulkImportProgress
// ============================================================================

interface ProgressState {
  status: ImportStatus;
  total: number;
  completed: number;
  failed: number;
  currentGame: string;
  recentSuccesses: string[];
  errors: Array<{ bggId?: number; gameName?: string; reason: string }>;
  message: string;
}

const initialState: ProgressState = {
  status: 'connecting',
  total: 0,
  completed: 0,
  failed: 0,
  currentGame: '',
  recentSuccesses: [],
  errors: [],
  message: 'Connecting...',
};

export function useBulkImportProgress(jobId: string) {
  const [state, setState] = useState<ProgressState>(initialState);
  const eventSourceRef = useRef<EventSource | null>(null);
  const recentRef = useRef<string[]>([]);

  const disconnect = useCallback(() => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
  }, []);

  const cancel = useCallback(async () => {
    disconnect();
    try {
      await fetch(`/api/v1/admin/games/bulk-import/${jobId}/cancel`, { method: 'POST' });
    } catch {
      // Best effort cancel
    }
    setState((prev) => ({ ...prev, status: 'cancelled', message: 'Import cancelled' }));
  }, [jobId, disconnect]);

  useEffect(() => {
    if (!jobId) return;

    const url = `/api/v1/admin/games/bulk-import/${jobId}/progress`;
    const es = new EventSource(url, { withCredentials: true });
    eventSourceRef.current = es;

    es.onopen = () => {
      setState((prev) => ({ ...prev, status: 'in_progress', message: 'Import in progress...' }));
    };

    es.onmessage = (event) => {
      try {
        const parsed: BulkImportProgressEvent = JSON.parse(event.data);
        const { type, data } = parsed;

        if (type === 'progress') {
          if (data.currentGame) {
            recentRef.current = [data.currentGame, ...recentRef.current].slice(0, 5);
          }
          setState((prev) => ({
            ...prev,
            status: 'in_progress',
            total: data.total,
            completed: data.completed,
            failed: data.failed,
            currentGame: data.currentGame ?? prev.currentGame,
            recentSuccesses: [...recentRef.current],
            message: `Processing ${data.completed}/${data.total}...`,
          }));
        } else if (type === 'complete') {
          disconnect();
          setState((prev) => ({
            ...prev,
            status: 'complete',
            total: data.total,
            completed: data.completed,
            failed: data.failed,
            errors: data.errors ?? prev.errors,
            message: 'Import complete!',
          }));
        } else if (type === 'error') {
          disconnect();
          setState((prev) => ({
            ...prev,
            status: 'error',
            errors: data.errors ?? prev.errors,
            message: data.message ?? 'Import failed',
          }));
        } else if (type === 'cancelled') {
          disconnect();
          setState((prev) => ({ ...prev, status: 'cancelled', message: 'Import cancelled' }));
        }
      } catch {
        // Ignore malformed events
      }
    };

    es.onerror = () => {
      // EventSource will auto-reconnect; if it closes, mark as error
      if (es.readyState === EventSource.CLOSED) {
        disconnect();
        setState((prev) => {
          if (prev.status === 'complete' || prev.status === 'cancelled') return prev;
          return { ...prev, status: 'error', message: 'Connection lost' };
        });
      }
    };

    return () => {
      disconnect();
    };
  }, [jobId, disconnect]);

  return { ...state, cancel, disconnect };
}

// ============================================================================
// BulkImportProgress Component
// ============================================================================

export function BulkImportProgress({
  jobId,
  onComplete,
  onCancel,
  className,
}: BulkImportProgressProps) {
  const {
    status, total, completed, failed, currentGame,
    recentSuccesses, message, cancel,
  } = useBulkImportProgress(jobId);

  const percentage = total > 0 ? Math.round((completed / total) * 100) : 0;

  useEffect(() => {
    if (status === 'complete' && onComplete) {
      onComplete({ total, completed, failed });
    }
  }, [status, total, completed, failed, onComplete]);

  const handleCancel = useCallback(async () => {
    await cancel();
    onCancel?.();
  }, [cancel, onCancel]);

  return (
    <Card className={cn('w-full', className)} data-testid="bulk-import-progress">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <StatusIcon status={status} />
            <CardTitle className="text-base">
              {status === 'connecting' && 'Connecting...'}
              {status === 'in_progress' && 'Import In Progress'}
              {status === 'complete' && 'Import Complete'}
              {status === 'error' && 'Import Failed'}
              {status === 'cancelled' && 'Import Cancelled'}
            </CardTitle>
          </div>
          {status === 'in_progress' && (
            <Button
              variant="outline"
              size="sm"
              onClick={handleCancel}
              data-testid="cancel-import-button"
            >
              <Ban className="h-4 w-4 mr-1" />
              Cancel
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Progress Bar */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <span className="text-sm text-muted-foreground">{message}</span>
            <span className="text-sm font-medium" data-testid="progress-percentage">
              {percentage}%
            </span>
          </div>
          <Progress
            value={percentage}
            className="h-3 [&>div]:bg-primary"
            aria-label={`Import progress: ${percentage}%`}
            data-testid="progress-bar"
          />
        </div>

        {/* Stats Grid */}
        <div className="grid grid-cols-3 gap-3" data-testid="stats-grid">
          <StatCard label="Total" value={total} variant="default" />
          <StatCard label="Completed" value={completed} variant="success" />
          <StatCard label="Failed" value={failed} variant="error" />
        </div>

        {/* Current Game */}
        {currentGame && status === 'in_progress' && (
          <div className="flex items-center gap-2 text-sm" data-testid="current-game">
            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
            <span className="text-muted-foreground">Processing:</span>
            <span className="font-medium truncate">{currentGame}</span>
          </div>
        )}

        {/* Recent Successes */}
        {recentSuccesses.length > 0 && (
          <div data-testid="recent-successes">
            <p className="text-xs text-muted-foreground mb-2 flex items-center gap-1">
              <BarChart3 className="h-3 w-3" />
              Recently imported:
            </p>
            <div className="flex flex-wrap gap-1.5">
              {recentSuccesses.map((name, i) => (
                <Badge key={`${name}-${i}`} variant="secondary" className="text-xs">
                  {name}
                </Badge>
              ))}
            </div>
          </div>
        )}

        {/* Error Message */}
        {status === 'error' && (
          <Alert variant="destructive" data-testid="error-alert">
            <AlertDescription>{message}</AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}

// ============================================================================
// Helper Components
// ============================================================================

function StatusIcon({ status }: { status: ImportStatus }) {
  switch (status) {
    case 'connecting':
      return <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" data-testid="status-connecting" />;
    case 'in_progress':
      return <Loader2 className="h-5 w-5 animate-spin text-primary" data-testid="status-in-progress" />;
    case 'complete':
      return <CheckCircle className="h-5 w-5 text-green-500" data-testid="status-complete" />;
    case 'error':
      return <XCircle className="h-5 w-5 text-destructive" data-testid="status-error" />;
    case 'cancelled':
      return <Ban className="h-5 w-5 text-muted-foreground" data-testid="status-cancelled" />;
  }
}

function StatCard({ label, value, variant }: { label: string; value: number; variant: 'default' | 'success' | 'error' }) {
  const colors = {
    default: 'border-border',
    success: 'border-green-500/30 bg-green-500/5',
    error: 'border-red-500/30 bg-red-500/5',
  };
  const textColors = {
    default: '',
    success: 'text-green-600 dark:text-green-400',
    error: 'text-red-600 dark:text-red-400',
  };

  return (
    <div className={cn('rounded-lg border p-3 text-center', colors[variant])} data-testid={`stat-${label.toLowerCase()}`}>
      <p className={cn('text-2xl font-bold', textColors[variant])}>{value}</p>
      <p className="text-xs text-muted-foreground">{label}</p>
    </div>
  );
}
