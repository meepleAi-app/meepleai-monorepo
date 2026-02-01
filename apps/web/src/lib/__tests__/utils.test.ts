/**
 * Utils Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Utility function tests for cn() class name merger
 */

import { describe, it, expect } from 'vitest';

import { cn } from '../utils';

describe('Utils - Issue #3026', () => {
  describe('cn (class name merger)', () => {
    it('should merge class names correctly', () => {
      const result = cn('class1', 'class2');
      expect(result).toBe('class1 class2');
    });

    it('should handle conditional classes', () => {
      const isActive = true;
      const isDisabled = false;

      const result = cn('base', isActive && 'active', isDisabled && 'disabled');
      expect(result).toBe('base active');
    });

    it('should handle undefined and null values', () => {
      const result = cn('class1', undefined, null, 'class2');
      expect(result).toBe('class1 class2');
    });

    it('should handle object syntax', () => {
      const result = cn({
        'bg-red-500': true,
        'text-white': true,
        'hidden': false,
      });
      expect(result).toBe('bg-red-500 text-white');
    });

    it('should merge tailwind classes correctly', () => {
      // twMerge should handle conflicting Tailwind classes
      const result = cn('p-4', 'p-2');
      expect(result).toBe('p-2');
    });

    it('should merge tailwind color classes correctly', () => {
      const result = cn('bg-red-500', 'bg-blue-500');
      expect(result).toBe('bg-blue-500');
    });

    it('should handle empty string', () => {
      const result = cn('class1', '', 'class2');
      expect(result).toBe('class1 class2');
    });

    it('should handle array syntax', () => {
      const result = cn(['class1', 'class2']);
      expect(result).toBe('class1 class2');
    });

    it('should return empty string for no classes', () => {
      const result = cn();
      expect(result).toBe('');
    });

    it('should handle mixed syntax', () => {
      const result = cn(
        'base',
        ['flex', 'items-center'],
        { 'justify-between': true, 'hidden': false }
      );
      expect(result).toBe('base flex items-center justify-between');
    });

    it('should preserve non-conflicting classes', () => {
      const result = cn('p-4', 'm-2', 'text-lg');
      expect(result).toBe('p-4 m-2 text-lg');
    });

    it('should handle responsive prefixes correctly', () => {
      const result = cn('md:p-4', 'lg:p-6', 'p-2');
      expect(result).toBe('md:p-4 lg:p-6 p-2');
    });
  });
});
