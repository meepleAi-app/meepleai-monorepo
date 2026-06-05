'use client';

/**
 * AlertRuleList (#1840 SP5 F4-C7) — tabella 7 colonne mockup `sp5-admin-alerts.html` riga 290-340.
 *
 * Re-skin completo del componente legacy (2 colonne semplici). Layout colonne:
 *
 *   1. Regola      — RuleMark icon (entity tinted) + name + id mono
 *   2. Metrica     — MetricChip mono (rule.alertType usato come proxy fino a
 *                    quando AlertRule estenderà con `Metric` field — follow-up)
 *   3. Condizione  — operator (default ">") + threshold value + unit
 *   4. Finestra    — durationMinutes formattato "5m" / "1h"
 *   5. Severità    — status chip colorato (entity-toolkit/warning/event)
 *   6. Canale      — ChannelChipStack basato su useAlertChannels (channels
 *                    globalmente configurati: TestAlertHandler usa hardcoded
 *                    ["slack","email"] BE-side, quindi mostro stessa lista)
 *   7. Attiva      — Switch toggle
 *   8. Azioni      — TestAlert button + Delete (Edit pending)
 *
 * Empty state: messaggio centrato + (futuro) CTA "Crea la prima regola".
 *
 * Note channels-per-rule: lo schema AlertRule non ha ancora un campo Channels
 * per-rule (BE follow-up tracciato da spec doc). Per ora mostro le destinazioni
 * globali leggendole da useAlertChannels — questo riflette il comportamento
 * reale di ChannelDispatchHandler che usa la lista hardcoded.
 */

import { Bell, Trash2, Zap } from 'lucide-react';

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Switch } from '@/components/ui/forms/switch';
import { Button } from '@/components/ui/primitives/button';
import { useAlertChannels } from '@/hooks/useAlertChannels';
import type { AlertRule } from '@/lib/api/schemas/alert-rules.schemas';
import { cn } from '@/lib/utils';

import { ChannelChipStack } from './ChannelChip';

export interface AlertRuleListProps {
  rules: AlertRule[];
  onEdit?: (rule: AlertRule) => void;
  onDelete: (id: string) => void;
  onToggle: (id: string) => void;
  /**
   * #1840 SP5 F4-C7: TestAlert handler. Quando undefined il pulsante è disabilitato
   * con tooltip esplicativo (caso transitorio in CI / preview senza endpoint).
   */
  onTestAlert?: (id: string) => void;
}

// -----------------------------------------------------------------------------
// Cell helpers
// -----------------------------------------------------------------------------

function formatDuration(minutes: number): string {
  if (minutes >= 60 && minutes % 60 === 0) return `${minutes / 60}h`;
  return `${minutes}m`;
}

function severityChipClass(severity: AlertRule['severity']): string {
  switch (severity) {
    case 'Critical':
      return 'border-rose-400/40 bg-rose-50 text-rose-700 dark:border-rose-500/30 dark:bg-rose-950/30 dark:text-rose-300';
    case 'Error':
      return 'border-rose-300/40 bg-rose-50 text-rose-700 dark:border-rose-500/30 dark:bg-rose-950/20 dark:text-rose-300';
    case 'Warning':
      return 'border-amber-400/40 bg-amber-50 text-amber-700 dark:border-amber-500/30 dark:bg-amber-950/30 dark:text-amber-300';
    case 'Info':
    default:
      return 'border-blue-400/40 bg-blue-50 text-blue-700 dark:border-blue-500/30 dark:bg-blue-950/30 dark:text-blue-300';
  }
}

function ruleMarkEntityClass(severity: AlertRule['severity']): string {
  switch (severity) {
    case 'Critical':
    case 'Error':
      return 'bg-entity-event/10 text-entity-event border-entity-event/25';
    case 'Warning':
      return 'bg-entity-agent/10 text-entity-agent border-entity-agent/25';
    case 'Info':
    default:
      return 'bg-entity-chat/10 text-entity-chat border-entity-chat/25';
  }
}

function RuleMark({ severity }: { severity: AlertRule['severity'] }) {
  return (
    <span
      aria-hidden
      className={cn(
        'inline-grid h-7 w-7 shrink-0 place-items-center rounded-md border',
        ruleMarkEntityClass(severity)
      )}
    >
      <Bell className="h-3.5 w-3.5" strokeWidth={2.5} />
    </span>
  );
}

// -----------------------------------------------------------------------------
// Component
// -----------------------------------------------------------------------------

export function AlertRuleList({
  rules,
  onEdit: _onEdit,
  onDelete,
  onToggle,
  onTestAlert,
}: AlertRuleListProps) {
  const { channels } = useAlertChannels();

  // Per-rule channels: oggi statico (vedi doc comment). Build una volta
  // basato sui channel enabled; passato a ogni riga.
  const ruleChannels = channels
    .filter(c => c.isEnabled)
    .map(c => ({
      type: c.type,
      label: undefined as string | undefined,
      status: c.lastTestStatus ?? null,
      tooltip:
        c.lastTestStatus === 'error' ? (c.lastTestMessage ?? `${c.type} disconnesso`) : undefined,
    }));

  return (
    <div
      className="overflow-hidden rounded-xl border border-border bg-card/60"
      data-testid="alert-rule-list"
    >
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/30">
            <TableHead className="w-[24%]">Regola</TableHead>
            <TableHead className="w-[16%]">Metrica</TableHead>
            <TableHead className="w-[14%]">Condizione</TableHead>
            <TableHead className="w-[8%]">Finestra</TableHead>
            <TableHead className="w-[9%]">Severità</TableHead>
            <TableHead className="w-[14%]">Canale</TableHead>
            <TableHead className="w-[6%]">Attiva</TableHead>
            <TableHead className="w-[9%] text-right">Azioni</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rules.length === 0 ? (
            <TableRow>
              <TableCell colSpan={8} className="py-10 text-center text-muted-foreground">
                Nessuna regola configurata · clicca <strong>+ Nuova regola</strong> per iniziare
              </TableCell>
            </TableRow>
          ) : (
            rules.map(rule => (
              <TableRow key={rule.id} data-testid={`alert-rule-row-${rule.id}`}>
                {/* Col 1 — Regola */}
                <TableCell>
                  <div className="flex items-center gap-2.5">
                    <RuleMark severity={rule.severity} />
                    <div className="flex min-w-0 flex-col leading-tight">
                      <span className="truncate font-quicksand text-sm font-bold text-foreground">
                        {rule.name}
                      </span>
                      <span className="mt-0.5 font-mono text-[9.5px] font-semibold tracking-wide text-muted-foreground">
                        {rule.id.slice(0, 8)}…
                      </span>
                    </div>
                  </div>
                </TableCell>
                {/* Col 2 — Metrica */}
                <TableCell>
                  <span className="inline-block rounded border border-border bg-muted/40 px-1.5 py-0.5 font-mono text-[11px] font-semibold text-foreground">
                    {rule.alertType}
                  </span>
                </TableCell>
                {/* Col 3 — Condizione */}
                <TableCell>
                  <span className="font-mono text-[11.5px] font-bold text-foreground">
                    <span className="mx-1 inline-block rounded bg-entity-event/10 px-1.5 py-px text-entity-event">
                      &gt;
                    </span>
                    {rule.thresholdValue}
                    {rule.thresholdUnit}
                  </span>
                </TableCell>
                {/* Col 4 — Finestra */}
                <TableCell className="font-mono text-[11.5px] font-semibold text-muted-foreground">
                  {formatDuration(rule.durationMinutes)}
                </TableCell>
                {/* Col 5 — Severità */}
                <TableCell>
                  <span
                    className={cn(
                      'inline-flex items-center rounded-full border px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wider',
                      severityChipClass(rule.severity)
                    )}
                  >
                    {rule.severity}
                  </span>
                </TableCell>
                {/* Col 6 — Canale */}
                <TableCell>
                  <ChannelChipStack channels={ruleChannels} emptyLabel="—" />
                </TableCell>
                {/* Col 7 — Attiva */}
                <TableCell>
                  <Switch
                    checked={rule.isEnabled}
                    onCheckedChange={() => onToggle(rule.id)}
                    aria-label={`Toggle regola ${rule.name}`}
                    data-testid={`alert-rule-toggle-${rule.id}`}
                  />
                </TableCell>
                {/* Col 8 — Azioni */}
                <TableCell>
                  <div className="flex justify-end gap-1">
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={!onTestAlert}
                      aria-label={`Test alert rule ${rule.name}`}
                      title={
                        onTestAlert
                          ? 'Invia notifica sintetica (dryRun default — usa il dropdown per Live)'
                          : 'Test endpoint non disponibile'
                      }
                      onClick={() => onTestAlert?.(rule.id)}
                      data-testid={`test-alert-${rule.id}`}
                    >
                      <Zap className="h-4 w-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      aria-label={`Elimina regola ${rule.name}`}
                      onClick={() => onDelete(rule.id)}
                      data-testid={`delete-alert-${rule.id}`}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}
