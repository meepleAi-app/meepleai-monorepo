/**
 * Admin Tier Management Page (Game Night Improvvisata)
 *
 * Displays and edits all tier definitions via:
 *   GET  /api/v1/admin/tiers
 *   POST /api/v1/admin/tiers
 *   PUT  /api/v1/admin/tiers/{name}
 */

'use client';

import { useEffect, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Plus } from 'lucide-react';
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
import type { TierDefinition, TierLimits } from '@/lib/api/schemas/tier.schemas';

// ── Limit key labels ─────────────────────────────────────────────────────────

type LimitKey = keyof Omit<TierLimits, 'sessionSaveEnabled'>;

const LIMIT_LABELS: Record<LimitKey, string> = {
  maxPrivateGames: 'Max Giochi',
  maxPdfUploadsPerMonth: 'Max PDF/mese',
  maxPdfSizeBytes: 'Max PDF size (bytes)',
  maxAgents: 'Max Agent',
  maxAgentQueriesPerDay: 'Query/giorno',
  maxSessionQueries: 'Query sessione',
  maxSessionPlayers: 'Max giocatori',
  maxPhotosPerSession: 'Foto sessione',
  maxCatalogProposalsPerWeek: 'Proposte/sett',
};

const LIMIT_KEYS = Object.keys(LIMIT_LABELS) as LimitKey[];

function getLimitValue(tier: TierDefinition, key: LimitKey): number {
  return (tier.limits[key] as number) ?? 0;
}

function formatLimit(value: number): string {
  if (value >= 2_147_483_647) return '∞';
  return String(value);
}

// ── Tier dialog form ──────────────────────────────────────────────────────────

interface TierFormState {
  name: string;
  displayName: string;
  llmModelTier: string;
  isDefault: boolean;
  sessionSaveEnabled: boolean;
  limits: Record<LimitKey, number>;
}

function buildFormState(tier?: TierDefinition): TierFormState {
  if (!tier) {
    return {
      name: '',
      displayName: '',
      llmModelTier: 'standard',
      isDefault: false,
      sessionSaveEnabled: true,
      limits: Object.fromEntries(LIMIT_KEYS.map(k => [k, 0])) as Record<LimitKey, number>,
    };
  }
  return {
    name: tier.name,
    displayName: tier.displayName,
    llmModelTier: tier.llmModelTier,
    isDefault: tier.isDefault,
    sessionSaveEnabled: tier.limits.sessionSaveEnabled,
    limits: Object.fromEntries(LIMIT_KEYS.map(k => [k, getLimitValue(tier, k)])) as Record<
      LimitKey,
      number
    >,
  };
}

interface TierDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tier?: TierDefinition;
  onSaved: () => void;
}

function TierDialog({ open, onOpenChange, tier, onSaved }: TierDialogProps) {
  const isEdit = !!tier;
  const [form, setForm] = useState<TierFormState>(() => buildFormState(tier));

  useEffect(() => {
    if (open) {
      setForm(buildFormState(tier));
    }
  }, [open, tier]);

  const handleOpenChange = onOpenChange;

  const saveMutation = useMutation({
    mutationFn: async () => {
      const limits: TierLimits = {
        ...Object.fromEntries(LIMIT_KEYS.map(k => [k, form.limits[k] ?? 0])),
        sessionSaveEnabled: form.sessionSaveEnabled,
      } as TierLimits;

      if (isEdit) {
        await api.tiers.updateTier(tier.name, {
          displayName: form.displayName,
          limits,
          llmModelTier: form.llmModelTier,
          isDefault: form.isDefault,
        });
      } else {
        await api.tiers.createTier({
          name: form.name,
          displayName: form.displayName,
          limits,
          llmModelTier: form.llmModelTier,
          isDefault: form.isDefault,
        });
      }
    },
    onSuccess: () => {
      toast.success(isEdit ? 'Tier aggiornato' : 'Tier creato');
      onSaved();
      onOpenChange(false);
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Errore durante il salvataggio');
    },
  });

  function setLimit(key: LimitKey, value: number) {
    setForm(prev => ({ ...prev, limits: { ...prev.limits, [key]: value } }));
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle className="font-quicksand">
            {isEdit ? `Modifica tier: ${tier.name}` : 'Nuovo tier'}
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {/* Basic fields */}
          <div className="grid grid-cols-2 gap-3">
            {!isEdit && (
              <div className="col-span-2">
                <label className="block text-xs font-medium text-muted-foreground mb-1">
                  Nome (identificativo)
                </label>
                <input
                  className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm"
                  value={form.name}
                  onChange={e => setForm(prev => ({ ...prev, name: e.target.value }))}
                  placeholder="free, premium, contributor…"
                  data-testid="field-name"
                />
              </div>
            )}
            <div>
              <label className="block text-xs font-medium text-muted-foreground mb-1">
                Display Name
              </label>
              <input
                className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm"
                value={form.displayName}
                onChange={e => setForm(prev => ({ ...prev, displayName: e.target.value }))}
                placeholder="Free, Premium, Contributor…"
                data-testid="field-displayName"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-muted-foreground mb-1">
                LLM Model Tier
              </label>
              <input
                className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm"
                value={form.llmModelTier}
                onChange={e => setForm(prev => ({ ...prev, llmModelTier: e.target.value }))}
                placeholder="standard, premium…"
                data-testid="field-llmModelTier"
              />
            </div>
          </div>

          {/* Limits */}
          <div>
            <p className="text-xs font-medium text-muted-foreground mb-2">Limiti</p>
            <div className="grid grid-cols-2 gap-2">
              {LIMIT_KEYS.map(key => (
                <div key={key}>
                  <label className="block text-xs text-muted-foreground mb-0.5">
                    {LIMIT_LABELS[key] ?? key}
                  </label>
                  <input
                    type="number"
                    min={0}
                    className="w-full rounded-md border border-border bg-background px-2 py-1.5 text-sm"
                    value={form.limits[key] ?? 0}
                    onChange={e => setLimit(key, Number(e.target.value))}
                    data-testid={`field-limit-${key}`}
                  />
                </div>
              ))}
            </div>
          </div>

          {/* Toggles */}
          <div className="space-y-2">
            <label className="flex items-center gap-2 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={form.sessionSaveEnabled}
                onChange={e => setForm(prev => ({ ...prev, sessionSaveEnabled: e.target.checked }))}
                data-testid="field-sessionSaveEnabled"
              />
              <span className="text-sm">Session Save abilitato</span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={form.isDefault}
                onChange={e => setForm(prev => ({ ...prev, isDefault: e.target.checked }))}
                data-testid="field-isDefault"
              />
              <span className="text-sm">Tier default</span>
            </label>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Annulla
          </Button>
          <Button
            onClick={() => saveMutation.mutate()}
            disabled={saveMutation.isPending}
            data-testid="btn-save"
          >
            {saveMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {isEdit ? 'Salva modifiche' : 'Crea tier'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function AdminTiersPage() {
  const queryClient = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingTier, setEditingTier] = useState<TierDefinition | undefined>();

  const tiersQuery = useQuery({
    queryKey: ['admin', 'tiers'],
    queryFn: () => api.tiers.getTiers(),
    staleTime: 30_000,
  });

  function handleCreate() {
    setEditingTier(undefined);
    setDialogOpen(true);
  }

  function handleEdit(tier: TierDefinition) {
    setEditingTier(tier);
    setDialogOpen(true);
  }

  function handleSaved() {
    void queryClient.invalidateQueries({ queryKey: ['admin', 'tiers'] });
  }

  const tiers = tiersQuery.data ?? [];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-quicksand text-xl font-bold tracking-tight">Tier Management</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">
            Gestisci i tier di abbonamento e i limiti associati.
          </p>
        </div>
        <Button onClick={handleCreate} data-testid="btn-create-tier">
          <Plus className="mr-2 h-4 w-4" />
          Nuovo Tier
        </Button>
      </div>

      {/* Table */}
      {tiersQuery.isLoading ? (
        <div className="flex items-center justify-center py-12 text-muted-foreground">
          <Loader2 className="mr-2 h-5 w-5 animate-spin" />
          Caricamento tier…
        </div>
      ) : tiersQuery.isError ? (
        <div className="py-12 text-center text-destructive text-sm">
          Errore nel caricamento dei tier.
        </div>
      ) : (
        <div className="overflow-x-auto rounded-md border">
          <table className="w-full text-sm" data-testid="tiers-table">
            <thead>
              <tr className="border-b bg-muted/50">
                <th className="px-3 py-2 text-left font-medium text-muted-foreground whitespace-nowrap">
                  Nome
                </th>
                <th className="px-3 py-2 text-left font-medium text-muted-foreground whitespace-nowrap">
                  Display Name
                </th>
                <th className="px-3 py-2 text-left font-medium text-muted-foreground whitespace-nowrap">
                  LLM Tier
                </th>
                {LIMIT_KEYS.map(k => (
                  <th
                    key={k}
                    className="px-3 py-2 text-right font-medium text-muted-foreground whitespace-nowrap"
                  >
                    {LIMIT_LABELS[k] ?? k}
                  </th>
                ))}
                <th className="px-3 py-2 text-center font-medium text-muted-foreground">Stato</th>
                <th className="px-3 py-2" />
              </tr>
            </thead>
            <tbody>
              {tiers.map(tier => (
                <tr
                  key={tier.id}
                  className="border-b border-border/50 transition-colors hover:bg-muted/40"
                  data-testid={`tier-row-${tier.name}`}
                >
                  <td className="px-3 py-2 font-mono text-xs font-medium">{tier.name}</td>
                  <td className="px-3 py-2 font-medium">{tier.displayName}</td>
                  <td className="px-3 py-2 text-xs text-muted-foreground">{tier.llmModelTier}</td>
                  {LIMIT_KEYS.map(k => (
                    <td key={k} className="px-3 py-2 text-right tabular-nums">
                      {formatLimit(getLimitValue(tier, k))}
                    </td>
                  ))}
                  <td className="px-3 py-2 text-center">
                    {tier.isDefault ? (
                      <Badge
                        variant="outline"
                        className="border-green-300 bg-green-50 text-green-800 dark:bg-green-950/30 dark:text-green-300"
                      >
                        Default
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="text-muted-foreground">
                        Custom
                      </Badge>
                    )}
                  </td>
                  <td className="px-3 py-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleEdit(tier)}
                      aria-label={`Modifica tier ${tier.name}`}
                      data-testid={`btn-edit-${tier.name}`}
                    >
                      <Pencil className="h-3 w-3" />
                      Modifica
                    </Button>
                  </td>
                </tr>
              ))}
              {tiers.length === 0 && (
                <tr>
                  <td
                    colSpan={LIMIT_KEYS.length + 5}
                    className="py-12 text-center text-muted-foreground"
                  >
                    Nessun tier trovato.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      <TierDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        tier={editingTier}
        onSaved={handleSaved}
      />
    </div>
  );
}
