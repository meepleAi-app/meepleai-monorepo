'use client';

/**
 * Pipeline Edge Component
 *
 * Custom React Flow edge for connections between plugin nodes.
 * Shows condition labels and supports animated execution visualization.
 *
 * @version 1.0.0
 * @see Issue #3425 - Visual Pipeline Builder
 */

import { memo } from 'react';

import {
  BaseEdge,
  EdgeLabelRenderer,
  getBezierPath,
  type EdgeProps,
  type Edge,
} from '@xyflow/react';
import { motion } from 'framer-motion';

import { cn } from '@/lib/utils';
import { usePipelineBuilderStore } from '@/stores/pipelineBuilderStore';

import { CONDITION_PRESETS } from './types';

import type { PipelineEdgeData, ConditionPreset } from './types';

// =============================================================================
// Component
// =============================================================================

export const PipelineEdgeComponent = memo(function PipelineEdgeComponent({
  id,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  style = {},
  data,
  selected,
  animated,
}: EdgeProps<Edge<PipelineEdgeData>>) {
  const { selectedEdgeId } = usePipelineBuilderStore();

  // Cast data to PipelineEdgeData since React Flow passes it as unknown
  const edgeData = data as PipelineEdgeData | undefined;

  const isSelected = selected || selectedEdgeId === id;

  const [edgePath, labelX, labelY] = getBezierPath({
    sourceX,
    sourceY,
    sourcePosition,
    targetX,
    targetY,
    targetPosition,
  });

  // Get display label for condition
  const getConditionLabel = () => {
    if (!edgeData) return '';
    if (edgeData.label) return edgeData.label;
    if (edgeData.conditionPreset && edgeData.conditionPreset !== 'custom') {
      return CONDITION_PRESETS[edgeData.conditionPreset as ConditionPreset].label;
    }
    if (edgeData.condition && edgeData.condition !== 'always') {
      return edgeData.condition.length > 20 ? `${edgeData.condition.slice(0, 20)}...` : edgeData.condition;
    }
    return '';
  };

  const conditionLabel = getConditionLabel();
  const hasCondition = conditionLabel && conditionLabel !== 'Always';

  return (
    <>
      {/* Edge Path */}
      <BaseEdge
        id={id}
        path={edgePath}
        style={{
          ...(typeof style === 'object' ? style : {}),
          strokeWidth: isSelected ? 3 : 2,
          stroke: isSelected
            ? 'hsl(var(--primary))'
            : edgeData?.isValid === false
              ? 'hsl(var(--destructive))'
              : 'hsl(var(--muted-foreground))',
        }}
        className={cn(
          'transition-all duration-200',
          animated && 'animate-pulse'
        )}
      />

      {/* Animated flow dots during execution */}
      {animated && (
        <motion.circle
          r={4}
          fill="hsl(var(--primary))"
          initial={{ offsetDistance: '0%' }}
          animate={{ offsetDistance: '100%' }}
          transition={{
            duration: 1.5,
            repeat: Infinity,
            ease: 'linear',
          }}
          style={{
            offsetPath: `path('${edgePath}')`,
          }}
        />
      )}

      {/* Edge Label */}
      <EdgeLabelRenderer>
        <div
          style={{
            position: 'absolute',
            transform: `translate(-50%, -50%) translate(${labelX}px,${labelY}px)`,
            pointerEvents: 'all',
          }}
          className="nodrag nopan"
        >
          {/* Condition Badge */}
          {hasCondition && (
            <motion.div
              initial={{ scale: 0, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              className={cn(
                'px-2 py-1 rounded-md text-xs font-medium shadow-md',
                'bg-card border',
                isSelected ? 'border-primary' : 'border-border',
                edgeData?.isValid === false && 'border-destructive bg-destructive/10'
              )}
            >
              <span className="text-muted-foreground">
                {conditionLabel}
              </span>
            </motion.div>
          )}

          {/* Timeout indicator */}
          {edgeData?.timeout && edgeData.timeout > 0 && (
            <div className="mt-1 text-center">
              <span className="text-[10px] text-muted-foreground/60">
                ⏱ {edgeData.timeout}ms
              </span>
            </div>
          )}

          {/* Validation error */}
          {edgeData?.validationError && (
            <div className="mt-1 px-2 py-0.5 bg-destructive/10 rounded text-[10px] text-destructive">
              {edgeData.validationError}
            </div>
          )}
        </div>
      </EdgeLabelRenderer>

      {/* Selection highlight overlay */}
      {isSelected && (
        <BaseEdge
          id={`${id}-selection`}
          path={edgePath}
          style={{
            strokeWidth: 8,
            stroke: 'hsl(var(--primary) / 0.2)',
          }}
        />
      )}
    </>
  );
});
