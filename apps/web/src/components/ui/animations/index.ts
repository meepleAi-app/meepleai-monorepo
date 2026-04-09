/**
 * Animation Components and Variants (Issue #2965 Wave 8)
 *
 * Centralized exports for all Framer Motion animations.
 * Provides reusable components and variants for common UI patterns.
 *
 * Note: PageTransition was removed via Path D (YAGNI) in #293 cycle.
 * See docs/development/page-transition-decision.md and issue #293 for rationale.
 */

// Components
export { FadeIn } from './FadeIn';
export type { FadeInProps } from './FadeIn';

export { StaggerChildren } from './StaggerChildren';
export type { StaggerChildrenProps } from './StaggerChildren';

// Variants
export {
  modalBackdropVariants,
  modalContentVariants,
  drawerVariants,
  dropdownVariants,
  toastVariants,
} from './ModalAnimations';
