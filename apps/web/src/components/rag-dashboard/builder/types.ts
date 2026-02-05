/**
 * RAG Pipeline Builder - Type Definitions
 *
 * Type definitions for 23 RAG building blocks used in the visual pipeline builder.
 * Based on research: docs/claudedocs/research_rag-building-blocks_2026-02-02.md
 *
 * @see #3454 - Define block type system and metadata
 */

import type { Node, Edge, XYPosition } from '@xyflow/react';

// =============================================================================
// Core Enums
// =============================================================================

/** Block categories for palette organization */
export type BlockCategory =
  | 'retrieval'
  | 'optimization'
  | 'ranking'
  | 'validation'
  | 'agents'
  | 'control';

/** Block type identifiers for all 23 blocks */
export type RagBlockType =
  // Retrieval (7 blocks)
  | 'vector-search'
  | 'keyword-search'
  | 'hybrid-search'
  | 'multi-hop-retrieval'
  | 'graph-rag'
  | 'web-search'
  | 'sentence-window'
  // Ranking (4 blocks)
  | 'cross-encoder-reranking'
  | 'metadata-filtering'
  | 'deduplication'
  | 'document-repacking'
  // Validation (5 blocks)
  | 'crag-evaluator'
  | 'self-rag'
  | 'confidence-scoring'
  | 'citation-verification'
  | 'hallucination-detection'
  // Agents (3 blocks)
  | 'sequential-agent'
  | 'parallel-agent'
  | 'supervisor-worker'
  // Optimization (4 blocks)
  | 'query-rewriting'
  | 'query-decomposition'
  | 'hyde'
  | 'rag-fusion'
  // Control (0 dedicated - uses built-in ReactFlow)
  ;

/** User tier requirements for block access */
export type UserTier = 'User' | 'Editor' | 'Admin';

/** Parameter types for block configuration */
export type ParameterType = 'number' | 'string' | 'boolean' | 'select' | 'range' | 'model';

// =============================================================================
// Parameter Definitions
// =============================================================================

/** Base parameter interface */
export interface BlockParameterBase {
  id: string;
  name: string;
  description: string;
  type: ParameterType;
  required: boolean;
}

/** Numeric parameter with range constraints */
export interface NumberParameter extends BlockParameterBase {
  type: 'number';
  default: number;
  min?: number;
  max?: number;
  step?: number;
}

/** String parameter */
export interface StringParameter extends BlockParameterBase {
  type: 'string';
  default: string;
  maxLength?: number;
  pattern?: string;
}

/** Boolean parameter */
export interface BooleanParameter extends BlockParameterBase {
  type: 'boolean';
  default: boolean;
}

/** Select parameter with predefined options */
export interface SelectParameter extends BlockParameterBase {
  type: 'select';
  default: string;
  options: Array<{ value: string; label: string }>;
}

/** Range parameter with min/max slider */
export interface RangeParameter extends BlockParameterBase {
  type: 'range';
  default: [number, number];
  min: number;
  max: number;
  step?: number;
}

/** Model selector parameter */
export interface ModelParameter extends BlockParameterBase {
  type: 'model';
  default: string;
  allowedModels: string[];
}

/** Union type for all parameter types */
export type BlockParameter =
  | NumberParameter
  | StringParameter
  | BooleanParameter
  | SelectParameter
  | RangeParameter
  | ModelParameter;

// =============================================================================
// Connection Types
// =============================================================================

/** Port position on node */
export type PortPosition = 'top' | 'right' | 'bottom' | 'left';

/** Connection port definition */
export interface ConnectionPort {
  id: string;
  name: string;
  type: 'input' | 'output';
  dataType: PortDataType;
  position: PortPosition;
  multiple?: boolean; // Allow multiple connections
}

/** Data types that can flow between blocks */
export type PortDataType =
  | 'query'      // User query string
  | 'documents'  // Retrieved documents array
  | 'embeddings' // Vector embeddings
  | 'scores'     // Relevance/confidence scores
  | 'response'   // Generated response
  | 'evaluation' // Evaluation result (pass/fail)
  | 'any';       // Any type (for flexible connections)

// =============================================================================
// Code Reference Types
// =============================================================================

/** Reference to backend implementation */
export interface CodeReference {
  type: 'backend' | 'api' | 'docs';
  path: string;
  description: string;
}

// =============================================================================
// Block Definition Interface
// =============================================================================

/**
 * Complete block definition with all metadata.
 * This interface defines the structure for all 23 RAG building blocks.
 */
export interface RagBlock {
  /** Unique identifier */
  id: RagBlockType;

  /** Block type for categorization */
  type: RagBlockType;

  /** Display name */
  name: string;

  /** Category for palette grouping */
  category: BlockCategory;

  /** Icon (emoji or icon name) */
  icon: string;

  /** Color for visual identification (HSL or hex) */
  color: string;

  /** Configurable parameters */
  parameters: BlockParameter[];

  /** Default parameter values */
  defaultParams: Record<string, unknown>;

  /** Input connection ports */
  inputs: ConnectionPort[];

  /** Output connection ports */
  outputs: ConnectionPort[];

  /** Block types this block can connect to */
  canConnectTo: RagBlockType[];

  /** Estimated token usage per execution */
  estimatedTokens: number;

  /** Estimated latency in milliseconds */
  estimatedLatencyMs: number;

  /** Estimated cost per execution (USD) */
  estimatedCost: number;

  /** Accuracy impact score (-1 to 1, 0 = neutral) */
  accuracyImpact: number;

  /** Minimum user tier required */
  requiredTier: UserTier;

  /** Maximum instances in a pipeline */
  maxInstances: number;

  /** Short description */
  description: string;

  /** Use case examples */
  useCases: string[];

  /** Backend code references */
  codeReferences: CodeReference[];
}

// =============================================================================
// ReactFlow Integration Types
// =============================================================================

/** Custom node data for ReactFlow */
export interface RagNodeData {
  block: RagBlock;
  params: Record<string, unknown>;
  status: NodeStatus;
  metrics?: NodeMetrics;
  // Index signature for ReactFlow compatibility
  [key: string]: unknown;
}

/** Node execution status */
export type NodeStatus =
  | 'idle'
  | 'running'
  | 'success'
  | 'error'
  | 'warning'
  | 'disabled';

/** Runtime metrics for a node */
export interface NodeMetrics {
  tokensUsed: number;
  latencyMs: number;
  cost: number;
  lastRun?: Date;
}

/** ReactFlow node with RAG block data */
export type RagNode = Node<RagNodeData, RagBlockType>;

/** ReactFlow edge with validation */
export interface RagEdge extends Edge {
  sourcePortId: string;
  targetPortId: string;
  dataType: PortDataType;
  animated?: boolean;
}

// =============================================================================
// Pipeline Definition Types
// =============================================================================

/** Complete pipeline definition */
export interface PipelineDefinition {
  id: string;
  name: string;
  description: string;
  version: string;
  nodes: RagNode[];
  edges: RagEdge[];
  createdAt: Date;
  updatedAt: Date;
  createdBy: string;
  isActive: boolean;
}

/** Pipeline validation result */
export interface PipelineValidation {
  isValid: boolean;
  errors: ValidationError[];
  warnings: ValidationWarning[];
  metrics: PipelineMetrics;
}

/** Validation error */
export interface ValidationError {
  nodeId?: string;
  edgeId?: string;
  code: string;
  message: string;
}

/** Validation warning */
export interface ValidationWarning {
  nodeId?: string;
  edgeId?: string;
  code: string;
  message: string;
}

/** Pipeline-level metrics */
export interface PipelineMetrics {
  totalEstimatedTokens: number;
  totalEstimatedLatencyMs: number;
  totalEstimatedCost: number;
  nodeCount: number;
  edgeCount: number;
}

// =============================================================================
// Palette Types
// =============================================================================

/** Block palette item for drag-and-drop */
export interface PaletteItem {
  block: RagBlock;
  disabled?: boolean;
  disabledReason?: string;
}

/** Grouped palette items by category */
export interface PaletteGroup {
  category: BlockCategory;
  label: string;
  icon: string;
  items: PaletteItem[];
  collapsed?: boolean;
}

// =============================================================================
// Canvas State Types
// =============================================================================

/** Canvas viewport state */
export interface CanvasViewport {
  x: number;
  y: number;
  zoom: number;
}

/** Selection state */
export interface SelectionState {
  nodes: string[];
  edges: string[];
}

/** Canvas interaction mode */
export type CanvasMode =
  | 'select'    // Default selection mode
  | 'pan'       // Pan only (space + drag)
  | 'connect'   // Drawing connections
  | 'readonly'; // View only

/** Complete canvas state */
export interface CanvasState {
  viewport: CanvasViewport;
  selection: SelectionState;
  mode: CanvasMode;
  gridEnabled: boolean;
  snapToGrid: boolean;
  minimapEnabled: boolean;
}

// =============================================================================
// Event Types
// =============================================================================

/** Block drop event from palette */
export interface BlockDropEvent {
  blockType: RagBlockType;
  position: XYPosition;
}

/** Connection attempt event */
export interface ConnectionAttemptEvent {
  sourceNodeId: string;
  sourcePortId: string;
  targetNodeId: string;
  targetPortId: string;
}

/** Node configuration change event */
export interface NodeConfigChangeEvent {
  nodeId: string;
  paramId: string;
  value: unknown;
}
