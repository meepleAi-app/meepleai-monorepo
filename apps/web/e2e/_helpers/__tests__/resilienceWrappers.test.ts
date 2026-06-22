import { describe, it, expect, vi } from 'vitest';
import { withRetry } from '../resilienceWrappers';

describe('resilienceWrappers (#1929 DEC-C-6)', () => {
  it('returns immediately on first-call success', async () => {
    const fn = vi.fn().mockResolvedValue('ok');
    const result = await withRetry(fn, { reason: 'test-success', backoffMs: 10 });
    expect(result).toBe('ok');
    expect(fn).toHaveBeenCalledTimes(1);
  });

  it('retries exactly once on first-call failure, returns success on second', async () => {
    const fn = vi
      .fn()
      .mockRejectedValueOnce(new Error('transient'))
      .mockResolvedValueOnce('recovered');
    const result = await withRetry(fn, { reason: 'test-retry', backoffMs: 10 });
    expect(result).toBe('recovered');
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it('throws loud aggregate error with both first + second error after two failures', async () => {
    const fn = vi
      .fn()
      .mockRejectedValueOnce(new Error('first-fail'))
      .mockRejectedValueOnce(new Error('second-fail'));
    await expect(withRetry(fn, { reason: 'test-loud', backoffMs: 10 })).rejects.toThrow(
      /test action failed twice.*reason: test-loud.*first.*first-fail.*second.*second-fail/i
    );
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it('respects backoffMs delay between attempts', async () => {
    const fn = vi.fn().mockRejectedValueOnce(new Error('e')).mockResolvedValueOnce('ok');
    const start = Date.now();
    await withRetry(fn, { reason: 'backoff', backoffMs: 100 });
    const elapsed = Date.now() - start;
    expect(elapsed).toBeGreaterThanOrEqual(100);
  });

  it('uses default 500ms backoff when not specified', async () => {
    const fn = vi.fn().mockRejectedValueOnce(new Error('e')).mockResolvedValueOnce('ok');
    const start = Date.now();
    await withRetry(fn, { reason: 'default-backoff' });
    const elapsed = Date.now() - start;
    expect(elapsed).toBeGreaterThanOrEqual(500);
  });
});
