'use client';

/**
 * Edge Configuration Panel
 *
 * Configure conditions and transforms between pipeline nodes.
 * Supports condition presets, custom expressions, and timeout configuration.
 *
 * @version 1.0.0
 * @see Issue #3428 - Edge Configuration Panel
 */

import { useState, useCallback } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  ArrowRight,
  Clock,
  Code2,
  AlertCircle,
  Check,
  Zap,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Slider } from '@/components/ui/primitives/slider';
import { Textarea } from '@/components/ui/primitives/textarea';
import { cn } from '@/lib/utils';
import { usePipelineBuilderStore, selectSelectedEdge } from '@/stores/pipelineBuilderStore';

import { CONDITION_PRESETS, PLUGIN_CATEGORY_COLORS } from './types';

import type { ConditionPreset } from './types';

// =============================================================================
// Types
// =============================================================================

interface EdgeConfigPanelProps {
  className?: string;
}

// =============================================================================
// Condition Preset Button
// =============================================================================

interface PresetButtonProps {
  preset: ConditionPreset;
  active: boolean;
  onClick: () => void;
}

function PresetButton({ preset, active, onClick }: PresetButtonProps) {
  const presetData = CONDITION_PRESETS[preset];

  return (
    <button
      onClick={onClick}
      className={cn(
        'flex items-center gap-2 px-3 py-2 rounded-md text-sm transition-all',
        'border hover:border-primary/50',
        active
          ? 'bg-primary text-primary-foreground border-primary'
          : 'bg-card border-border hover:bg-muted'
      )}
    >
      {active && <Check className="h-3.5 w-3.5" />}
      <span>{presetData.label}</span>
    </button>
  );
}

// =============================================================================
// Expression Editor
// =============================================================================

interface ExpressionEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  label?: string;
  error?: string;
  syntax?: 'condition' | 'transform';
}

function ExpressionEditor({
  value,
  onChange,
  placeholder,
  label,
  error,
  syntax = 'condition',
}: ExpressionEditorProps) {
  const [showHelp, setShowHelp] = useState(false);

  const syntaxHelp = {
    condition: [
      { example: 'always', desc: 'Always execute' },
      { example: 'never', desc: 'Never execute' },
      { example: 'confidence >= 0.8', desc: 'Confidence threshold' },
      { example: 'queryType == "rules"', desc: 'Query type match' },
      { example: 'output.success && output.score > 0.5', desc: 'Complex condition' },
    ],
    transform: [
      { example: 'output', desc: 'Pass output as-is' },
      { example: 'output.data', desc: 'Extract data field' },
      { example: '{ ...output, extra: "value" }', desc: 'Add field' },
      { example: 'output.items.map(i => i.id)', desc: 'Transform array' },
    ],
  };

  return (
    <div className="space-y-2">
      {label && (
        <div className="flex items-center justify-between">
          <Label className="text-sm">{label}</Label>
          <button
            onClick={() => setShowHelp(!showHelp)}
            className="text-xs text-muted-foreground hover:text-foreground"
          >
            {showHelp ? 'Hide help' : 'Show help'}
          </button>
        </div>
      )}

      <div className="relative">
        <Textarea
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className={cn(
            'font-mono text-sm min-h-[60px] resize-none',
            error && 'border-destructive focus-visible:ring-destructive'
          )}
        />
        <div className="absolute right-2 top-2">
          <Code2 className="h-4 w-4 text-muted-foreground/50" />
        </div>
      </div>

      <AnimatePresence>
        {showHelp && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden"
          >
            <div className="p-2 bg-muted/50 rounded-md space-y-1">
              {syntaxHelp[syntax].map((item) => (
                <div key={item.example} className="flex items-start gap-2 text-xs">
                  <code className="px-1.5 py-0.5 bg-background rounded font-mono text-[10px]">
                    {item.example}
                  </code>
                  <span className="text-muted-foreground">{item.desc}</span>
                </div>
              ))}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {error && (
        <p className="text-xs text-destructive flex items-center gap-1">
          <AlertCircle className="h-3 w-3" />
          {error}
        </p>
      )}
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export function EdgeConfigPanel({ className }: EdgeConfigPanelProps) {
  const selectedEdge = usePipelineBuilderStore(selectSelectedEdge);
  const { pipeline, updateEdge, validateEdge } = usePipelineBuilderStore();

  const handlePresetChange = useCallback(
    (preset: ConditionPreset) => {
      if (!selectedEdge) return;

      const presetData = CONDITION_PRESETS[preset];
      updateEdge(selectedEdge.id, {
        conditionPreset: preset,
        condition: presetData.expression,
      });
      validateEdge(selectedEdge.id);
    },
    [selectedEdge, updateEdge, validateEdge]
  );

  const handleConditionChange = useCallback(
    (condition: string) => {
      if (!selectedEdge) return;

      updateEdge(selectedEdge.id, {
        condition,
        conditionPreset: 'custom',
      });
    },
    [selectedEdge, updateEdge]
  );

  const handleTransformChange = useCallback(
    (transform: string) => {
      if (!selectedEdge) return;
      updateEdge(selectedEdge.id, { transform });
    },
    [selectedEdge, updateEdge]
  );

  const handleTimeoutChange = useCallback(
    (timeout: number) => {
      if (!selectedEdge) return;
      updateEdge(selectedEdge.id, { timeout });
    },
    [selectedEdge, updateEdge]
  );

  const handleLabelChange = useCallback(
    (label: string) => {
      if (!selectedEdge) return;
      updateEdge(selectedEdge.id, { label });
    },
    [selectedEdge, updateEdge]
  );

  if (!selectedEdge) {
    return (
      <div className={cn('flex items-center justify-center h-full', className)}>
        <div className="text-center p-6">
          <ArrowRight className="h-10 w-10 mx-auto text-muted-foreground/50 mb-3" />
          <p className="text-sm text-muted-foreground">Select an edge to configure</p>
        </div>
      </div>
    );
  }

  // Find source and target nodes
  const sourceNode = pipeline?.nodes.find((n) => n.id === selectedEdge.source);
  const targetNode = pipeline?.nodes.find((n) => n.id === selectedEdge.target);

  const edgeData = selectedEdge.data || {
    condition: 'always',
    conditionPreset: 'always' as ConditionPreset,
    isValid: true,
  };

  return (
    <div className={cn('flex flex-col h-full', className)}>
      {/* Header */}
      <div className="p-3 border-b">
        <div className="flex items-center gap-2 text-sm">
          {sourceNode && (
            <Badge
              variant="secondary"
              style={{
                backgroundColor: `${PLUGIN_CATEGORY_COLORS[sourceNode.data.category]}20`,
                color: PLUGIN_CATEGORY_COLORS[sourceNode.data.category],
              }}
            >
              {sourceNode.data.pluginName}
            </Badge>
          )}
          <ArrowRight className="h-4 w-4 text-muted-foreground" />
          {targetNode && (
            <Badge
              variant="secondary"
              style={{
                backgroundColor: `${PLUGIN_CATEGORY_COLORS[targetNode.data.category]}20`,
                color: PLUGIN_CATEGORY_COLORS[targetNode.data.category],
              }}
            >
              {targetNode.data.pluginName}
            </Badge>
          )}
        </div>
        {!edgeData.isValid && (
          <Badge variant="destructive" className="text-[10px] mt-2">
            Invalid Configuration
          </Badge>
        )}
      </div>

      {/* Form */}
      <ScrollArea className="flex-1">
        <div className="p-3 space-y-6">
          {/* Edge Label */}
          <div className="space-y-2">
            <Label className="text-sm">Edge Label (optional)</Label>
            <Input
              value={edgeData.label || ''}
              onChange={(e) => handleLabelChange(e.target.value)}
              placeholder="Custom label..."
              className="h-8"
            />
          </div>

          {/* Condition Presets */}
          <div className="space-y-3">
            <Label className="text-sm flex items-center gap-2">
              <Zap className="h-4 w-4" />
              Condition
            </Label>
            <div className="grid grid-cols-2 gap-2">
              {(
                ['always', 'never', 'high_confidence', 'medium_confidence'] as ConditionPreset[]
              ).map((preset) => (
                <PresetButton
                  key={preset}
                  preset={preset}
                  active={edgeData.conditionPreset === preset}
                  onClick={() => handlePresetChange(preset)}
                />
              ))}
            </div>
            <div className="grid grid-cols-2 gap-2">
              {(['rules_query', 'strategy_query'] as ConditionPreset[]).map((preset) => (
                <PresetButton
                  key={preset}
                  preset={preset}
                  active={edgeData.conditionPreset === preset}
                  onClick={() => handlePresetChange(preset)}
                />
              ))}
            </div>
            <PresetButton
              preset="custom"
              active={edgeData.conditionPreset === 'custom'}
              onClick={() => handlePresetChange('custom')}
            />
          </div>

          {/* Custom Condition Expression */}
          <AnimatePresence>
            {edgeData.conditionPreset === 'custom' && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
              >
                <ExpressionEditor
                  value={edgeData.condition}
                  onChange={handleConditionChange}
                  placeholder="confidence >= 0.8 && queryType == 'rules'"
                  label="Custom Condition Expression"
                  error={edgeData.validationError}
                  syntax="condition"
                />
              </motion.div>
            )}
          </AnimatePresence>

          {/* Transform Expression */}
          <ExpressionEditor
            value={edgeData.transform || ''}
            onChange={handleTransformChange}
            placeholder="output (optional transform)"
            label="Output Transform (optional)"
            syntax="transform"
          />

          {/* Timeout Configuration */}
          <div className="space-y-3">
            <Label className="text-sm flex items-center gap-2">
              <Clock className="h-4 w-4" />
              Timeout
            </Label>
            <div className="space-y-2">
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">0ms (no timeout)</span>
                <span className="font-mono text-foreground">
                  {edgeData.timeout || 0}ms
                </span>
                <span className="text-muted-foreground">5min</span>
              </div>
              <Slider
                value={[edgeData.timeout || 0]}
                min={0}
                max={300000}
                step={1000}
                onValueChange={([v]) => handleTimeoutChange(v)}
              />
              <p className="text-[10px] text-muted-foreground">
                Set to 0 for no timeout. Maximum: 5 minutes (300,000ms)
              </p>
            </div>
          </div>

          {/* Current Configuration Summary */}
          <div className="p-3 bg-muted/50 rounded-md space-y-2">
            <h4 className="text-xs font-medium text-muted-foreground">
              Configuration Summary
            </h4>
            <div className="space-y-1 text-xs">
              <div className="flex justify-between">
                <span className="text-muted-foreground">Condition:</span>
                <code className="font-mono text-[10px] bg-background px-1 rounded">
                  {edgeData.condition || 'always'}
                </code>
              </div>
              {edgeData.transform && (
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Transform:</span>
                  <code className="font-mono text-[10px] bg-background px-1 rounded truncate max-w-[120px]">
                    {edgeData.transform}
                  </code>
                </div>
              )}
              {edgeData.timeout && edgeData.timeout > 0 && (
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Timeout:</span>
                  <span className="font-mono">{edgeData.timeout}ms</span>
                </div>
              )}
            </div>
          </div>
        </div>
      </ScrollArea>
    </div>
  );
}
