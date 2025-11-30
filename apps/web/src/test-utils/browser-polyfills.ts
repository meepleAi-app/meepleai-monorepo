/**
 * Comprehensive browser API polyfills for jsdom test environment.
 *
 * jsdom (the DOM implementation used by Vitest) doesn't include all browser APIs.
 * This module provides polyfills for missing APIs to prevent "X is not a function" errors.
 *
 * Usage:
 * Import and call setupBrowserPolyfills() in vitest.setup.ts before running tests.
 *
 * @module test-utils/browser-polyfills
 */

import { vi } from 'vitest';

/**
 * Sets up all browser API polyfills for the test environment.
 * Call this function once in vitest.setup.ts to ensure all browser APIs are available.
 *
 * Currently includes:
 * - window.matchMedia (for responsive behavior and media queries)
 * - Element.prototype.scrollIntoView (for auto-scroll features)
 *
 * Future additions as needed:
 * - window.ResizeObserver (if not already mocked)
 * - window.IntersectionObserver (if not already mocked)
 *
 * @example
 * // In vitest.setup.ts
 * import { setupBrowserPolyfills } from './src/test-utils/browser-polyfills';
 * setupBrowserPolyfills();
 */
export function setupBrowserPolyfills() {
  setupMatchMedia();
  setupScrollIntoView();
  setupURLMethods();
}

/**
 * Polyfills window.matchMedia for testing responsive behavior.
 *
 * Used by components that check media queries (e.g., useReducedMotion hook in SkeletonLoader).
 *
 * Default behavior:
 * - matches: false (no media query matches, e.g., no reduced motion preference)
 * - Includes both modern (addEventListener) and legacy (addListener) methods
 *
 * @example
 * // Component using matchMedia
 * const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
 *
 * // In tests, matches will be false by default
 * // To test different behavior, override in specific tests:
 * window.matchMedia = vi.fn().mockImplementation(query => ({
 *   matches: true, // Override for this test
 *   media: query,
 *   // ... other properties
 * }));
 */
function setupMatchMedia() {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation(query => ({
      matches: false, // Default: no media query matches
      media: query,
      onchange: null,
      addListener: vi.fn(), // Deprecated, but included for compatibility
      removeListener: vi.fn(), // Deprecated, but included for compatibility
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

/**
 * Polyfills Element.prototype.scrollIntoView for testing scroll behavior.
 *
 * Used by components that auto-scroll to elements (e.g., ChatPage scrolling to latest message).
 *
 * Implementation:
 * - Simple mock that does nothing (scroll behavior not testable in jsdom)
 * - Prevents "scrollIntoView is not a function" errors
 * - Can be spied on to verify scroll was called: expect(element.scrollIntoView).toHaveBeenCalled()
 *
 * @example
 * // Component using scrollIntoView
 * messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
 *
 * // In tests, verify it was called
 * const scrollSpy = vi.spyOn(Element.prototype, 'scrollIntoView');
 * // ... render component, trigger scroll
 * expect(scrollSpy).toHaveBeenCalled();
 */
function setupScrollIntoView() {
  if (typeof Element.prototype.scrollIntoView === 'undefined') {
    Element.prototype.scrollIntoView = vi.fn();
  }
}

/**
 * Polyfills URL.createObjectURL and URL.revokeObjectURL for file download testing.
 *
 * Used by downloadFile utility and components that create download links.
 *
 * Implementation:
 * - createObjectURL: Returns a fake blob URL
 * - revokeObjectURL: Mock function (no-op)
 *
 * @example
 * // Component using createObjectURL
 * const url = URL.createObjectURL(blob);
 * link.href = url;
 *
 * // In tests, verify it was called
 * const createSpy = vi.spyOn(URL, 'createObjectURL');
 * expect(createSpy).toHaveBeenCalledWith(blob);
 */
function setupURLMethods() {
  if (typeof URL.createObjectURL === 'undefined') {
    URL.createObjectURL = vi.fn(() => 'blob:http://localhost/mock-object-url');
  }
  if (typeof URL.revokeObjectURL === 'undefined') {
    URL.revokeObjectURL = vi.fn();
  }
}

/**
 * Future: Add ResizeObserver polyfill if needed.
 *
 * Note: Currently handled in vitest.setup.ts as a global class.
 * Consider moving here for consistency if refactoring.
 */
// export function setupResizeObserver() { ... }

/**
 * Future: Add IntersectionObserver polyfill if needed.
 *
 * Note: Currently handled in vitest.setup.ts as a global class.
 * Consider moving here for consistency if refactoring.
 */
// export function setupIntersectionObserver() { ... }
