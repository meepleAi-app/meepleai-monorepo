/**
 * #4511: bodies the API really sends to the RAG pipeline page, taken from the local stack on
 * 2026-10-19 (`GET` with an admin session against `meepleai-api`, catalogue restored from the
 * snapshot). They are the API's, not written to fit the schemas: a schema that refuses them is
 * wrong, not the body.
 */

/** GET /api/v1/admin/kb/pipeline/health. `recentActivity` was empty on that stack. */
export const REAL_PIPELINE_HEALTH_BODY = {
  stages: [
    {
      name: 'Ingest',
      status: 'healthy',
      metrics: {
        activeJobs: 0,
        queuedJobs: 0,
        failedJobs: 0,
      },
    },
    {
      name: 'Extract',
      status: 'warning',
      metrics: {
        avgDurationMs: '19021',
        p95Ms: '49449',
        sampleSize: 100,
      },
    },
    {
      name: 'Chunk',
      status: 'warning',
      metrics: {
        avgDurationMs: '18920',
        p95Ms: '49449',
        sampleSize: 100,
      },
    },
    {
      name: 'Embed',
      status: 'healthy',
      metrics: {
        serviceHealth: 'Healthy',
        requestsTotal: 344,
        avgDurationMs: 1609.45,
        failureRate: 0,
      },
    },
    {
      name: 'Index',
      status: 'healthy',
      metrics: {
        vectorCount: 140,
        isAvailable: true,
      },
    },
    {
      name: 'Retrieve',
      status: 'healthy',
      metrics: {
        vectorStoreHealth: 'Healthy',
      },
    },
    {
      name: 'Generate',
      status: 'healthy',
      metrics: {
        note: 'LLM service via OpenRouter',
      },
    },
  ],
  summary: {
    healthyCount: 5,
    warningCount: 2,
    errorCount: 0,
  },
  recentActivity: [],
  distribution: {
    totalDocuments: 148,
    totalChunks: 11015,
    vectorCount: 140,
    totalFiles: 148,
    storageSizeFormatted: '1.2 GB',
  },
  checkedAt: '2026-10-19T16:20:12.3272293Z',
};

/** GET /api/v1/admin/pdfs/metrics/processing. Durations in seconds. */
export const REAL_PROCESSING_METRICS_BODY = {
  averages: {
    Uploading: {
      step: 'Uploading',
      avgDuration: 19.006600000000006,
      sampleSize: 100,
    },
    Extracting: {
      step: 'Extracting',
      avgDuration: 19.020600000000005,
      sampleSize: 100,
    },
    Chunking: {
      step: 'Chunking',
      avgDuration: 18.920400000000008,
      sampleSize: 100,
    },
    Embedding: {
      step: 'Embedding',
      avgDuration: 21.26200000000001,
      sampleSize: 100,
    },
    Indexing: {
      step: 'Indexing',
      avgDuration: 21.44150000000001,
      sampleSize: 100,
    },
  },
  percentiles: {
    Uploading: {
      p50: 14.915,
      p95: 49.44899999999996,
      p99: 81.33600000000008,
    },
    Extracting: {
      p50: 14.915,
      p95: 49.44899999999996,
      p99: 81.33600000000008,
    },
    Chunking: {
      p50: 14.915,
      p95: 49.44899999999996,
      p99: 81.33600000000008,
    },
    Embedding: {
      p50: 15.434999999999999,
      p95: 49.44899999999996,
      p99: 81.33600000000008,
    },
    Indexing: {
      p50: 16.17,
      p95: 49.44899999999996,
      p99: 81.33600000000008,
    },
  },
  lastUpdated: '2026-10-19T16:20:13.1386695Z',
};

/**
 * GET /api/v1/admin/kb/vector-stats, with `gameBreakdown` cut to its first three games (the real
 * one has a row per indexed game). Everything else is as sent.
 */
export const REAL_VECTOR_STATS_BODY = {
  totalVectors: 11015,
  dimensions: 768,
  gamesIndexed: 128,
  avgHealthPercent: 100,
  sizeEstimateBytes: 33838080,
  gameBreakdown: [
    {
      gameId: 'fb9d1c45-3f1f-45c5-1fe0-5a88887822e2',
      gameName: 'Android: Netrunner',
      vectorCount: 201,
      completedCount: 201,
      failedCount: 0,
      healthPercent: 100,
    },
    {
      gameId: 'bb92d9c8-1547-543d-c1c0-ada9195ca496',
      gameName: 'Le Havre',
      vectorCount: 118,
      completedCount: 118,
      failedCount: 0,
      healthPercent: 100,
    },
    {
      gameId: '73121806-6f5b-2d10-04e2-5aeee0d97fcb',
      gameName: 'Munchkin',
      vectorCount: 62,
      completedCount: 62,
      failedCount: 0,
      healthPercent: 100,
    },
  ],
};
