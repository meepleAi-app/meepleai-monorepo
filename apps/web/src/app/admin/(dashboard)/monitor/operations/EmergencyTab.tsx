'use client';

/**
 * EmergencyTab — LLM emergency override controls
 * Issue #129 — Enhanced Emergency Controls Tab
 *
 * Enhancements over stub (#126):
 * - Toast feedback on activate/deactivate
 * - Loading reset on refetch
 * - Level 2 confirmation for activate
 */

import { useCallback, useEffect, useState } from 'react';

import { AlertTriangle, CheckCircle2, Clock, Loader2, ShieldAlert, ShieldOff } from 'lucide-react';

import {
  AdminConfirmationDialog,
  AdminConfirmationLevel,
} from '@/components/ui/admin/admin-confirmation-dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useToast } from '@/hooks/useToast';
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

  // Confirmation dialogs
  const [deactivateTarget, setDeactivateTarget] = useState<string | null>(null);
  const [activateConfirmOpen, setActivateConfirmOpen] = useState(false);

  const { toast } = useToast();

  const fetchOverrides = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.admin.getActiveEmergencyOverrides();
      setOverrides(result);
    } catch {
      toast({ title: 'Failed to load emergency overrides', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [toast]);

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
      toast({ title: `Emergency override activated: ${action}` });
      setReason('');
      setTargetProvider('');
      fetchOverrides();
    } catch {
      toast({ title: 'Failed to activate override', variant: 'destructive' });
    } finally {
      setActivating(false);
    }
  };

  const handleDeactivate = async () => {
    if (!deactivateTarget) return;
    try {
      await api.admin.deactivateEmergencyOverride(deactivateTarget);
      toast({ title: `Override deactivated: ${deactivateTarget}` });
      fetchOverrides();
    } catch {
      toast({ title: 'Failed to deactivate override', variant: 'destructive' });
    } finally {
      setDeactivateTarget(null);
    }
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
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setDeactivateTarget(override.action)}
                    >
                      <ShieldOff className="h-4 w-4 mr-1" />
                      Deactivate
                    </Button>
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
                onClick={() => setActivateConfirmOpen(true)}
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

      {/* Activate Confirmation — Level 2 (critical action) */}
      <AdminConfirmationDialog
        isOpen={activateConfirmOpen}
        onClose={() => setActivateConfirmOpen(false)}
        onConfirm={handleActivate}
        level={AdminConfirmationLevel.Level2}
        title="Activate Emergency Override"
        message={`This will activate "${action}" override for ${duration} minutes. LLM routing will be immediately affected.`}
        warningMessage="This is a critical action that affects production LLM traffic."
        isLoading={activating}
      />

      {/* Deactivate Confirmation — Level 1 */}
      <AdminConfirmationDialog
        isOpen={!!deactivateTarget}
        onClose={() => setDeactivateTarget(null)}
        onConfirm={handleDeactivate}
        level={AdminConfirmationLevel.Level1}
        title="Deactivate Override"
        message={`This will remove the "${deactivateTarget}" emergency override. LLM requests will resume normal routing.`}
      />
    </div>
  );
}
