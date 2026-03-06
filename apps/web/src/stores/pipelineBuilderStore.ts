/**
 * Pipeline Builder Store
 *
 * Zustand store for managing visual pipeline builder state.
 * Supports undo/redo, drag-drop, execution, and persistence.
 *
 * @version 1.0.0
 * @see Issue #3425 - Visual Pipeline Builder
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

import type {
  PipelineBuilderState,
  PipelineBuilderActions,
  PipelineDefinition,
  PluginNode,
  PluginNodeData,
  PipelineEdge,
  PipelineEdgeData,
  ExecutionTrace,
} from '@/components/pipeline-builder/types';

import type { XYPosition } from '@xyflow/react';

// =============================================================================
// Initial State
// =============================================================================

const createEmptyPipeline = (name: string, description?: string): PipelineDefinition => ({
  id: crypto.randomUUID(),
  name,
  description,
  version: '1.0.0',
  nodes: [],
  edges: [],
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  isValid: true,
  validationErrors: [],
});

const initialState: PipelineBuilderState = {
  pipeline: null,
  isDirty: false,
  selectedNodeId: null,
  selectedEdgeId: null,
  isDragging: false,
  draggedPlugin: null,
  isExecuting: false,
  executionTrace: null,
  stepMode: false,
  currentStepIndex: 0,
  history: [],
  historyIndex: -1,
  maxHistorySize: 10,
  showMiniMap: true,
  showGrid: true,
  isLocked: false,
  zoomLevel: 1,
};

// =============================================================================
// Store
// =============================================================================

export type PipelineBuilderStore = PipelineBuilderState & PipelineBuilderActions;

export const usePipelineBuilderStore = create<PipelineBuilderStore>()(
  persist(
    (set, get) => ({
      ...initialState,

      // =========================================================================
      // Pipeline CRUD
      // =========================================================================

      createPipeline: (name, description) => {
        const pipeline = createEmptyPipeline(name, description);
        set({
          pipeline,
          isDirty: false,
          selectedNodeId: null,
          selectedEdgeId: null,
          history: [structuredClone(pipeline)],
          historyIndex: 0,
        });
      },

      loadPipeline: pipeline => {
        set({
          pipeline: structuredClone(pipeline),
          isDirty: false,
          selectedNodeId: null,
          selectedEdgeId: null,
          history: [structuredClone(pipeline)],
          historyIndex: 0,
        });
      },

      savePipeline: async () => {
        const { pipeline } = get();
        if (!pipeline) return;

        const response = await fetch('/api/v1/rag/pipelines', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            name: pipeline.name,
            description: pipeline.description,
            pipeline,
            tags: [],
          }),
        });

        if (!response.ok) {
          throw new Error('Failed to save pipeline');
        }

        set(state => ({
          pipeline: state.pipeline
            ? { ...state.pipeline, updatedAt: new Date().toISOString() }
            : null,
          isDirty: false,
        }));
      },

      clearPipeline: () => {
        set({
          ...initialState,
        });
      },

      // =========================================================================
      // Node Operations
      // =========================================================================

      addNode: (plugin, position) => {
        const { pipeline } = get();
        if (!pipeline) return '';

        const nodeId = `node-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;

        const newNode: PluginNode = {
          id: nodeId,
          type: 'plugin',
          position,
          data: {
            pluginId: plugin.id,
            pluginName: plugin.name,
            category: plugin.category,
            config: extractDefaultConfig(plugin.configSchema),
            configSchema: plugin.configSchema,
            isValid: true,
            validationErrors: [],
          },
        };

        get().pushHistory();

        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                nodes: [...state.pipeline.nodes, newNode],
                updatedAt: new Date().toISOString(),
              }
            : null,
          isDirty: true,
          selectedNodeId: nodeId,
          selectedEdgeId: null,
        }));

        return nodeId;
      },

      updateNode: (nodeId, data) => {
        const { pipeline } = get();
        if (!pipeline) return;

        get().pushHistory();

        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                nodes: state.pipeline.nodes.map(node =>
                  node.id === nodeId ? { ...node, data: { ...node.data, ...data } } : node
                ),
                updatedAt: new Date().toISOString(),
              }
            : null,
          isDirty: true,
        }));
      },

      removeNode: nodeId => {
        const { pipeline } = get();
        if (!pipeline) return;

        get().pushHistory();

        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                nodes: state.pipeline.nodes.filter(node => node.id !== nodeId),
                edges: state.pipeline.edges.filter(
                  edge => edge.source !== nodeId && edge.target !== nodeId
                ),
                updatedAt: new Date().toISOString(),
              }
            : null,
          isDirty: true,
          selectedNodeId: state.selectedNodeId === nodeId ? null : state.selectedNodeId,
        }));
      },

      updateNodePosition: (nodeId, position) => {
        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                nodes: state.pipeline.nodes.map(node =>
                  node.id === nodeId ? { ...node, position } : node
                ),
              }
            : null,
          isDirty: true,
        }));
      },

      updateNodeConfig: (nodeId, config) => {
        const { pipeline, validateNode } = get();
        if (!pipeline) return;

        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                nodes: state.pipeline.nodes.map(node =>
                  node.id === nodeId
                    ? {
                        ...node,
                        data: { ...node.data, config: { ...node.data.config, ...config } },
                      }
                    : node
                ),
                updatedAt: new Date().toISOString(),
              }
            : null,
          isDirty: true,
        }));

        validateNode(nodeId);
      },

      // =========================================================================
      // Edge Operations
      // =========================================================================

      addEdge: (sourceId, targetId, data) => {
        const { pipeline } = get();
        if (!pipeline) return null;

        // Prevent duplicate edges
        const exists = pipeline.edges.some(e => e.source === sourceId && e.target === targetId);
        if (exists) return null;

        // Prevent self-loops
        if (sourceId === targetId) return null;

        const edgeId = `edge-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;

        const newEdge: PipelineEdge = {
          id: edgeId,
          source: sourceId,
          target: targetId,
          type: 'default',
          data: {
            condition: 'always',
            conditionPreset: 'always',
            isValid: true,
            ...data,
          },
        };

        get().pushHistory();

        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                edges: [...state.pipeline.edges, newEdge],
                updatedAt: new Date().toISOString(),
              }
            : null,
          isDirty: true,
          selectedEdgeId: edgeId,
          selectedNodeId: null,
        }));

        return edgeId;
      },

      updateEdge: (edgeId, data) => {
        const { pipeline } = get();
        if (!pipeline) return;

        get().pushHistory();

        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                edges: state.pipeline.edges.map(edge =>
                  edge.id === edgeId
                    ? // eslint-disable-next-line @typescript-eslint/consistent-type-assertions -- spread requires assertion
                      { ...edge, data: { ...edge.data, ...data } as PipelineEdgeData }
                    : edge
                ),
                updatedAt: new Date().toISOString(),
              }
            : null,
          isDirty: true,
        }));
      },

      removeEdge: edgeId => {
        const { pipeline } = get();
        if (!pipeline) return;

        get().pushHistory();

        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                edges: state.pipeline.edges.filter(edge => edge.id !== edgeId),
                updatedAt: new Date().toISOString(),
              }
            : null,
          isDirty: true,
          selectedEdgeId: state.selectedEdgeId === edgeId ? null : state.selectedEdgeId,
        }));
      },

      // =========================================================================
      // Selection
      // =========================================================================

      selectNode: nodeId => {
        set({ selectedNodeId: nodeId, selectedEdgeId: null });
      },

      selectEdge: edgeId => {
        set({ selectedEdgeId: edgeId, selectedNodeId: null });
      },

      // =========================================================================
      // Drag Operations
      // =========================================================================

      startDrag: plugin => {
        set({ isDragging: true, draggedPlugin: plugin });
      },

      endDrag: () => {
        set({ isDragging: false, draggedPlugin: null });
      },

      // =========================================================================
      // History (Undo/Redo)
      // =========================================================================

      pushHistory: () => {
        const { pipeline, history, historyIndex, maxHistorySize } = get();
        if (!pipeline) return;

        const newHistory = history.slice(0, historyIndex + 1);
        newHistory.push(structuredClone(pipeline));

        // Limit history size
        if (newHistory.length > maxHistorySize) {
          newHistory.shift();
        }

        set({
          history: newHistory,
          historyIndex: newHistory.length - 1,
        });
      },

      undo: () => {
        const { history, historyIndex } = get();
        if (historyIndex <= 0) return;

        const newIndex = historyIndex - 1;
        // eslint-disable-next-line security/detect-object-injection -- newIndex is numeric
        const historyState = history[newIndex];
        set({
          pipeline: structuredClone(historyState),
          historyIndex: newIndex,
          isDirty: true,
          selectedNodeId: null,
          selectedEdgeId: null,
        });
      },

      redo: () => {
        const { history, historyIndex } = get();
        if (historyIndex >= history.length - 1) return;

        const newIndex = historyIndex + 1;
        // eslint-disable-next-line security/detect-object-injection -- newIndex is numeric
        const historyState = history[newIndex];
        set({
          pipeline: structuredClone(historyState),
          historyIndex: newIndex,
          isDirty: true,
          selectedNodeId: null,
          selectedEdgeId: null,
        });
      },

      canUndo: () => {
        const { historyIndex } = get();
        return historyIndex > 0;
      },

      canRedo: () => {
        const { history, historyIndex } = get();
        return historyIndex < history.length - 1;
      },

      // =========================================================================
      // Execution
      // =========================================================================

      runPipeline: async query => {
        const { pipeline } = get();
        if (!pipeline || pipeline.nodes.length === 0) return;

        const trace: ExecutionTrace = {
          id: crypto.randomUUID(),
          pipelineId: pipeline.id,
          query,
          startedAt: new Date().toISOString(),
          status: 'running',
          steps: pipeline.nodes.map(node => ({
            nodeId: node.id,
            nodeName: node.data.pluginName,
            status: 'pending',
          })),
          metrics: {
            totalTokens: 0,
            estimatedCost: 0,
            totalLatencyMs: 0,
            nodesExecuted: 0,
            cacheHits: 0,
          },
        };

        set({ isExecuting: true, executionTrace: trace, currentStepIndex: 0 });

        // Simulate execution for each node
        for (let i = 0; i < trace.steps.length; i++) {
          if (!get().isExecuting) break; // Stopped

          // eslint-disable-next-line security/detect-object-injection -- i is numeric index
          const step = trace.steps[i];

          // Update step to running
          set(state => ({
            executionTrace: state.executionTrace
              ? {
                  ...state.executionTrace,
                  steps: state.executionTrace.steps.map((s, idx) =>
                    idx === i ? { ...s, status: 'running' } : s
                  ),
                }
              : null,
            currentStepIndex: i,
          }));

          // Simulate execution delay
          await new Promise(resolve => setTimeout(resolve, 500 + Math.random() * 1000));

          // Update step to success
          const durationMs = 200 + Math.random() * 800;
          set(state => ({
            executionTrace: state.executionTrace
              ? {
                  ...state.executionTrace,
                  steps: state.executionTrace.steps.map((s, idx) =>
                    idx === i
                      ? {
                          ...s,
                          status: 'success' as const,
                          durationMs,
                          output: { result: `Output from ${step.nodeName}` },
                        }
                      : s
                  ),
                  metrics: {
                    ...state.executionTrace.metrics,
                    totalLatencyMs: state.executionTrace.metrics.totalLatencyMs + durationMs,
                    nodesExecuted: state.executionTrace.metrics.nodesExecuted + 1,
                    totalTokens:
                      state.executionTrace.metrics.totalTokens + Math.floor(Math.random() * 500),
                  },
                }
              : null,
          }));

          // In step mode, wait for user to continue
          const { stepMode } = get();
          if (stepMode && i < trace.steps.length - 1) {
            // Wait until stepThrough is called
            await new Promise<void>(resolve => {
              const unsubscribe = usePipelineBuilderStore.subscribe(state => {
                if (state.currentStepIndex > i || !state.isExecuting) {
                  unsubscribe();
                  resolve();
                }
              });
            });
          }
        }

        // Complete execution
        set(state => ({
          isExecuting: false,
          executionTrace: state.executionTrace
            ? {
                ...state.executionTrace,
                status: 'completed',
                completedAt: new Date().toISOString(),
                metrics: {
                  ...state.executionTrace.metrics,
                  estimatedCost: state.executionTrace.metrics.totalTokens * 0.000003, // Simplified cost calc
                },
              }
            : null,
        }));
      },

      runDryRun: async query => {
        // Dry run simulates without actual API calls
        await get().runPipeline(query);
      },

      stepThrough: () => {
        const { currentStepIndex, executionTrace } = get();
        if (!executionTrace) return;

        if (currentStepIndex < executionTrace.steps.length - 1) {
          set({ currentStepIndex: currentStepIndex + 1 });
        }
      },

      stopExecution: () => {
        set(state => ({
          isExecuting: false,
          executionTrace: state.executionTrace
            ? {
                ...state.executionTrace,
                status: 'failed',
                completedAt: new Date().toISOString(),
              }
            : null,
        }));
      },

      setStepMode: enabled => {
        set({ stepMode: enabled });
      },

      // =========================================================================
      // Validation
      // =========================================================================

      validatePipeline: () => {
        const { pipeline, validateNode, validateEdge } = get();
        if (!pipeline) return false;

        const errors: string[] = [];

        // Check for empty pipeline
        if (pipeline.nodes.length === 0) {
          errors.push('Pipeline must have at least one node');
        }

        // Validate all nodes
        pipeline.nodes.forEach(node => {
          if (!validateNode(node.id)) {
            errors.push(`Node "${node.data.pluginName}" has validation errors`);
          }
        });

        // Validate all edges
        pipeline.edges.forEach(edge => {
          if (!validateEdge(edge.id)) {
            errors.push(`Edge from ${edge.source} to ${edge.target} has validation errors`);
          }
        });

        // Check for disconnected nodes (warning only)
        const connectedNodes = new Set<string>();
        pipeline.edges.forEach(edge => {
          connectedNodes.add(edge.source);
          connectedNodes.add(edge.target);
        });

        const isValid = errors.length === 0;

        set(state => ({
          pipeline: state.pipeline
            ? { ...state.pipeline, isValid, validationErrors: errors }
            : null,
        }));

        return isValid;
      },

      validateNode: nodeId => {
        const { pipeline } = get();
        if (!pipeline) return false;

        const node = pipeline.nodes.find(n => n.id === nodeId);
        if (!node) return false;

        const errors: string[] = [];

        // Validate required config fields
        const { configSchema, config } = node.data;
        if (configSchema.required) {
          configSchema.required.forEach(field => {
            // eslint-disable-next-line security/detect-object-injection -- field from schema.required array
            const fieldValue = config[field];
            if (fieldValue === undefined || fieldValue === null || fieldValue === '') {
              errors.push(`Required field "${field}" is missing`);
            }
          });
        }

        const isValid = errors.length === 0;

        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                nodes: state.pipeline.nodes.map(n =>
                  n.id === nodeId
                    ? {
                        ...n,
                        data: { ...n.data, isValid, validationErrors: errors },
                      }
                    : n
                ),
              }
            : null,
        }));

        return isValid;
      },

      validateEdge: edgeId => {
        const { pipeline } = get();
        if (!pipeline) return false;

        const edge = pipeline.edges.find(e => e.id === edgeId);
        if (!edge || !edge.data) return false;

        let isValid = true;
        let error: string | undefined;

        // Validate condition expression
        const { condition, conditionPreset } = edge.data;
        if (conditionPreset === 'custom' && (!condition || condition.trim() === '')) {
          isValid = false;
          error = 'Custom condition expression is required';
        }

        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                edges: state.pipeline.edges.map(e =>
                  e.id === edgeId
                    ? {
                        ...e,
                        // eslint-disable-next-line @typescript-eslint/consistent-type-assertions -- spread requires assertion
                        data: { ...e.data, isValid, validationError: error } as PipelineEdgeData,
                      }
                    : e
                ),
              }
            : null,
        }));

        return isValid;
      },

      // =========================================================================
      // Layout
      // =========================================================================

      autoLayout: () => {
        const { pipeline } = get();
        if (!pipeline || pipeline.nodes.length === 0) return;

        get().pushHistory();

        // Simple auto-layout: arrange nodes in a grid/flow pattern
        const nodeSpacing = { x: 280, y: 150 };
        const startPosition = { x: 50, y: 50 };

        // Build adjacency list
        const adjacency = new Map<string, string[]>();
        pipeline.nodes.forEach(node => adjacency.set(node.id, []));
        pipeline.edges.forEach(edge => {
          adjacency.get(edge.source)?.push(edge.target);
        });

        // Find root nodes (no incoming edges)
        const hasIncoming = new Set(pipeline.edges.map(e => e.target));
        const roots = pipeline.nodes.filter(n => !hasIncoming.has(n.id));

        // BFS layout
        const positions = new Map<string, XYPosition>();
        const visited = new Set<string>();
        let currentLevel = roots.length > 0 ? roots : [pipeline.nodes[0]];
        let level = 0;

        while (currentLevel.length > 0) {
          const nextLevel: PluginNode[] = [];

          currentLevel.forEach((node, index) => {
            if (visited.has(node.id)) return;
            visited.add(node.id);

            positions.set(node.id, {
              x: startPosition.x + level * nodeSpacing.x,
              y: startPosition.y + index * nodeSpacing.y,
            });

            const children = adjacency.get(node.id) || [];
            children.forEach(childId => {
              const childNode = pipeline.nodes.find(n => n.id === childId);
              if (childNode && !visited.has(childId)) {
                nextLevel.push(childNode);
              }
            });
          });

          currentLevel = nextLevel;
          level++;
        }

        // Update node positions
        set(state => ({
          pipeline: state.pipeline
            ? {
                ...state.pipeline,
                nodes: state.pipeline.nodes.map(node => ({
                  ...node,
                  position: positions.get(node.id) || node.position,
                })),
              }
            : null,
          isDirty: true,
        }));
      },

      fitView: () => {
        // This will be handled by React Flow's fitView
        set({ zoomLevel: 1 });
      },

      setZoom: level => {
        set({ zoomLevel: Math.max(0.1, Math.min(2, level)) });
      },

      toggleMiniMap: () => {
        set(state => ({ showMiniMap: !state.showMiniMap }));
      },

      toggleGrid: () => {
        set(state => ({ showGrid: !state.showGrid }));
      },

      toggleLock: () => {
        set(state => ({ isLocked: !state.isLocked }));
      },
    }),
    {
      name: 'pipeline-builder-storage',
      partialize: state => ({
        pipeline: state.pipeline,
        showMiniMap: state.showMiniMap,
        showGrid: state.showGrid,
      }),
    }
  )
);

// =============================================================================
// Helper Functions
// =============================================================================

function extractDefaultConfig(schema: PluginNodeData['configSchema']): Record<string, unknown> {
  const config: Record<string, unknown> = {};

  if (schema.properties) {
    Object.entries(schema.properties).forEach(([key, prop]) => {
      if ('default' in prop && prop.default !== undefined) {
        // eslint-disable-next-line security/detect-object-injection -- key from Object.entries
        config[key] = prop.default;
      }
    });
  }

  return config;
}

// =============================================================================
// Selectors
// =============================================================================

export const selectPipeline = (state: PipelineBuilderStore) => state.pipeline;
export const selectSelectedNode = (state: PipelineBuilderStore) => {
  if (!state.pipeline || !state.selectedNodeId) return null;
  return state.pipeline.nodes.find(n => n.id === state.selectedNodeId) || null;
};
export const selectSelectedEdge = (state: PipelineBuilderStore) => {
  if (!state.pipeline || !state.selectedEdgeId) return null;
  return state.pipeline.edges.find(e => e.id === state.selectedEdgeId) || null;
};
export const selectIsExecuting = (state: PipelineBuilderStore) => state.isExecuting;
export const selectExecutionTrace = (state: PipelineBuilderStore) => state.executionTrace;
