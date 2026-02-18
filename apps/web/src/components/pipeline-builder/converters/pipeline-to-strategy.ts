/**
 * Pipeline to Strategy Format Converter
 *
 * Converts a visual pipeline builder definition to a portable strategy format
 * that can be imported by the RAG strategy builder or shared as configuration.
 *
 * @see Issue #3712 - Visual Pipeline Builder (Export to Strategy)
 */

import type {
  PipelineDefinition,
  PluginNode,
  PipelineEdge,
  PluginCategory,
  ConditionPreset,
} from '../types';

// =============================================================================
// Exported Strategy Format
// =============================================================================

export interface ExportedStrategy {
  id: string;
  name: string;
  description: string;
  version: string;
  exportedAt: string;
  sourceFormat: 'pipeline-builder';
  metadata: StrategyMetadata;
  steps: StrategyStep[];
  connections: StrategyConnection[];
}

export interface StrategyMetadata {
  nodeCount: number;
  edgeCount: number;
  estimatedTokens: number;
  estimatedLatencyMs: number;
  estimatedCost: number;
  categories: string[];
  difficulty: 'beginner' | 'intermediate' | 'advanced';
}

export interface StrategyStep {
  id: string;
  order: number;
  type: string;
  name: string;
  category: PluginCategory;
  config: Record<string, unknown>;
  position: { x: number; y: number };
}

export interface StrategyConnection {
  id: string;
  from: string;
  to: string;
  condition: string;
  conditionPreset?: string;
}

// =============================================================================
// Plugin to Category Mapping
// =============================================================================

const PLUGIN_CATEGORY_MAP: Record<string, string> = {
  'routing-llm': 'query-routing',
  'routing-rules': 'query-routing',
  'cache-semantic': 'cache',
  'retrieval-hybrid': 'hybrid-search',
  'retrieval-multihop': 'multi-hop-retrieval',
  'evaluation-crag': 'crag-evaluator',
  'evaluation-reranker': 'cross-encoder-reranking',
  'generation-llm': 'llm-generation',
  'generation-consensus': 'consensus-generation',
  'validation-citation': 'citation-verification',
  'validation-selfrag': 'self-rag',
  'transform-format': 'output-formatting',
};

// =============================================================================
// Converter Functions
// =============================================================================

/**
 * Calculates topological order of nodes based on edges.
 * Nodes without incoming edges come first.
 */
function calculateNodeOrder(nodes: PluginNode[], edges: PipelineEdge[]): Map<string, number> {
  const inDegree = new Map<string, number>();
  const adjacency = new Map<string, string[]>();

  nodes.forEach((n) => {
    inDegree.set(n.id, 0);
    adjacency.set(n.id, []);
  });

  edges.forEach((e) => {
    adjacency.get(e.source)?.push(e.target);
    inDegree.set(e.target, (inDegree.get(e.target) ?? 0) + 1);
  });

  const queue: string[] = [];
  inDegree.forEach((deg, id) => {
    if (deg === 0) queue.push(id);
  });

  const order = new Map<string, number>();
  let idx = 0;

  while (queue.length > 0) {
    const current = queue.shift(); // Safe: queue.length > 0 guarantees element exists
    if (!current) break; // TypeScript safety check
    order.set(current, idx++);

    for (const neighbor of adjacency.get(current) ?? []) {
      const newDeg = (inDegree.get(neighbor) ?? 1) - 1;
      inDegree.set(neighbor, newDeg);
      if (newDeg === 0) queue.push(neighbor);
    }
  }

  // Handle any remaining nodes (cycles) - assign incrementing order
  nodes.forEach((n) => {
    if (!order.has(n.id)) {
      order.set(n.id, idx++);
    }
  });

  return order;
}

/**
 * Infer difficulty based on pipeline complexity.
 */
function inferDifficulty(
  nodeCount: number,
  edgeCount: number,
  categories: Set<string>,
): 'beginner' | 'intermediate' | 'advanced' {
  if (nodeCount <= 2 && categories.size <= 2) return 'beginner';
  if (nodeCount <= 5 && categories.size <= 4) return 'intermediate';
  return 'advanced';
}

/**
 * Estimate token usage for a plugin based on its category.
 */
function estimateTokensForPlugin(pluginId: string): number {
  const estimates: Record<string, number> = {
    'routing-llm': 150,
    'routing-rules': 10,
    'cache-semantic': 50,
    'retrieval-hybrid': 200,
    'retrieval-multihop': 600,
    'evaluation-crag': 300,
    'evaluation-reranker': 100,
    'generation-llm': 1500,
    'generation-consensus': 4500,
    'validation-citation': 200,
    'validation-selfrag': 800,
    'transform-format': 50,
  };
  return estimates[pluginId] ?? 100;
}

/**
 * Estimate latency in ms for a plugin based on its category.
 */
function estimateLatencyForPlugin(pluginId: string): number {
  const estimates: Record<string, number> = {
    'routing-llm': 200,
    'routing-rules': 5,
    'cache-semantic': 30,
    'retrieval-hybrid': 150,
    'retrieval-multihop': 800,
    'evaluation-crag': 400,
    'evaluation-reranker': 100,
    'generation-llm': 1200,
    'generation-consensus': 3500,
    'validation-citation': 300,
    'validation-selfrag': 1000,
    'transform-format': 10,
  };
  return estimates[pluginId] ?? 100;
}

/**
 * Estimate cost per execution for a plugin (in USD).
 */
function estimateCostForPlugin(pluginId: string): number {
  const estimates: Record<string, number> = {
    'routing-llm': 0.0001,
    'routing-rules': 0,
    'cache-semantic': 0.00001,
    'retrieval-hybrid': 0.0002,
    'retrieval-multihop': 0.001,
    'evaluation-crag': 0.0005,
    'evaluation-reranker': 0.0001,
    'generation-llm': 0.003,
    'generation-consensus': 0.01,
    'validation-citation': 0.0003,
    'validation-selfrag': 0.002,
    'transform-format': 0.00001,
  };
  return estimates[pluginId] ?? 0.0001;
}

/**
 * Convert a pipeline definition to an exportable strategy format.
 */
export function convertPipelineToStrategy(pipeline: PipelineDefinition): ExportedStrategy {
  const nodeOrder = calculateNodeOrder(pipeline.nodes, pipeline.edges);
  const categories = new Set(pipeline.nodes.map((n) => n.data.category));

  const steps: StrategyStep[] = pipeline.nodes.map((node) => ({
    id: node.id,
    order: nodeOrder.get(node.id) ?? 0,
    type: PLUGIN_CATEGORY_MAP[node.data.pluginId] ?? node.data.pluginId,
    name: node.data.pluginName,
    category: node.data.category,
    config: node.data.config,
    position: node.position,
  }));

  // Sort steps by topological order
  steps.sort((a, b) => a.order - b.order);

  const connections: StrategyConnection[] = pipeline.edges.map((edge) => ({
    id: edge.id,
    from: edge.source,
    to: edge.target,
    condition: edge.data?.condition ?? 'always',
    conditionPreset: edge.data?.conditionPreset,
  }));

  const estimatedTokens = pipeline.nodes.reduce(
    (sum, n) => sum + estimateTokensForPlugin(n.data.pluginId),
    0,
  );
  const estimatedLatencyMs = pipeline.nodes.reduce(
    (sum, n) => sum + estimateLatencyForPlugin(n.data.pluginId),
    0,
  );
  const estimatedCost = pipeline.nodes.reduce(
    (sum, n) => sum + estimateCostForPlugin(n.data.pluginId),
    0,
  );

  return {
    id: pipeline.id,
    name: pipeline.name,
    description: pipeline.description ?? '',
    version: pipeline.version,
    exportedAt: new Date().toISOString(),
    sourceFormat: 'pipeline-builder',
    metadata: {
      nodeCount: pipeline.nodes.length,
      edgeCount: pipeline.edges.length,
      estimatedTokens,
      estimatedLatencyMs,
      estimatedCost,
      categories: Array.from(categories),
      difficulty: inferDifficulty(pipeline.nodes.length, pipeline.edges.length, categories),
    },
    steps,
    connections,
  };
}

/**
 * Export a pipeline as a downloadable JSON file.
 */
export function exportPipelineAsJSON(pipeline: PipelineDefinition): void {
  const strategy = convertPipelineToStrategy(pipeline);
  const json = JSON.stringify(strategy, null, 2);
  const blob = new Blob([json], { type: 'application/json' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = `${pipeline.name.toLowerCase().replace(/\s+/g, '-')}-strategy.json`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Import a previously exported strategy as a pipeline definition.
 */
export function importStrategyAsPipeline(strategy: ExportedStrategy): PipelineDefinition {
  return {
    id: strategy.id,
    name: strategy.name,
    description: strategy.description,
    version: strategy.version,
    nodes: strategy.steps.map((step) => ({
      id: step.id,
      type: 'plugin' as const,
      position: step.position,
      data: {
        pluginId: step.type,
        pluginName: step.name,
        category: step.category,
        config: step.config,
        configSchema: { type: 'object' as const },
        isValid: true,
        validationErrors: [],
      },
    })),
    edges: strategy.connections.map((conn) => ({
      id: conn.id,
      source: conn.from,
      target: conn.to,
      type: 'default',
      data: {
        condition: conn.condition,
        conditionPreset: conn.conditionPreset as ConditionPreset | undefined,
        isValid: true,
      },
    })),
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    isValid: true,
    validationErrors: [],
  };
}
