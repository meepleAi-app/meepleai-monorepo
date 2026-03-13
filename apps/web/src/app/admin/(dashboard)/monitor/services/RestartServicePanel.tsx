'use client';

/**
 * RestartServicePanel — Service restart control panel with Level 2 confirmation.
 * Issue #133 — SuperAdmin only, requires typing service name to confirm.
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { AlertTriangle, Check, RefreshCw, Shield, Timer, X } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/use-toast';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

/* ------------------------------------------------------------------ */
/*  Types                                                              */
/* ------------------------------------------------------------------ */

interface ServiceDefinition {
  id: string;
  label: string;
  description: string;
}

interface RestartResult {
  message: string;
  estimatedDowntime: string;
}

interface CooldownState {
  /** Remaining seconds */
  remaining: number;
  result: RestartResult | null;
}

/* ------------------------------------------------------------------ */
/*  Constants                                                          */
/* ------------------------------------------------------------------ */

const COOLDOWN_SECONDS = 5 * 60; // 5 minutes

const SERVICES: ServiceDefinition[] = [
  { id: 'api', label: 'API Service', description: 'Core backend API (.NET 9)' },
  {
    id: 'embedding-service',
    label: 'Embedding Service',
    description: 'Sentence-transformer embeddings (Python)',
  },
  {
    id: 'reranker-service',
    label: 'Reranker Service',
    description: 'Cross-encoder reranking (Python)',
  },
  {
    id: 'unstructured-service',
    label: 'Unstructured Service',
    description: 'PDF extraction and processing',
  },
  {
    id: 'smoldocling-service',
    label: 'SmolDocling Service',
    description: 'Document understanding and OCR',
  },
];

/* ------------------------------------------------------------------ */
/*  Helpers                                                            */
/* ------------------------------------------------------------------ */

function formatCooldown(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}

/* ------------------------------------------------------------------ */
/*  Sub-components                                                     */
/* ------------------------------------------------------------------ */

interface ConfirmationDialogProps {
  service: ServiceDefinition;
  onConfirm: () => void;
  onCancel: () => void;
  isLoading: boolean;
}

function ConfirmationDialog({ service, onConfirm, onCancel, isLoading }: ConfirmationDialogProps) {
  const [confirmText, setConfirmText] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  const isMatch = confirmText === service.id;

  return (
    <div
      data-testid={`confirm-dialog-${service.id}`}
      className="mt-3 rounded-lg border border-destructive/30 bg-destructive/5 p-4 dark:border-destructive/40 dark:bg-destructive/10"
    >
      <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-destructive">
        <AlertTriangle className="h-4 w-4" />
        <span>Level 2 Confirmation Required</span>
      </div>

      <p className="mb-3 text-sm text-muted-foreground">
        Type{' '}
        <code className="rounded bg-muted px-1.5 py-0.5 font-mono text-xs font-bold text-foreground">
          {service.id}
        </code>{' '}
        to confirm restart of <strong>{service.label}</strong>.
      </p>

      <input
        ref={inputRef}
        data-testid={`confirm-input-${service.id}`}
        type="text"
        value={confirmText}
        onChange={e => setConfirmText(e.target.value)}
        placeholder={`Type "${service.id}" to confirm`}
        className={cn(
          'mb-3 w-full rounded-md border bg-background px-3 py-2 text-sm outline-none transition-colors',
          'placeholder:text-muted-foreground/60',
          'focus:border-destructive/50 focus:ring-1 focus:ring-destructive/30',
          isMatch && 'border-green-500/50 focus:border-green-500/50 focus:ring-green-500/30'
        )}
        onKeyDown={e => {
          if (e.key === 'Enter' && isMatch && !isLoading) {
            onConfirm();
          }
          if (e.key === 'Escape') {
            onCancel();
          }
        }}
      />

      <div className="flex items-center gap-2">
        <Button
          data-testid={`confirm-cancel-${service.id}`}
          variant="ghost"
          size="sm"
          onClick={onCancel}
          disabled={isLoading}
        >
          <X className="mr-1 h-3 w-3" />
          Cancel
        </Button>
        <Button
          data-testid={`confirm-restart-${service.id}`}
          variant="destructive"
          size="sm"
          disabled={!isMatch || isLoading}
          onClick={onConfirm}
        >
          {isLoading ? (
            <RefreshCw className="mr-1 h-3 w-3 animate-spin" />
          ) : (
            <Check className="mr-1 h-3 w-3" />
          )}
          {isLoading ? 'Restarting...' : 'Confirm Restart'}
        </Button>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  ServiceRow                                                         */
/* ------------------------------------------------------------------ */

interface ServiceRowProps {
  service: ServiceDefinition;
  cooldown: CooldownState | null;
  onRestart: (serviceId: string) => Promise<void>;
}

function ServiceRow({ service, cooldown, onRestart }: ServiceRowProps) {
  const [showConfirm, setShowConfirm] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const handleConfirm = useCallback(async () => {
    setIsLoading(true);
    try {
      await onRestart(service.id);
    } finally {
      setIsLoading(false);
      setShowConfirm(false);
    }
  }, [onRestart, service.id]);

  const handleCancel = useCallback(() => {
    setShowConfirm(false);
  }, []);

  const isCoolingDown = cooldown !== null && cooldown.remaining > 0;

  return (
    <div
      data-testid={`service-row-${service.id}`}
      className="rounded-lg border border-border/40 bg-white/50 p-4 transition-colors dark:bg-zinc-800/50"
    >
      <div className="flex items-center justify-between">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-foreground">{service.label}</span>
            <code className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
              {service.id}
            </code>
          </div>
          <p className="mt-0.5 text-xs text-muted-foreground">{service.description}</p>
        </div>

        <div className="ml-4 flex-shrink-0">
          {isCoolingDown ? (
            <div
              data-testid={`cooldown-${service.id}`}
              className="flex items-center gap-1.5 rounded-md bg-amber-500/10 px-3 py-1.5 text-xs font-medium text-amber-600 dark:text-amber-400"
            >
              <Timer className="h-3.5 w-3.5" />
              <span>Cooldown: {formatCooldown(cooldown.remaining)}</span>
            </div>
          ) : (
            <Button
              data-testid={`restart-btn-${service.id}`}
              variant="destructive"
              size="sm"
              onClick={() => setShowConfirm(true)}
              disabled={showConfirm}
            >
              <RefreshCw className="mr-1 h-3 w-3" />
              Restart
            </Button>
          )}
        </div>
      </div>

      {/* Restart result feedback */}
      {cooldown?.result && (
        <div
          data-testid={`result-${service.id}`}
          className="mt-3 flex items-start gap-2 rounded-md bg-green-500/10 px-3 py-2 text-xs dark:bg-green-500/15"
        >
          <Check className="mt-0.5 h-3.5 w-3.5 flex-shrink-0 text-green-600 dark:text-green-400" />
          <div>
            <p className="font-medium text-green-700 dark:text-green-300">
              {cooldown.result.message}
            </p>
            <p className="mt-0.5 text-green-600/80 dark:text-green-400/80">
              Estimated downtime: {cooldown.result.estimatedDowntime}
            </p>
          </div>
        </div>
      )}

      {/* Level 2 confirmation */}
      {showConfirm && !isCoolingDown && (
        <ConfirmationDialog
          service={service}
          onConfirm={handleConfirm}
          onCancel={handleCancel}
          isLoading={isLoading}
        />
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Main Component                                                     */
/* ------------------------------------------------------------------ */

export function RestartServicePanel() {
  const { toast } = useToast();
  const [cooldowns, setCooldowns] = useState<Record<string, CooldownState>>({});
  const intervalsRef = useRef<Record<string, ReturnType<typeof setInterval>>>({});

  // Clean up intervals on unmount
  useEffect(() => {
    const intervals = intervalsRef.current;
    return () => {
      Object.values(intervals).forEach(clearInterval);
    };
  }, []);

  const startCooldown = useCallback((serviceId: string, result: RestartResult) => {
    // Clear any existing interval for this service
    if (intervalsRef.current[serviceId]) {
      clearInterval(intervalsRef.current[serviceId]);
    }

    setCooldowns(prev => ({
      ...prev,
      [serviceId]: { remaining: COOLDOWN_SECONDS, result },
    }));

    const interval = setInterval(() => {
      setCooldowns(prev => {
        const current = prev[serviceId];
        if (!current || current.remaining <= 1) {
          clearInterval(intervalsRef.current[serviceId]);
          delete intervalsRef.current[serviceId];
          const { [serviceId]: _, ...rest } = prev;
          return rest;
        }
        return {
          ...prev,
          [serviceId]: { ...current, remaining: current.remaining - 1 },
        };
      });
    }, 1000);

    intervalsRef.current[serviceId] = interval;
  }, []);

  const handleRestart = useCallback(
    async (serviceId: string) => {
      try {
        const data = await api.admin.restartService(serviceId);

        startCooldown(serviceId, data);

        toast({
          title: 'Service restart initiated',
          description: `${serviceId}: ${data.message}`,
        });
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Unknown error';
        toast({
          title: 'Restart failed',
          description: message,
          variant: 'destructive',
        });
      }
    },
    [startCooldown, toast]
  );

  return (
    <section
      data-testid="restart-service-panel"
      className="rounded-xl border bg-white/70 p-6 backdrop-blur-md dark:bg-zinc-900/70"
    >
      {/* Header */}
      <div className="mb-5 flex items-center gap-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-destructive/10 dark:bg-destructive/20">
          <Shield className="h-5 w-5 text-destructive" />
        </div>
        <div className="flex items-center gap-2">
          <h2 className="text-lg font-semibold text-foreground">Service Control</h2>
          <Badge
            data-testid="superadmin-badge"
            variant="destructive"
            className="text-[10px] uppercase tracking-wider"
          >
            SuperAdmin
          </Badge>
        </div>
      </div>

      {/* Service list */}
      <div className="flex flex-col gap-3">
        {SERVICES.map(service => (
          <ServiceRow
            key={service.id}
            service={service}
            cooldown={cooldowns[service.id] ?? null}
            onRestart={handleRestart}
          />
        ))}
      </div>
    </section>
  );
}
