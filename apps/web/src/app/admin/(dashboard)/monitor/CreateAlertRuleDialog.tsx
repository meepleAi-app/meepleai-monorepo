'use client';

/**
 * CreateAlertRuleDialog (#1840 SP5 F4-C7) — rebuild mockup `sp5-admin-alerts.html`
 * riga 360-440. Form completo con MetricSelector dinamico, operator select,
 * ChannelChip multi-select + preview pill.
 *
 * Acceptance scenario C (spec doc):
 *   - MetricSelector dropdown popolato da GET /api/v1/admin/metrics/labels
 *   - Operator radio buttons mono (`>`, `>=`, `<`, `<=`, `==`, `!=`)
 *   - Threshold value + unit
 *   - Duration window (default 5m)
 *   - Severity radio (Info/Warning/Critical)
 *   - ChannelChip multi-select Email + Slack
 *   - Preview pill: "WHEN {metric} {op} {value}{unit} FOR {duration} → {channels}"
 *   - Submit → POST /alert-rules + table refresh
 *
 * Nota channels-per-rule: il BE non ha ancora field Channels su AlertRule.
 * I chip selezionati NON vengono mandati al server oggi — sono pre-cablati
 * a chip globali via useAlertChannels. Tracked come follow-up nella spec doc.
 */

import React, { useMemo, useState } from 'react';

import { ChannelChip } from '@/components/admin/alert-rules/ChannelChip';
import { MetricSelector } from '@/components/admin/alert-rules/MetricSelector';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Label } from '@/components/ui/primitives/label';
import { useAlertChannels } from '@/hooks/useAlertChannels';
import { useToast } from '@/hooks/useToast';
import { alertRulesApi } from '@/lib/api/alert-rules.api';
import type { AlertChannelType } from '@/lib/api/schemas/alert-channels.schemas';
import type { CreateAlertRule } from '@/lib/api/schemas/alert-rules.schemas';
import { cn } from '@/lib/utils';

interface Props {
  open: boolean;
  onClose: () => void;
  onCreated: () => void;
}

const SEVERITY_OPTIONS = ['Info', 'Warning', 'Error', 'Critical'] as const;

// Operator UI label → BE wire representation. Today the BE only persists
// `alertType` (no operator field) so the operator is purely a display hint —
// stored ergonomically in the preview pill. Tracked as follow-up.
const OPERATORS = [
  { label: '>', value: '>' },
  { label: '≥', value: '>=' },
  { label: '<', value: '<' },
  { label: '≤', value: '<=' },
  { label: '=', value: '==' },
  { label: '≠', value: '!=' },
] as const;
type OperatorValue = (typeof OPERATORS)[number]['value'];

export function CreateAlertRuleDialog({ open, onClose, onCreated }: Props): React.JSX.Element {
  const { toast } = useToast();
  const { channels: availableChannels } = useAlertChannels();

  const [name, setName] = useState('');
  const [metric, setMetric] = useState('');
  const [operator, setOperator] = useState<OperatorValue>('>');
  const [severity, setSeverity] = useState<CreateAlertRule['severity']>('Warning');
  const [thresholdValue, setThresholdValue] = useState('');
  const [thresholdUnit, setThresholdUnit] = useState('');
  const [durationMinutes, setDurationMinutes] = useState('5');
  const [description, setDescription] = useState('');
  const [selectedChannels, setSelectedChannels] = useState<Set<AlertChannelType>>(
    () => new Set(['email', 'slack'])
  );
  const [submitting, setSubmitting] = useState(false);

  const isValid =
    name.trim().length > 0 && metric.trim().length > 0 && thresholdValue.trim().length > 0;

  const previewText = useMemo(() => {
    const channelLabel =
      selectedChannels.size === 0
        ? 'nessun canale'
        : Array.from(selectedChannels)
            .map(c => c.toUpperCase())
            .join(' + ');
    const metricLabel = metric.trim() || '<metric>';
    const valueLabel = `${thresholdValue || '?'}${thresholdUnit || ''}`;
    const durationLabel = `${durationMinutes || '?'}m`;
    return `WHEN ${metricLabel} ${operator} ${valueLabel} FOR ${durationLabel} → ${channelLabel}`;
  }, [metric, operator, thresholdValue, thresholdUnit, durationMinutes, selectedChannels]);

  const handleToggleChannel = (type: AlertChannelType) => {
    setSelectedChannels(prev => {
      const next = new Set(prev);
      if (next.has(type)) next.delete(type);
      else next.add(type);
      return next;
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid) return;

    setSubmitting(true);
    try {
      // NOTE: operator + selectedChannels not yet persisted (BE follow-up).
      // The alertType field carries the metric name today. When AlertRule
      // gains `Operator` + `Channels` fields, extend the CreateAlertRuleCommand
      // payload here.
      await alertRulesApi.create({
        name: name.trim(),
        alertType: metric.trim(),
        severity,
        thresholdValue: parseFloat(thresholdValue),
        thresholdUnit: thresholdUnit.trim(),
        durationMinutes: parseInt(durationMinutes, 10),
        description: description.trim() || undefined,
      });
      toast({ title: 'Regola creata', description: `"${name}" creata con successo.` });
      onCreated();
      handleReset();
    } catch {
      toast({
        title: 'Errore',
        description: 'Creazione regola fallita.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleReset = () => {
    setName('');
    setMetric('');
    setOperator('>');
    setSeverity('Warning');
    setThresholdValue('');
    setThresholdUnit('');
    setDurationMinutes('5');
    setDescription('');
    setSelectedChannels(new Set(['email', 'slack']));
  };

  const handleOpenChange = (isOpen: boolean) => {
    if (!isOpen) {
      handleReset();
      onClose();
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Nuova regola di alert</DialogTitle>
          <DialogDescription>
            Definisci metrica, soglia e canale notifiche. Il backend valuterà la condizione e
            invierà alert ai canali configurati.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Riga 1: Nome */}
          <div className="space-y-1">
            <Label htmlFor="alert-rule-name">Nome</Label>
            <input
              id="alert-rule-name"
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="es. High Error Rate"
              className="w-full rounded border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              required
            />
          </div>

          {/* Riga 2: MetricSelector */}
          <div className="space-y-1">
            <Label htmlFor="alert-rule-metric">Metrica Prometheus</Label>
            <MetricSelector id="alert-rule-metric" value={metric} onChange={setMetric} required />
          </div>

          {/* Riga 3: Operator + Threshold + Unit (grid 3 col) */}
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div className="space-y-1">
              <Label htmlFor="alert-rule-operator">Operatore</Label>
              <div
                id="alert-rule-operator"
                role="radiogroup"
                aria-label="Operatore condizione"
                className="flex flex-wrap gap-1"
              >
                {OPERATORS.map(op => (
                  <button
                    key={op.value}
                    type="button"
                    role="radio"
                    aria-checked={operator === op.value}
                    onClick={() => setOperator(op.value)}
                    className={cn(
                      'h-9 w-9 rounded border font-mono text-base font-bold transition-colors',
                      operator === op.value
                        ? 'border-primary bg-primary text-primary-foreground'
                        : 'border-border bg-background hover:bg-muted'
                    )}
                  >
                    {op.label}
                  </button>
                ))}
              </div>
            </div>

            <div className="space-y-1">
              <Label htmlFor="alert-rule-threshold">Valore soglia</Label>
              <input
                id="alert-rule-threshold"
                type="number"
                value={thresholdValue}
                onChange={e => setThresholdValue(e.target.value)}
                placeholder="es. 5"
                min={0}
                step="any"
                className="w-full rounded border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                required
              />
            </div>

            <div className="space-y-1">
              <Label htmlFor="alert-rule-unit">Unità</Label>
              <input
                id="alert-rule-unit"
                type="text"
                value={thresholdUnit}
                onChange={e => setThresholdUnit(e.target.value)}
                placeholder="es. % o ms"
                className="w-full rounded border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              />
            </div>
          </div>

          {/* Riga 4: Durata + Severity (grid 2 col) */}
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div className="space-y-1">
              <Label htmlFor="alert-rule-duration">Finestra (minuti)</Label>
              <input
                id="alert-rule-duration"
                type="number"
                value={durationMinutes}
                onChange={e => setDurationMinutes(e.target.value)}
                placeholder="5"
                min={1}
                step={1}
                className="w-full rounded border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                required
              />
            </div>

            <div className="space-y-1">
              <Label htmlFor="alert-rule-severity">Severità</Label>
              <select
                id="alert-rule-severity"
                value={severity}
                onChange={e => setSeverity(e.target.value as CreateAlertRule['severity'])}
                className="w-full rounded border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              >
                {SEVERITY_OPTIONS.map(opt => (
                  <option key={opt} value={opt}>
                    {opt}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Riga 5: Canali multi-select */}
          <div className="space-y-1">
            <Label>Canali notifiche</Label>
            <div role="group" aria-label="Canali notifiche" className="flex flex-wrap gap-2">
              {(['email', 'slack'] as const).map(type => {
                const channelInfo = availableChannels.find(c => c.type === type);
                const isConfigured = !!channelInfo && channelInfo.isEnabled;
                return (
                  <ChannelChip
                    key={type}
                    type={type}
                    interactive
                    selected={selectedChannels.has(type)}
                    status={channelInfo?.lastTestStatus ?? null}
                    tooltip={
                      isConfigured
                        ? undefined
                        : `${type} non configurato — usa il pulsante ⚙ Canali per attivarlo`
                    }
                    onClick={() => handleToggleChannel(type)}
                  />
                );
              })}
            </div>
            {selectedChannels.size === 0 && (
              <p className="text-[11px] text-rose-600 dark:text-rose-400">
                Seleziona almeno un canale per ricevere notifiche
              </p>
            )}
          </div>

          {/* Riga 6: Description */}
          <div className="space-y-1">
            <Label htmlFor="alert-rule-description">Descrizione (opzionale)</Label>
            <textarea
              id="alert-rule-description"
              value={description}
              onChange={e => setDescription(e.target.value)}
              placeholder="Cosa segnala questa regola, cosa fare quando si attiva…"
              rows={2}
              className="w-full rounded border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>

          {/* Preview pill */}
          <div className="rounded border border-dashed border-entity-event/35 bg-entity-event/5 px-3 py-2">
            <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
              Preview
            </p>
            <p className="mt-1 font-mono text-[12px] font-semibold text-foreground">
              {previewText}
            </p>
          </div>

          <DialogFooter>
            <button
              type="button"
              onClick={() => handleOpenChange(false)}
              className="rounded border border-border px-4 py-2 text-sm hover:bg-muted"
            >
              Annulla
            </button>
            <button
              type="submit"
              disabled={!isValid || submitting}
              className="rounded bg-primary px-4 py-2 text-sm text-primary-foreground hover:opacity-90 disabled:opacity-50"
            >
              {submitting ? 'Creazione…' : 'Crea regola'}
            </button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
