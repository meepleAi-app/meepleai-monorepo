/**
 * Tooltip Positioning Algorithm
 * Epic #4068 - Issue #4186
 *
 * Smart positioning with viewport boundary detection and auto-flip
 */

export interface TooltipPosition {
  top?: number;
  bottom?: number;
  left?: number;
  right?: number;
  placement: 'top' | 'bottom' | 'left' | 'right';
}

export interface TooltipSize {
  width: number;
  height: number;
}

export interface ViewportSize {
  width: number;
  height: number;
}

const GAP = 8; // px spacing between trigger and tooltip

/**
 * Calculate optimal tooltip position with viewport boundary detection
 * and automatic flip when space insufficient
 *
 * Algorithm:
 * 1. Calculate available space in 4 directions
 * 2. Prefer vertical placement (top/bottom) over horizontal
 * 3. Auto-flip if insufficient space
 * 4. Clamp to viewport bounds
 *
 * @param triggerRect - Bounding rectangle of trigger element
 * @param tooltipSize - Width and height of tooltip content
 * @param viewportSize - Current viewport dimensions (defaults to window size)
 * @returns Position object with placement and coordinates
 */
export function calculateOptimalPosition(
  triggerRect: DOMRect,
  tooltipSize: TooltipSize,
  viewportSize: ViewportSize = {
    width: typeof window !== 'undefined' ? window.innerWidth : 1920,
    height: typeof window !== 'undefined' ? window.innerHeight : 1080
  }
): TooltipPosition {
  // Step 1: Calculate available space in each direction
  const spaceAbove = triggerRect.top;
  const spaceBelow = viewportSize.height - triggerRect.bottom;
  const spaceLeft = triggerRect.left;
  const spaceRight = viewportSize.width - triggerRect.right;

  // Step 2: Check if tooltip fits in each direction
  const fitsAbove = spaceAbove >= tooltipSize.height + GAP;
  const fitsBelow = spaceBelow >= tooltipSize.height + GAP;
  const fitsLeft = spaceLeft >= tooltipSize.width + GAP;
  const fitsRight = spaceRight >= tooltipSize.width + GAP;

  // Step 3: Choose optimal placement (prefer vertical)
  let placement: TooltipPosition['placement'];
  let position: Partial<TooltipPosition> = {};

  // Prefer below if space available and more space than above
  if (fitsBelow && spaceBelow >= spaceAbove) {
    placement = 'bottom';
    position.top = triggerRect.bottom + GAP;
    position.left = triggerRect.left + triggerRect.width / 2 - tooltipSize.width / 2;
  }
  // Flip to above if more space or below doesn't fit
  else if (fitsAbove) {
    placement = 'top';
    position.bottom = viewportSize.height - triggerRect.top + GAP;
    position.left = triggerRect.left + triggerRect.width / 2 - tooltipSize.width / 2;
  }
  // Try right if vertical doesn't work
  else if (fitsRight && spaceRight >= spaceLeft) {
    placement = 'right';
    position.left = triggerRect.right + GAP;
    position.top = triggerRect.top + triggerRect.height / 2 - tooltipSize.height / 2;
  }
  // Try left as last resort
  else if (fitsLeft) {
    placement = 'left';
    position.right = viewportSize.width - triggerRect.left + GAP;
    position.top = triggerRect.top + triggerRect.height / 2 - tooltipSize.height / 2;
  }
  // Fallback: place below with best effort (no space anywhere)
  else {
    placement = 'bottom';
    position.top = triggerRect.bottom + GAP;
    position.left = triggerRect.left;
  }

  // Step 4: Clamp to viewport bounds
  if (position.left !== undefined) {
    position.left = Math.max(
      GAP,
      Math.min(position.left, viewportSize.width - tooltipSize.width - GAP)
    );
  }

  if (position.top !== undefined) {
    position.top = Math.max(
      GAP,
      Math.min(position.top, viewportSize.height - tooltipSize.height - GAP)
    );
  }

  return { ...position, placement } as TooltipPosition;
}

/**
 * Debounce utility for scroll/resize handlers
 */
export function debounce<T extends (...args: any[]) => any>(
  fn: T,
  delay: number
): (...args: Parameters<T>) => void {
  let timeoutId: NodeJS.Timeout;

  return (...args: Parameters<T>) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn(...args), delay);
  };
}
