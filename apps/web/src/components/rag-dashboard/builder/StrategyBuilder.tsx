'use client';

/**
 * RAG Pipeline Builder - Strategy Builder
 *
 * Main component combining BlockPalette, PipelineCanvas, BlockConfigPanel, and ValidationPanel.
 * Provides the complete visual pipeline builder experience with real-time validation.
 *
 * @see #3457 - Setup ReactFlow canvas infrastructure for strategy builder
 * @see #3458 - Tier 1 blocks (7 essential RAG blocks)
 * @see #3459 - Drag-drop mechanics
 * @see #3460 - Block connection validation
 * @see #3461 - Parameter configuration panel
 * @see #3462 - Strategy validation engine
 */

import { useState, useCallback, useMemo, useEffect } from 'react';

import { ReactFlowProvider, type Node, type Edge } from '@xyflow/react';
import {
  PanelLeftClose,
  PanelLeftOpen,
  PanelRightClose,
  PanelRightOpen,
  Save,
  Play,
  RotateCcw,
} from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import { BlockConfigPanel } from './BlockConfigPanel';
import { BlockPalette } from './BlockPalette';
import { PipelineCanvas } from './PipelineCanvas';
import { PipelineTestPanel } from './PipelineTestPanel';
import { validatePipeline } from './validation-engine';
import { ValidationPanel } from './ValidationPanel';

import type {
  RagNode,
  RagEdge,
  RagBlock,
  UserTier,
  PipelineDefinition,
  RagNodeData,
  NodeConfigChangeEvent,
} from './types';
import type { DetailedValidation } from './validation-engine';

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
  /** Show validation panel */
  showValidation?: boolean;
  /** Show config panel */
  showConfig?: boolean;
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
  showValidation = true,
  showConfig = true,
}: StrategyBuilderProps) {
  // Panel states
  const [paletteCollapsed, setPaletteCollapsed] = useState(false);
  const [configPanelOpen, setConfigPanelOpen] = useState(false);
  const [testPanelOpen, setTestPanelOpen] = useState(false);

  // Pipeline state
  const [currentNodes, setCurrentNodes] = useState<RagNode[]>(
    initialPipeline?.nodes || []
  );
  const [currentEdges, setCurrentEdges] = useState<RagEdge[]>(
    initialPipeline?.edges || []
  );
  const [hasChanges, setHasChanges] = useState(false);

  // Selection state
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);

  // Validation state
  const [validation, setValidation] = useState<DetailedValidation | null>(null);

  // Get selected node and its block
  const selectedNode = useMemo(() => {
    if (!selectedNodeId) return null;
    return currentNodes.find((n) => n.id === selectedNodeId) || null;
  }, [selectedNodeId, currentNodes]);

  const selectedBlock = useMemo(() => {
    if (!selectedNode) return null;
    const data = selectedNode.data as RagNodeData | undefined;
    return data?.block || null;
  }, [selectedNode]);

  const selectedParams = useMemo(() => {
    if (!selectedNode) return {};
    const data = selectedNode.data as RagNodeData | undefined;
    return data?.params || {};
  }, [selectedNode]);

  // Run validation when pipeline changes
  useEffect(() => {
    const result = validatePipeline({
      nodes: currentNodes as Node<RagNodeData>[],
      edges: currentEdges as Edge[],
      userTier,
    });
    setValidation(result);
  }, [currentNodes, currentEdges, userTier]);

  // Handle pipeline changes from canvas
  const handlePipelineChange = useCallback(
    (nodes: RagNode[], edges: RagEdge[]) => {
      setCurrentNodes(nodes);
      setCurrentEdges(edges);
      setHasChanges(true);
    },
    []
  );

  // Handle node selection
  const handleNodeSelect = useCallback((nodeId: string | null) => {
    setSelectedNodeId(nodeId);
    if (nodeId) {
      setConfigPanelOpen(true);
    }
  }, []);

  // Handle parameter change
  const handleParamChange = useCallback(
    (event: NodeConfigChangeEvent) => {
      setCurrentNodes((nodes) =>
        nodes.map((node) => {
          if (node.id !== event.nodeId) return node;
          const data = node.data as RagNodeData;
          return {
            ...node,
            data: {
              ...data,
              params: {
                ...data.params,
                [event.paramId]: event.value,
              },
            },
          };
        })
      );
      setHasChanges(true);
    },
    []
  );

  // Handle block drag start
  const handleBlockDragStart = useCallback(
    (_block: RagBlock, _event: React.DragEvent) => {
      // Visual feedback could be added here
    },
    []
  );

  // Build pipeline definition
  const buildPipelineDefinition = useCallback((): PipelineDefinition => {
    return {
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
  }, [currentNodes, currentEdges, initialPipeline]);

  // Handle save
  const handleSave = useCallback(() => {
    if (!validation?.isValid) {
      // Could show a confirmation dialog here
      return;
    }
    onSave?.(buildPipelineDefinition());
    setHasChanges(false);
  }, [buildPipelineDefinition, onSave, validation]);

  // Handle test - opens test panel
  const handleTest = useCallback(() => {
    setTestPanelOpen(true);
    setConfigPanelOpen(false);
    onTest?.(buildPipelineDefinition());
  }, [buildPipelineDefinition, onTest]);

  // Handle reset
  const handleReset = useCallback(() => {
    setCurrentNodes(initialPipeline?.nodes || []);
    setCurrentEdges(initialPipeline?.edges || []);
    setHasChanges(false);
    setSelectedNodeId(null);
  }, [initialPipeline]);

  // Handle config panel close
  const handleConfigClose = useCallback(() => {
    setConfigPanelOpen(false);
    setSelectedNodeId(null);
  }, []);

  // Handle test panel close
  const handleTestPanelClose = useCallback(() => {
    setTestPanelOpen(false);
  }, []);

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
      <div className="flex-1 flex flex-col min-w-0">
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
              <span className="text-xs text-muted-foreground">
                (unsaved changes)
              </span>
            )}

            {/* Validation status badge */}
            {showValidation && validation && (
              <>
                <div className="h-6 w-px bg-border" />
                <ValidationPanel validation={validation} compact />
              </>
            )}
          </div>

          <div className="flex items-center gap-2">
            {showConfig && (
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button
                      variant={configPanelOpen ? 'secondary' : 'ghost'}
                      size="icon"
                      className="h-8 w-8"
                      onClick={() => setConfigPanelOpen(!configPanelOpen)}
                    >
                      {configPanelOpen ? (
                        <PanelRightClose className="h-4 w-4" />
                      ) : (
                        <PanelRightOpen className="h-4 w-4" />
                      )}
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent>
                    {configPanelOpen ? 'Hide Config' : 'Show Config'}
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            )}

            <div className="h-6 w-px bg-border" />

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
                        disabled={!hasChanges || !validation?.isValid}
                      >
                        <Save className="h-4 w-4 mr-1" />
                        Save
                      </Button>
                    </TooltipTrigger>
                    <TooltipContent>
                      {!validation?.isValid
                        ? 'Fix validation errors before saving'
                        : 'Save pipeline'}
                    </TooltipContent>
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

        {/* Canvas with optional validation panel */}
        <div className="flex-1 flex min-h-0">
          <ReactFlowProvider>
            <PipelineCanvas
              initialPipeline={initialPipeline}
              onPipelineChange={handlePipelineChange}
              onNodeSelect={handleNodeSelect}
              selectedNodeId={selectedNodeId}
              readOnly={readOnly}
              className="flex-1"
            />
          </ReactFlowProvider>

          {/* Validation Panel (inline) */}
          {showValidation && !configPanelOpen && (
            <div className="w-72 border-l bg-card">
              <div className="p-3 border-b">
                <h3 className="font-medium text-sm">Validation</h3>
              </div>
              <ValidationPanel validation={validation} />
            </div>
          )}
        </div>
      </div>

      {/* Config Panel (replaces validation when open) */}
      {showConfig && configPanelOpen && (
        <BlockConfigPanel
          block={selectedBlock}
          params={selectedParams}
          nodeId={selectedNodeId}
          onParamChange={handleParamChange}
          onClose={handleConfigClose}
        />
      )}

      {/* Test Panel (Issue #3463) */}
      {testPanelOpen && (
        <div className="w-80 border-l flex flex-col">
          <div className="p-2 border-b flex items-center justify-between">
            <span className="text-sm font-medium">Test Pipeline</span>
            <Button
              variant="ghost"
              size="icon"
              className="h-6 w-6"
              onClick={handleTestPanelClose}
            >
              <PanelRightClose className="h-4 w-4" />
            </Button>
          </div>
          <PipelineTestPanel
            pipeline={buildPipelineDefinition()}
            className="flex-1"
            disabled={currentNodes.length === 0}
          />
        </div>
      )}
    </div>
  );
}

export default StrategyBuilder;
