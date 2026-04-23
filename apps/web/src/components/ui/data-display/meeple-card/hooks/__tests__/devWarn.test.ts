import { describe, it, expect, vi, beforeEach } from 'vitest';
import { devWarnOnce, __resetDevWarnDedup } from '../devWarn';

describe('devWarnOnce', () => {
  beforeEach(() => {
    __resetDevWarnDedup();
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
  });

  it('emits a warning exactly once for identical messages', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    devWarnOnce('duplicate message');
    devWarnOnce('duplicate message');
    devWarnOnce('duplicate message');
    expect(warn).toHaveBeenCalledTimes(1);
  });

  it('emits distinct messages independently', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    devWarnOnce('a');
    devWarnOnce('b');
    expect(warn).toHaveBeenCalledTimes(2);
  });

  it('is silent in production', () => {
    vi.stubEnv('NODE_ENV', 'production');
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    devWarnOnce('should not appear');
    expect(warn).not.toHaveBeenCalled();
  });

  it('__resetDevWarnDedup allows re-emission after reset', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    devWarnOnce('msg');
    __resetDevWarnDedup();
    devWarnOnce('msg');
    expect(warn).toHaveBeenCalledTimes(2);
  });
});
