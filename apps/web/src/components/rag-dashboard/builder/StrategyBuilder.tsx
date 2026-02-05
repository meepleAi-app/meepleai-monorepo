'use client';

/**
 * RAG Pipeline Builder - Strategy Builder
 *
 * Main component combining BlockPalette and PipelineCanvas.
 * Provides the complete visual pipeline builder experience.
 *
 * @see #3457 - Setup ReactFlow canvas infrastructure for strategy builder
 */

import { useState, useCallback } from 'react';
import { ReactFlowProvider } from '@xyflow/react';
import { PanelLeftClose, PanelLeftOpen, Save, Play, RotateCcw } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/primitives/button';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { BlockPalette } from './BlockPalette';
import { PipelineCanvas } from './PipelineCanvas';
import type {
  RagNode,
  RagEdge,
  RagBlock,
  UserTier,
  PipelineDefinition,
} from './types';

// =============================================================================
// Types
// =============================================================================

export interface StrategyBuilderProps {
  /** Current user tier for access control */
  userTier: UserTier;
  /** Initial pipeline to load */
  initialPipeline?: PipelineDefinition;
  /** Callback when pipeline is saved */
  onSave?: (pipeline: PipelineDefinition) => void;
  /** Callback when pipeline is tested */
  onTest?: (pipeline: PipelineDefinition) => void;
  /** Whether builder is read-only */
  readOnly?: boolean;
  /** Additional class names */
  className?: string;
}

// =============================================================================
// Main Component
// =============================================================================

export function StrategyBuilder({
  userTier,
  initialPipeline,
  onSave,
  onTest,
  readOnly = false,
  className,
}: StrategyBuilderProps) {
  const [paletteCollapsed, setPaletteCollapsed] = useState(false);
  const [currentNodes, setCurrentNodes] = useState<RagNode[]>(initialPipeline?.nodes || []);
  const [currentEdges, setCurrentEdges] = useState<RagEdge[]>(initialPipeline?.edges || []);
  const [hasChanges, setHasChanges] = useState(false);

  // Handle pipeline changes from canvas
  const handlePipelineChange = useCallback((nodes: RagNode[], edges: RagEdge[]) => {
    setCurrentNodes(nodes);
    setCurrentEdges(edges);
    setHasChanges(true);
  }, []);

  // Handle block drag start
  const handleBlockDragStart = useCallback((_block: RagBlock, _event: React.DragEvent) => {
    // Visual feedback could be added here
  }, []);

  // Handle save
  const handleSave = useCallback(() => {
    const pipeline: PipelineDefinition = {
      id: initialPipeline?.id || `pipeline-${Date.now()}`,
      name: initialPipeline?.name || 'New Pipeline',
      description: initialPipeline?.description || '',
      version: initialPipeline?.version || '1.0.0',
      nodes: currentNodes,
      edges: currentEdges,
      createdAt: initialPipeline?.createdAt || new Date(),
      updatedAt: new Date(),
      createdBy: initialPipeline?.createdBy || 'unknown',
      isActive: initialPipeline?.isActive ?? false,
    };

    onSave?.(pipeline);
    setHasChanges(false);
  }, [currentNodes, currentEdges, initialPipeline, onSave]);

  // Handle test
  const handleTest = useCallback(() => {
    const pipeline: PipelineDefinition = {
      id: initialPipeline?.id || `pipeline-${Date.now()}`,
      name: initialPipeline?.name || 'New Pipeline',
      description: initialPipeline?.description || '',
      version: initialPipeline?.version || '1.0.0',
      nodes: currentNodes,
      edges: currentEdges,
      createdAt: initialPipeline?.createdAt || new Date(),
      updatedAt: new Date(),
      createdBy: initialPipeline?.createdBy || 'unknown',
      isActive: initialPipeline?.isActive ?? false,
    };

    onTest?.(pipeline);
  }, [currentNodes, currentEdges, initialPipeline, onTest]);

  // Handle reset
  const handleReset = useCallback(() => {
    setCurrentNodes(initialPipeline?.nodes || []);
    setCurrentEdges(initialPipeline?.edges || []);
    setHasChanges(false);
  }, [initialPipeline]);

  return (
    <div className={cn('flex h-full', className)}>
      {/* Block Palette */}
      <BlockPalette
        userTier={userTier}
        onBlockDragStart={handleBlockDragStart}
        collapsed={paletteCollapsed}
        onCollapseChange={setPaletteCollapsed}
      />

      {/* Main Canvas Area */}
      <div className="flex-1 flex flex-col">
        {/* Toolbar */}
        <div className="h-12 bg-card border-b flex items-center justify-between px-4">
          <div className="flex items-center gap-2">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-8 w-8"
                    onClick={() => setPaletteCollapsed(!paletteCollapsed)}
                  >
                    {paletteCollapsed ? (
                      <PanelLeftOpen className="h-4 w-4" />
                    ) : (
                      <PanelLeftClose className="h-4 w-4" />
                    )}
                  </Button>
                </TooltipTrigger>
                <TooltipContent>
                  {paletteCollapsed ? 'Show Palette' : 'Hide Palette'}
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>

            <div className="h-6 w-px bg-border" />

            <span className="text-sm font-medium">
              {initialPipeline?.name || 'New Strategy'}
            </span>
            {hasChanges && (
              <span className="text-xs text-muted-foreground">(unsaved changes)</span>
            )}
          </div>

          <div className="flex items-center gap-2">
            {!readOnly && (
              <>
                <TooltipProvider>
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={handleReset}
                        disabled={!hasChanges}
                      >
                        <RotateCcw className="h-4 w-4 mr-1" />
                        Reset
                      </Button>
                    </TooltipTrigger>
                    <TooltipContent>Discard changes</TooltipContent>
                  </Tooltip>
                </TooltipProvider>

                <TooltipProvider>
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={handleSave}
                        disabled={!hasChanges}
                      >
                        <Save className="h-4 w-4 mr-1" />
                        Save
                      </Button>
                    </TooltipTrigger>
                    <TooltipContent>Save pipeline</TooltipContent>
                  </Tooltip>
                </TooltipProvider>
              </>
            )}

            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant="default"
                    size="sm"
                    onClick={handleTest}
                    disabled={currentNodes.length === 0}
                  >
                    <Play className="h-4 w-4 mr-1" />
                    Test
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Test pipeline with sample query</TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </div>
        </div>

        {/* Canvas */}
        <ReactFlowProvider>
          <PipelineCanvas
            initialPipeline={initialPipeline}
            onPipelineChange={handlePipelineChange}
            readOnly={readOnly}
            className="flex-1"
          />
        </ReactFlowProvider>
      </div>
    </div>
  );
}

export default StrategyBuilder;
