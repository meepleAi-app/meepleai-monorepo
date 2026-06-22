'use client';

/**
 * SetBudgetDialog (#1838 SP5 F4-C5) — modal per impostare/aggiornare l'AppBudget
 * singleton (Scenario B nello spec doc).
 *
 * Pre-popolato con i valori correnti se il budget è già configurato; vuoto sulla
 * prima visita (Scenario I empty state).
 *
 * Concurrency: passa il `rowVersion` corrente al PUT — il backend ritorna 409
 * ConflictException se un altro admin ha modificato la riga nel frattempo.
 * In quel caso mostriamo il toast di errore e l'utente deve riaprire il modal
 * (`useBudget.refetch` parte automaticamente grazie alla invalidation).
 */

import React, { useEffect, useState } from 'react';

import { toast } from 'sonner';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Label } from '@/components/ui/primitives/label';
import { useBudget } from '@/hooks/useBudget';

interface SetBudgetDialogProps {
  open: boolean;
  onClose: () => void;
}

export function SetBudgetDialog({ open, onClose }: SetBudgetDialogProps): React.JSX.Element {
  const { budget, upsert, isUpserting } = useBudget();

  const [amount, setAmount] = useState<string>('');
  const [alertPct, setAlertPct] = useState<string>('80');
  const [criticalPct, setCriticalPct] = useState<string>('95');

  // Pre-populate when opening with an existing budget.
  useEffect(() => {
    if (open && budget) {
      setAmount(budget.monthlyLimit.toFixed(2));
      setAlertPct(String(budget.alertThresholdPct));
      setCriticalPct(String(budget.criticalThresholdPct));
    } else if (open && !budget) {
      setAmount('');
      setAlertPct('80');
      setCriticalPct('95');
    }
  }, [open, budget]);

  const parsedAmount = parseFloat(amount);
  const parsedAlert = parseInt(alertPct, 10);
  const parsedCritical = parseInt(criticalPct, 10);

  const isValid =
    !Number.isNaN(parsedAmount) &&
    parsedAmount > 0 &&
    !Number.isNaN(parsedAlert) &&
    parsedAlert >= 1 &&
    parsedAlert <= 99 &&
    !Number.isNaN(parsedCritical) &&
    parsedCritical > parsedAlert &&
    parsedCritical <= 100;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid) return;
    try {
      await upsert({
        monthlyLimitAmount: parsedAmount,
        monthlyLimitCurrency: 'USD',
        alertThresholdPct: parsedAlert,
        criticalThresholdPct: parsedCritical,
        rowVersion: budget?.rowVersion ?? null,
      });
      toast.success(budget ? 'Budget aggiornato' : 'Budget configurato', {
        description: `Limite mensile: $${parsedAmount.toFixed(2)}`,
      });
      onClose();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error('Salvataggio budget fallito', { description: message });
    }
  };

  const handleOpenChange = (isOpen: boolean) => {
    if (!isOpen) onClose();
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{budget ? 'Modifica budget mensile' : 'Imposta budget mensile'}</DialogTitle>
          <DialogDescription>
            Definisci il limite di spesa USD per il mese corrente e le soglie di alert (warning +
            critical). Le soglie sono usate da BudgetGauge e da AlertRule pre-built.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="budget-amount">Limite mensile (USD)</Label>
            <div className="flex items-center gap-2">
              <span className="font-mono text-sm text-muted-foreground">$</span>
              <input
                id="budget-amount"
                type="number"
                value={amount}
                onChange={e => setAmount(e.target.value)}
                placeholder="es. 1200.00"
                min={0.01}
                step="0.01"
                className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                required
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label htmlFor="budget-alert">Soglia alert (%)</Label>
              <input
                id="budget-alert"
                type="number"
                value={alertPct}
                onChange={e => setAlertPct(e.target.value)}
                min={1}
                max={99}
                step={1}
                className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                required
              />
              <p className="text-[11px] text-muted-foreground">Default 80%</p>
            </div>
            <div className="space-y-1">
              <Label htmlFor="budget-critical">Soglia critical (%)</Label>
              <input
                id="budget-critical"
                type="number"
                value={criticalPct}
                onChange={e => setCriticalPct(e.target.value)}
                min={2}
                max={100}
                step={1}
                className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                required
              />
              <p className="text-[11px] text-muted-foreground">Default 95% &gt; alert%</p>
            </div>
          </div>

          {!isValid && amount && (
            <p className="text-[11px] text-rose-600 dark:text-rose-400">
              Verifica: importo &gt; 0 e soglia critical &gt; alert.
            </p>
          )}

          <DialogFooter>
            <button
              type="button"
              onClick={onClose}
              className="rounded border border-border px-4 py-2 text-sm hover:bg-muted"
            >
              Annulla
            </button>
            <button
              type="submit"
              disabled={!isValid || isUpserting}
              className="rounded bg-primary px-4 py-2 text-sm text-primary-foreground hover:opacity-90 disabled:opacity-50"
            >
              {isUpserting ? 'Salvataggio…' : budget ? 'Aggiorna' : 'Imposta'}
            </button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
