'use client';

/**
 * EmergencyTab — LLM emergency override controls
 * Issue #129 — Emergency Controls Tab (stub for #126 page setup)
 */

import { useCallback, useEffect, useState } from 'react';

import { AlertTriangle, CheckCircle2, Clock, Loader2, ShieldAlert, ShieldOff } from 'lucide-react';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { ActiveOverride } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

const OVERRIDE_ACTIONS = [
  {
    value: 'force-ollama-only',
    label: 'Force Ollama Only',
    description: 'Route all LLM requests through local Ollama',
  },
  {
    value: 'reset-circuit-breaker',
    label: 'Reset Circuit Breaker',
    description: 'Reset the circuit breaker for a provider',
  },
  {
    value: 'flush-quota-cache',
    label: 'Flush Quota Cache',
    description: 'Clear all cached free model quota data',
  },
] as const;

export function EmergencyTab() {
  const [overrides, setOverrides] = useState<ActiveOverride[]>([]);
  const [loading, setLoading] = useState(true);
  const [activating, setActivating] = useState(false);

  // Activation form state
  const [action, setAction] = useState<string>(OVERRIDE_ACTIONS[0].value);
  const [reason, setReason] = useState('');
  const [duration, setDuration] = useState(30);
  const [targetProvider, setTargetProvider] = useState('');

  const fetchOverrides = useCallback(async () => {
    try {
      const result = await api.admin.getActiveEmergencyOverrides();
      setOverrides(result);
    } catch {
      // silent
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchOverrides();
    const interval = setInterval(fetchOverrides, 30_000);
    return () => clearInterval(interval);
  }, [fetchOverrides]);

  const handleActivate = async () => {
    if (!reason.trim()) return;
    setActivating(true);
    try {
      await api.admin.activateEmergencyOverride({
        action,
        reason: reason.trim(),
        durationMinutes: duration,
        targetProvider: targetProvider.trim() || undefined,
      });
      setReason('');
      setTargetProvider('');
      fetchOverrides();
    } finally {
      setActivating(false);
    }
  };

  const handleDeactivate = async (overrideAction: string) => {
    await api.admin.deactivateEmergencyOverride(overrideAction);
    fetchOverrides();
  };

  return (
    <div className="space-y-6" data-testid="emergency-tab">
      <div>
        <h2 className="font-quicksand text-lg font-semibold">Emergency Controls</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          LLM emergency overrides for production incidents.
        </p>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-10">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <>
          {/* Active Overrides */}
          {overrides.length === 0 ? (
            <div
              className="flex flex-col items-center justify-center py-10 text-center"
              data-testid="no-overrides"
            >
              <CheckCircle2 className="h-10 w-10 text-green-500 mb-3" />
              <p className="text-sm font-medium">No active emergency overrides</p>
              <p className="text-xs text-muted-foreground mt-1">
                All LLM systems operating normally.
              </p>
            </div>
          ) : (
            <div className="space-y-3" data-testid="active-overrides">
              {overrides.map(override => (
                <div
                  key={override.action}
                  className={cn(
                    'rounded-xl border-2 border-red-500/50 bg-red-50/50 dark:bg-red-950/20 p-4'
                  )}
                >
                  <div className="flex items-start justify-between">
                    <div className="flex items-center gap-2">
                      <ShieldAlert className="h-5 w-5 text-red-600" />
                      <div>
                        <p className="font-medium text-sm">{override.action}</p>
                        <p className="text-xs text-muted-foreground">{override.reason}</p>
                      </div>
                    </div>
                    <AlertDialog>
                      <AlertDialogTrigger asChild>
                        <Button variant="outline" size="sm">
                          <ShieldOff className="h-4 w-4 mr-1" />
                          Deactivate
                        </Button>
                      </AlertDialogTrigger>
                      <AlertDialogContent>
                        <AlertDialogHeader>
                          <AlertDialogTitle>Deactivate Override</AlertDialogTitle>
                          <AlertDialogDescription>
                            This will remove the &quot;{override.action}&quot; emergency override.
                            LLM requests will resume normal routing.
                          </AlertDialogDescription>
                        </AlertDialogHeader>
                        <AlertDialogFooter>
                          <AlertDialogCancel>Cancel</AlertDialogCancel>
                          <AlertDialogAction onClick={() => handleDeactivate(override.action)}>
                            Deactivate
                          </AlertDialogAction>
                        </AlertDialogFooter>
                      </AlertDialogContent>
                    </AlertDialog>
                  </div>
                  <div className="flex items-center gap-4 mt-2 text-xs text-muted-foreground">
                    <span className="flex items-center gap-1">
                      <Clock className="h-3 w-3" />
                      {override.remainingMinutes.toFixed(0)} min remaining
                    </span>
                    {override.targetProvider && <span>Provider: {override.targetProvider}</span>}
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Activate New Override */}
          <div className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-4">
            <div className="flex items-center gap-2 mb-4">
              <AlertTriangle className="h-4 w-4 text-amber-600" />
              <h3 className="font-quicksand font-semibold text-sm">Activate Emergency Override</h3>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="text-xs font-medium text-muted-foreground mb-1 block">
                  Action
                </label>
                <select
                  value={action}
                  onChange={e => setAction(e.target.value)}
                  className="w-full rounded-md border bg-background px-3 py-2 text-sm"
                  data-testid="override-action-select"
                >
                  {OVERRIDE_ACTIONS.map(a => (
                    <option key={a.value} value={a.value}>
                      {a.label}
                    </option>
                  ))}
                </select>
                <p className="text-xs text-muted-foreground mt-1">
                  {OVERRIDE_ACTIONS.find(a => a.value === action)?.description}
                </p>
              </div>

              <div>
                <label className="text-xs font-medium text-muted-foreground mb-1 block">
                  Reason (required)
                </label>
                <Input
                  value={reason}
                  onChange={e => setReason(e.target.value)}
                  placeholder="Production incident — high error rate..."
                  data-testid="override-reason-input"
                />
              </div>

              <div>
                <label className="text-xs font-medium text-muted-foreground mb-1 block">
                  Duration (minutes)
                </label>
                <Input
                  type="number"
                  min={5}
                  max={1440}
                  value={duration}
                  onChange={e => setDuration(Number(e.target.value))}
                  data-testid="override-duration-input"
                />
              </div>

              {action === 'reset-circuit-breaker' && (
                <div>
                  <label className="text-xs font-medium text-muted-foreground mb-1 block">
                    Target Provider (optional)
                  </label>
                  <Input
                    value={targetProvider}
                    onChange={e => setTargetProvider(e.target.value)}
                    placeholder="openrouter"
                    data-testid="override-provider-input"
                  />
                </div>
              )}
            </div>

            <div className="mt-4">
              <Button
                onClick={handleActivate}
                disabled={!reason.trim() || activating}
                className="bg-red-600 hover:bg-red-700 text-white"
                data-testid="activate-override-button"
              >
                {activating && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
                Activate Override
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
