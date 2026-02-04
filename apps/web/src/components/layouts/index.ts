/**
 * Public Layout Components - Issue #2230
 * Updated: Issue #3104 - Removed PublicHeader (replaced by UnifiedHeader)
 *
 * Esportazioni per i componenti del layout pubblico.
 */

export { PublicFooter } from './PublicFooter';
export type { PublicFooterProps } from './PublicFooter';

export { PublicLayout } from './PublicLayout';
export type { PublicLayoutProps } from './PublicLayout';

export { PublicLayoutWrapper } from './PublicLayoutWrapper';
export type { PublicLayoutWrapperProps } from './PublicLayoutWrapper';

/**
 * Auth Layout Components - Issue #2231
 *
 * Specialized layout for authentication pages.
 */

export { AuthLayout } from './AuthLayout';
export type { AuthLayoutProps } from './AuthLayout';

/**
 * Authenticated Layout Components - Issue #3479
 *
 * Layout System v2 for authenticated pages.
 * Combines UnifiedHeader (desktop nav) + UnifiedActionBar (mobile nav with integrated FAB).
 */

export { AuthenticatedLayout } from './AuthenticatedLayout';
export type { AuthenticatedLayoutProps } from './AuthenticatedLayout';
