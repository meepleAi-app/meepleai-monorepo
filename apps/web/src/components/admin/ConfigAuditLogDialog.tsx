/**
 * ConfigAuditLogDialog (Issue #1836)
 *
 * Aggregate audit log dialog for a set of configurations (the FeatureFlag set
 * for this iteration). Fan-outs `api.config.getHistory(id)` in parallel,
 * merges entries by date, and renders a single chronological timeline.
 *
 * This is intentionally a thin client-side aggregation — the backend does not
 * yet expose a `/configurations/history` endpoint that returns a unified feed.
 * Once it does, replace the fan-out with a single call (see [[project_issue_1836_config_flags_wip]]).
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { Clock } from 'lucide-react';
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

export interface ConfigAuditLogDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /**
   * Configuration IDs to aggregate (typically the visible FeatureFlag set).
   * Empty list → empty state, no fetches.
   */
  configurationIds: readonly string[];
  /**
   * Per-config history fetch limit. Defaults to 10. The aggregate is then
   * truncated to {@link maxEntries} after merging.
   */
  perConfigLimit?: number;
  /**
   * Maximum entries shown after merging. Defaults to 50.
   */
  maxEntries?: number;
  /**
   * Friendly title shown in the dialog header.
   */
  title?: string;
}

export function ConfigAuditLogDialog({
  open,
  onOpenChange,
  configurationIds,
  perConfigLimit = 10,
  maxEntries = 50,
  title = 'Audit log — Feature flags',
}: ConfigAuditLogDialogProps) {
  const [entries, setEntries] = useState<ConfigurationHistoryDto[]>([]);
  const [loading, setLoading] = useState(false);

  const fetchEntries = useCallback(async () => {
    if (configurationIds.length === 0) {
      setEntries([]);
      return;
    }
    setLoading(true);
    setEntries([]);
    try {
      const results = await Promise.all(
        configurationIds.map(id =>
          api.config.getHistory(id, perConfigLimit).catch(() => [] as ConfigurationHistoryDto[])
        )
      );
      const flat = results.flat();
      flat.sort((a, b) => b.changedAt.localeCompare(a.changedAt));
      setEntries(flat.slice(0, maxEntries));
    } catch {
      toast.error('Failed to load audit log');
    } finally {
      setLoading(false);
    }
  }, [configurationIds, perConfigLimit, maxEntries]);

  useEffect(() => {
    if (open) {
      fetchEntries();
    }
  }, [open, fetchEntries]);

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

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-w-2xl max-h-[80vh] overflow-hidden flex flex-col"
        data-testid="config-audit-log-dialog"
      >
        <DialogHeader>
          <DialogTitle className="font-quicksand flex items-center gap-2">
            <Clock className="h-5 w-5 text-muted-foreground" />
            {title}
          </DialogTitle>
        </DialogHeader>

        <div className="py-2 overflow-y-auto flex-1">
          {loading ? (
            <div
              className="flex items-center justify-center py-8 text-muted-foreground text-sm"
              data-testid="config-audit-log-loading"
            >
              Caricamento…
            </div>
          ) : entries.length === 0 ? (
            <div
              className="text-center py-8 text-muted-foreground text-sm"
              data-testid="config-audit-log-empty"
            >
              {configurationIds.length === 0 ? 'No flags to audit.' : 'No history available yet.'}
            </div>
          ) : (
            <ul className="space-y-0" data-testid="config-audit-log-entries">
              {entries.map((entry, idx) => {
                const isCreation = entry.changeReason === 'Configuration created';
                const isLast = idx === entries.length - 1;
                const displayKey = entry.key.replace('Features:', '');

                return (
                  <li
                    key={entry.id}
                    className="relative flex gap-3"
                    data-testid={`config-audit-log-entry-${entry.id}`}
                  >
                    {/* Timeline rail */}
                    <div className="flex flex-col items-center">
                      <div className="h-3 w-3 rounded-full border-2 border-primary bg-primary mt-1.5" />
                      {!isLast && <div className="w-px flex-1 bg-border" />}
                    </div>

                    {/* Content */}
                    <div className="pb-4 flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1 flex-wrap">
                        <span className="font-medium text-foreground text-sm truncate">
                          {displayKey}
                        </span>
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
                            Created with value{' '}
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
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            data-testid="btn-close-audit-log"
          >
            Chiudi
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
