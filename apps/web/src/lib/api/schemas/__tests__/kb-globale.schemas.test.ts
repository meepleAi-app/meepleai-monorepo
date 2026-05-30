/**
 * KB Globale Schemas Tests (Issue #1482 Task 0)
 *
 * Unit tests for Zod schemas covering POST /api/v1/knowledge-base/search/global
 * request/response validation.
 */

import { describe, it, expect } from 'vitest';
import {
  SearchModeSchema,
  GlobalKbSearchRequestSchema,
  GlobalKbSearchResultSchema,
  GlobalKbSearchResponseSchema,
  type GlobalKbSearchRequest,
  type GlobalKbSearchResponse,
} from '../kb-globale.schemas';

describe('KB Globale Schemas', () => {
  const validResult = {
    chunkId: 'doc-123_0',
    docId: '550e8400-e29b-41d4-a716-446655440000',
    docTitle: 'Azul Rulebook',
    gameId: '550e8400-e29b-41d4-a716-446655440001',
    gameName: 'Azul',
    docType: 'base',
    headingPath: 'Setup > Initial Tiles',
    snippet: 'Place the starting tiles on the board.',
    pageNumber: 3,
    score: 0.95,
  };

  const validEnvelope = {
    results: [validResult],
    hasMore: false,
    nextCursor: null,
  };

  // ──────────────────────────────────────────────────────────────────
  // GlobalKbSearchResponseSchema tests
  // ──────────────────────────────────────────────────────────────────

  it('parses a full valid response', () => {
    expect(() => GlobalKbSearchResponseSchema.parse(validEnvelope)).not.toThrow();
  });

  it('parses with hasMore + cursor', () => {
    const envelope = {
      results: [validResult],
      hasMore: true,
      nextCursor: 'opaque-cursor-string-abc123',
    };
    expect(() => GlobalKbSearchResponseSchema.parse(envelope)).not.toThrow();
  });

  it('result accepts headingPath=null and pageNumber=null', () => {
    const result = {
      ...validResult,
      headingPath: null,
      pageNumber: null,
    };
    const envelope = { results: [result], hasMore: false, nextCursor: null };
    expect(() => GlobalKbSearchResponseSchema.parse(envelope)).not.toThrow();
  });

  it('rejects unknown fields in envelope', () => {
    const invalid = { ...validEnvelope, totalCount: 0 };
    expect(() => GlobalKbSearchResponseSchema.parse(invalid)).toThrow();
  });

  it('rejects malformed docId UUID', () => {
    const result = { ...validResult, docId: 'not-a-uuid' };
    const envelope = { results: [result], hasMore: false, nextCursor: null };
    expect(() => GlobalKbSearchResponseSchema.parse(envelope)).toThrow();
  });

  it('rejects malformed gameId UUID', () => {
    const result = { ...validResult, gameId: 'invalid-uuid' };
    const envelope = { results: [result], hasMore: false, nextCursor: null };
    expect(() => GlobalKbSearchResponseSchema.parse(envelope)).toThrow();
  });

  it('accepts empty snippet', () => {
    const result = { ...validResult, snippet: '' };
    const envelope = { results: [result], hasMore: false, nextCursor: null };
    expect(() => GlobalKbSearchResponseSchema.parse(envelope)).not.toThrow();
  });

  // ──────────────────────────────────────────────────────────────────
  // GlobalKbSearchRequestSchema tests
  // ──────────────────────────────────────────────────────────────────

  it('request: rejects query=""', () => {
    const req = { query: '' };
    expect(() => GlobalKbSearchRequestSchema.parse(req)).toThrow();
  });

  it('request: accepts minimal { query: "azul" }', () => {
    const req = { query: 'azul' };
    expect(() => GlobalKbSearchRequestSchema.parse(req)).not.toThrow();
  });

  it('request: accepts all optional fields', () => {
    const req = {
      query: 'azul rules',
      limit: 50,
      cursor: 'next-page-cursor',
      mode: 'Semantic' as const,
    };
    expect(() => GlobalKbSearchRequestSchema.parse(req)).not.toThrow();
  });

  // ──────────────────────────────────────────────────────────────────
  // SearchModeSchema tests
  // ──────────────────────────────────────────────────────────────────

  it("SearchModeSchema: accepts 'Semantic'", () => {
    expect(() => SearchModeSchema.parse('Semantic')).not.toThrow();
  });

  it("SearchModeSchema: rejects 'Keyword'", () => {
    expect(() => SearchModeSchema.parse('Keyword')).toThrow();
  });

  // ──────────────────────────────────────────────────────────────────
  // Type-level inference tests
  // ──────────────────────────────────────────────────────────────────

  it('type-level: GlobalKbSearchResponse infers correct shape', () => {
    const response: GlobalKbSearchResponse = validEnvelope;
    expect(response.results[0].chunkId).toBe('doc-123_0');
    expect(response.hasMore).toBe(false);
  });

  it('type-level: GlobalKbSearchRequest infers correct shape', () => {
    const request: GlobalKbSearchRequest = { query: 'test' };
    expect(request.query).toBe('test');
  });
});
