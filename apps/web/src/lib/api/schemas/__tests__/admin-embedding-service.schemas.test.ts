/**
 * #4446: the schemas of GET /api/v1/admin/embedding/info and /metrics against the bodies the API
 * really sends.
 *
 * No consumer validated these bodies, and they read fields the API never sent (`dimensions`,
 * `totalRequests`, `throughputPerSecond`). The healthy info body and the metrics body below were
 * taken from the API of this branch on the local stack (2026-10-18), talking to the real embedding
 * service. The unavailable info body is what the API sends when the service does not answer: the
 * dimension is the API's, so it is still there.
 */

import { describe, expect, it } from 'vitest';

import {
  EmbeddingServiceInfoSchema,
  EmbeddingServiceMetricsSchema,
} from '../admin-knowledge-base.schemas';

const REAL_HEALTHY_BODY = JSON.parse(
  '{"status":"healthy","model":"intfloat/multilingual-e5-base","device":"cpu","supportedLanguages":["en","it","de","fr","es"],"dimension":768,"maxInputChars":4096}'
);

const UNAVAILABLE_BODY = {
  status: 'unavailable',
  model: null,
  device: null,
  supportedLanguages: [],
  dimension: 768,
  maxInputChars: null,
};

describe('EmbeddingServiceInfoSchema (#4446)', () => {
  it('accepts the real body of a healthy service', () => {
    const parsed = EmbeddingServiceInfoSchema.parse(REAL_HEALTHY_BODY);

    expect(parsed.model).toBe('intfloat/multilingual-e5-base');
    expect(parsed.dimension).toBe(768);
    expect(parsed.supportedLanguages).toEqual(['en', 'it', 'de', 'fr', 'es']);
    expect(parsed.maxInputChars).toBe(4096);
  });

  it('accepts the body of a service that does not answer', () => {
    expect(EmbeddingServiceInfoSchema.safeParse(UNAVAILABLE_BODY).success).toBe(true);
  });

  it('refuses a body without `dimension`, the field the Embedding tab never found', () => {
    const { dimension: _dimension, ...withoutDimension } = REAL_HEALTHY_BODY;

    const result = EmbeddingServiceInfoSchema.safeParse({ ...withoutDimension, dimensions: 768 });

    expect(result.success).toBe(false);
  });
});

const REAL_METRICS_BODY = JSON.parse(
  '{"requestsTotal":37,"failuresTotal":0,"durationMsSum":101272,"totalCharsSum":49626,"avgDurationMs":2737.08,"failureRate":0}'
);

describe('EmbeddingServiceMetricsSchema (#4446)', () => {
  it('accepts the real body', () => {
    const parsed = EmbeddingServiceMetricsSchema.parse(REAL_METRICS_BODY);

    expect(parsed.requestsTotal).toBe(37);
    expect(parsed.avgDurationMs).toBe(2737.08);
  });

  it('refuses the names the RAG pipeline used to read', () => {
    const result = EmbeddingServiceMetricsSchema.safeParse({
      totalRequests: 37,
      failedRequests: 0,
      avgLatencyMs: 2737.08,
      throughputPerSecond: 1.2,
    });

    expect(result.success).toBe(false);
  });
});
