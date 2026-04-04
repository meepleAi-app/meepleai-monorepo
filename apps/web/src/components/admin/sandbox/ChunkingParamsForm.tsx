'use client';

import { useSandboxSession } from '@/components/admin/sandbox/contexts/SandboxSessionContext';
import { Switch } from '@/components/ui/forms/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Label } from '@/components/ui/primitives/label';
import { Slider } from '@/components/ui/slider';

const CHUNK_STRATEGY_OPTIONS = [
  { value: 'semantic', label: 'Semantico' },
  { value: 'fixed-size', label: 'Dimensione fissa' },
  { value: 'page-based', label: 'Per pagina' },
] as const;

export function ChunkingParamsForm() {
  const { agentConfig, updateConfig } = useSandboxSession();

  return (
    <div className="space-y-5">
      {/* Chunk Strategy */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label htmlFor="chunkStrategy">Strategia di chunking</Label>
          <span className="text-xs text-muted-foreground">{agentConfig.chunkStrategy}</span>
        </div>
        <Select
          value={agentConfig.chunkStrategy}
          onValueChange={value => updateConfig({ chunkStrategy: value })}
        >
          <SelectTrigger id="chunkStrategy">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {CHUNK_STRATEGY_OPTIONS.map(opt => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Chunk Size */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label>Dimensione chunk (tokens)</Label>
          <span className="text-xs font-mono text-muted-foreground">{agentConfig.chunkSize}</span>
        </div>
        <Slider
          min={128}
          max={1024}
          step={64}
          value={[agentConfig.chunkSize]}
          onValueChange={([value]) => updateConfig({ chunkSize: value })}
          aria-label="Chunk size"
        />
      </div>

      {/* Overlap */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label>Overlap (tokens)</Label>
          <span className="text-xs font-mono text-muted-foreground">{agentConfig.overlap}</span>
        </div>
        <Slider
          min={0}
          max={256}
          step={16}
          value={[agentConfig.overlap]}
          onValueChange={([value]) => updateConfig({ overlap: value })}
          aria-label="Overlap"
        />
      </div>

      {/* Respect Page Boundaries */}
      <div className="flex items-center justify-between">
        <Label htmlFor="respectPageBoundaries">Rispetta confini pagina</Label>
        <Switch
          id="respectPageBoundaries"
          checked={agentConfig.respectPageBoundaries}
          onCheckedChange={checked => updateConfig({ respectPageBoundaries: checked })}
        />
      </div>
    </div>
  );
}
