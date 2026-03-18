'use client';

import { useSandboxSession } from '@/components/admin/sandbox/contexts/SandboxSessionContext';
import { Switch } from '@/components/ui/forms/switch';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Slider } from '@/components/ui/slider';

const STRATEGY_OPTIONS = [
  { value: 'hybrid-v2', label: 'Hybrid v2' },
  { value: 'dense-only', label: 'Dense Only' },
  { value: 'sparse-only', label: 'Sparse Only' },
  { value: 'hybrid-v1', label: 'Hybrid v1' },
] as const;

const TOP_K_OPTIONS = [3, 5, 8, 10, 15, 20] as const;

const RERANKER_OPTIONS = [
  { value: 'cross-encoder/ms-marco', label: 'cross-encoder/ms-marco' },
  { value: 'bge-reranker', label: 'bge-reranker' },
] as const;

export function RagStrategyForm() {
  const { agentConfig, updateConfig } = useSandboxSession();

  return (
    <div className="space-y-5">
      {/* Strategy */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label htmlFor="strategy">Strategia di retrieval</Label>
          <span className="text-xs text-muted-foreground">{agentConfig.strategy}</span>
        </div>
        <Select
          value={agentConfig.strategy}
          onValueChange={value => updateConfig({ strategy: value })}
        >
          <SelectTrigger id="strategy">
            <SelectValue placeholder="Seleziona strategia" />
          </SelectTrigger>
          <SelectContent>
            {STRATEGY_OPTIONS.map(opt => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Dense Weight */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label>Peso dense</Label>
          <span className="text-xs font-mono text-muted-foreground">
            {agentConfig.denseWeight.toFixed(1)}
          </span>
        </div>
        <Slider
          min={0}
          max={1}
          step={0.1}
          value={[agentConfig.denseWeight]}
          onValueChange={([value]) => updateConfig({ denseWeight: value })}
          aria-label="Dense weight"
        />
      </div>

      {/* Sparse Weight (read-only) */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label>Peso sparse</Label>
          <span className="text-xs font-mono text-muted-foreground">
            {agentConfig.sparseWeight.toFixed(1)}
          </span>
        </div>
        <p className="text-xs text-muted-foreground">
          Calcolato automaticamente (1 - dense weight)
        </p>
      </div>

      {/* Top-K */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label htmlFor="topK">Top-K risultati</Label>
          <span className="text-xs text-muted-foreground">{agentConfig.topK}</span>
        </div>
        <Select
          value={String(agentConfig.topK)}
          onValueChange={value => updateConfig({ topK: Number(value) })}
        >
          <SelectTrigger id="topK">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {TOP_K_OPTIONS.map(k => (
              <SelectItem key={k} value={String(k)}>
                {k}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Reranking */}
      <div className="flex items-center justify-between">
        <Label htmlFor="reranking">Reranking</Label>
        <Switch
          id="reranking"
          checked={agentConfig.reranking}
          onCheckedChange={checked => updateConfig({ reranking: checked })}
        />
      </div>

      {/* Reranker Model (visible only if reranking on) */}
      {agentConfig.reranking && (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <Label htmlFor="rerankerModel">Modello reranker</Label>
            <span className="text-xs text-muted-foreground">{agentConfig.rerankerModel}</span>
          </div>
          <Select
            value={agentConfig.rerankerModel}
            onValueChange={value => updateConfig({ rerankerModel: value })}
          >
            <SelectTrigger id="rerankerModel">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {RERANKER_OPTIONS.map(opt => (
                <SelectItem key={opt.value} value={opt.value}>
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {/* Min Confidence */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label>Confidenza minima</Label>
          <span className="text-xs font-mono text-muted-foreground">
            {agentConfig.minConfidence.toFixed(2)}
          </span>
        </div>
        <Slider
          min={0}
          max={1}
          step={0.05}
          value={[agentConfig.minConfidence]}
          onValueChange={([value]) => updateConfig({ minConfidence: value })}
          aria-label="Min confidence"
        />
      </div>
    </div>
  );
}
