'use client';

/**
 * ShowcaseControls — Props control panel
 *
 * Renders interactive controls for each prop defined in the story's `controls` map.
 * Control types: select, boolean, range, text, number.
 */

import { Switch } from '@/components/ui/forms/switch';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/overlays/select';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Slider } from '@/components/ui/primitives/slider';
import { cn } from '@/lib/utils';

import type { ControlDef, PropsState } from './types';

interface ShowcaseControlsProps {
  controls: Partial<Record<string, ControlDef>>;
  values: PropsState;
  onChange: (key: string, value: string | boolean | number) => void;
}

export function ShowcaseControls({ controls, values, onChange }: ShowcaseControlsProps) {
  const entries = Object.entries(controls) as [string, ControlDef][];

  if (entries.length === 0) {
    return (
      <div className="p-4">
        <p className="text-xs text-muted-foreground">No controls available for this story.</p>
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* Header */}
      <div className="border-b border-border/60 px-4 py-3">
        <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/70">
          Controls
        </span>
      </div>

      {/* Controls list */}
      <div className="flex-1 overflow-y-auto px-4 py-3 space-y-4">
        {entries.map(([key, def]) => (
          <ControlRow
            key={key}
            propKey={key}
            def={def}
            // eslint-disable-next-line security/detect-object-injection
            value={values[key]}
            onChange={(v) => onChange(key, v)}
          />
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Individual Control Row
// ============================================================================

interface ControlRowProps {
  propKey: string;
  def: ControlDef;
  value: string | boolean | number | undefined;
  onChange: (value: string | boolean | number) => void;
}

function ControlRow({ propKey, def, value, onChange }: ControlRowProps) {
  const labelText = def.label || propKey;

  return (
    <div className="space-y-1.5">
      <Label className="text-xs font-medium text-foreground/80">{labelText}</Label>
      <ControlInput def={def} value={value} onChange={onChange} propKey={propKey} />
    </div>
  );
}

interface ControlInputProps {
  def: ControlDef;
  value: string | boolean | number | undefined;
  onChange: (value: string | boolean | number) => void;
  propKey: string;
}

function ControlInput({ def, value, onChange, propKey }: ControlInputProps) {
  switch (def.type) {
    case 'select': {
      const strVal = String(value ?? def.default);
      return (
        <Select value={strVal} onValueChange={(v) => onChange(v)}>
          <SelectTrigger className="h-8 text-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {def.options.map((opt) => (
              <SelectItem key={opt} value={opt} className="text-xs">
                {opt}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      );
    }

    case 'boolean': {
      const boolVal = Boolean(value ?? def.default);
      return (
        <div className="flex items-center gap-2">
          <Switch
            id={`control-${propKey}`}
            checked={boolVal}
            onCheckedChange={(checked) => onChange(checked)}
          />
          <span className={cn('text-xs', boolVal ? 'text-foreground' : 'text-muted-foreground')}>
            {boolVal ? 'true' : 'false'}
          </span>
        </div>
      );
    }

    case 'range': {
      const numVal = Number(value ?? def.default);
      return (
        <div className="flex items-center gap-3">
          <Slider
            min={def.min}
            max={def.max}
            step={def.step ?? 1}
            value={[numVal]}
            onValueChange={([v]) => onChange(v)}
            className="flex-1"
          />
          <span className="min-w-[2.5rem] text-right text-xs tabular-nums text-muted-foreground">
            {numVal}
          </span>
        </div>
      );
    }

    case 'text': {
      const strVal = String(value ?? def.default);
      return (
        <Input
          value={strVal}
          onChange={(e) => onChange(e.target.value)}
          className="h-8 text-xs"
        />
      );
    }

    case 'number': {
      const numVal = Number(value ?? def.default);
      return (
        <Input
          type="number"
          value={numVal}
          min={def.min}
          max={def.max}
          onChange={(e) => onChange(Number(e.target.value))}
          className="h-8 text-xs"
        />
      );
    }

    default:
      return <span className="text-xs text-muted-foreground">Unknown control type</span>;
  }
}
