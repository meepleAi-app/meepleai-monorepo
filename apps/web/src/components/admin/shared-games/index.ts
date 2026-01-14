/**
 * Shared Game Catalog UI Components - Issue #2372
 *
 * Reusable components for the SharedGameCatalog admin interface.
 * All components follow shadcn/ui patterns and support size variants.
 */

// Status Badge
export { GameStatusBadge, getStatusLabel, getStatusColorClass } from './GameStatusBadge';
export type { GameStatusBadgeProps } from './GameStatusBadge';

// Info Badges
export {
  PlayersBadge,
  PlayTimeBadge,
  ComplexityBadge,
  RatingBadge,
  AgeBadge,
} from './GameInfoBadges';
export type {
  PlayersBadgeProps,
  PlayTimeBadgeProps,
  ComplexityBadgeProps,
  RatingBadgeProps,
  AgeBadgeProps,
} from './GameInfoBadges';

// Category Pills
export { CategoryPill, CategoryPillList } from './CategoryPill';
export type { CategoryPillProps, CategoryPillListProps } from './CategoryPill';

// Mechanic Pills
export { MechanicPill, MechanicPillList } from './MechanicPill';
export type { MechanicPillProps, MechanicPillListProps } from './MechanicPill';

// Game Form
export { GameForm } from './GameForm';
export type { GameFormProps } from './GameForm';

// Document Management (Issue #2391 Sprint 1)
export { VersionBadge } from './VersionBadge';
export type { VersionBadgeProps } from './VersionBadge';
export { TagInput } from './TagInput';
export type { TagInputProps } from './TagInput';
export { PdfDocumentList } from './PdfDocumentList';
export type { PdfDocumentListProps } from './PdfDocumentList';
