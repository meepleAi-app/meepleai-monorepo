/**
 * Accessible Components Library (UI-05)
 *
 * A collection of fully accessible React components following WCAG 2.1 AA standards.
 *
 * All components include:
 * - Proper ARIA attributes
 * - Keyboard navigation support
 * - Focus management
 * - Screen reader announcements
 * - High contrast support
 *
 * @example
 * ```tsx
 * import {
 *   AccessibleButton,
 *   AccessibleModal,
 *   AccessibleFormInput,
 *   AccessibleSkipLink
 * } from '@/components/accessible';
 * ```
 */

export { AccessibleButton } from './AccessibleButton';
export type { AccessibleButtonProps } from './AccessibleButton';

export { AccessibleModal } from './AccessibleModal';
export type { AccessibleModalProps } from './AccessibleModal';

export { AccessibleFormInput } from './AccessibleFormInput';
export type { AccessibleFormInputProps } from './AccessibleFormInput';

export { AccessibleSkipLink } from './AccessibleSkipLink';
export type { AccessibleSkipLinkProps } from './AccessibleSkipLink';
