'use client';

/**
 * RAG Pipeline Builder - Pipeline Canvas
 *
 * ReactFlow canvas with custom nodes, edge validation, pan/zoom, and minimap.
 *
 * @see #3457 - Setup ReactFlow canvas infrastructure for strategy builder
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ReactFlow,
  Controls,
  MiniMap,
  Background,
  BackgroundVariant,
  Panel,
  useNodesState,
  useEdgesState,
  addEdge,
  ConnectionMode,
  type Connection,
  type Edge,
  type Node,
  type NodeTypes,
  type OnConnect,
  type OnNodesChange,
  type OnEdgesChange,
  type ReactFlowInstance,
  type XYPosition,
  MarkerType,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { ZoomIn, ZoomOut, Maximize, Grid3X3, Map } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/primitives/button';
import { Badge } from '@/components/ui/data-display/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { RagBlockNode } from './RagBlockNode';
import type {
  RagNode,
  RagEdge,
  RagNodeData,
  RagBlockType,
  PipelineDefinition,
} from './types';
import { BLOCKS_BY_TYPE, isValidConnection as checkBlockConnection } from './block-definitions';
import { validatePipelineConstraints, PIPELINE_CONSTRAINTS } from './block-metadata';

// =============================================================================
// Types
// =============================================================================

export interface PipelineCanvasProps {
  /** Initial pipeline definition */
  initialPipeline?: PipelineDefinition;
  /** Callback when pipeline changes */
  onPipelineChange?: (nodes: RagNode[], edges: RagEdge[]) => void;
  /** Whether canvas is read-only */
  readOnly?: boolean;
  /** Additional class names */
  className?: string;
}

// =============================================================================
// Custom Node Types
// =============================================================================

const nodeTypes: NodeTypes = {
  ragBlock: RagBlockNode as unknown as NodeTypes[string],
};

// =============================================================================
// Edge Validation
// =============================================================================

function isConnectionValidForCanvas(
  connection: Connection,
  nodes: Node[]
): boolean {
  const { source, target, sourceHandle, targetHandle } = connection;

  if (!source || !target || source === target) return false;

  const sourceNode = nodes.find((n) => n.id === source);
  const targetNode = nodes.find((n) => n.id === target);

  if (!sourceNode || !targetNode) return false;
  if (!sourceNode.data || !targetNode.data) return false;
  if (typeof sourceNode.data !== 'object' || typeof targetNode.data !== 'object') return false;
  if (!('block' in sourceNode.data) || !('block' in targetNode.data)) return false;

  const sourceData = sourceNode.data as RagNodeData;
  const targetData = targetNode.data as RagNodeData;

  const sourceBlock = sourceData.block;
  const targetBlock = targetData.block;

  // Check if connection is allowed between these block types
  if (!checkBlockConnection(sourceBlock.type, targetBlock.type)) {
    return false;
  }

  // Check port data type compatibility
  const sourcePort = sourceBlock.outputs.find((p) => p.id === sourceHandle);
  const targetPort = targetBlock.inputs.find((p) => p.id === targetHandle);

  if (!sourcePort || !targetPort) return false;

  // Any type matches everything
  if (sourcePort.dataType === 'any' || targetPort.dataType === 'any') {
    return true;
  }

  return sourcePort.dataType === targetPort.dataType;
}

// =============================================================================
// Helpers
// =============================================================================

function createNodeId(): string {
  return `node-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
}

function createNodeFromBlock(
  blockType: RagBlockType,
  position: XYPosition
): Node | null {
  const block = BLOCKS_BY_TYPE[blockType];
  if (!block) return null;

  const data: RagNodeData = {
    block,
    params: { ...block.defaultParams },
    status: 'idle',
  };

  return {
    id: createNodeId(),
    type: 'ragBlock',
    position,
    data,
  };
}

// =============================================================================
// Main Component
// =============================================================================

export function PipelineCanvas({
  initialPipeline,
  onPipelineChange,
  readOnly = false,
  className,
}: PipelineCanvasProps) {
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const [reactFlowInstance, setReactFlowInstance] = useState<ReactFlowInstance | null>(null);
  const [showMinimap, setShowMinimap] = useState(true);
  const [showGrid, setShowGrid] = useState(true);

  // Nodes and edges state - use ReactFlow default types
  const initialNodes = (initialPipeline?.nodes || []) as unknown as Node[];
  const initialEdges = (initialPipeline?.edges || []) as unknown as Edge[];

  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);

  // Calculate validation
  const validation = useMemo(() => {
    const blockTypes = nodes
      .filter((n) => n.data && typeof n.data === 'object' && 'block' in n.data)
      .map((n) => {
        const data = n.data as RagNodeData;
        return data.block.type;
      });
    return validatePipelineConstraints(blockTypes, edges.length);
  }, [nodes, edges]);

  // Handle connection
  const onConnect: OnConnect = useCallback(
    (connection) => {
      if (readOnly) return;

      if (!isConnectionValidForCanvas(connection, nodes)) {
        return;
      }

      const newEdge: Edge = {
        ...connection,
        id: `edge-${Date.now()}`,
        type: 'smoothstep',
        animated: false,
        markerEnd: {
          type: MarkerType.ArrowClosed,
          width: 15,
          height: 15,
        },
      } as Edge;

      setEdges((eds) => addEdge(newEdge, eds));
    },
    [nodes, readOnly, setEdges]
  );

  // Handle node changes
  const handleNodesChange: OnNodesChange = useCallback(
    (changes) => {
      if (readOnly) return;
      onNodesChange(changes);
    },
    [readOnly, onNodesChange]
  );

  // Handle edge changes
  const handleEdgesChange: OnEdgesChange = useCallback(
    (changes) => {
      if (readOnly) return;
      onEdgesChange(changes);
    },
    [readOnly, onEdgesChange]
  );

  // Handle drop from palette
  const onDragOver = useCallback((event: React.DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'copy';
  }, []);

  const onDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();
      if (readOnly || !reactFlowInstance || !reactFlowWrapper.current) return;

      const data = event.dataTransfer.getData('application/rag-block');
      if (!data) return;

      try {
        const { type } = JSON.parse(data) as { type: RagBlockType };

        const reactFlowBounds = reactFlowWrapper.current.getBoundingClientRect();
        const position = reactFlowInstance.screenToFlowPosition({
          x: event.clientX - reactFlowBounds.left,
          y: event.clientY - reactFlowBounds.top,
        });

        const newNode = createNodeFromBlock(type, position);
        if (newNode) {
          setNodes((nds) => [...nds, newNode]);
        }
      } catch (error) {
        console.error('Failed to parse dropped block data:', error);
      }
    },
    [readOnly, reactFlowInstance, setNodes]
  );

  // Notify parent of changes
  useEffect(() => {
    if (onPipelineChange) {
      onPipelineChange(nodes as unknown as RagNode[], edges as unknown as RagEdge[]);
    }
  }, [nodes, edges, onPipelineChange]);

  // Toolbar actions
  const handleZoomIn = useCallback(() => {
    reactFlowInstance?.zoomIn();
  }, [reactFlowInstance]);

  const handleZoomOut = useCallback(() => {
    reactFlowInstance?.zoomOut();
  }, [reactFlowInstance]);

  const handleFitView = useCallback(() => {
    reactFlowInstance?.fitView({ padding: 0.2 });
  }, [reactFlowInstance]);

  return (
    <div
      ref={reactFlowWrapper}
      className={cn('flex-1 h-full bg-background', className)}
    >
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={handleNodesChange}
        onEdgesChange={handleEdgesChange}
        onConnect={onConnect}
        onInit={setReactFlowInstance}
        onDragOver={onDragOver}
        onDrop={onDrop}
        nodeTypes={nodeTypes}
        fitView
        fitViewOptions={{ padding: 0.2 }}
        minZoom={0.1}
        maxZoom={2}
        snapToGrid={showGrid}
        snapGrid={[16, 16]}
        deleteKeyCode={readOnly ? null : ['Backspace', 'Delete']}
        multiSelectionKeyCode={['Shift']}
        panOnScroll
        selectionOnDrag
        panOnDrag={[1, 2]}
        selectNodesOnDrag={false}
        connectionMode={ConnectionMode.Loose}
        defaultEdgeOptions={{
          type: 'smoothstep',
          markerEnd: {
            type: MarkerType.ArrowClosed,
            width: 15,
            height: 15,
          },
        }}
        proOptions={{ hideAttribution: true }}
      >
        {/* Background */}
        {showGrid && (
          <Background
            variant={BackgroundVariant.Dots}
            gap={16}
            size={1}
            color="hsl(var(--muted-foreground) / 0.2)"
          />
        )}

        {/* Controls */}
        <Controls
          showZoom={false}
          showFitView={false}
          showInteractive={false}
          className="!bg-card !border !rounded-lg !shadow-md"
        />

        {/* Minimap */}
        {showMinimap && (
          <MiniMap
            nodeColor={(node) => {
              const data = node.data as RagNodeData | undefined;
              return data?.block?.color || '#888';
            }}
            maskColor="hsl(var(--background) / 0.8)"
            className="!bg-card !border !rounded-lg"
            pannable
            zoomable
          />
        )}

        {/* Top Toolbar */}
        <Panel position="top-right" className="flex items-center gap-2">
          <div className="flex items-center gap-1 bg-card border rounded-lg p-1 shadow-md">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-8 w-8"
                    onClick={handleZoomIn}
                  >
                    <ZoomIn className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Zoom In</TooltipContent>
              </Tooltip>
            </TooltipProvider>

            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-8 w-8"
                    onClick={handleZoomOut}
                  >
                    <ZoomOut className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Zoom Out</TooltipContent>
              </Tooltip>
            </TooltipProvider>

            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-8 w-8"
                    onClick={handleFitView}
                  >
                    <Maximize className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Fit View</TooltipContent>
              </Tooltip>
            </TooltipProvider>

            <div className="w-px h-6 bg-border" />

            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant={showGrid ? 'secondary' : 'ghost'}
                    size="icon"
                    className="h-8 w-8"
                    onClick={() => setShowGrid(!showGrid)}
                  >
                    <Grid3X3 className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Toggle Grid</TooltipContent>
              </Tooltip>
            </TooltipProvider>

            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant={showMinimap ? 'secondary' : 'ghost'}
                    size="icon"
                    className="h-8 w-8"
                    onClick={() => setShowMinimap(!showMinimap)}
                  >
                    <Map className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Toggle Minimap</TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </div>
        </Panel>

        {/* Bottom Stats Panel */}
        <Panel position="bottom-left" className="!mb-2 !ml-2">
          <div className="bg-card border rounded-lg p-2 shadow-md space-y-1.5">
            <div className="flex items-center gap-2 text-xs">
              <span className="text-muted-foreground">Nodes:</span>
              <Badge variant="outline" className="text-xs">
                {nodes.length}/{PIPELINE_CONSTRAINTS.maxNodes}
              </Badge>
              <span className="text-muted-foreground">Edges:</span>
              <Badge variant="outline" className="text-xs">
                {edges.length}/{PIPELINE_CONSTRAINTS.maxEdges}
              </Badge>
            </div>
            <div className="flex items-center gap-2 text-xs">
              <span className="text-muted-foreground">Est. Tokens:</span>
              <Badge
                variant={validation.tokensValid ? 'outline' : 'destructive'}
                className="text-xs"
              >
                {validation.details.estimatedTokens.toLocaleString()}/{PIPELINE_CONSTRAINTS.maxTokens.toLocaleString()}
              </Badge>
            </div>
            <div className="flex items-center gap-2 text-xs">
              <span className="text-muted-foreground">Est. Cost:</span>
              <Badge
                variant={validation.costValid ? 'outline' : 'destructive'}
                className="text-xs"
              >
                ${validation.details.estimatedCost.toFixed(4)}/${PIPELINE_CONSTRAINTS.maxCostUsd.toFixed(2)}
              </Badge>
            </div>
            {!validation.isValid && (
              <p className="text-xs text-destructive font-medium">
                Pipeline exceeds constraints
              </p>
            )}
          </div>
        </Panel>
      </ReactFlow>
    </div>
  );
}

export default PipelineCanvas;
