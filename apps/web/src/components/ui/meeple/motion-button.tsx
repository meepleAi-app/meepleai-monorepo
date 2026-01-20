'use client';

/**
 * MotionButton Component
 *
 * A reusable button component with framer-motion animations.
 * Wraps the shadcn/ui Button component with consistent hover and tap animations.
 *
 * Issue #1437: UX-002 - Extract MotionButton Component
 *
 * Features:
 * - Consistent scale animations (hover: 1.05, tap: 0.95)
 * - Full Button prop support (variant, size, className, etc.)
 * - asChild composition support for polymorphic usage
 * - Accessibility-friendly with proper display styles
 *
 * @example
 * // Basic usage
 * <MotionButton onClick={handleClick}>Click me</MotionButton>
 *
 * @example
 * // With variant
 * <MotionButton variant="outline">Outlined</MotionButton>
 *
 * @example
 * // With asChild for link composition
 * <MotionButton asChild>
 *   <a href="/path">Link Button</a>
 * </MotionButton>
 */

import * as React from 'react';

import { motion } from 'framer-motion';

import { Button, type ButtonProps } from '@/components/ui/primitives/button';

export interface MotionButtonProps extends ButtonProps {
  /**
   * Optional custom whileHover animation
   * @default {{ scale: 1.05 }}
   */
  whileHover?: React.ComponentProps<typeof motion.div>['whileHover'];

  /**
   * Optional custom whileTap animation
   * @default {{ scale: 0.95 }}
   */
  whileTap?: React.ComponentProps<typeof motion.div>['whileTap'];
}

const MotionButton = React.forwardRef<HTMLButtonElement, MotionButtonProps>(
  ({ children, whileHover = { scale: 1.05 }, whileTap = { scale: 0.95 }, ...buttonProps }, ref) => {
    return (
      <motion.div whileHover={whileHover} whileTap={whileTap} style={{ display: 'inline-block' }}>
        <Button ref={ref} {...buttonProps}>
          {children}
        </Button>
      </motion.div>
    );
  }
);

MotionButton.displayName = 'MotionButton';

export { MotionButton };
