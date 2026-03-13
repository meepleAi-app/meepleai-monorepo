'use client';

/**
 * RestartAllPanel — Dependency-ordered sequential restart of all services.
 * Issue #145 — Restart All with dependency order.
 *
 * Restart order: infrastructure first (postgres, redis, qdrant) → services (embedding, reranker,
 * unstructured, smoldocling) → application (api).
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { AlertTriangle, Check, Loader2, RefreshCw, Shield, X } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/use-toast';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

/* ------------------------------------------------------------------ */
/*  Types & Constants                                                  */
/* ------------------------------------------------------------------ */

interface ServiceDef {
  id: string;
  label: string;
  tier: number; // lower = restart first
}

/**
 * Dependency-ordered service tiers (restartable services only).
 * Infrastructure containers (postgres, redis, qdrant) are excluded — they are
 * managed by docker compose and the Docker API is read-only.
 *
 * Tier 1: AI/processing services (independent, restart first)
 * Tier 2: Application layer (depends on AI services)
 */
const SERVICE_TIERS: ServiceDef[] = [
  // Tier 1 — AI/processing services (no internal deps)
  { id: 'embedding-service', label: 'Embedding Service', tier: 1 },
  { id: 'reranker-service', label: 'Reranker Service', tier: 1 },
  { id: 'unstructured-service', label: 'Unstructured Service', tier: 1 },
  { id: 'smoldocling-service', label: 'SmolDocling Service', tier: 1 },
  // Tier 2 — Application (depends on all above)
  { id: 'api', label: 'API Service', tier: 2 },
];

type RestartStatus = 'pending' | 'restarting' | 'done' | 'failed';

interface ServiceProgress {
  id: string;
  label: string;
  status: RestartStatus;
  message?: string;
}

/* ------------------------------------------------------------------ */
/*  Sub-components                                                     */
/* ------------------------------------------------------------------ */

function ServiceProgressRow({ service }: { service: ServiceProgress }) {
  return (
    <div
      data-testid={`restart-progress-${service.id}`}
      className="flex items-center justify-between rounded-lg border px-3 py-2"
    >
      <div className="flex items-center gap-2">
        {service.status === 'restarting' && (
          <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" />
        )}
        {service.status === 'done' && <Check className="h-3.5 w-3.5 text-green-600" />}
        {service.status === 'failed' && <X className="h-3.5 w-3.5 text-red-600" />}
        {service.status === 'pending' && (
          <span className="h-3.5 w-3.5 rounded-full border-2 border-muted-foreground/30" />
        )}
        <span className="text-sm font-medium">{service.label}</span>
      </div>
      <div className="text-xs text-muted-foreground">
        {service.status === 'restarting' && 'Restarting...'}
        {service.status === 'done' && (service.message ?? 'Done')}
        {service.status === 'failed' && (service.message ?? 'Failed')}
        {service.status === 'pending' && 'Waiting'}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Main Component                                                     */
/* ------------------------------------------------------------------ */

export function RestartAllPanel() {
  const { toast } = useToast();
  const [showConfirm, setShowConfirm] = useState(false);
  const [confirmText, setConfirmText] = useState('');
  const [isRestarting, setIsRestarting] = useState(false);
  const [progress, setProgress] = useState<ServiceProgress[]>([]);
  const mountedRef = useRef(true);

  useEffect(() => {
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const isConfirmMatch = confirmText === 'RESTART ALL';

  const executeRestartAll = useCallback(async () => {
    setIsRestarting(true);
    setShowConfirm(false);
    setConfirmText('');

    // Initialize progress
    const initial: ServiceProgress[] = SERVICE_TIERS.map(s => ({
      id: s.id,
      label: s.label,
      status: 'pending',
    }));
    setProgress(initial);

    // Group by tier for sequential execution
    const tiers = [...new Set(SERVICE_TIERS.map(s => s.tier))].sort((a, b) => a - b);

    let allSucceeded = true;

    for (const tier of tiers) {
      const tierServices = SERVICE_TIERS.filter(s => s.tier === tier);

      // Restart all services in same tier sequentially
      for (const service of tierServices) {
        if (!mountedRef.current) return;

        setProgress(prev =>
          prev.map(p => (p.id === service.id ? { ...p, status: 'restarting' } : p))
        );

        try {
          const result = await api.admin.restartService(service.id);
          if (!mountedRef.current) return;
          setProgress(prev =>
            prev.map(p =>
              p.id === service.id ? { ...p, status: 'done', message: result.message } : p
            )
          );
        } catch (err) {
          if (!mountedRef.current) return;
          const message = err instanceof Error ? err.message : 'Unknown error';
          setProgress(prev =>
            prev.map(p => (p.id === service.id ? { ...p, status: 'failed', message } : p))
          );
          allSucceeded = false;
          // Continue with remaining services even if one fails
        }

        // Brief delay between services in same tier
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
    }

    if (!mountedRef.current) return;

    setIsRestarting(false);

    toast({
      title: allSucceeded ? 'All services restarted' : 'Restart completed with errors',
      description: allSucceeded
        ? 'All services have been restarted in dependency order.'
        : 'Some services failed to restart. Check the progress panel.',
      variant: allSucceeded ? 'default' : 'destructive',
    });
  }, [toast]);

  return (
    <section
      data-testid="restart-all-panel"
      className="rounded-xl border bg-white/70 p-6 backdrop-blur-md dark:bg-zinc-900/70"
    >
      {/* Header */}
      <div className="mb-5 flex items-center gap-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-destructive/10 dark:bg-destructive/20">
          <Shield className="h-5 w-5 text-destructive" />
        </div>
        <div className="flex items-center gap-2">
          <h2 className="text-lg font-semibold text-foreground">Restart All Services</h2>
          <Badge
            data-testid="superadmin-badge"
            variant="destructive"
            className="text-[10px] uppercase tracking-wider"
          >
            SuperAdmin
          </Badge>
        </div>
      </div>

      <p className="mb-4 text-sm text-muted-foreground">
        Restarts all services in dependency order: AI services first, then the API. Each tier waits
        for the previous to complete.
      </p>

      {/* Progress */}
      {progress.length > 0 && (
        <div className="mb-4 space-y-2" data-testid="restart-progress">
          {progress.map(service => (
            <ServiceProgressRow key={service.id} service={service} />
          ))}
        </div>
      )}

      {/* Confirmation */}
      {showConfirm && !isRestarting && (
        <div
          data-testid="restart-all-confirm"
          className="mb-4 rounded-lg border border-destructive/30 bg-destructive/5 p-4 dark:border-destructive/40 dark:bg-destructive/10"
        >
          <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-destructive">
            <AlertTriangle className="h-4 w-4" />
            <span>Level 2 Confirmation Required</span>
          </div>

          <p className="mb-3 text-sm text-muted-foreground">
            Type{' '}
            <code className="rounded bg-muted px-1.5 py-0.5 font-mono text-xs font-bold text-foreground">
              RESTART ALL
            </code>{' '}
            to confirm restarting all services.
          </p>

          <input
            autoFocus
            data-testid="restart-all-confirm-input"
            type="text"
            value={confirmText}
            onChange={e => setConfirmText(e.target.value)}
            placeholder='Type "RESTART ALL" to confirm'
            className={cn(
              'mb-3 w-full rounded-md border bg-background px-3 py-2 text-sm outline-none transition-colors',
              'placeholder:text-muted-foreground/60',
              'focus:border-destructive/50 focus:ring-1 focus:ring-destructive/30',
              isConfirmMatch &&
                'border-green-500/50 focus:border-green-500/50 focus:ring-green-500/30'
            )}
            onKeyDown={e => {
              if (e.key === 'Enter' && isConfirmMatch) {
                executeRestartAll();
              }
              if (e.key === 'Escape') {
                setShowConfirm(false);
                setConfirmText('');
              }
            }}
          />

          <div className="flex items-center gap-2">
            <Button
              data-testid="restart-all-cancel"
              variant="ghost"
              size="sm"
              onClick={() => {
                setShowConfirm(false);
                setConfirmText('');
              }}
            >
              <X className="mr-1 h-3 w-3" />
              Cancel
            </Button>
            <Button
              data-testid="restart-all-execute"
              variant="destructive"
              size="sm"
              disabled={!isConfirmMatch}
              onClick={executeRestartAll}
            >
              <RefreshCw className="mr-1 h-3 w-3" />
              Confirm Restart All
            </Button>
          </div>
        </div>
      )}

      {/* Action button */}
      {!showConfirm && !isRestarting && (
        <Button
          data-testid="restart-all-btn"
          variant="destructive"
          onClick={() => setShowConfirm(true)}
          className="gap-1.5"
        >
          <RefreshCw className="h-4 w-4" />
          Restart All Services
        </Button>
      )}

      {isRestarting && (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          <span>Restarting services in dependency order...</span>
        </div>
      )}
    </section>
  );
}
