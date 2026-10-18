/**
 * Unit tests for formatFileSize — #1676 scope (a) F1.
 */

import { describe, expect, it } from 'vitest';

import { formatFileSize, formatSizeLimit } from '../file-size';

describe('formatFileSize', () => {
  it('returns "0 B" for zero bytes', () => {
    expect(formatFileSize(0)).toBe('0 B');
  });

  it('renders bytes (< 1 KB) as integer with B suffix', () => {
    expect(formatFileSize(873)).toBe('873 B');
    expect(formatFileSize(1)).toBe('1 B');
    expect(formatFileSize(1023)).toBe('1023 B');
  });

  it('renders KB with 1 decimal', () => {
    // 12.4 KB ≈ 12.4 * 1024 = 12_697 bytes
    expect(formatFileSize(12_697)).toBe('12.4 KB');
    // 1 KB exact boundary
    expect(formatFileSize(1024)).toBe('1.0 KB');
  });

  it('renders the mockup example 8.4 MB exactly', () => {
    // Mockup sp5-admin-kb.html L147 — "8.4 MB"
    // 8.4 MB ≈ 8.4 * 1024 * 1024 = 8_808_038 bytes
    expect(formatFileSize(8_808_038)).toBe('8.4 MB');
  });

  it('switches to GB above 1 GiB', () => {
    // 1.2 GB ≈ 1.2 * 1024^3
    expect(formatFileSize(1_288_490_188)).toBe('1.2 GB');
  });

  it('renders em-dash for non-finite input', () => {
    expect(formatFileSize(Number.NaN)).toBe('—');
    expect(formatFileSize(Number.POSITIVE_INFINITY)).toBe('—');
    expect(formatFileSize(Number.NEGATIVE_INFINITY)).toBe('—');
  });

  it('treats negative input defensively as "0 B"', () => {
    expect(formatFileSize(-1)).toBe('0 B');
    expect(formatFileSize(-1_000_000)).toBe('0 B');
  });
});

describe('formatSizeLimit (#4449)', () => {
  it('shows the single PDF limit as the backend messages do', () => {
    expect(formatSizeLimit(104_857_600)).toBe('100 MB');
  });

  it('shows a plan cap in whole megabytes', () => {
    expect(formatSizeLimit(50 * 1024 * 1024)).toBe('50 MB');
  });
});
