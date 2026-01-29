/**
 * Priority Utilities Tests (Issue #2895)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { calculatePriority, getPriorityBadgeVariant, getPriorityLabel } from '../priority';

describe('calculatePriority', () => {
  beforeEach(() => {
    // Mock current date to 2026-01-29 12:00:00 UTC
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-01-29T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should return "low" for items submitted today', () => {
    const createdAt = '2026-01-29T10:00:00Z'; // 2 hours ago
    expect(calculatePriority(createdAt)).toBe('low');
  });

  it('should return "low" for items submitted 1 day ago', () => {
    const createdAt = '2026-01-28T12:00:00Z'; // 1 day ago
    expect(calculatePriority(createdAt)).toBe('low');
  });

  it('should return "low" for items submitted 2 days ago', () => {
    const createdAt = '2026-01-27T12:00:00Z'; // 2 days ago
    expect(calculatePriority(createdAt)).toBe('low');
  });

  it('should return "medium" for items submitted exactly 3 days ago', () => {
    const createdAt = '2026-01-26T12:00:00Z'; // 3 days ago
    expect(calculatePriority(createdAt)).toBe('medium');
  });

  it('should return "medium" for items submitted 5 days ago', () => {
    const createdAt = '2026-01-24T12:00:00Z'; // 5 days ago
    expect(calculatePriority(createdAt)).toBe('medium');
  });

  it('should return "medium" for items submitted 7 days ago', () => {
    const createdAt = '2026-01-22T12:00:00Z'; // 7 days ago
    expect(calculatePriority(createdAt)).toBe('medium');
  });

  it('should return "high" for items submitted 8 days ago', () => {
    const createdAt = '2026-01-21T12:00:00Z'; // 8 days ago
    expect(calculatePriority(createdAt)).toBe('high');
  });

  it('should return "high" for items submitted 14 days ago', () => {
    const createdAt = '2026-01-15T12:00:00Z'; // 14 days ago
    expect(calculatePriority(createdAt)).toBe('high');
  });

  it('should return "high" for items submitted 30 days ago', () => {
    const createdAt = '2025-12-30T12:00:00Z'; // 30 days ago
    expect(calculatePriority(createdAt)).toBe('high');
  });

  it('should handle different time zones correctly', () => {
    const createdAtUTC = '2026-01-21T00:00:00Z'; // 8+ days ago in UTC
    expect(calculatePriority(createdAtUTC)).toBe('high');
  });

  it('should handle ISO datetime with milliseconds', () => {
    const createdAt = '2026-01-26T12:00:00.123Z'; // 3 days ago
    expect(calculatePriority(createdAt)).toBe('medium');
  });
});

describe('getPriorityBadgeVariant', () => {
  it('should return "destructive" for high priority', () => {
    expect(getPriorityBadgeVariant('high')).toBe('destructive');
  });

  it('should return "secondary" for medium priority', () => {
    expect(getPriorityBadgeVariant('medium')).toBe('secondary');
  });

  it('should return "default" for low priority', () => {
    expect(getPriorityBadgeVariant('low')).toBe('default');
  });
});

describe('getPriorityLabel', () => {
  it('should return Italian label for high priority', () => {
    expect(getPriorityLabel('high')).toBe('Alta priorità');
  });

  it('should return Italian label for medium priority', () => {
    expect(getPriorityLabel('medium')).toBe('Media priorità');
  });

  it('should return Italian label for low priority', () => {
    expect(getPriorityLabel('low')).toBe('Priorità normale');
  });
});

describe('Priority Integration Tests', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-01-29T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should correctly map priority to badge variant', () => {
    const highPriorityDate = '2026-01-21T12:00:00Z'; // 8 days ago
    const priority = calculatePriority(highPriorityDate);
    const variant = getPriorityBadgeVariant(priority);

    expect(priority).toBe('high');
    expect(variant).toBe('destructive');
  });

  it('should correctly map medium priority to secondary variant', () => {
    const mediumPriorityDate = '2026-01-26T12:00:00Z'; // 3 days ago
    const priority = calculatePriority(mediumPriorityDate);
    const variant = getPriorityBadgeVariant(priority);

    expect(priority).toBe('medium');
    expect(variant).toBe('secondary');
  });

  it('should correctly map low priority to default variant', () => {
    const lowPriorityDate = '2026-01-28T12:00:00Z'; // 1 day ago
    const priority = calculatePriority(lowPriorityDate);
    const variant = getPriorityBadgeVariant(priority);

    expect(priority).toBe('low');
    expect(variant).toBe('default');
  });
});
