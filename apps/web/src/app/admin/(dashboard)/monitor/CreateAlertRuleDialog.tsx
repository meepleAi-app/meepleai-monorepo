'use client';

import React, { useState } from 'react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Label } from '@/components/ui/primitives/label';
import { useToast } from '@/hooks/useToast';
import { alertRulesApi } from '@/lib/api/alert-rules.api';
import type { CreateAlertRule } from '@/lib/api/schemas/alert-rules.schemas';

interface Props {
  open: boolean;
  onClose: () => void;
  onCreated: () => void;
}

const SEVERITY_OPTIONS = ['Info', 'Warning', 'Error', 'Critical'] as const;

export function CreateAlertRuleDialog({ open, onClose, onCreated }: Props): React.JSX.Element {
  const { toast } = useToast();

  const [name, setName] = useState('');
  const [alertType, setAlertType] = useState('');
  const [severity, setSeverity] = useState<CreateAlertRule['severity']>('Warning');
  const [thresholdValue, setThresholdValue] = useState('');
  const [thresholdUnit, setThresholdUnit] = useState('');
  const [durationMinutes, setDurationMinutes] = useState('');
  const [description, setDescription] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const isValid = name.trim().length > 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid) return;

    setSubmitting(true);
    try {
      await alertRulesApi.create({
        name: name.trim(),
        alertType: alertType.trim(),
        severity,
        thresholdValue: parseFloat(thresholdValue),
        thresholdUnit: thresholdUnit.trim(),
        durationMinutes: parseInt(durationMinutes, 10),
        description: description.trim() || undefined,
      });
      toast({ title: 'Alert rule created', description: `"${name}" created successfully.` });
      onCreated();
      handleReset();
    } catch {
      toast({
        title: 'Error',
        description: 'Failed to create alert rule.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleReset = () => {
    setName('');
    setAlertType('');
    setSeverity('Warning');
    setThresholdValue('');
    setThresholdUnit('');
    setDurationMinutes('');
    setDescription('');
  };

  const handleOpenChange = (isOpen: boolean) => {
    if (!isOpen) {
      handleReset();
      onClose();
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Crea nuova regola di alert</DialogTitle>
          <DialogDescription>
            Configura i parametri per la nuova regola di monitoraggio.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="alert-rule-name">Nome</Label>
            <input
              id="alert-rule-name"
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="es. High Error Rate"
              className="w-full rounded border px-3 py-2 text-sm"
              required
            />
          </div>

          <div className="space-y-1">
            <Label htmlFor="alert-rule-type">Alert Type</Label>
            <input
              id="alert-rule-type"
              type="text"
              value={alertType}
              onChange={e => setAlertType(e.target.value)}
              placeholder="es. error_rate"
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>

          <div className="space-y-1">
            <Label htmlFor="alert-rule-severity">Severity</Label>
            <select
              id="alert-rule-severity"
              value={severity}
              onChange={e => setSeverity(e.target.value as CreateAlertRule['severity'])}
              className="w-full rounded border px-3 py-2 text-sm"
            >
              {SEVERITY_OPTIONS.map(opt => (
                <option key={opt} value={opt}>
                  {opt}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <Label htmlFor="alert-rule-threshold">Threshold</Label>
            <input
              id="alert-rule-threshold"
              type="number"
              value={thresholdValue}
              onChange={e => setThresholdValue(e.target.value)}
              placeholder="es. 5"
              min={0}
              step="any"
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>

          <div className="space-y-1">
            <Label htmlFor="alert-rule-unit">Unit</Label>
            <input
              id="alert-rule-unit"
              type="text"
              value={thresholdUnit}
              onChange={e => setThresholdUnit(e.target.value)}
              placeholder="es. %"
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>

          <div className="space-y-1">
            <Label htmlFor="alert-rule-duration">Duration (minuti)</Label>
            <input
              id="alert-rule-duration"
              type="number"
              value={durationMinutes}
              onChange={e => setDurationMinutes(e.target.value)}
              placeholder="es. 10"
              min={1}
              step={1}
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>

          <div className="space-y-1">
            <Label htmlFor="alert-rule-description">Descrizione (opzionale)</Label>
            <textarea
              id="alert-rule-description"
              value={description}
              onChange={e => setDescription(e.target.value)}
              placeholder="Descrizione della regola..."
              rows={2}
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>

          <DialogFooter>
            <button
              type="button"
              onClick={() => handleOpenChange(false)}
              className="rounded border px-4 py-2 text-sm"
            >
              Annulla
            </button>
            <button
              type="submit"
              disabled={!isValid || submitting}
              className="rounded bg-primary px-4 py-2 text-sm text-primary-foreground disabled:opacity-50"
            >
              {submitting ? 'Creazione...' : 'Crea Regola'}
            </button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
