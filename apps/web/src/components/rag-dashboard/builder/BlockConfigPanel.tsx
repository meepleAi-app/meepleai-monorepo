'use client';

/**
 * Block Configuration Panel
 *
 * Right sidebar for configuring selected block parameters.
 * Dynamic form generation based on block parameter definitions.
 *
 * @see #3461 - Create parameter configuration panel for blocks
 */

import { useCallback, useMemo } from 'react';

import {
  X,
  Settings,
  Info,
  Code,
  ExternalLink,
  Zap,
  Clock,
  Coins,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Switch } from '@/components/ui/forms/switch';
import { Separator } from '@/components/ui/navigation/separator';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Slider } from '@/components/ui/primitives/slider';
import { cn } from '@/lib/utils';

import type {
  RagBlock,
  BlockParameter,
  NumberParameter,
  StringParameter,
  BooleanParameter,
  SelectParameter,
  RangeParameter,
  ModelParameter,
  NodeConfigChangeEvent,
} from './types';

// =============================================================================
// Types
// =============================================================================

export interface BlockConfigPanelProps {
  /** Selected block definition */
  block: RagBlock | null;
  /** Current parameter values */
  params: Record<string, unknown>;
  /** Node ID being configured */
  nodeId: string | null;
  /** Callback when parameter changes */
  onParamChange: (event: NodeConfigChangeEvent) => void;
  /** Callback when panel is closed */
  onClose: () => void;
  /** Additional class names */
  className?: string;
}

// =============================================================================
// Parameter Renderers
// =============================================================================

interface ParamRendererProps<T extends BlockParameter> {
  param: T;
  value: unknown;
  onChange: (value: unknown) => void;
}

function NumberParamRenderer({
  param,
  value,
  onChange,
}: ParamRendererProps<NumberParameter>) {
  const numValue = (value as number) ?? param.default;

  return (
    <div className="space-y-2" data-testid={`param-${param.id}`}>
      <div className="flex items-center justify-between">
        <Label htmlFor={param.id} className="text-sm">
          {param.name}
          {param.required && <span className="text-destructive ml-1">*</span>}
        </Label>
        <span className="text-xs text-muted-foreground font-mono">{numValue}</span>
      </div>
      <Slider
        id={param.id}
        value={[numValue]}
        onValueChange={([v]) => onChange(v)}
        min={param.min ?? 0}
        max={param.max ?? 100}
        step={param.step ?? 1}
        className="w-full"
      />
      <p className="text-xs text-muted-foreground">{param.description}</p>
    </div>
  );
}

function StringParamRenderer({
  param,
  value,
  onChange,
}: ParamRendererProps<StringParameter>) {
  const strValue = (value as string) ?? param.default;

  return (
    <div className="space-y-2" data-testid={`param-${param.id}`}>
      <Label htmlFor={param.id} className="text-sm">
        {param.name}
        {param.required && <span className="text-destructive ml-1">*</span>}
      </Label>
      <Input
        id={param.id}
        value={strValue}
        onChange={(e) => onChange(e.target.value)}
        maxLength={param.maxLength}
        placeholder={param.default}
        className="h-8"
      />
      <p className="text-xs text-muted-foreground">{param.description}</p>
    </div>
  );
}

function BooleanParamRenderer({
  param,
  value,
  onChange,
}: ParamRendererProps<BooleanParameter>) {
  const boolValue = (value as boolean) ?? param.default;

  return (
    <div className="flex items-center justify-between py-2">
      <div className="space-y-0.5">
        <Label htmlFor={param.id} className="text-sm">
          {param.name}
        </Label>
        <p className="text-xs text-muted-foreground">{param.description}</p>
      </div>
      <Switch
        id={param.id}
        checked={boolValue}
        onCheckedChange={(checked: boolean) => onChange(checked)}
      />
    </div>
  );
}

function SelectParamRenderer({
  param,
  value,
  onChange,
}: ParamRendererProps<SelectParameter>) {
  const selectValue = (value as string) ?? param.default;

  return (
    <div className="space-y-2">
      <Label htmlFor={param.id} className="text-sm">
        {param.name}
        {param.required && <span className="text-destructive ml-1">*</span>}
      </Label>
      <Select value={selectValue} onValueChange={onChange}>
        <SelectTrigger id={param.id} className="h-8">
          <SelectValue placeholder="Select..." />
        </SelectTrigger>
        <SelectContent>
          {param.options.map((opt) => (
            <SelectItem key={opt.value} value={opt.value}>
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <p className="text-xs text-muted-foreground">{param.description}</p>
    </div>
  );
}

function RangeParamRenderer({
  param,
  value,
  onChange,
}: ParamRendererProps<RangeParameter>) {
  const rangeValue = (value as [number, number]) ?? param.default;

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <Label htmlFor={param.id} className="text-sm">
          {param.name}
          {param.required && <span className="text-destructive ml-1">*</span>}
        </Label>
        <span className="text-xs text-muted-foreground font-mono">
          {rangeValue[0].toFixed(2)} - {rangeValue[1].toFixed(2)}
        </span>
      </div>
      <Slider
        id={param.id}
        value={rangeValue}
        onValueChange={(v) => onChange(v as [number, number])}
        min={param.min}
        max={param.max}
        step={param.step ?? 0.01}
        className="w-full"
      />
      <p className="text-xs text-muted-foreground">{param.description}</p>
    </div>
  );
}

function ModelParamRenderer({
  param,
  value,
  onChange,
}: ParamRendererProps<ModelParameter>) {
  const modelValue = (value as string) ?? param.default;

  return (
    <div className="space-y-2">
      <Label htmlFor={param.id} className="text-sm">
        {param.name}
        {param.required && <span className="text-destructive ml-1">*</span>}
      </Label>
      <Select value={modelValue} onValueChange={onChange}>
        <SelectTrigger id={param.id} className="h-8">
          <SelectValue placeholder="Select model..." />
        </SelectTrigger>
        <SelectContent>
          {param.allowedModels.map((model) => (
            <SelectItem key={model} value={model}>
              {model}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <p className="text-xs text-muted-foreground">{param.description}</p>
    </div>
  );
}

// =============================================================================
// Parameter Factory
// =============================================================================

function renderParameter(
  param: BlockParameter,
  value: unknown,
  onChange: (value: unknown) => void
) {
  switch (param.type) {
    case 'number':
      return (
        <NumberParamRenderer
          key={param.id}
          param={param as NumberParameter}
          value={value}
          onChange={onChange}
        />
      );
    case 'string':
      return (
        <StringParamRenderer
          key={param.id}
          param={param as StringParameter}
          value={value}
          onChange={onChange}
        />
      );
    case 'boolean':
      return (
        <BooleanParamRenderer
          key={param.id}
          param={param as BooleanParameter}
          value={value}
          onChange={onChange}
        />
      );
    case 'select':
      return (
        <SelectParamRenderer
          key={param.id}
          param={param as SelectParameter}
          value={value}
          onChange={onChange}
        />
      );
    case 'range':
      return (
        <RangeParamRenderer
          key={param.id}
          param={param as RangeParameter}
          value={value}
          onChange={onChange}
        />
      );
    case 'model':
      return (
        <ModelParamRenderer
          key={param.id}
          param={param as ModelParameter}
          value={value}
          onChange={onChange}
        />
      );
    default:
      return null;
  }
}

// =============================================================================
// Main Component
// =============================================================================

export function BlockConfigPanel({
  block,
  params,
  nodeId,
  onParamChange,
  onClose,
  className,
}: BlockConfigPanelProps) {
  // Handle parameter change
  const handleParamChange = useCallback(
    (paramId: string, value: unknown) => {
      if (!nodeId) return;
      onParamChange({ nodeId, paramId, value });
    },
    [nodeId, onParamChange]
  );

  // Calculate estimates
  const estimates = useMemo(() => {
    if (!block) return null;
    return {
      tokens: block.estimatedTokens,
      latency: block.estimatedLatencyMs,
      cost: block.estimatedCost,
      accuracy: block.accuracyImpact,
    };
  }, [block]);

  // Empty state
  if (!block || !nodeId) {
    return (
      <div
        className={cn(
          'w-80 bg-card border-l flex flex-col items-center justify-center text-center p-6',
          className
        )}
      >
        <Settings className="h-12 w-12 text-muted-foreground/50 mb-4" />
        <h3 className="font-medium text-muted-foreground">No Block Selected</h3>
        <p className="text-sm text-muted-foreground mt-1">
          Select a block on the canvas to configure its parameters
        </p>
      </div>
    );
  }

  return (
    <div className={cn('w-80 bg-card border-l flex flex-col', className)} data-testid="block-config-panel">
      {/* Header */}
      <div className="p-3 border-b flex items-center gap-2">
        <div
          className="w-8 h-8 rounded flex items-center justify-center text-lg"
          style={{ backgroundColor: `${block.color}20` }}
        >
          {block.icon}
        </div>
        <div className="flex-1 min-w-0">
          <h3 className="font-medium text-sm truncate" data-testid="config-block-name">{block.name}</h3>
          <p className="text-xs text-muted-foreground">{block.category}</p>
        </div>
        <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onClose} data-testid="config-close-button">
          <X className="h-4 w-4" />
        </Button>
      </div>

      {/* Content */}
      <ScrollArea className="flex-1">
        <div className="p-4 space-y-6">
          {/* Description */}
          <div className="space-y-2">
            <div className="flex items-center gap-1 text-xs font-medium text-muted-foreground">
              <Info className="h-3 w-3" />
              <span>DESCRIPTION</span>
            </div>
            <p className="text-sm">{block.description}</p>
          </div>

          <Separator />

          {/* Parameters */}
          <div className="space-y-4">
            <div className="flex items-center gap-1 text-xs font-medium text-muted-foreground">
              <Settings className="h-3 w-3" />
              <span>PARAMETERS</span>
            </div>

            {block.parameters.length > 0 ? (
              <div className="space-y-4">
                {block.parameters.map((param) =>
                  renderParameter(param, params[param.id], (value) =>
                    handleParamChange(param.id, value)
                  )
                )}
              </div>
            ) : (
              <p className="text-sm text-muted-foreground italic">
                No configurable parameters
              </p>
            )}
          </div>

          <Separator />

          {/* Estimates */}
          {estimates && (
            <div className="space-y-3">
              <div className="flex items-center gap-1 text-xs font-medium text-muted-foreground">
                <Zap className="h-3 w-3" />
                <span>ESTIMATES</span>
              </div>

              <div className="grid grid-cols-2 gap-2">
                <div className="p-2 bg-muted/50 rounded-md">
                  <div className="flex items-center gap-1 text-xs text-muted-foreground">
                    <Clock className="h-3 w-3" />
                    <span>Latency</span>
                  </div>
                  <p className="text-sm font-medium mt-0.5">
                    ~{estimates.latency}ms
                  </p>
                </div>

                <div className="p-2 bg-muted/50 rounded-md">
                  <div className="flex items-center gap-1 text-xs text-muted-foreground">
                    <Coins className="h-3 w-3" />
                    <span>Cost</span>
                  </div>
                  <p className="text-sm font-medium mt-0.5">
                    ${estimates.cost.toFixed(4)}
                  </p>
                </div>

                <div className="p-2 bg-muted/50 rounded-md col-span-2">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-1 text-xs text-muted-foreground">
                      <span>Tokens</span>
                    </div>
                    <span className="text-sm font-medium">
                      ~{estimates.tokens.toLocaleString()}
                    </span>
                  </div>
                  <div className="flex items-center justify-between mt-1">
                    <div className="flex items-center gap-1 text-xs text-muted-foreground">
                      <span>Accuracy Impact</span>
                    </div>
                    <Badge
                      variant={estimates.accuracy >= 0.5 ? 'default' : 'secondary'}
                      className="text-[10px]"
                    >
                      {estimates.accuracy >= 0 ? '+' : ''}
                      {(estimates.accuracy * 100).toFixed(0)}%
                    </Badge>
                  </div>
                </div>
              </div>
            </div>
          )}

          <Separator />

          {/* Use Cases */}
          {block.useCases.length > 0 && (
            <div className="space-y-2">
              <div className="text-xs font-medium text-muted-foreground">USE CASES</div>
              <ul className="text-sm space-y-1">
                {block.useCases.map((useCase, i) => (
                  <li key={i} className="flex items-start gap-2">
                    <span className="text-muted-foreground">•</span>
                    <span>{useCase}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* Code References */}
          {block.codeReferences.length > 0 && (
            <div className="space-y-2">
              <div className="flex items-center gap-1 text-xs font-medium text-muted-foreground">
                <Code className="h-3 w-3" />
                <span>CODE REFERENCES</span>
              </div>
              <div className="space-y-1">
                {block.codeReferences.map((ref, i) => (
                  <TooltipProvider key={i}>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <div className="flex items-center gap-1 text-xs p-1.5 bg-muted/50 rounded cursor-help">
                          <Badge variant="outline" className="text-[9px]">
                            {ref.type}
                          </Badge>
                          <span className="truncate flex-1 font-mono text-muted-foreground">
                            {ref.path.split('/').pop()}
                          </span>
                          <ExternalLink className="h-3 w-3 text-muted-foreground" />
                        </div>
                      </TooltipTrigger>
                      <TooltipContent side="left" className="max-w-xs">
                        <p className="font-medium">{ref.description}</p>
                        <p className="text-xs text-muted-foreground mt-1 font-mono">
                          {ref.path}
                        </p>
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                ))}
              </div>
            </div>
          )}
        </div>
      </ScrollArea>

      {/* Footer */}
      <div className="p-3 border-t">
        <div className="flex items-center justify-between text-xs">
          <Badge variant="outline">{block.requiredTier} Tier</Badge>
          <span className="text-muted-foreground">
            Max {block.maxInstances} instance{block.maxInstances > 1 ? 's' : ''}
          </span>
        </div>
      </div>
    </div>
  );
}

export default BlockConfigPanel;
