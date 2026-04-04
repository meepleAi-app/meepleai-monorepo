'use client';

/**
 * Pipeline Canvas Component
 *
 * Main drag-drop canvas for visual RAG pipeline building using React Flow.
 * Features: zoom/pan, node dragging, edge creation, mini-map, grid background.
 *
 * @version 1.0.0
 * @see Issue #3425 - Visual Pipeline Builder
 */

import { useCallback, useRef, useMemo, type DragEvent } from 'react';

import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  useNodesState,
  useEdgesState,
  addEdge,
  type Connection,
  type Node,
  type Edge,
  ReactFlowProvider,
  useReactFlow,
  BackgroundVariant,
  MarkerType,
  Panel,
  type NodeChange,
  type EdgeChange,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';

import { cn } from '@/lib/utils';
import { usePipelineBuilderStore } from '@/stores/pipelineBuilderStore';

import { PipelineEdgeComponent } from './PipelineEdge';
import { PluginNodeComponent } from './PluginNode';
import { PLUGIN_CATEGORY_COLORS } from './types';

import type { PluginNodeData, PipelineEdgeData, PluginCategory } from './types';

// =============================================================================
// Node and Edge Types
// =============================================================================

// Use type assertion to satisfy React Flow's strict typing while using our custom components
const nodeTypes = {
  plugin: PluginNodeComponent,
} as const;

const edgeTypes = {
  default: PipelineEdgeComponent,
} as const;

// =============================================================================
// Inner Canvas (with React Flow hooks)
// =============================================================================

interface PipelineCanvasInnerProps {
  className?: string;
}

function PipelineCanvasInner({ className }: PipelineCanvasInnerProps) {
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const { screenToFlowPosition } = useReactFlow();

  const {
    pipeline,
    showMiniMap,
    showGrid,
    isLocked,
    isDragging,
    draggedPlugin,
    isExecuting,
    executionTrace,
    currentStepIndex,
    addNode,
    addEdge: addPipelineEdge,
    removeNode,
    removeEdge,
    updateNodePosition,
    selectNode,
    selectEdge,
    endDrag,
  } = usePipelineBuilderStore();

  // Convert store nodes/edges to React Flow format
  const initialNodes = useMemo(() => (pipeline?.nodes || []) as Node[], [pipeline?.nodes]);
  const initialEdges = useMemo(() => (pipeline?.edges || []) as Edge[], [pipeline?.edges]);

  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);

  // Sync React Flow state with store
  const handleNodesChange = useCallback(
    (changes: NodeChange[]) => {
      if (isLocked) return;

      onNodesChange(changes);

      // Handle position changes
      changes.forEach(change => {
        if (change.type === 'position' && 'position' in change && change.position) {
          updateNodePosition(change.id, change.position);
        }
        if (change.type === 'remove') {
          removeNode(change.id);
        }
      });
    },
    [isLocked, onNodesChange, updateNodePosition, removeNode]
  );

  const handleEdgesChange = useCallback(
    (changes: EdgeChange[]) => {
      if (isLocked) return;

      onEdgesChange(changes);

      changes.forEach(change => {
        if (change.type === 'remove') {
          removeEdge(change.id);
        }
      });
    },
    [isLocked, onEdgesChange, removeEdge]
  );

  // Handle new edge connections
  const handleConnect = useCallback(
    (connection: Connection) => {
      if (isLocked || !connection.source || !connection.target) return;

      const edgeId = addPipelineEdge(connection.source, connection.target);
      if (edgeId) {
        const newEdge: Edge<PipelineEdgeData> = {
          id: edgeId,
          source: connection.source,
          target: connection.target,
          type: 'default',
          markerEnd: { type: MarkerType.ArrowClosed },
          data: {
            condition: 'always',
            conditionPreset: 'always',
            isValid: true,
          },
        };
        setEdges(eds => addEdge(newEdge, eds));
      }
    },
    [isLocked, addPipelineEdge, setEdges]
  );

  // Handle node selection
  const handleNodeClick = useCallback(
    (_: React.MouseEvent, node: Node) => {
      selectNode(node.id);
    },
    [selectNode]
  );

  // Handle edge selection
  const handleEdgeClick = useCallback(
    (_: React.MouseEvent, edge: Edge) => {
      selectEdge(edge.id);
    },
    [selectEdge]
  );

  // Handle canvas click (deselect)
  const handlePaneClick = useCallback(() => {
    selectNode(null);
    selectEdge(null);
  }, [selectNode, selectEdge]);

  // Handle drag over canvas
  const handleDragOver = useCallback((event: DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  // Handle drop on canvas
  const handleDrop = useCallback(
    (event: DragEvent) => {
      event.preventDefault();

      if (!draggedPlugin || !reactFlowWrapper.current) {
        return;
      }

      const position = screenToFlowPosition({
        x: event.clientX,
        y: event.clientY,
      });

      const nodeId = addNode(draggedPlugin, position);

      if (nodeId) {
        const newNode: Node<PluginNodeData> = {
          id: nodeId,
          type: 'plugin',
          position,
          data: {
            pluginId: draggedPlugin.id,
            pluginName: draggedPlugin.name,
            category: draggedPlugin.category,
            config: {},
            configSchema: draggedPlugin.configSchema,
            isValid: true,
          },
        };
        setNodes(nds => [...nds, newNode]);
      }

      endDrag();
    },
    [draggedPlugin, screenToFlowPosition, addNode, endDrag, setNodes]
  );

  // Enhance nodes with execution state
  const enhancedNodes = useMemo(() => {
    if (!isExecuting || !executionTrace) return nodes;

    return nodes.map(node => {
      const step = executionTrace.steps.find(s => s.nodeId === node.id);
      const nodeData = node.data as PluginNodeData;
      return {
        ...node,
        data: {
          ...nodeData,
          executionStatus: step?.status,
          executionResult: step?.output
            ? {
                nodeId: node.id,
                input: null,
                output: step.output,
                durationMs: step.durationMs || 0,
              }
            : undefined,
        },
      };
    });
  }, [nodes, isExecuting, executionTrace]);

  // Enhance edges with animation during execution
  const enhancedEdges = useMemo(() => {
    if (!isExecuting || !executionTrace) return edges;

    const executingNodeId = executionTrace.steps[currentStepIndex]?.nodeId;

    return edges.map(edge => ({
      ...edge,
      animated: edge.source === executingNodeId || edge.target === executingNodeId,
      style: {
        ...edge.style,
        stroke: edge.source === executingNodeId ? '#22c55e' : undefined,
        strokeWidth: edge.source === executingNodeId ? 3 : 2,
      },
    }));
  }, [edges, isExecuting, executionTrace, currentStepIndex]);

  // MiniMap node color function
  const miniMapNodeColor = useCallback((node: Node) => {
    const data = node.data as PluginNodeData | undefined;
    return data?.category
      ? PLUGIN_CATEGORY_COLORS[data.category as PluginCategory] || '#6b7280'
      : '#6b7280';
  }, []);

  return (
    <div
      ref={reactFlowWrapper}
      className={cn('h-full w-full', isDragging && 'ring-2 ring-primary ring-offset-2', className)}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
    >
      <ReactFlow
        nodes={enhancedNodes}
        edges={enhancedEdges}
        onNodesChange={handleNodesChange}
        onEdgesChange={handleEdgesChange}
        onConnect={handleConnect}
        onNodeClick={handleNodeClick}
        onEdgeClick={handleEdgeClick}
        onPaneClick={handlePaneClick}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        fitView
        snapToGrid
        snapGrid={[15, 15]}
        defaultEdgeOptions={{
          type: 'default',
          markerEnd: { type: MarkerType.ArrowClosed },
        }}
        proOptions={{ hideAttribution: true }}
        nodesDraggable={!isLocked}
        nodesConnectable={!isLocked}
        elementsSelectable={!isLocked}
        className="bg-background"
      >
        {showGrid && (
          <Background variant={BackgroundVariant.Dots} gap={15} size={1} className="bg-muted/30" />
        )}

        <Controls
          showZoom
          showFitView
          showInteractive={false}
          className="bg-card border border-border rounded-lg shadow-lg"
        />

        {showMiniMap && (
          <MiniMap
            nodeColor={miniMapNodeColor}
            nodeStrokeWidth={3}
            pannable
            zoomable
            className="bg-card/90 border border-border rounded-lg shadow-lg"
          />
        )}

        {/* Status indicator during execution */}
        {isExecuting && (
          <Panel position="top-center">
            <div className="bg-primary text-primary-foreground px-4 py-2 rounded-full shadow-lg animate-pulse flex items-center gap-2">
              <div className="w-2 h-2 bg-green-400 rounded-full animate-ping" />
              <span className="text-sm font-medium">
                Executing: {executionTrace?.steps[currentStepIndex]?.nodeName || 'Starting...'}
              </span>
            </div>
          </Panel>
        )}

        {/* Lock indicator */}
        {isLocked && (
          <Panel position="top-right">
            <div className="bg-amber-500 text-white px-3 py-1.5 rounded-md shadow-lg flex items-center gap-2">
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"
                />
              </svg>
              <span className="text-xs font-medium">Canvas Locked</span>
            </div>
          </Panel>
        )}
      </ReactFlow>
    </div>
  );
}

// =============================================================================
// Main Export (with Provider)
// =============================================================================

export interface PipelineCanvasProps {
  className?: string;
}

export function PipelineCanvas({ className }: PipelineCanvasProps) {
  return (
    <ReactFlowProvider>
      <PipelineCanvasInner className={className} />
    </ReactFlowProvider>
  );
}
