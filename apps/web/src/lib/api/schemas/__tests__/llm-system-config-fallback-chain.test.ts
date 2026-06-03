import { describe, it, expect } from 'vitest';

import { parseFallbackChain } from '../llm-system-config.schemas';

describe('parseFallbackChain (#1834 PR2)', () => {
  it('returns empty array for null/undefined/empty string', () => {
    expect(parseFallbackChain(null)).toEqual([]);
    expect(parseFallbackChain(undefined)).toEqual([]);
    expect(parseFallbackChain('')).toEqual([]);
  });

  it('returns empty array for invalid JSON', () => {
    expect(parseFallbackChain('not-json')).toEqual([]);
  });

  it('returns empty array for non-array JSON', () => {
    expect(parseFallbackChain('{"provider":"x"}')).toEqual([]);
    expect(parseFallbackChain('42')).toEqual([]);
  });

  it('parses canonical shape with all fields', () => {
    const json = JSON.stringify([
      {
        provider: 'deepseek',
        model: 'deepseek-chat',
        priority: 'primary',
        failoverConditions: ['429', '5xx'],
      },
    ]);
    const result = parseFallbackChain(json);
    expect(result).toEqual([
      {
        provider: 'deepseek',
        model: 'deepseek-chat',
        priority: 'primary',
        failoverConditions: ['429', '5xx'],
      },
    ]);
  });

  it('tolerates alternate provider keys (name, providerName)', () => {
    const json = JSON.stringify([
      { name: 'deepseek', model: 'm', priority: 'primary' },
      { providerName: 'openrouter', defaultModel: 'm2', priority: 'secondary' },
    ]);
    const result = parseFallbackChain(json);
    expect(result.map(r => r.provider)).toEqual(['deepseek', 'openrouter']);
    expect(result.map(r => r.model)).toEqual(['m', 'm2']);
  });

  it('derives priority from index when missing', () => {
    const json = JSON.stringify([
      { provider: 'a', model: 'a' },
      { provider: 'b', model: 'b' },
      { provider: 'c', model: 'c' },
      { provider: 'd', model: 'd' },
    ]);
    const result = parseFallbackChain(json);
    expect(result.map(r => r.priority)).toEqual(['primary', 'secondary', 'tertiary', 'standby']);
  });

  it('falls back to standby for unknown priority strings', () => {
    const json = JSON.stringify([{ provider: 'a', model: 'a', priority: 'wildcard' }]);
    const result = parseFallbackChain(json);
    expect(result[0]?.priority).toBe('standby');
  });

  it('defaults failoverConditions for primary node to common HTTP errors', () => {
    const json = JSON.stringify([{ provider: 'a', model: 'a' }]);
    const result = parseFallbackChain(json);
    expect(result[0]?.failoverConditions).toEqual(['429', '5xx', 'timeout']);
  });

  it('defaults failoverConditions for fallback nodes to circuit-open', () => {
    const json = JSON.stringify([
      { provider: 'a', model: 'a' },
      { provider: 'b', model: 'b' },
    ]);
    const result = parseFallbackChain(json);
    expect(result[1]?.failoverConditions).toEqual(['circuit-open']);
  });

  it('skips entries missing required provider/model', () => {
    const json = JSON.stringify([
      { provider: 'a', model: 'a' },
      { priority: 'secondary' }, // no provider/model
      { provider: 'c' }, // no model
      { model: 'd' }, // no provider
      { provider: 'e', model: 'e' },
    ]);
    const result = parseFallbackChain(json);
    expect(result.map(r => r.provider)).toEqual(['a', 'e']);
  });

  it('skips non-object entries', () => {
    const json = JSON.stringify([
      { provider: 'a', model: 'a' },
      null,
      'string-entry',
      42,
      { provider: 'b', model: 'b' },
    ]);
    const result = parseFallbackChain(json);
    expect(result.map(r => r.provider)).toEqual(['a', 'b']);
  });

  it('accepts conditions array alias for failoverConditions', () => {
    const json = JSON.stringify([
      { provider: 'a', model: 'a', conditions: ['quota', 'rate-limit'] },
    ]);
    const result = parseFallbackChain(json);
    expect(result[0]?.failoverConditions).toEqual(['quota', 'rate-limit']);
  });
});
