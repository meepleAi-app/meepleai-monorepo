'use client';

/**
 * Modal Animation Variants (Issue #2965 Wave 8)
 *
 * Pre-configured animation variants for modals, dialogs, and overlays.
 * Optimized for performance and accessibility.
 *
 * Usage:
 * Import variants and apply to Framer Motion components:
 * <motion.div variants={modalBackdropVariants} initial="hidden" animate="visible" exit="exit">
 */

import { Variants } from 'framer-motion';

/**
 * Backdrop fade animation for modal overlays
 */
export const modalBackdropVariants: Variants = {
  hidden: {
    opacity: 0,
  },
  visible: {
    opacity: 1,
    transition: {
      duration: 0.2,
      ease: 'easeOut',
    },
  },
  exit: {
    opacity: 0,
    transition: {
      duration: 0.15,
      ease: 'easeIn',
    },
  },
};

/**
 * Modal content animation - scale from center
 */
export const modalContentVariants: Variants = {
  hidden: {
    opacity: 0,
    scale: 0.95,
    y: 20,
  },
  visible: {
    opacity: 1,
    scale: 1,
    y: 0,
    transition: {
      duration: 0.3,
      ease: [0.4, 0, 0.2, 1], // Custom ease-out
      delay: 0.05, // Slight delay after backdrop
    },
  },
  exit: {
    opacity: 0,
    scale: 0.95,
    y: 10,
    transition: {
      duration: 0.2,
      ease: 'easeIn',
    },
  },
};

/**
 * Drawer/Sheet animation - slide from side
 */
export const drawerVariants = (side: 'left' | 'right' | 'top' | 'bottom' = 'right'): Variants => {
  const axis = side === 'left' || side === 'right' ? 'x' : 'y';
  const direction = side === 'left' || side === 'top' ? -1 : 1;
  const distance = side === 'left' || side === 'right' ? 320 : 400;

  return {
    hidden: {
      [axis]: direction * distance,
      opacity: 0.8,
    },
    visible: {
      [axis]: 0,
      opacity: 1,
      transition: {
        type: 'spring',
        damping: 25,
        stiffness: 200,
      },
    },
    exit: {
      [axis]: direction * distance,
      opacity: 0.8,
      transition: {
        duration: 0.2,
        ease: 'easeIn',
      },
    },
  };
};

/**
 * Dropdown/Popover animation - scale from anchor
 */
export const dropdownVariants: Variants = {
  hidden: {
    opacity: 0,
    scale: 0.95,
    y: -10,
  },
  visible: {
    opacity: 1,
    scale: 1,
    y: 0,
    transition: {
      duration: 0.15,
      ease: 'easeOut',
    },
  },
  exit: {
    opacity: 0,
    scale: 0.95,
    y: -5,
    transition: {
      duration: 0.1,
      ease: 'easeIn',
    },
  },
};

/**
 * Toast/Notification animation - slide from edge
 */
export const toastVariants = (position: 'top' | 'bottom' = 'bottom'): Variants => {
  const yStart = position === 'top' ? -100 : 100;

  return {
    hidden: {
      opacity: 0,
      y: yStart,
      scale: 0.95,
    },
    visible: {
      opacity: 1,
      y: 0,
      scale: 1,
      transition: {
        type: 'spring',
        damping: 20,
        stiffness: 300,
      },
    },
    exit: {
      opacity: 0,
      scale: 0.9,
      transition: {
        duration: 0.2,
        ease: 'easeIn',
      },
    },
  };
};
