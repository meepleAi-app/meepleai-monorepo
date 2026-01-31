/**
 * Animation Components and Variants (Issue #2965 Wave 8)
 *
 * Centralized exports for all Framer Motion animations.
 * Provides reusable components and variants for common UI patterns.
 */

// Components
export { FadeIn } from './FadeIn';
export type { FadeInProps } from './FadeIn';

export { StaggerChildren } from './StaggerChildren';
export type { StaggerChildrenProps } from './StaggerChildren';

export { PageTransition } from './PageTransition';
export type { PageTransitionProps } from './PageTransition';

// Variants
export {
  modalBackdropVariants,
  modalContentVariants,
  drawerVariants,
  dropdownVariants,
  toastVariants,
} from './ModalAnimations';
