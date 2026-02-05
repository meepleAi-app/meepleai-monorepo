'use client';

/**
 * Plugin Node Component
 *
 * Custom React Flow node for displaying plugin nodes in the pipeline canvas.
 * Shows plugin info, status, and provides connection handles.
 *
 * @version 1.0.0
 * @see Issue #3425 - Visual Pipeline Builder
 */

import { memo } from 'react';

import { Handle, Position, type NodeProps, type Node } from '@xyflow/react';
import { motion } from 'framer-motion';

import { cn } from '@/lib/utils';
import { usePipelineBuilderStore } from '@/stores/pipelineBuilderStore';

import { PLUGIN_CATEGORY_COLORS, PLUGIN_CATEGORY_ICONS } from './types';

import type { PluginNodeData, PluginCategory } from './types';

// =============================================================================
// Component
// =============================================================================

export const PluginNodeComponent = memo(function PluginNodeComponent({
  id,
  data,
  selected,
}: NodeProps<Node<PluginNodeData>>) {
  const { selectedNodeId } = usePipelineBuilderStore();

  // Cast data to PluginNodeData since React Flow passes it as unknown
  const nodeData = data as PluginNodeData;

  const isSelected = selected || selectedNodeId === id;
  const categoryColor = PLUGIN_CATEGORY_COLORS[nodeData.category as PluginCategory];
  const categoryIcon = PLUGIN_CATEGORY_ICONS[nodeData.category as PluginCategory];

  // Execution status styling
  const executionStyles = {
    pending: 'opacity-50',
    running: 'ring-2 ring-blue-500 animate-pulse',
    success: 'ring-2 ring-green-500',
    error: 'ring-2 ring-red-500',
  };

  const statusIcons = {
    pending: '⏳',
    running: '🔄',
    success: '✅',
    error: '❌',
  };

  return (
    <motion.div
      initial={{ scale: 0.8, opacity: 0 }}
      animate={{ scale: 1, opacity: 1 }}
      transition={{ type: 'spring', stiffness: 300, damping: 20 }}
      className={cn(
        'relative min-w-[200px] rounded-xl shadow-lg transition-all duration-200',
        'bg-card border-2',
        isSelected ? 'border-primary shadow-xl scale-105' : 'border-border',
        !nodeData.isValid && 'border-destructive',
        nodeData.executionStatus && executionStyles[nodeData.executionStatus]
      )}
      style={{
        borderLeftColor: categoryColor,
        borderLeftWidth: '4px',
      }}
    >
      {/* Input Handle */}
      <Handle
        type="target"
        position={Position.Left}
        className={cn(
          'w-3 h-3 !bg-muted-foreground border-2 border-background',
          'hover:!bg-primary hover:scale-125 transition-all'
        )}
      />

      {/* Header */}
      <div
        className="px-4 py-2 rounded-t-xl flex items-center gap-2"
        style={{ backgroundColor: `${categoryColor}20` }}
      >
        <span className="text-lg" role="img" aria-label={nodeData.category}>
          {categoryIcon}
        </span>
        <span className="font-semibold text-sm truncate flex-1">{nodeData.pluginName}</span>
        {nodeData.executionStatus && (
          <span className="text-sm" role="img" aria-label={nodeData.executionStatus}>
            {statusIcons[nodeData.executionStatus]}
          </span>
        )}
      </div>

      {/* Body */}
      <div className="px-4 py-3 space-y-2">
        {/* Category Badge */}
        <div className="flex items-center gap-2">
          <span
            className="px-2 py-0.5 rounded-full text-xs font-medium capitalize"
            style={{
              backgroundColor: `${categoryColor}20`,
              color: categoryColor,
            }}
          >
            {nodeData.category}
          </span>
          {!nodeData.isValid && (
            <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-destructive/20 text-destructive">
              Invalid
            </span>
          )}
        </div>

        {/* Config Preview */}
        {Object.keys(nodeData.config).length > 0 && (
          <div className="text-xs text-muted-foreground space-y-1">
            {Object.entries(nodeData.config)
              .slice(0, 3)
              .map(([key, value]) => (
                <div key={key} className="flex items-center gap-1 truncate">
                  <span className="font-medium">{key}:</span>
                  <span className="text-foreground/70 truncate">
                    {typeof value === 'object'
                      ? JSON.stringify(value).slice(0, 20) + '...'
                      : String(value)}
                  </span>
                </div>
              ))}
            {Object.keys(nodeData.config).length > 3 && (
              <span className="text-muted-foreground/60">
                +{Object.keys(nodeData.config).length - 3} more
              </span>
            )}
          </div>
        )}

        {/* Execution Result */}
        {nodeData.executionResult && (
          <div className="mt-2 p-2 bg-muted/50 rounded-md">
            <div className="text-xs space-y-1">
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Duration:</span>
                <span className="font-mono">{nodeData.executionResult.durationMs.toFixed(0)}ms</span>
              </div>
              {nodeData.executionResult.tokensUsed && (
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Tokens:</span>
                  <span className="font-mono">{nodeData.executionResult.tokensUsed}</span>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Validation Errors */}
        {nodeData.validationErrors && nodeData.validationErrors.length > 0 && (
          <div className="mt-2 p-2 bg-destructive/10 rounded-md">
            <ul className="text-xs text-destructive space-y-0.5">
              {nodeData.validationErrors.map((error: string, i: number) => (
                <li key={i} className="flex items-start gap-1">
                  <span>⚠</span>
                  <span>{error}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>

      {/* Output Handle */}
      <Handle
        type="source"
        position={Position.Right}
        className={cn(
          'w-3 h-3 !bg-muted-foreground border-2 border-background',
          'hover:!bg-primary hover:scale-125 transition-all'
        )}
      />

      {/* Selection indicator */}
      {isSelected && (
        <motion.div
          layoutId="node-selection"
          className="absolute inset-0 rounded-xl border-2 border-primary pointer-events-none"
          initial={false}
          transition={{ type: 'spring', stiffness: 300, damping: 30 }}
        />
      )}
    </motion.div>
  );
});
