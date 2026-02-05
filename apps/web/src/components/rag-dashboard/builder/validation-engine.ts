/**
 * Strategy Validation Engine
 *
 * Validates RAG strategy pipelines before save.
 * Checks: tokens < 30K, latency < 30s, no cycles, required blocks.
 *
 * @see #3462 - Implement strategy validation engine
 */

import { isValidConnection } from './block-definitions';
import { PIPELINE_CONSTRAINTS, hasTierAccess } from './block-metadata';

import type {
  RagNodeData,
  RagBlockType,
  UserTier,
  PipelineValidation,
  ValidationError,
  ValidationWarning,
  PipelineMetrics,
  ConnectionPort,
} from './types';
import type { Node, Edge } from '@xyflow/react';

// =============================================================================
// Error & Warning Codes
// =============================================================================

export const ValidationCodes = {
  // Errors - block execution
  CYCLE_DETECTED: 'CYCLE_DETECTED',
  DISCONNECTED_NODE: 'DISCONNECTED_NODE',
  INVALID_CONNECTION: 'INVALID_CONNECTION',
  MISSING_REQUIRED_INPUT: 'MISSING_REQUIRED_INPUT',
  EXCEEDS_TOKEN_LIMIT: 'EXCEEDS_TOKEN_LIMIT',
  EXCEEDS_LATENCY_LIMIT: 'EXCEEDS_LATENCY_LIMIT',
  EXCEEDS_COST_LIMIT: 'EXCEEDS_COST_LIMIT',
  EXCEEDS_NODE_LIMIT: 'EXCEEDS_NODE_LIMIT',
  EXCEEDS_EDGE_LIMIT: 'EXCEEDS_EDGE_LIMIT',
  TIER_ACCESS_DENIED: 'TIER_ACCESS_DENIED',
  DUPLICATE_BLOCK_LIMIT: 'DUPLICATE_BLOCK_LIMIT',
  NO_ENTRY_POINT: 'NO_ENTRY_POINT',
  NO_EXIT_POINT: 'NO_EXIT_POINT',

  // Warnings - recommendations
  MISSING_RERANKER: 'MISSING_RERANKER',
  MISSING_VALIDATION: 'MISSING_VALIDATION',
  HIGH_TOKEN_USAGE: 'HIGH_TOKEN_USAGE',
  HIGH_LATENCY: 'HIGH_LATENCY',
  HIGH_COST: 'HIGH_COST',
  SUBOPTIMAL_ORDER: 'SUBOPTIMAL_ORDER',
  REDUNDANT_BLOCKS: 'REDUNDANT_BLOCKS',
} as const;

// =============================================================================
// Validation Types
// =============================================================================

export interface ValidationContext {
  nodes: Node[];
  edges: Edge[];
  userTier: UserTier;
}

export interface DetailedValidation extends PipelineValidation {
  /** Errors by category */
  errorsByCategory: {
    structure: ValidationError[];
    resources: ValidationError[];
    access: ValidationError[];
  };
  /** Warnings by category */
  warningsByCategory: {
    performance: ValidationWarning[];
    quality: ValidationWarning[];
    recommendations: ValidationWarning[];
  };
  /** Validation timestamp */
  validatedAt: Date;
}

// =============================================================================
// Graph Analysis Utilities
// =============================================================================

/**
 * Build adjacency list from edges
 */
function buildAdjacencyList(
  nodes: Node[],
  edges: Edge[]
): Map<string, string[]> {
  const adjacency = new Map<string, string[]>();

  // Initialize all nodes
  for (const node of nodes) {
    adjacency.set(node.id, []);
  }

  // Add edges
  for (const edge of edges) {
    if (edge.source && edge.target) {
      const targets = adjacency.get(edge.source) || [];
      targets.push(edge.target);
      adjacency.set(edge.source, targets);
    }
  }

  return adjacency;
}

/**
 * Detect cycles using DFS
 */
function detectCycles(
  nodes: Node[],
  edges: Edge[]
): { hasCycle: boolean; cycleNodes: string[] } {
  const adjacency = buildAdjacencyList(nodes, edges);
  const visited = new Set<string>();
  const recursionStack = new Set<string>();
  const cycleNodes: string[] = [];

  function dfs(nodeId: string, path: string[]): boolean {
    visited.add(nodeId);
    recursionStack.add(nodeId);

    const neighbors = adjacency.get(nodeId) || [];
    for (const neighbor of neighbors) {
      if (!visited.has(neighbor)) {
        if (dfs(neighbor, [...path, neighbor])) {
          return true;
        }
      } else if (recursionStack.has(neighbor)) {
        // Cycle found
        const cycleStart = path.indexOf(neighbor);
        cycleNodes.push(...path.slice(cycleStart));
        return true;
      }
    }

    recursionStack.delete(nodeId);
    return false;
  }

  for (const node of nodes) {
    if (!visited.has(node.id)) {
      if (dfs(node.id, [node.id])) {
        return { hasCycle: true, cycleNodes };
      }
    }
  }

  return { hasCycle: false, cycleNodes: [] };
}

/**
 * Find disconnected nodes (no inputs and no outputs)
 */
function findDisconnectedNodes(nodes: Node[], edges: Edge[]): string[] {
  const connectedNodes = new Set<string>();

  for (const edge of edges) {
    if (edge.source) connectedNodes.add(edge.source);
    if (edge.target) connectedNodes.add(edge.target);
  }

  // A node is disconnected if it has no edges AND it's not the only node
  if (nodes.length === 1) return [];

  return nodes
    .filter((n) => !connectedNodes.has(n.id))
    .map((n) => n.id);
}

/**
 * Find entry points (nodes with no incoming edges)
 */
function findEntryPoints(nodes: Node[], edges: Edge[]): string[] {
  const hasIncoming = new Set<string>();

  for (const edge of edges) {
    if (edge.target) hasIncoming.add(edge.target);
  }

  return nodes
    .filter((n) => !hasIncoming.has(n.id))
    .map((n) => n.id);
}

/**
 * Find exit points (nodes with no outgoing edges)
 */
function findExitPoints(nodes: Node[], edges: Edge[]): string[] {
  const hasOutgoing = new Set<string>();

  for (const edge of edges) {
    if (edge.source) hasOutgoing.add(edge.source);
  }

  return nodes
    .filter((n) => !hasOutgoing.has(n.id))
    .map((n) => n.id);
}

// =============================================================================
// Connection Validation
// =============================================================================

/**
 * Validate all connections for data type compatibility
 */
function validateConnections(
  nodes: Node[],
  edges: Edge[]
): ValidationError[] {
  const errors: ValidationError[] = [];
  const nodeMap = new Map(nodes.map((n) => [n.id, n]));

  for (const edge of edges) {
    const sourceNode = nodeMap.get(edge.source || '');
    const targetNode = nodeMap.get(edge.target || '');

    if (!sourceNode || !targetNode) continue;

    const sourceData = sourceNode.data as RagNodeData | undefined;
    const targetData = targetNode.data as RagNodeData | undefined;

    if (!sourceData?.block || !targetData?.block) continue;

    // Check block type compatibility
    if (!isValidConnection(sourceData.block.type, targetData.block.type)) {
      errors.push({
        edgeId: edge.id,
        code: ValidationCodes.INVALID_CONNECTION,
        message: `${sourceData.block.name} cannot connect to ${targetData.block.name}`,
      });
      continue;
    }

    // Check port data type compatibility
    const sourceHandle = edge.sourceHandle;
    const targetHandle = edge.targetHandle;

    if (sourceHandle && targetHandle) {
      const sourcePort = sourceData.block.outputs.find(
        (p: ConnectionPort) => p.id === sourceHandle
      );
      const targetPort = targetData.block.inputs.find(
        (p: ConnectionPort) => p.id === targetHandle
      );

      if (sourcePort && targetPort) {
        if (
          sourcePort.dataType !== 'any' &&
          targetPort.dataType !== 'any' &&
          sourcePort.dataType !== targetPort.dataType
        ) {
          errors.push({
            edgeId: edge.id,
            code: ValidationCodes.INVALID_CONNECTION,
            message: `Data type mismatch: ${sourcePort.dataType} → ${targetPort.dataType}`,
          });
        }
      }
    }
  }

  return errors;
}

/**
 * Check for missing required inputs on nodes
 */
function validateRequiredInputs(
  nodes: Node[],
  edges: Edge[]
): ValidationError[] {
  const errors: ValidationError[] = [];
  const incomingByNode = new Map<string, Set<string>>();

  // Build map of incoming port connections per node
  for (const edge of edges) {
    if (!edge.target || !edge.targetHandle) continue;
    const set = incomingByNode.get(edge.target) || new Set();
    set.add(edge.targetHandle);
    incomingByNode.set(edge.target, set);
  }

  // Check each node's required inputs
  for (const node of nodes) {
    const data = node.data as RagNodeData | undefined;
    if (!data?.block) continue;

    const connectedPorts = incomingByNode.get(node.id) || new Set();

    // For now, we consider a node valid if it has at least one input OR it's an entry point
    // More sophisticated validation could check specific required ports
    const hasInputs = data.block.inputs.length === 0 || connectedPorts.size > 0;

    if (!hasInputs && data.block.inputs.length > 0) {
      // This is a node that needs inputs but has none - might be valid as entry
      // Only warn if it's not a retrieval/optimization block (which can be entry points)
      const entryCategories = ['retrieval', 'optimization'];
      if (!entryCategories.includes(data.block.category)) {
        errors.push({
          nodeId: node.id,
          code: ValidationCodes.MISSING_REQUIRED_INPUT,
          message: `${data.block.name} requires at least one input connection`,
        });
      }
    }
  }

  return errors;
}

// =============================================================================
// Resource Validation
// =============================================================================

/**
 * Calculate pipeline metrics
 */
function calculateMetrics(nodes: Node[]): PipelineMetrics {
  let totalTokens = 0;
  let totalLatency = 0;
  let totalCost = 0;

  for (const node of nodes) {
    const data = node.data as RagNodeData | undefined;
    if (!data?.block) continue;

    totalTokens += data.block.estimatedTokens;
    totalLatency += data.block.estimatedLatencyMs;
    totalCost += data.block.estimatedCost;
  }

  return {
    totalEstimatedTokens: totalTokens,
    totalEstimatedLatencyMs: totalLatency,
    totalEstimatedCost: totalCost,
    nodeCount: nodes.length,
    edgeCount: 0, // Set by caller
  };
}

/**
 * Validate resource constraints
 */
function validateResources(
  metrics: PipelineMetrics,
  edgeCount: number
): ValidationError[] {
  const errors: ValidationError[] = [];

  if (metrics.totalEstimatedTokens > PIPELINE_CONSTRAINTS.maxTokens) {
    errors.push({
      code: ValidationCodes.EXCEEDS_TOKEN_LIMIT,
      message: `Token usage (${metrics.totalEstimatedTokens.toLocaleString()}) exceeds limit (${PIPELINE_CONSTRAINTS.maxTokens.toLocaleString()})`,
    });
  }

  if (metrics.totalEstimatedLatencyMs > PIPELINE_CONSTRAINTS.maxLatencyMs) {
    errors.push({
      code: ValidationCodes.EXCEEDS_LATENCY_LIMIT,
      message: `Latency (${metrics.totalEstimatedLatencyMs}ms) exceeds limit (${PIPELINE_CONSTRAINTS.maxLatencyMs}ms)`,
    });
  }

  if (metrics.totalEstimatedCost > PIPELINE_CONSTRAINTS.maxCostUsd) {
    errors.push({
      code: ValidationCodes.EXCEEDS_COST_LIMIT,
      message: `Cost ($${metrics.totalEstimatedCost.toFixed(4)}) exceeds limit ($${PIPELINE_CONSTRAINTS.maxCostUsd.toFixed(2)})`,
    });
  }

  if (metrics.nodeCount > PIPELINE_CONSTRAINTS.maxNodes) {
    errors.push({
      code: ValidationCodes.EXCEEDS_NODE_LIMIT,
      message: `Node count (${metrics.nodeCount}) exceeds limit (${PIPELINE_CONSTRAINTS.maxNodes})`,
    });
  }

  if (edgeCount > PIPELINE_CONSTRAINTS.maxEdges) {
    errors.push({
      code: ValidationCodes.EXCEEDS_EDGE_LIMIT,
      message: `Edge count (${edgeCount}) exceeds limit (${PIPELINE_CONSTRAINTS.maxEdges})`,
    });
  }

  return errors;
}

// =============================================================================
// Access Control Validation
// =============================================================================

/**
 * Validate tier access for all blocks
 */
function validateTierAccess(
  nodes: Node[],
  userTier: UserTier
): ValidationError[] {
  const errors: ValidationError[] = [];

  for (const node of nodes) {
    const data = node.data as RagNodeData | undefined;
    if (!data?.block) continue;

    if (!hasTierAccess(userTier, data.block.requiredTier)) {
      errors.push({
        nodeId: node.id,
        code: ValidationCodes.TIER_ACCESS_DENIED,
        message: `${data.block.name} requires ${data.block.requiredTier} tier (you have ${userTier})`,
      });
    }
  }

  return errors;
}

/**
 * Validate block instance limits
 */
function validateBlockLimits(nodes: Node[]): ValidationError[] {
  const errors: ValidationError[] = [];
  const blockCounts = new Map<RagBlockType, number>();

  for (const node of nodes) {
    const data = node.data as RagNodeData | undefined;
    if (!data?.block) continue;

    const count = (blockCounts.get(data.block.type) || 0) + 1;
    blockCounts.set(data.block.type, count);

    if (count > data.block.maxInstances) {
      errors.push({
        nodeId: node.id,
        code: ValidationCodes.DUPLICATE_BLOCK_LIMIT,
        message: `${data.block.name} exceeded max instances (${data.block.maxInstances})`,
      });
    }
  }

  return errors;
}

// =============================================================================
// Quality Warnings
// =============================================================================

/**
 * Generate quality warnings for suboptimal configurations
 */
function generateWarnings(
  nodes: Node[],
  edges: Edge[],
  metrics: PipelineMetrics
): ValidationWarning[] {
  const warnings: ValidationWarning[] = [];
  const blockTypes = new Set<RagBlockType>();

  for (const node of nodes) {
    const data = node.data as RagNodeData | undefined;
    if (data?.block) {
      blockTypes.add(data.block.type);
    }
  }

  // Check for missing reranker after retrieval
  const hasRetrieval = ['vector-search', 'keyword-search', 'hybrid-search'].some(
    (t) => blockTypes.has(t as RagBlockType)
  );
  const hasReranker = blockTypes.has('cross-encoder-reranking');

  if (hasRetrieval && !hasReranker && nodes.length > 1) {
    warnings.push({
      code: ValidationCodes.MISSING_RERANKER,
      message: 'Consider adding a reranker for +20-40% precision improvement',
    });
  }

  // Check for missing validation
  const hasValidation = ['crag-evaluator', 'confidence-scoring', 'citation-verification'].some(
    (t) => blockTypes.has(t as RagBlockType)
  );

  if (!hasValidation && nodes.length > 2) {
    warnings.push({
      code: ValidationCodes.MISSING_VALIDATION,
      message: 'Consider adding validation blocks for quality assurance',
    });
  }

  // High resource usage warnings
  const tokenThreshold = PIPELINE_CONSTRAINTS.maxTokens * 0.8;
  const latencyThreshold = PIPELINE_CONSTRAINTS.maxLatencyMs * 0.8;
  const costThreshold = PIPELINE_CONSTRAINTS.maxCostUsd * 0.8;

  if (metrics.totalEstimatedTokens > tokenThreshold) {
    warnings.push({
      code: ValidationCodes.HIGH_TOKEN_USAGE,
      message: `Token usage at ${((metrics.totalEstimatedTokens / PIPELINE_CONSTRAINTS.maxTokens) * 100).toFixed(0)}% of limit`,
    });
  }

  if (metrics.totalEstimatedLatencyMs > latencyThreshold) {
    warnings.push({
      code: ValidationCodes.HIGH_LATENCY,
      message: `Latency at ${((metrics.totalEstimatedLatencyMs / PIPELINE_CONSTRAINTS.maxLatencyMs) * 100).toFixed(0)}% of limit`,
    });
  }

  if (metrics.totalEstimatedCost > costThreshold) {
    warnings.push({
      code: ValidationCodes.HIGH_COST,
      message: `Cost at ${((metrics.totalEstimatedCost / PIPELINE_CONSTRAINTS.maxCostUsd) * 100).toFixed(0)}% of limit`,
    });
  }

  // Check for redundant blocks (multiple of same category without clear purpose)
  const categoryCount = new Map<string, number>();
  for (const node of nodes) {
    const data = node.data as RagNodeData | undefined;
    if (data?.block) {
      const count = (categoryCount.get(data.block.category) || 0) + 1;
      categoryCount.set(data.block.category, count);
    }
  }

  // More than 3 retrieval blocks might be redundant
  if ((categoryCount.get('retrieval') || 0) > 3) {
    warnings.push({
      code: ValidationCodes.REDUNDANT_BLOCKS,
      message: 'Multiple retrieval blocks detected - consider consolidating',
    });
  }

  return warnings;
}

// =============================================================================
// Main Validation Function
// =============================================================================

/**
 * Validate a complete pipeline
 */
export function validatePipeline(
  context: ValidationContext
): DetailedValidation {
  const { nodes, edges, userTier } = context;

  const errors: ValidationError[] = [];
  const warnings: ValidationWarning[] = [];

  // Early return for empty pipeline
  if (nodes.length === 0) {
    return {
      isValid: true,
      errors: [],
      warnings: [],
      metrics: {
        totalEstimatedTokens: 0,
        totalEstimatedLatencyMs: 0,
        totalEstimatedCost: 0,
        nodeCount: 0,
        edgeCount: 0,
      },
      errorsByCategory: {
        structure: [],
        resources: [],
        access: [],
      },
      warningsByCategory: {
        performance: [],
        quality: [],
        recommendations: [],
      },
      validatedAt: new Date(),
    };
  }

  // Structure validation
  const structureErrors: ValidationError[] = [];

  // Check for cycles
  const cycleResult = detectCycles(nodes, edges);
  if (cycleResult.hasCycle) {
    structureErrors.push({
      code: ValidationCodes.CYCLE_DETECTED,
      message: `Cycle detected in pipeline. Cycles prevent execution.`,
    });
  }

  // Check for disconnected nodes
  const disconnected = findDisconnectedNodes(nodes, edges);
  for (const nodeId of disconnected) {
    const node = nodes.find((n) => n.id === nodeId);
    const data = node?.data as RagNodeData | undefined;
    structureErrors.push({
      nodeId,
      code: ValidationCodes.DISCONNECTED_NODE,
      message: `${data?.block?.name || 'Block'} is not connected to the pipeline`,
    });
  }

  // Validate connections
  structureErrors.push(...validateConnections(nodes, edges));

  // Validate required inputs
  structureErrors.push(...validateRequiredInputs(nodes, edges));

  // Check for entry/exit points (only if we have multiple nodes)
  if (nodes.length > 1 && edges.length > 0) {
    const entryPoints = findEntryPoints(nodes, edges);
    const exitPoints = findExitPoints(nodes, edges);

    if (entryPoints.length === 0) {
      structureErrors.push({
        code: ValidationCodes.NO_ENTRY_POINT,
        message: 'Pipeline has no entry point (all nodes have incoming edges)',
      });
    }

    if (exitPoints.length === 0) {
      structureErrors.push({
        code: ValidationCodes.NO_EXIT_POINT,
        message: 'Pipeline has no exit point (all nodes have outgoing edges)',
      });
    }
  }

  // Calculate metrics
  const metrics = calculateMetrics(nodes);
  metrics.edgeCount = edges.length;

  // Resource validation
  const resourceErrors = validateResources(metrics, edges.length);

  // Access validation
  const accessErrors: ValidationError[] = [];
  accessErrors.push(...validateTierAccess(nodes, userTier));
  accessErrors.push(...validateBlockLimits(nodes));

  // Combine all errors
  errors.push(...structureErrors, ...resourceErrors, ...accessErrors);

  // Generate warnings
  const allWarnings = generateWarnings(nodes, edges, metrics);

  // Categorize warnings
  const performanceCodes = ['HIGH_TOKEN_USAGE', 'HIGH_LATENCY', 'HIGH_COST'];
  const qualityCodes = ['SUBOPTIMAL_ORDER', 'REDUNDANT_BLOCKS'];
  const recommendationCodes = ['MISSING_RERANKER', 'MISSING_VALIDATION'];

  const performanceWarnings = allWarnings.filter((w) =>
    performanceCodes.includes(w.code)
  );
  const qualityWarnings = allWarnings.filter((w) =>
    qualityCodes.includes(w.code)
  );
  const recommendationWarnings = allWarnings.filter((w) =>
    recommendationCodes.includes(w.code)
  );

  warnings.push(...allWarnings);

  return {
    isValid: errors.length === 0,
    errors,
    warnings,
    metrics,
    errorsByCategory: {
      structure: structureErrors,
      resources: resourceErrors,
      access: accessErrors,
    },
    warningsByCategory: {
      performance: performanceWarnings,
      quality: qualityWarnings,
      recommendations: recommendationWarnings,
    },
    validatedAt: new Date(),
  };
}

/**
 * Quick validation check (returns boolean only)
 */
export function isValidPipeline(
  nodes: Node[],
  edges: Edge[],
  userTier: UserTier
): boolean {
  const result = validatePipeline({ nodes, edges, userTier });
  return result.isValid;
}

/**
 * Validate a single connection attempt
 */
export function validateConnection(
  sourceNode: Node,
  targetNode: Node,
  sourceHandle: string,
  targetHandle: string
): { valid: boolean; reason?: string } {
  const sourceData = sourceNode.data as RagNodeData | undefined;
  const targetData = targetNode.data as RagNodeData | undefined;

  if (!sourceData?.block || !targetData?.block) {
    return { valid: false, reason: 'Invalid node data' };
  }

  // Check block type compatibility
  if (!isValidConnection(sourceData.block.type, targetData.block.type)) {
    return {
      valid: false,
      reason: `${sourceData.block.name} cannot connect to ${targetData.block.name}`,
    };
  }

  // Check port data type compatibility
  const sourcePort = sourceData.block.outputs.find(
    (p: ConnectionPort) => p.id === sourceHandle
  );
  const targetPort = targetData.block.inputs.find(
    (p: ConnectionPort) => p.id === targetHandle
  );

  if (!sourcePort || !targetPort) {
    return { valid: false, reason: 'Invalid port' };
  }

  if (
    sourcePort.dataType !== 'any' &&
    targetPort.dataType !== 'any' &&
    sourcePort.dataType !== targetPort.dataType
  ) {
    return {
      valid: false,
      reason: `Data type mismatch: ${sourcePort.dataType} → ${targetPort.dataType}`,
    };
  }

  return { valid: true };
}

export default validatePipeline;
