/**
 * Pipeline Builder Types
 *
 * Type definitions for the visual RAG pipeline builder.
 * Supports drag-drop canvas, plugin nodes, and edge configuration.
 *
 * @version 1.0.0
 * @see Issue #3425 - Visual Pipeline Builder
 */

import type { Node, Edge, XYPosition } from '@xyflow/react';

// =============================================================================
// Plugin Categories
// =============================================================================

export type PluginCategory =
  | 'routing'
  | 'cache'
  | 'retrieval'
  | 'evaluation'
  | 'generation'
  | 'validation'
  | 'transform';

export const PLUGIN_CATEGORY_COLORS: Record<PluginCategory, string> = {
  routing: 'hsl(221 83% 53%)',    // Blue
  cache: 'hsl(262 83% 62%)',      // Purple
  retrieval: 'hsl(142 76% 36%)',  // Green
  evaluation: 'hsl(45 93% 47%)',  // Gold
  generation: 'hsl(25 95% 53%)',  // Orange
  validation: 'hsl(0 72% 51%)',   // Red
  transform: 'hsl(200 95% 40%)',  // Cyan
};

export const PLUGIN_CATEGORY_ICONS: Record<PluginCategory, string> = {
  routing: '🧠',
  cache: '💾',
  retrieval: '📚',
  evaluation: '✅',
  generation: '✨',
  validation: '🔍',
  transform: '🔄',
};

// =============================================================================
// Plugin Definition
// =============================================================================

export interface PluginDefinition {
  id: string;
  name: string;
  description: string;
  category: PluginCategory;
  version: string;
  icon?: string;
  configSchema: JsonSchema;
  inputSchema: JsonSchema;
  outputSchema: JsonSchema;
  isFavorite?: boolean;
  recentlyUsed?: boolean;
}

export interface JsonSchema {
  type: 'object' | 'string' | 'number' | 'boolean' | 'array';
  properties?: Record<string, JsonSchemaProperty>;
  required?: string[];
  additionalProperties?: boolean;
}

export interface JsonSchemaProperty {
  type: 'string' | 'number' | 'integer' | 'boolean' | 'array' | 'object';
  title?: string;
  description?: string;
  default?: unknown;
  enum?: (string | number)[];
  minimum?: number;
  maximum?: number;
  minLength?: number;
  maxLength?: number;
  pattern?: string;
  items?: JsonSchemaProperty;
  properties?: Record<string, JsonSchemaProperty>;
  required?: string[];
  format?: 'email' | 'uri' | 'date' | 'date-time' | 'password' | 'textarea';
  // Conditional logic
  if?: { properties: Record<string, { const: unknown }> };
  then?: { properties: Record<string, JsonSchemaProperty> };
}

// =============================================================================
// Pipeline Node Types
// =============================================================================

export interface PluginNodeData {
  pluginId: string;
  pluginName: string;
  category: PluginCategory;
  config: Record<string, unknown>;
  configSchema: JsonSchema;
  isValid: boolean;
  validationErrors?: string[];
  isExecuting?: boolean;
  executionStatus?: 'pending' | 'running' | 'success' | 'error';
  executionResult?: ExecutionResult;
  [key: string]: unknown; // Index signature for React Flow compatibility
}

export type PluginNode = Node<PluginNodeData, 'plugin'>;

// =============================================================================
// Pipeline Edge Types
// =============================================================================

export interface PipelineEdgeData {
  condition: string;
  conditionPreset?: ConditionPreset;
  transform?: string;
  timeout?: number;
  label?: string;
  isValid: boolean;
  validationError?: string;
  [key: string]: unknown; // Index signature for React Flow compatibility
}

export type PipelineEdge = Edge<PipelineEdgeData>;

export type ConditionPreset =
  | 'always'
  | 'never'
  | 'high_confidence'
  | 'medium_confidence'
  | 'rules_query'
  | 'strategy_query'
  | 'custom';

export const CONDITION_PRESETS: Record<ConditionPreset, { label: string; expression: string }> = {
  always: { label: 'Always', expression: 'always' },
  never: { label: 'Never', expression: 'never' },
  high_confidence: { label: 'High Confidence ≥80%', expression: 'confidence >= 0.8' },
  medium_confidence: { label: 'Medium Confidence ≥50%', expression: 'confidence >= 0.5' },
  rules_query: { label: 'Rules Query', expression: 'queryType == "rules"' },
  strategy_query: { label: 'Strategy Query', expression: 'queryType == "strategy"' },
  custom: { label: 'Custom', expression: '' },
};

// =============================================================================
// Pipeline Definition
// =============================================================================

export interface PipelineDefinition {
  id: string;
  name: string;
  description?: string;
  version: string;
  nodes: PluginNode[];
  edges: PipelineEdge[];
  createdAt: string;
  updatedAt: string;
  isValid: boolean;
  validationErrors?: string[];
}

// =============================================================================
// Execution Types
// =============================================================================

export interface ExecutionResult {
  nodeId: string;
  input: unknown;
  output: unknown;
  durationMs: number;
  tokensUsed?: number;
  error?: string;
}

export interface ExecutionTrace {
  id: string;
  pipelineId: string;
  query: string;
  startedAt: string;
  completedAt?: string;
  status: 'running' | 'completed' | 'failed';
  steps: ExecutionStep[];
  metrics: ExecutionMetrics;
}

export interface ExecutionStep {
  nodeId: string;
  nodeName: string;
  status: 'pending' | 'running' | 'success' | 'error' | 'skipped';
  input?: unknown;
  output?: unknown;
  durationMs?: number;
  error?: string;
}

export interface ExecutionMetrics {
  totalTokens: number;
  estimatedCost: number;
  totalLatencyMs: number;
  nodesExecuted: number;
  cacheHits: number;
}

// =============================================================================
// Builder State
// =============================================================================

export interface PipelineBuilderState {
  // Current pipeline
  pipeline: PipelineDefinition | null;
  isDirty: boolean;

  // Selection
  selectedNodeId: string | null;
  selectedEdgeId: string | null;

  // Drag state
  isDragging: boolean;
  draggedPlugin: PluginDefinition | null;

  // Execution
  isExecuting: boolean;
  executionTrace: ExecutionTrace | null;
  stepMode: boolean;
  currentStepIndex: number;

  // History (undo/redo)
  history: PipelineDefinition[];
  historyIndex: number;
  maxHistorySize: number;

  // UI State
  showMiniMap: boolean;
  showGrid: boolean;
  isLocked: boolean;
  zoomLevel: number;
}

// =============================================================================
// Builder Actions
// =============================================================================

export interface PipelineBuilderActions {
  // Pipeline CRUD
  createPipeline: (name: string, description?: string) => void;
  loadPipeline: (pipeline: PipelineDefinition) => void;
  savePipeline: () => Promise<void>;
  clearPipeline: () => void;

  // Node operations
  addNode: (plugin: PluginDefinition, position: XYPosition) => string;
  updateNode: (nodeId: string, data: Partial<PluginNodeData>) => void;
  removeNode: (nodeId: string) => void;
  updateNodePosition: (nodeId: string, position: XYPosition) => void;
  updateNodeConfig: (nodeId: string, config: Record<string, unknown>) => void;

  // Edge operations
  addEdge: (sourceId: string, targetId: string, data?: Partial<PipelineEdgeData>) => string | null;
  updateEdge: (edgeId: string, data: Partial<PipelineEdgeData>) => void;
  removeEdge: (edgeId: string) => void;

  // Selection
  selectNode: (nodeId: string | null) => void;
  selectEdge: (edgeId: string | null) => void;

  // Drag operations
  startDrag: (plugin: PluginDefinition) => void;
  endDrag: () => void;

  // History
  undo: () => void;
  redo: () => void;
  canUndo: () => boolean;
  canRedo: () => boolean;
  pushHistory: () => void;

  // Execution
  runPipeline: (query: string) => Promise<void>;
  runDryRun: (query: string) => Promise<void>;
  stepThrough: () => void;
  stopExecution: () => void;
  setStepMode: (enabled: boolean) => void;

  // Validation
  validatePipeline: () => boolean;
  validateNode: (nodeId: string) => boolean;
  validateEdge: (edgeId: string) => boolean;

  // Layout
  autoLayout: () => void;
  fitView: () => void;
  setZoom: (level: number) => void;
  toggleMiniMap: () => void;
  toggleGrid: () => void;
  toggleLock: () => void;
}

// =============================================================================
// Built-in Plugins (based on RAG layers)
// =============================================================================

export const BUILT_IN_PLUGINS: PluginDefinition[] = [
  // Routing plugins
  {
    id: 'routing-llm',
    name: 'LLM Router',
    description: 'Query classification using LLM to determine optimal strategy',
    category: 'routing',
    version: '1.0.0',
    icon: '🧠',
    configSchema: {
      type: 'object',
      properties: {
        model: {
          type: 'string',
          title: 'Model',
          description: 'LLM model for classification',
          enum: ['claude-haiku-4.5', 'gpt-4o-mini', 'llama-3.3-70b'],
          default: 'claude-haiku-4.5',
        },
        maxTokens: {
          type: 'integer',
          title: 'Max Tokens',
          description: 'Maximum tokens for classification',
          default: 100,
          minimum: 50,
          maximum: 500,
        },
      },
      required: ['model'],
    },
    inputSchema: {
      type: 'object',
      properties: {
        query: { type: 'string', title: 'Query' },
        context: { type: 'object', title: 'Context' },
      },
      required: ['query'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        strategy: { type: 'string', title: 'Selected Strategy' },
        confidence: { type: 'number', title: 'Confidence Score' },
        queryType: { type: 'string', title: 'Query Type' },
      },
    },
  },
  {
    id: 'routing-rules',
    name: 'Rules Router',
    description: 'Fast pattern-based routing using predefined rules',
    category: 'routing',
    version: '1.0.0',
    icon: '📋',
    configSchema: {
      type: 'object',
      properties: {
        rules: {
          type: 'array',
          title: 'Routing Rules',
          items: {
            type: 'object',
            properties: {
              pattern: { type: 'string', title: 'Pattern' },
              strategy: { type: 'string', title: 'Strategy' },
            },
          },
        },
        defaultStrategy: {
          type: 'string',
          title: 'Default Strategy',
          default: 'BALANCED',
        },
      },
    },
    inputSchema: {
      type: 'object',
      properties: {
        query: { type: 'string', title: 'Query' },
      },
      required: ['query'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        strategy: { type: 'string', title: 'Selected Strategy' },
        matchedRule: { type: 'string', title: 'Matched Rule' },
      },
    },
  },

  // Cache plugins
  {
    id: 'cache-semantic',
    name: 'Semantic Cache',
    description: 'Vector similarity-based cache lookup with 80% hit rate target',
    category: 'cache',
    version: '1.0.0',
    icon: '💾',
    configSchema: {
      type: 'object',
      properties: {
        similarityThreshold: {
          type: 'number',
          title: 'Similarity Threshold',
          description: 'Minimum similarity for cache hit (0-1)',
          default: 0.92,
          minimum: 0.5,
          maximum: 1.0,
        },
        ttlHours: {
          type: 'integer',
          title: 'TTL (hours)',
          description: 'Cache entry time-to-live',
          default: 48,
          minimum: 1,
          maximum: 720,
        },
        maxEntries: {
          type: 'integer',
          title: 'Max Entries',
          description: 'Maximum cache entries',
          default: 10000,
        },
      },
    },
    inputSchema: {
      type: 'object',
      properties: {
        query: { type: 'string', title: 'Query' },
        embedding: { type: 'array', title: 'Query Embedding', items: { type: 'number' } },
      },
      required: ['query'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        hit: { type: 'boolean', title: 'Cache Hit' },
        cachedResponse: { type: 'object', title: 'Cached Response' },
        similarity: { type: 'number', title: 'Similarity Score' },
      },
    },
  },

  // Retrieval plugins
  {
    id: 'retrieval-hybrid',
    name: 'Hybrid Retrieval',
    description: 'Combined vector + BM25 keyword search with RRF fusion',
    category: 'retrieval',
    version: '1.0.0',
    icon: '📚',
    configSchema: {
      type: 'object',
      properties: {
        topK: {
          type: 'integer',
          title: 'Top K Results',
          default: 10,
          minimum: 1,
          maximum: 100,
        },
        vectorWeight: {
          type: 'number',
          title: 'Vector Weight',
          description: 'Weight for vector search (0-1)',
          default: 0.7,
          minimum: 0,
          maximum: 1,
        },
        keywordWeight: {
          type: 'number',
          title: 'Keyword Weight',
          description: 'Weight for keyword search (0-1)',
          default: 0.3,
          minimum: 0,
          maximum: 1,
        },
        collection: {
          type: 'string',
          title: 'Collection',
          description: 'Vector collection to search',
        },
      },
      required: ['collection'],
    },
    inputSchema: {
      type: 'object',
      properties: {
        query: { type: 'string', title: 'Query' },
        filters: { type: 'object', title: 'Filters' },
      },
      required: ['query'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        documents: {
          type: 'array',
          title: 'Retrieved Documents',
          items: {
            type: 'object',
            properties: {
              id: { type: 'string' },
              content: { type: 'string' },
              score: { type: 'number' },
            },
          },
        },
        totalFound: { type: 'integer', title: 'Total Found' },
      },
    },
  },
  {
    id: 'retrieval-multihop',
    name: 'Multi-Hop Retrieval',
    description: 'Iterative retrieval for complex multi-document queries',
    category: 'retrieval',
    version: '1.0.0',
    icon: '🔗',
    configSchema: {
      type: 'object',
      properties: {
        maxHops: {
          type: 'integer',
          title: 'Max Hops',
          description: 'Maximum retrieval iterations',
          default: 3,
          minimum: 1,
          maximum: 5,
        },
        hopStrategy: {
          type: 'string',
          title: 'Hop Strategy',
          enum: ['breadth_first', 'depth_first', 'hybrid'],
          default: 'hybrid',
        },
        documentsPerHop: {
          type: 'integer',
          title: 'Documents per Hop',
          default: 5,
          minimum: 1,
          maximum: 20,
        },
      },
    },
    inputSchema: {
      type: 'object',
      properties: {
        query: { type: 'string', title: 'Query' },
        initialDocuments: { type: 'array', title: 'Initial Documents' },
      },
      required: ['query'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        documents: { type: 'array', title: 'Retrieved Documents' },
        hopChain: { type: 'array', title: 'Hop Chain' },
        totalHops: { type: 'integer', title: 'Total Hops' },
      },
    },
  },

  // Evaluation plugins
  {
    id: 'evaluation-crag',
    name: 'CRAG Evaluator',
    description: 'Corrective RAG quality evaluation with T5-Large grading',
    category: 'evaluation',
    version: '1.0.0',
    icon: '✅',
    configSchema: {
      type: 'object',
      properties: {
        threshold: {
          type: 'number',
          title: 'Quality Threshold',
          description: 'Minimum quality score to pass (0-1)',
          default: 0.7,
          minimum: 0.1,
          maximum: 1.0,
        },
        action: {
          type: 'string',
          title: 'Failure Action',
          enum: ['refetch', 'websearch', 'fail'],
          default: 'refetch',
        },
      },
    },
    inputSchema: {
      type: 'object',
      properties: {
        query: { type: 'string', title: 'Query' },
        documents: { type: 'array', title: 'Documents' },
      },
      required: ['query', 'documents'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        passed: { type: 'boolean', title: 'Passed' },
        score: { type: 'number', title: 'Quality Score' },
        reasoning: { type: 'string', title: 'Reasoning' },
        filteredDocuments: { type: 'array', title: 'Filtered Documents' },
      },
    },
  },
  {
    id: 'evaluation-reranker',
    name: 'Cross-Encoder Reranker',
    description: 'Rerank documents using cross-encoder model for better relevance',
    category: 'evaluation',
    version: '1.0.0',
    icon: '📊',
    configSchema: {
      type: 'object',
      properties: {
        model: {
          type: 'string',
          title: 'Reranker Model',
          enum: ['ms-marco-MiniLM-L-6-v2', 'bge-reranker-large', 'cohere-rerank-v3'],
          default: 'ms-marco-MiniLM-L-6-v2',
        },
        topK: {
          type: 'integer',
          title: 'Top K After Rerank',
          default: 5,
          minimum: 1,
          maximum: 20,
        },
      },
    },
    inputSchema: {
      type: 'object',
      properties: {
        query: { type: 'string', title: 'Query' },
        documents: { type: 'array', title: 'Documents to Rerank' },
      },
      required: ['query', 'documents'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        documents: { type: 'array', title: 'Reranked Documents' },
        scores: { type: 'array', title: 'Relevance Scores' },
      },
    },
  },

  // Generation plugins
  {
    id: 'generation-llm',
    name: 'LLM Generator',
    description: 'Generate response using LLM with context and prompt template',
    category: 'generation',
    version: '1.0.0',
    icon: '✨',
    configSchema: {
      type: 'object',
      properties: {
        model: {
          type: 'string',
          title: 'Model',
          enum: ['claude-sonnet-4.5', 'claude-haiku-4.5', 'gpt-4o', 'deepseek-chat'],
          default: 'claude-sonnet-4.5',
        },
        temperature: {
          type: 'number',
          title: 'Temperature',
          default: 0.7,
          minimum: 0,
          maximum: 2,
        },
        maxTokens: {
          type: 'integer',
          title: 'Max Tokens',
          default: 2048,
          minimum: 100,
          maximum: 8192,
        },
        systemPrompt: {
          type: 'string',
          title: 'System Prompt',
          format: 'textarea',
        },
      },
      required: ['model'],
    },
    inputSchema: {
      type: 'object',
      properties: {
        query: { type: 'string', title: 'Query' },
        context: { type: 'array', title: 'Context Documents' },
      },
      required: ['query'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        response: { type: 'string', title: 'Generated Response' },
        tokensUsed: { type: 'integer', title: 'Tokens Used' },
        citations: { type: 'array', title: 'Citations' },
      },
    },
  },
  {
    id: 'generation-consensus',
    name: 'Consensus Generator',
    description: 'Multi-LLM voting with aggregation for high-stakes decisions',
    category: 'generation',
    version: '1.0.0',
    icon: '🗳️',
    configSchema: {
      type: 'object',
      properties: {
        voters: {
          type: 'array',
          title: 'Voter Models',
          items: {
            type: 'string',
            enum: ['claude-sonnet-4.5', 'gpt-4o', 'deepseek-chat'],
          },
          default: ['claude-sonnet-4.5', 'gpt-4o', 'deepseek-chat'],
        },
        aggregator: {
          type: 'string',
          title: 'Aggregator Model',
          default: 'claude-sonnet-4.5',
        },
        requireUnanimity: {
          type: 'boolean',
          title: 'Require Unanimity',
          default: false,
        },
      },
    },
    inputSchema: {
      type: 'object',
      properties: {
        query: { type: 'string', title: 'Query' },
        context: { type: 'array', title: 'Context' },
      },
      required: ['query'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        response: { type: 'string', title: 'Consensus Response' },
        votes: { type: 'array', title: 'Individual Votes' },
        agreement: { type: 'number', title: 'Agreement Score' },
      },
    },
  },

  // Validation plugins
  {
    id: 'validation-citation',
    name: 'Citation Validator',
    description: 'Verify citations and detect hallucinations in generated responses',
    category: 'validation',
    version: '1.0.0',
    icon: '🔍',
    configSchema: {
      type: 'object',
      properties: {
        strictMode: {
          type: 'boolean',
          title: 'Strict Mode',
          description: 'Require exact matches for citations',
          default: false,
        },
        minCitations: {
          type: 'integer',
          title: 'Min Citations',
          description: 'Minimum required citations',
          default: 1,
          minimum: 0,
        },
      },
    },
    inputSchema: {
      type: 'object',
      properties: {
        response: { type: 'string', title: 'Generated Response' },
        sourceDocuments: { type: 'array', title: 'Source Documents' },
      },
      required: ['response', 'sourceDocuments'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        valid: { type: 'boolean', title: 'Is Valid' },
        citationCoverage: { type: 'number', title: 'Citation Coverage' },
        hallucinationScore: { type: 'number', title: 'Hallucination Score' },
        issues: { type: 'array', title: 'Validation Issues' },
      },
    },
  },
  {
    id: 'validation-selfrag',
    name: 'Self-RAG Validator',
    description: 'Self-reflective validation with automatic correction',
    category: 'validation',
    version: '1.0.0',
    icon: '🔄',
    configSchema: {
      type: 'object',
      properties: {
        maxRetries: {
          type: 'integer',
          title: 'Max Retries',
          description: 'Maximum self-correction attempts',
          default: 2,
          minimum: 0,
          maximum: 5,
        },
        correctionModel: {
          type: 'string',
          title: 'Correction Model',
          enum: ['claude-sonnet-4.5', 'gpt-4o'],
          default: 'claude-sonnet-4.5',
        },
      },
    },
    inputSchema: {
      type: 'object',
      properties: {
        response: { type: 'string', title: 'Response to Validate' },
        context: { type: 'array', title: 'Context' },
        query: { type: 'string', title: 'Original Query' },
      },
      required: ['response', 'query'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        finalResponse: { type: 'string', title: 'Final Response' },
        corrections: { type: 'array', title: 'Corrections Made' },
        retryCount: { type: 'integer', title: 'Retry Count' },
      },
    },
  },

  // Transform plugins
  {
    id: 'transform-format',
    name: 'Output Formatter',
    description: 'Format response according to template (markdown, JSON, etc.)',
    category: 'transform',
    version: '1.0.0',
    icon: '📝',
    configSchema: {
      type: 'object',
      properties: {
        format: {
          type: 'string',
          title: 'Output Format',
          enum: ['markdown', 'json', 'html', 'plain'],
          default: 'markdown',
        },
        template: {
          type: 'string',
          title: 'Template',
          format: 'textarea',
          description: 'Output template with {{placeholders}}',
        },
      },
      required: ['format'],
    },
    inputSchema: {
      type: 'object',
      properties: {
        data: { type: 'object', title: 'Data to Format' },
      },
      required: ['data'],
    },
    outputSchema: {
      type: 'object',
      properties: {
        formatted: { type: 'string', title: 'Formatted Output' },
      },
    },
  },
];

// =============================================================================
// Query Presets for Testing
// =============================================================================

export interface QueryPreset {
  id: string;
  name: string;
  query: string;
  expectedRoute: string;
  description: string;
}

export const QUERY_PRESETS: QueryPreset[] = [
  {
    id: 'rules-simple',
    name: 'Simple Rule',
    query: 'How many cards do I draw at the start in Catan?',
    expectedRoute: 'FAST',
    description: 'Basic FAQ question expecting quick lookup',
  },
  {
    id: 'rules-complex',
    name: 'Complex Rule',
    query: 'Can I build a settlement adjacent to another settlement if there is a road between them?',
    expectedRoute: 'BALANCED',
    description: 'Rule requiring context and validation',
  },
  {
    id: 'strategy',
    name: 'Strategy Query',
    query: "What's the best opening strategy for Catan as the first player?",
    expectedRoute: 'PRECISE',
    description: 'Strategic advice requiring multi-agent analysis',
  },
  {
    id: 'verification',
    name: 'Citation Check',
    query: 'Where in the rulebook does it say you can trade with the bank?',
    expectedRoute: 'PRECISE',
    description: 'Citation verification query',
  },
];
