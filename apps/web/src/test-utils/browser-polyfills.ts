/**
 * Comprehensive browser API polyfills for jsdom test environment.
 *
 * jsdom (the DOM implementation used by Jest) doesn't include all browser APIs.
 * This module provides polyfills for missing APIs to prevent "X is not a function" errors.
 *
 * Usage:
 * Import and call setupBrowserPolyfills() in jest.setup.js before running tests.
 *
 * @module test-utils/browser-polyfills
 */

/**
 * Sets up all browser API polyfills for the test environment.
 * Call this function once in jest.setup.js to ensure all browser APIs are available.
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
 * // In jest.setup.js
 * import { setupBrowserPolyfills } from './src/test-utils/browser-polyfills';
 * setupBrowserPolyfills();
 */
export function setupBrowserPolyfills() {
  setupMatchMedia();
  setupScrollIntoView();
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
 * window.matchMedia = jest.fn().mockImplementation(query => ({
 *   matches: true, // Override for this test
 *   media: query,
 *   // ... other properties
 * }));
 */
function setupMatchMedia() {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: jest.fn().mockImplementation(query => ({
      matches: false, // Default: no media query matches
      media: query,
      onchange: null,
      addListener: jest.fn(), // Deprecated, but included for compatibility
      removeListener: jest.fn(), // Deprecated, but included for compatibility
      addEventListener: jest.fn(),
      removeEventListener: jest.fn(),
      dispatchEvent: jest.fn(),
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
 * const scrollSpy = jest.spyOn(Element.prototype, 'scrollIntoView');
 * // ... render component, trigger scroll
 * expect(scrollSpy).toHaveBeenCalled();
 */
function setupScrollIntoView() {
  if (typeof Element.prototype.scrollIntoView === 'undefined') {
    Element.prototype.scrollIntoView = jest.fn();
  }
}

/**
 * Future: Add ResizeObserver polyfill if needed.
 *
 * Note: Currently handled in jest.setup.js as a global class.
 * Consider moving here for consistency if refactoring.
 */
// export function setupResizeObserver() { ... }

/**
 * Future: Add IntersectionObserver polyfill if needed.
 *
 * Note: Currently handled in jest.setup.js as a global class.
 * Consider moving here for consistency if refactoring.
 */
// export function setupIntersectionObserver() { ... }
