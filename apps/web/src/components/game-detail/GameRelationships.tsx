'use client';

/**
 * GameRelationships — Read-only grouped game relationships display
 *
 * Shows entity links for a game, grouped by link type with Italian labels.
 * Returns null when no links exist (relationships are supplementary info).
 * Uses EntityLinkCard for individual link display.
 *
 * Issue #US-43 — Entity Relationships frontend integration
 */

import React from 'react';

import { Link as LinkIcon, Loader2 } from 'lucide-react';

import { EntityLinkCard } from '@/components/ui/data-display/entity-link/entity-link-card';
import type {
  EntityLinkDto,
  EntityLinkType,
  LinkEntityType,
} from '@/components/ui/data-display/entity-link/entity-link-types';
import { useEntityLinks } from '@/components/ui/data-display/entity-link/use-entity-links';
import { cn } from '@/lib/utils';

// ============================================================================
// Italian link type labels
// ============================================================================

const LINK_TYPE_LABELS: Record<EntityLinkType, string> = {
  ExpansionOf: 'Espansioni',
  SequelOf: 'Sequel',
  Reimplements: 'Reimplementazioni',
  CompanionTo: 'Giochi Companion',
  RelatedTo: 'Giochi Correlati',
  PartOf: 'Parte di',
  CollaboratesWith: 'Collabora con',
  SpecializedBy: 'Specializzazioni',
};

/**
 * Ordered link types for consistent display.
 * Game-relevant types first, then agent/event types.
 */
const LINK_TYPE_ORDER: EntityLinkType[] = [
  'ExpansionOf',
  'SequelOf',
  'Reimplements',
  'CompanionTo',
  'RelatedTo',
  'PartOf',
  'CollaboratesWith',
  'SpecializedBy',
];

// ============================================================================
// Props
// ============================================================================

export interface GameRelationshipsProps {
  /** The game ID to fetch relationships for */
  gameId: string;
  /** Optional game name for context */
  gameName?: string;
  /** Called when user clicks navigate on a linked entity */
  onNavigate?: (entityType: LinkEntityType, entityId: string) => void;
  className?: string;
}

// ============================================================================
// Helper: resolve display name from link metadata
// ============================================================================

function resolveTargetName(link: EntityLinkDto): string {
  if (link.metadata) {
    try {
      const meta = JSON.parse(link.metadata) as { name?: string };
      if (meta.name) return meta.name;
    } catch {
      // metadata is not JSON — use it as-is
      return link.metadata;
    }
  }
  return `${link.targetEntityType} ${link.targetEntityId.slice(0, 8)}...`;
}

// ============================================================================
// Component
// ============================================================================

export function GameRelationships({
  gameId,
  gameName,
  onNavigate,
  className,
}: GameRelationshipsProps) {
  const { links, loading, error } = useEntityLinks('Game', gameId);

  // Loading state: skeleton
  if (loading) {
    return (
      <div
        className={cn('flex items-center justify-center py-6', className)}
        data-testid="game-relationships-loading"
        aria-busy="true"
      >
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" aria-hidden="true" />
      </div>
    );
  }

  // Error state: silent (supplementary content)
  if (error) {
    return null;
  }

  // No links: return null (supplementary content)
  if (links.length === 0) {
    return null;
  }

  // Group links by linkType
  const grouped = new Map<EntityLinkType, EntityLinkDto[]>();
  for (const link of links) {
    const existing = grouped.get(link.linkType) ?? [];
    existing.push(link);
    grouped.set(link.linkType, existing);
  }

  return (
    <section
      className={cn('space-y-4', className)}
      data-testid="game-relationships"
      aria-label={gameName ? `Relazioni di ${gameName}` : 'Relazioni del gioco'}
    >
      {/* Section header */}
      <div className="flex items-center gap-2">
        <LinkIcon className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
        <h3 className="font-quicksand text-sm font-bold text-foreground">Relazioni</h3>
        <span className="rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-semibold text-muted-foreground">
          {links.length}
        </span>
      </div>

      {/* Grouped link sections */}
      <div className="space-y-4">
        {LINK_TYPE_ORDER.map(linkType => {
          const groupLinks = grouped.get(linkType);
          if (!groupLinks || groupLinks.length === 0) return null;

          return (
            <div key={linkType} data-testid={`relationship-group-${linkType}`}>
              <div className="mb-2 flex items-center gap-1.5">
                <span className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground/60">
                  {LINK_TYPE_LABELS[linkType]}
                </span>
                <span className="rounded-full bg-muted px-1 py-0.5 text-[9px] font-bold text-muted-foreground/70">
                  {groupLinks.length}
                </span>
              </div>

              <div className="flex flex-col gap-2">
                {groupLinks.map(link => (
                  <EntityLinkCard
                    key={link.id}
                    link={link}
                    targetName={resolveTargetName(link)}
                    onNavigate={onNavigate}
                    showBggBadge={link.isBggImported}
                    data-testid={`relationship-card-${link.id}`}
                  />
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}

export default GameRelationships;
