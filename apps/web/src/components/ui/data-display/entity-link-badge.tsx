/**
 * EntityLinkBadge
 *
 * Renders a compact pill/badge for an entity link relationship.
 * Includes a dedicated handler for `KbCard` links (PDF documents)
 * and a generic fallback for all other link types.
 *
 * Used in GameDetailDrawer's "Documenti KB" section header.
 * Issue #5194: EntityLinkBadge with kb_card handler.
 *
 * @example
 * ```tsx
 * // KB Card link (specific handler)
 * <EntityLinkBadge
 *   linkType="PartOf"
 *   sourceEntityType="KbCard"
 *   count={3}
 * />
 *
 * // Generic link (fallback handler)
 * <EntityLinkBadge linkType="RelatedTo" />
 * ```
 */

'use client';

import { memo } from 'react';

import { FileText, Link2, BookOpen } from 'lucide-react';

import type { EntityLinkType, MeepleEntityType } from '@/lib/api/schemas/entity-link.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface EntityLinkBadgeProps {
  /** The link relationship type */
  linkType: EntityLinkType;
  /** Source entity type — drives the specific handler (e.g. KbCard) */
  sourceEntityType?: MeepleEntityType;
  /** Target entity type */
  targetEntityType?: MeepleEntityType;
  /** Optional count to display alongside the label */
  count?: number;
  /** Optional custom label override */
  label?: string;
  /** Visual size */
  size?: 'sm' | 'md';
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

/** Human-readable label per link type (Italian locale, matching rest of UI) */
const LINK_TYPE_LABELS: Record<EntityLinkType, string> = {
  ExpansionOf: 'Espansione',
  SequelOf: 'Seguito',
  Reimplements: 'Reimplementa',
  CompanionTo: 'Companion',
  RelatedTo: 'Correlato',
  CollaboratesWith: 'Collabora',
  PartOf: 'Parte di',
  SpecializedBy: 'Specializzato da',
};

// ============================================================================
// Component
// ============================================================================

export const EntityLinkBadge = memo(function EntityLinkBadge({
  linkType,
  sourceEntityType,
  count,
  label,
  size = 'sm',
  className,
}: EntityLinkBadgeProps) {
  const isKbCard = sourceEntityType === 'KbCard';

  // KB Card handler: specific icon + label
  if (isKbCard) {
    return (
      <span
        className={cn(
          'inline-flex items-center gap-1.5 rounded-md border font-semibold',
          size === 'sm' ? 'px-2 py-0.5 text-[10px]' : 'px-2.5 py-1 text-xs',
          'bg-teal-50 border-teal-200 text-teal-700',
          'dark:bg-teal-950/40 dark:border-teal-800 dark:text-teal-300',
          className
        )}
        data-testid="entity-link-badge-kb"
        aria-label={`Documenti KB: ${count ?? 0} collegate`}
      >
        <FileText aria-hidden="true" className={cn(size === 'sm' ? 'w-3 h-3' : 'w-3.5 h-3.5')} />
        {label ?? 'Documenti KB'}
        {count !== undefined && <span className="ml-0.5 font-bold">{count}</span>}
      </span>
    );
  }

  // Generic handler: link icon + link type label
  const genericLabel = label ?? LINK_TYPE_LABELS[linkType] ?? linkType;
  const Icon = linkType === 'PartOf' ? BookOpen : Link2;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-md border font-semibold',
        size === 'sm' ? 'px-2 py-0.5 text-[10px]' : 'px-2.5 py-1 text-xs',
        'bg-muted/50 border-border/60 text-muted-foreground',
        className
      )}
      data-testid="entity-link-badge-generic"
      aria-label={`${genericLabel}${count !== undefined ? `: ${count}` : ''}`}
    >
      <Icon aria-hidden="true" className={cn(size === 'sm' ? 'w-3 h-3' : 'w-3.5 h-3.5')} />
      {genericLabel}
      {count !== undefined && <span className="ml-0.5 font-bold">{count}</span>}
    </span>
  );
});
