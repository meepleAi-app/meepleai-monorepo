'use client';

import { useCallback, useEffect, useState } from 'react';

import { Clock, RotateCcw } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { ConfigurationHistoryDto } from '@/lib/api/schemas/config.schemas';

interface ConfigHistoryDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  configId: string;
  configKey: string;
  onRollbackComplete: () => void;
}

export function ConfigHistoryDialog({
  open,
  onOpenChange,
  configId,
  configKey,
  onRollbackComplete,
}: ConfigHistoryDialogProps) {
  const [history, setHistory] = useState<ConfigurationHistoryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [rollingBack, setRollingBack] = useState(false);

  const fetchHistory = useCallback(async () => {
    if (!configId) return;
    setHistory([]);
    setLoading(true);
    try {
      const data = await api.config.getHistory(configId, 20);
      setHistory(data);
    } catch {
      setHistory([]);
      toast.error('Errore nel caricamento della cronologia');
    } finally {
      setLoading(false);
    }
  }, [configId]);

  useEffect(() => {
    if (open) {
      fetchHistory();
    }
  }, [open, fetchHistory]);

  const canRollback =
    history.length > 0 &&
    history[0].changeReason !== 'Configuration created' &&
    history[0].oldValue !== '';

  const handleRollback = async () => {
    if (!canRollback) return;

    const entry = history[0];
    const confirmed = window.confirm(`Ripristinare il valore precedente '${entry.oldValue}'?`);
    if (!confirmed) return;

    setRollingBack(true);
    try {
      await api.config.rollback(configId, entry.version);
      toast.success('Configurazione ripristinata');
      onRollbackComplete();
      onOpenChange(false);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Errore durante il rollback';
      toast.error(msg);
    } finally {
      setRollingBack(false);
    }
  };

  const formatDate = (iso: string) => {
    try {
      return new Intl.DateTimeFormat('it-IT', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(iso));
    } catch {
      return iso;
    }
  };

  const displayKey = configKey.replace('Features:', '');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg" data-testid="config-history-dialog">
        <DialogHeader>
          <DialogTitle className="font-quicksand flex items-center gap-2">
            <Clock className="h-5 w-5 text-muted-foreground" />
            Cronologia: {displayKey}
          </DialogTitle>
        </DialogHeader>

        <div className="py-2">
          {loading ? (
            <div className="flex items-center justify-center py-8 text-muted-foreground text-sm">
              Caricamento…
            </div>
          ) : history.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground text-sm">
              Nessuna cronologia disponibile.
            </div>
          ) : (
            <div className="space-y-0">
              {history.map((entry, idx) => {
                const isFirst = idx === 0;
                const isCreation = entry.changeReason === 'Configuration created';
                const isLast = idx === history.length - 1;

                return (
                  <div
                    key={entry.id}
                    className="relative flex gap-3"
                    data-testid={`history-entry-${entry.version}`}
                  >
                    {/* Timeline dot */}
                    <div className="flex flex-col items-center">
                      <div
                        className={`h-3 w-3 rounded-full border-2 mt-1.5 ${
                          isFirst
                            ? 'border-primary bg-primary'
                            : 'border-muted-foreground/40 bg-background'
                        }`}
                      />
                      {!isLast && <div className="w-px flex-1 bg-border" />}
                    </div>

                    {/* Content */}
                    <div className="pb-4 flex-1">
                      <div className="flex items-center gap-2 mb-1">
                        <Badge variant="outline" className="text-xs font-mono">
                          v{entry.version}
                        </Badge>
                        <span className="text-xs text-muted-foreground">
                          {formatDate(entry.changedAt)}
                        </span>
                      </div>

                      <div className="text-sm">
                        {isCreation ? (
                          <span className="text-muted-foreground">
                            Creato con valore{' '}
                            <code className="px-1 py-0.5 rounded bg-muted text-xs">
                              {entry.newValue}
                            </code>
                          </span>
                        ) : (
                          <span>
                            <code className="px-1 py-0.5 rounded bg-red-50 dark:bg-red-950/30 text-red-700 dark:text-red-300 text-xs">
                              {entry.oldValue}
                            </code>
                            <span className="mx-1.5 text-muted-foreground">&rarr;</span>
                            <code className="px-1 py-0.5 rounded bg-green-50 dark:bg-green-950/30 text-green-700 dark:text-green-300 text-xs">
                              {entry.newValue}
                            </code>
                          </span>
                        )}
                      </div>

                      <p className="text-xs text-muted-foreground mt-0.5">{entry.changeReason}</p>

                      {isFirst && canRollback && (
                        <Button
                          variant="outline"
                          size="sm"
                          className="mt-2"
                          onClick={handleRollback}
                          disabled={rollingBack}
                          data-testid="btn-rollback"
                        >
                          <RotateCcw className="h-3 w-3 mr-1" />
                          {rollingBack ? 'Ripristino…' : 'Rollback'}
                        </Button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            data-testid="btn-close-history"
          >
            Chiudi
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
