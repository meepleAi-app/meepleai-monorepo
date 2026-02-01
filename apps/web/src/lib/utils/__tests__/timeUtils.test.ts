/**
 * Time Utils Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Time formatting utility function tests
 * - formatRelativeTime: Format dates as relative time strings
 * - formatMessageTime: Format dates as HH:mm time strings
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { formatRelativeTime, formatMessageTime } from '../timeUtils';

describe('TimeUtils - Issue #3026', () => {
  beforeEach(() => {
    // Set a fixed system time for consistent test results
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2024-06-15T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('formatRelativeTime', () => {
    it('should format recent date with suffix', () => {
      const recentDate = new Date('2024-06-15T11:58:00Z'); // 2 minutes ago
      const result = formatRelativeTime(recentDate);

      // Italian locale: "2 minuti fa" or similar
      expect(result).toContain('fa');
    });

    it('should format date without suffix when addSuffix is false', () => {
      const recentDate = new Date('2024-06-15T11:58:00Z');
      const result = formatRelativeTime(recentDate, false);

      // Should not contain "fa" (ago)
      expect(result).not.toContain('fa');
    });

    it('should format hours ago', () => {
      const hoursAgo = new Date('2024-06-15T09:00:00Z'); // 3 hours ago
      const result = formatRelativeTime(hoursAgo);

      // Should contain time indicator in Italian
      expect(result.length).toBeGreaterThan(0);
      expect(result).toContain('fa');
    });

    it('should format days ago', () => {
      const daysAgo = new Date('2024-06-13T12:00:00Z'); // 2 days ago
      const result = formatRelativeTime(daysAgo);

      // Should contain "giorni" (days) or similar time indicator
      expect(result.length).toBeGreaterThan(0);
      expect(result).toContain('fa');
    });

    it('should handle future dates', () => {
      const futureDate = new Date('2024-06-15T14:00:00Z'); // 2 hours in future
      const result = formatRelativeTime(futureDate);

      // Should indicate future time (in Italian: "tra")
      expect(result.length).toBeGreaterThan(0);
    });

    it('should handle exact now', () => {
      const now = new Date('2024-06-15T12:00:00Z');
      const result = formatRelativeTime(now);

      // Should return "meno di un minuto fa" or similar
      expect(result.length).toBeGreaterThan(0);
    });

    it('should handle very old dates', () => {
      const oldDate = new Date('2023-06-15T12:00:00Z'); // 1 year ago
      const result = formatRelativeTime(oldDate);

      // Should contain time indicator
      expect(result.length).toBeGreaterThan(0);
      expect(result).toContain('fa');
    });
  });

  describe('formatMessageTime', () => {
    it('should format time in HH:mm format', () => {
      const date = new Date('2024-06-15T14:30:00Z');
      const result = formatMessageTime(date);

      // Should return time in HH:mm format (depends on timezone)
      expect(result).toMatch(/^\d{2}:\d{2}$/);
    });

    it('should format morning time', () => {
      const morningDate = new Date('2024-06-15T08:15:00Z');
      const result = formatMessageTime(morningDate);

      // Should return formatted time
      expect(result).toMatch(/^\d{2}:\d{2}$/);
    });

    it('should format midnight correctly', () => {
      const midnight = new Date('2024-06-15T00:00:00Z');
      const result = formatMessageTime(midnight);

      // Should return formatted time
      expect(result).toMatch(/^\d{2}:\d{2}$/);
    });

    it('should format noon correctly', () => {
      const noon = new Date('2024-06-15T12:00:00Z');
      const result = formatMessageTime(noon);

      // Should return formatted time
      expect(result).toMatch(/^\d{2}:\d{2}$/);
    });

    it('should format end of day correctly', () => {
      const endOfDay = new Date('2024-06-15T23:59:00Z');
      const result = formatMessageTime(endOfDay);

      // Should return formatted time
      expect(result).toMatch(/^\d{2}:\d{2}$/);
    });

    it('should use 24-hour format (Italian locale)', () => {
      const afternoonDate = new Date('2024-06-15T15:30:00Z');
      const result = formatMessageTime(afternoonDate);

      // Italian locale uses 24-hour format
      expect(result).toMatch(/^\d{2}:\d{2}$/);
      // Should not have AM/PM
      expect(result).not.toMatch(/[AP]M/i);
    });
  });
});
