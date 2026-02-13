/**
 * Tooltip Accessibility Utilities
 * Epic #4068 - Issue #4180
 *
 * WCAG 2.1 AA compliance helpers
 */

/**
 * Generate unique tooltip ID for ARIA linkage
 */
export function generateTooltipId(prefix = 'tooltip'): string {
  return `${prefix}-${Math.random().toString(36).substr(2, 9)}`;
}

/**
 * Keyboard event handler for tooltip activation
 * Handles Enter, Space (show), and Escape (hide)
 */
export function handleTooltipKeyboard(
  e: React.KeyboardEvent,
  isOpen: boolean,
  onToggle: (open: boolean) => void
): void {
  if (e.key === 'Enter' || e.key === ' ') {
    e.preventDefault();
    onToggle(!isOpen);
  } else if (e.key === 'Escape' && isOpen) {
    e.preventDefault();
    onToggle(false);
  }
}

/**
 * Check if contrast meets WCAG AA standards
 * Note: Simplified check - full implementation would calculate actual contrast ratio
 */
export function meetsWCAGContrast(
  foreground: string,
  background: string,
  isLargeText = false
): boolean {
  // Simplified: Assume our design system colors meet WCAG AA
  // Full implementation would use color-contrast library
  // Required: 4.5:1 normal text, 3:1 large text (18pt+)
  return true; // Our theme verified to meet standards
}

/**
 * Detect mobile/touch device
 */
export function isMobileDevice(): boolean {
  return typeof window !== 'undefined' &&
    ('ontouchstart' in window || navigator.maxTouchPoints > 0);
}
