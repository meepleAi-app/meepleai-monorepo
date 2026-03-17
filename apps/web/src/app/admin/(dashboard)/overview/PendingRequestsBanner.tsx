'use client';

import { useState } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Check, Loader2, X } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { createApiClient } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

export interface AccessRequest {
  id: string;
  email: string;
  status: string;
  requestedAt: string;
}

export interface PendingRequestsBannerProps {
  requests: AccessRequest[];
  totalCount: number;
}

// ============================================================================
// Component
// ============================================================================

export function PendingRequestsBanner({ requests, totalCount }: PendingRequestsBannerProps) {
  const queryClient = useQueryClient();
  const [processing, setProcessing] = useState<Set<string>>(new Set());

  const approveMutation = useMutation({
    mutationFn: async (id: string) => {
      const api = createApiClient();
      await api.accessRequests.approveAccessRequest(id);
    },
    onMutate: (id: string) => {
      setProcessing(prev => new Set(prev).add(id));
    },
    onSettled: (_data, _error, id: string) => {
      setProcessing(prev => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'overview'] });
    },
  });

  const rejectMutation = useMutation({
    mutationFn: async (id: string) => {
      const api = createApiClient();
      await api.accessRequests.rejectAccessRequest(id);
    },
    onMutate: (id: string) => {
      setProcessing(prev => new Set(prev).add(id));
    },
    onSettled: (_data, _error, id: string) => {
      setProcessing(prev => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'overview'] });
    },
  });

  // React Rules of Hooks: all hooks must be called before any early return
  if (requests.length === 0) {
    return null;
  }

  const visibleRequests = requests.slice(0, 5);
  const showViewAll = totalCount > 5;

  return (
    <div className="rounded-2xl border border-amber-200 dark:border-amber-800/50 bg-amber-50 dark:bg-amber-950/30 p-4">
      {/* Header */}
      <div className="flex items-center gap-2 mb-3">
        <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400 shrink-0" />
        <span className="font-quicksand font-semibold text-amber-900 dark:text-amber-200 text-sm">
          {totalCount} richieste di accesso in attesa
        </span>
      </div>

      {/* Request rows */}
      <ul className="space-y-2">
        {visibleRequests.map(request => {
          const isProcessing = processing.has(request.id);
          return (
            <li key={request.id} className="flex items-center justify-between gap-3">
              <span className="font-nunito text-sm text-amber-800 dark:text-amber-300 truncate">
                {request.email}
              </span>
              <div className="flex items-center gap-2 shrink-0">
                <Button
                  size="sm"
                  disabled={isProcessing}
                  aria-label={`Approva ${request.email}`}
                  className="bg-emerald-600 hover:bg-emerald-700 text-white h-7 px-3 text-xs"
                  onClick={() => approveMutation.mutate(request.id)}
                >
                  {isProcessing ? (
                    <Loader2 className="h-3 w-3 animate-spin" />
                  ) : (
                    <Check className="h-3 w-3" />
                  )}
                  <span className="ml-1">Approva</span>
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={isProcessing}
                  aria-label={`Rifiuta ${request.email}`}
                  className="border-red-300 text-red-600 hover:bg-red-50 h-7 px-3 text-xs"
                  onClick={() => rejectMutation.mutate(request.id)}
                >
                  {isProcessing ? (
                    <Loader2 className="h-3 w-3 animate-spin" />
                  ) : (
                    <X className="h-3 w-3" />
                  )}
                  <span className="ml-1">Rifiuta</span>
                </Button>
              </div>
            </li>
          );
        })}
      </ul>

      {/* "Vedi tutte" link */}
      {showViewAll && (
        <div className="mt-3 pt-2 border-t border-amber-200 dark:border-amber-800/30">
          <Link
            href="/admin/users/access-requests"
            className="font-quicksand text-xs font-semibold text-amber-700 dark:text-amber-400 hover:underline"
          >
            Vedi tutte →
          </Link>
        </div>
      )}
    </div>
  );
}

export default PendingRequestsBanner;
