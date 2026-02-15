/**
 * Tests for Pipeline to Strategy Conversion
 *
 * Validates the export/import flow between pipeline builder and strategy format.
 *
 * @see Issue #3712 - Visual Pipeline Builder (Export to Strategy)
 */

import { describe, it, expect } from 'vitest';
import {
  convertPipelineToStrategy,
  importStrategyAsPipeline,
} from '../converters/pipeline-to-strategy';
import type { ExportedStrategy } from '../converters/pipeline-to-strategy';
import type { PipelineDefinition, PluginNode, PipelineEdge } from '../types';

// =============================================================================
// Test Fixtures
// =============================================================================

function createTestNode(
  id: string,
  pluginId: string,
  pluginName: string,
  category: 'routing' | 'retrieval' | 'generation' | 'evaluation',
  x = 0,
  y = 0,
): PluginNode {
  return {
    id,
    type: 'plugin',
    position: { x, y },
    data: {
      pluginId,
      pluginName,
      category,
      config: { topK: 10 },
      configSchema: { type: 'object' },
      isValid: true,
      validationErrors: [],
    },
  };
}

function createTestEdge(id: string, source: string, target: string): PipelineEdge {
  return {
    id,
    source,
    target,
    type: 'default',
    data: {
      condition: 'always',
      conditionPreset: 'always',
      isValid: true,
    },
  };
}

function createTestPipeline(overrides?: Partial<PipelineDefinition>): PipelineDefinition {
  return {
    id: 'test-pipeline-1',
    name: 'Test Pipeline',
    description: 'A test pipeline for unit tests',
    version: '1.0.0',
    nodes: [
      createTestNode('node-1', 'retrieval-hybrid', 'Hybrid Retrieval', 'retrieval', 0, 0),
      createTestNode('node-2', 'evaluation-reranker', 'Cross-Encoder Reranker', 'evaluation', 300, 0),
      createTestNode('node-3', 'generation-llm', 'LLM Generator', 'generation', 600, 0),
    ],
    edges: [
      createTestEdge('edge-1', 'node-1', 'node-2'),
      createTestEdge('edge-2', 'node-2', 'node-3'),
    ],
    createdAt: '2026-02-15T10:00:00.000Z',
    updatedAt: '2026-02-15T10:00:00.000Z',
    isValid: true,
    validationErrors: [],
    ...overrides,
  };
}

// =============================================================================
// Tests
// =============================================================================

describe('convertPipelineToStrategy', () => {
  it('should convert a basic pipeline to strategy format', () => {
    const pipeline = createTestPipeline();
    const strategy = convertPipelineToStrategy(pipeline);

    expect(strategy.id).toBe(pipeline.id);
    expect(strategy.name).toBe(pipeline.name);
    expect(strategy.description).toBe(pipeline.description);
    expect(strategy.version).toBe(pipeline.version);
    expect(strategy.sourceFormat).toBe('pipeline-builder');
  });

  it('should include metadata with correct counts', () => {
    const pipeline = createTestPipeline();
    const strategy = convertPipelineToStrategy(pipeline);

    expect(strategy.metadata.nodeCount).toBe(3);
    expect(strategy.metadata.edgeCount).toBe(2);
  });

  it('should calculate estimated metrics', () => {
    const pipeline = createTestPipeline();
    const strategy = convertPipelineToStrategy(pipeline);

    expect(strategy.metadata.estimatedTokens).toBeGreaterThan(0);
    expect(strategy.metadata.estimatedLatencyMs).toBeGreaterThan(0);
    expect(strategy.metadata.estimatedCost).toBeGreaterThan(0);
  });

  it('should list all categories used', () => {
    const pipeline = createTestPipeline();
    const strategy = convertPipelineToStrategy(pipeline);

    expect(strategy.metadata.categories).toContain('retrieval');
    expect(strategy.metadata.categories).toContain('evaluation');
    expect(strategy.metadata.categories).toContain('generation');
  });

  it('should convert steps with topological order', () => {
    const pipeline = createTestPipeline();
    const strategy = convertPipelineToStrategy(pipeline);

    // Steps should be ordered: retrieval → evaluation → generation
    expect(strategy.steps[0].name).toBe('Hybrid Retrieval');
    expect(strategy.steps[1].name).toBe('Cross-Encoder Reranker');
    expect(strategy.steps[2].name).toBe('LLM Generator');

    // Order should be sequential
    expect(strategy.steps[0].order).toBe(0);
    expect(strategy.steps[1].order).toBe(1);
    expect(strategy.steps[2].order).toBe(2);
  });

  it('should convert connections correctly', () => {
    const pipeline = createTestPipeline();
    const strategy = convertPipelineToStrategy(pipeline);

    expect(strategy.connections).toHaveLength(2);
    expect(strategy.connections[0].from).toBe('node-1');
    expect(strategy.connections[0].to).toBe('node-2');
    expect(strategy.connections[0].condition).toBe('always');
  });

  it('should preserve node positions in steps', () => {
    const pipeline = createTestPipeline();
    const strategy = convertPipelineToStrategy(pipeline);

    const step = strategy.steps.find((s) => s.id === 'node-1');
    expect(step?.position).toEqual({ x: 0, y: 0 });
  });

  it('should preserve node config in steps', () => {
    const pipeline = createTestPipeline();
    const strategy = convertPipelineToStrategy(pipeline);

    const step = strategy.steps.find((s) => s.id === 'node-1');
    expect(step?.config).toEqual({ topK: 10 });
  });

  it('should set exportedAt timestamp', () => {
    const pipeline = createTestPipeline();
    const strategy = convertPipelineToStrategy(pipeline);

    expect(strategy.exportedAt).toBeTruthy();
    expect(new Date(strategy.exportedAt).getTime()).not.toBeNaN();
  });

  it('should infer beginner difficulty for simple pipelines', () => {
    const pipeline = createTestPipeline({
      nodes: [
        createTestNode('n1', 'retrieval-hybrid', 'Hybrid Retrieval', 'retrieval'),
        createTestNode('n2', 'generation-llm', 'LLM Generator', 'generation'),
      ],
      edges: [createTestEdge('e1', 'n1', 'n2')],
    });

    const strategy = convertPipelineToStrategy(pipeline);
    expect(strategy.metadata.difficulty).toBe('beginner');
  });

  it('should infer intermediate difficulty for medium pipelines', () => {
    const pipeline = createTestPipeline(); // 3 nodes, 3 categories
    const strategy = convertPipelineToStrategy(pipeline);
    expect(strategy.metadata.difficulty).toBe('intermediate');
  });

  it('should infer advanced difficulty for complex pipelines', () => {
    const pipeline = createTestPipeline({
      nodes: [
        createTestNode('n1', 'routing-llm', 'LLM Router', 'routing', 0, 0),
        createTestNode('n2', 'retrieval-hybrid', 'Hybrid', 'retrieval', 200, 0),
        createTestNode('n3', 'retrieval-multihop', 'Multi-Hop', 'retrieval', 200, 200),
        createTestNode('n4', 'evaluation-crag', 'CRAG', 'evaluation', 400, 0),
        createTestNode('n5', 'evaluation-reranker', 'Reranker', 'evaluation', 400, 200),
        createTestNode('n6', 'generation-llm', 'Generator', 'generation', 600, 100),
      ],
      edges: [
        createTestEdge('e1', 'n1', 'n2'),
        createTestEdge('e2', 'n1', 'n3'),
        createTestEdge('e3', 'n2', 'n4'),
        createTestEdge('e4', 'n3', 'n5'),
        createTestEdge('e5', 'n4', 'n6'),
        createTestEdge('e6', 'n5', 'n6'),
      ],
    });

    const strategy = convertPipelineToStrategy(pipeline);
    expect(strategy.metadata.difficulty).toBe('advanced');
  });

  it('should handle empty pipeline', () => {
    const pipeline = createTestPipeline({
      nodes: [],
      edges: [],
    });

    const strategy = convertPipelineToStrategy(pipeline);
    expect(strategy.steps).toHaveLength(0);
    expect(strategy.connections).toHaveLength(0);
    expect(strategy.metadata.nodeCount).toBe(0);
  });

  it('should handle pipeline without description', () => {
    const pipeline = createTestPipeline({ description: undefined });
    const strategy = convertPipelineToStrategy(pipeline);
    expect(strategy.description).toBe('');
  });
});

describe('importStrategyAsPipeline', () => {
  it('should round-trip convert pipeline → strategy → pipeline', () => {
    const original = createTestPipeline();
    const strategy = convertPipelineToStrategy(original);
    const imported = importStrategyAsPipeline(strategy);

    expect(imported.id).toBe(original.id);
    expect(imported.name).toBe(original.name);
    expect(imported.version).toBe(original.version);
    expect(imported.nodes).toHaveLength(original.nodes.length);
    expect(imported.edges).toHaveLength(original.edges.length);
  });

  it('should preserve node positions after round-trip', () => {
    const original = createTestPipeline();
    const strategy = convertPipelineToStrategy(original);
    const imported = importStrategyAsPipeline(strategy);

    const originalNode = original.nodes.find((n) => n.id === 'node-1');
    const importedNode = imported.nodes.find((n) => n.id === 'node-1');
    expect(importedNode?.position).toEqual(originalNode?.position);
  });

  it('should preserve edge connections after round-trip', () => {
    const original = createTestPipeline();
    const strategy = convertPipelineToStrategy(original);
    const imported = importStrategyAsPipeline(strategy);

    expect(imported.edges[0].source).toBe('node-1');
    expect(imported.edges[0].target).toBe('node-2');
  });

  it('should set isValid to true on imported pipeline', () => {
    const strategy = convertPipelineToStrategy(createTestPipeline());
    const imported = importStrategyAsPipeline(strategy);

    expect(imported.isValid).toBe(true);
  });

  it('should set new timestamps on imported pipeline', () => {
    const strategy = convertPipelineToStrategy(createTestPipeline());
    const imported = importStrategyAsPipeline(strategy);

    expect(imported.createdAt).toBeTruthy();
    expect(imported.updatedAt).toBeTruthy();
  });

  it('should handle strategy with no connections', () => {
    const strategy: ExportedStrategy = {
      id: 'test-1',
      name: 'Simple',
      description: '',
      version: '1.0.0',
      exportedAt: new Date().toISOString(),
      sourceFormat: 'pipeline-builder',
      metadata: {
        nodeCount: 1,
        edgeCount: 0,
        estimatedTokens: 100,
        estimatedLatencyMs: 50,
        estimatedCost: 0.001,
        categories: ['generation'],
        difficulty: 'beginner',
      },
      steps: [
        {
          id: 'node-1',
          order: 0,
          type: 'llm-generation',
          name: 'LLM Generator',
          category: 'generation',
          config: {},
          position: { x: 0, y: 0 },
        },
      ],
      connections: [],
    };

    const imported = importStrategyAsPipeline(strategy);
    expect(imported.nodes).toHaveLength(1);
    expect(imported.edges).toHaveLength(0);
  });
});
