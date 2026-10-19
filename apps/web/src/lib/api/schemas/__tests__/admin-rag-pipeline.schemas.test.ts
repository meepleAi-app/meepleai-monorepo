/**
 * #4511: the schemas of the RAG pipeline page against the bodies the API really sends.
 *
 * The page's sidebar called `/admin/pipeline/metrics`, `/health` and `/cleanup-orphans`, routes the
 * API does not have, and the Embedding tab read `/admin/kb/vector-stats` with names the API never
 * sent. None of those calls went through a schema, so nothing broke visibly. The three GET bodies
 * are the ones in the fixtures, taken from the local stack; each "refuses" test feeds the schema
 * the shape the page used to expect.
 */

import { describe, expect, it } from 'vitest';

import {
  REAL_PIPELINE_HEALTH_BODY,
  REAL_PROCESSING_METRICS_BODY,
  REAL_VECTOR_STATS_BODY,
} from '@/__tests__/fixtures/admin-rag-pipeline-bodies';

import {
  CleanupOrphansResultSchema,
  PipelineHealthResponseSchema,
  ProcessingMetricsSchema,
  VectorStatsSchema,
} from '../admin-knowledge-base.schemas';

describe('PipelineHealthResponseSchema (#4511)', () => {
  it('accepts the real body', () => {
    const parsed = PipelineHealthResponseSchema.parse(REAL_PIPELINE_HEALTH_BODY);

    expect(parsed.stages.map(s => s.name)).toEqual([
      'Ingest',
      'Extract',
      'Chunk',
      'Embed',
      'Index',
      'Retrieve',
      'Generate',
    ]);
    expect(parsed.stages.find(s => s.name === 'Extract')?.status).toBe('warning');
    expect(parsed.summary).toEqual({ healthyCount: 5, warningCount: 2, errorCount: 0 });
  });

  it('accepts a recent job with no completion time and no duration', () => {
    // The real body had no recent jobs; this one has the fields AdminPipelineEndpoints writes,
    // with the two that are null for a job that never started.
    const withActivity = {
      ...REAL_PIPELINE_HEALTH_BODY,
      recentActivity: [
        {
          jobId: '0b7e3c1e-56a4-4c2f-9d0a-2f4c1d7e8a90',
          fileName: 'rules.pdf',
          status: 'Failed',
          completedAt: null,
          durationMs: null,
        },
      ],
    };

    expect(PipelineHealthResponseSchema.safeParse(withActivity).success).toBe(true);
  });

  it('refuses the shape the sidebar used to expect', () => {
    const result = PipelineHealthResponseSchema.safeParse({
      stages: [{ stage: 'Extract', status: 'degraded' }],
      overallStatus: 'degraded',
    });

    expect(result.success).toBe(false);
  });

  it('refuses a stage status the API never writes', () => {
    // The real body with one change: the sidebar's `degraded`, which the API calls `warning`.
    const stages = REAL_PIPELINE_HEALTH_BODY.stages.map(s =>
      s.name === 'Extract' ? { ...s, status: 'degraded' } : s
    );

    const result = PipelineHealthResponseSchema.safeParse({ ...REAL_PIPELINE_HEALTH_BODY, stages });

    expect(result.success).toBe(false);
  });
});

describe('ProcessingMetricsSchema (#4511)', () => {
  it('accepts the real body', () => {
    const parsed = ProcessingMetricsSchema.parse(REAL_PROCESSING_METRICS_BODY);

    expect(Object.keys(parsed.averages)).toEqual([
      'Uploading',
      'Extracting',
      'Chunking',
      'Embedding',
      'Indexing',
    ]);
    expect(parsed.averages.Extracting?.sampleSize).toBe(100);
  });

  it('refuses the shape the sidebar used to expect', () => {
    const result = ProcessingMetricsSchema.safeParse({
      stepTimings: [{ step: 'Extracting', avgDurationSeconds: 19, sampleCount: 100 }],
      avgTotalSeconds: 99,
    });

    expect(result.success).toBe(false);
  });
});

describe('VectorStatsSchema (#4511)', () => {
  it('accepts the real body', () => {
    const parsed = VectorStatsSchema.parse(REAL_VECTOR_STATS_BODY);

    expect(parsed.totalVectors).toBe(11015);
    expect(parsed.gameBreakdown[0]).toMatchObject({
      gameName: 'Android: Netrunner',
      vectorCount: 201,
      healthPercent: 100,
    });
  });

  it('refuses the shape the Embedding tab used to expect', () => {
    const result = VectorStatsSchema.safeParse({
      games: [{ gameId: 'g', gameName: 'Le Havre', vectorCount: 118, documentCount: 1 }],
      totalVectors: 118,
      totalDocuments: 1,
    });

    expect(result.success).toBe(false);
  });
});

describe('CleanupOrphansResultSchema (#4511)', () => {
  it('accepts the real body', () => {
    // Answered by this branch's API to the sidebar button, on a copy of the local database.
    const parsed = CleanupOrphansResultSchema.parse(
      JSON.parse('{"orphanedChunks":0,"orphanedVectors":0}')
    );

    expect(parsed).toEqual({ orphanedChunks: 0, orphanedVectors: 0 });
  });

  it('refuses the `cleaned` count the sidebar used to expect', () => {
    expect(CleanupOrphansResultSchema.safeParse({ cleaned: 3 }).success).toBe(false);
  });
});
