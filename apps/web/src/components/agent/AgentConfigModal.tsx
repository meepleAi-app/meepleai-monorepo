/**
 * AgentConfigModal - Modal for Agent Configuration (Issue #3186 - AGT-012)
 *
 * Features:
 * - Typology dropdown (from approved typologies API)
 * - Model dropdown (tier-filtered: Free vs Premium)
 * - Cost estimation display
 * - Quota progress bar with warning (>90%)
 * - localStorage cache for last selection
 * - Save config and launch chat
 */

'use client';

import { AlertCircle } from 'lucide-react';

import { useAgentConfigModal } from '@/hooks/useAgentConfigModal';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Progress } from '@/components/ui/feedback/progress';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { Badge } from '@/components/ui/data-display/badge';
import { Label } from '@/components/ui/primitives/label';

export interface AgentConfigModalProps {
  gameId: string;
  trigger?: React.ReactNode;
  onConfigSaved?: () => void;
}

export function AgentConfigModal({
  gameId,
  trigger,
  onConfigSaved,
}: AgentConfigModalProps): JSX.Element {
  const {
    selectedTypologyId,
    setSelectedTypologyId,
    selectedModelName,
    setSelectedModelName,
    typologies,
    typologiesLoading,
    availableModels,
    userTier,
    estimatedCost,
    quota,
    quotaLoading,
    showWarning,
    saveConfig,
    saving,
    isValid,
  } = useAgentConfigModal({ gameId });

  const handleSave = async () => {
    await saveConfig();
    if (onConfigSaved) {
      onConfigSaved();
    }
  };

  return (
    <Dialog>
      <DialogTrigger asChild>
        {trigger || <button>Configura AI</button>}
      </DialogTrigger>

      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>Configurazione Agente AI</DialogTitle>
          <DialogDescription>
            Seleziona la tipologia di agente e il modello AI da utilizzare per le sessioni di
            gioco.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-6 py-4">
          {/* Typology Dropdown */}
          <div className="space-y-2">
            <Label htmlFor="typology-select">Tipologia Agente</Label>
            <Select
              value={selectedTypologyId ?? undefined}
              onValueChange={setSelectedTypologyId}
              disabled={typologiesLoading}
            >
              <SelectTrigger id="typology-select">
                <SelectValue placeholder="Seleziona tipologia">
                  {selectedTypologyId &&
                    typologies.find((t) => t.id === selectedTypologyId)?.name}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {typologies.map((typology) => (
                  <SelectItem key={typology.id} value={typology.id}>
                    <div className="flex items-start gap-2">
                      <div>
                        <div className="font-medium">{typology.name}</div>
                        <div className="text-xs text-muted-foreground line-clamp-2">
                          {typology.description}
                        </div>
                      </div>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Model Dropdown (tier-filtered) */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label htmlFor="model-select">Modello AI</Label>
              <Badge variant="secondary" className="text-xs">
                {userTier}
              </Badge>
            </div>
            <Select
              value={selectedModelName ?? undefined}
              onValueChange={setSelectedModelName}
            >
              <SelectTrigger id="model-select">
                <SelectValue placeholder="Seleziona modello">
                  {selectedModelName}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {availableModels.map((model) => (
                  <SelectItem key={model.name} value={model.name}>
                    <div className="flex items-center justify-between gap-4 w-full">
                      <span>
                        {model.name}
                        {model.recommended && (
                          <span className="ml-2 text-xs text-primary">(Consigliato)</span>
                        )}
                      </span>
                      <span className="text-xs text-muted-foreground">
                        ${model.cost.toFixed(4)}/query
                      </span>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Cost Estimation */}
          {estimatedCost !== null && (
            <div className="flex items-center justify-between p-3 bg-muted/50 rounded-md">
              <span className="text-sm font-medium">Costo stimato per query</span>
              <Badge variant="outline">${estimatedCost.toFixed(4)}</Badge>
            </div>
          )}

          {/* Quota Display */}
          {quota && !quota.isUnlimited && (
            <div className="space-y-3">
              <div className="flex items-center justify-between text-sm">
                <span className="font-medium">Utilizzo Quota</span>
                <span className="text-muted-foreground">
                  {quota.currentSessions} / {quota.maxSessions}
                </span>
              </div>
              <Progress
                value={quota.percentageUsed}
                className={
                  quota.percentageUsed >= 90
                    ? '[&>*]:bg-destructive'
                    : quota.percentageUsed >= 75
                      ? '[&>*]:bg-amber-500'
                      : '[&>*]:bg-primary'
                }
              />
              <div className="text-xs text-muted-foreground">
                {quota.remainingSlots} slot disponibili
              </div>
            </div>
          )}

          {/* Warning Alert (>90% quota) */}
          {showWarning && quota && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                Attenzione: stai utilizzando oltre il {quota.percentageUsed}% della tua quota.
                Considera di completare le sessioni attive prima di avviarne di nuove.
              </AlertDescription>
            </Alert>
          )}

          {/* Quota Full Block */}
          {quota && !quota.canCreateNew && !quota.isUnlimited && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                Hai raggiunto il limite massimo di sessioni attive. Completa o termina alcune
                sessioni prima di avviarne di nuove.
              </AlertDescription>
            </Alert>
          )}
        </div>

        <DialogFooter>
          <LoadingButton
            onClick={handleSave}
            isLoading={saving || quotaLoading}
            loadingText="Salvataggio..."
            disabled={!isValid || (quota && !quota.canCreateNew && !quota.isUnlimited)}
            className="w-full sm:w-auto"
          >
            Salva e Lancia Agente
          </LoadingButton>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
