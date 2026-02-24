'use client';

/**
 * RelatedEntitiesSection — C5
 *
 * Full Links tab content for the ExtraMeepleCardDrawer.
 * Groups EntityLinks by link type, renders EntityLinkCard items,
 * and provides "+ Add" actions.
 *
 * Issue #5161 — Epic C5
 */

import React, { useState } from 'react';

import { Plus, Link as LinkIcon, Loader2 } from 'lucide-react';

import { cn } from '@/lib/utils';

import { EntityLinkCard } from './entity-link-card';
import {
  LINK_TYPE_GROUPS,
  type EntityLinkDto,
  type EntityLinkType,
  type LinkEntityType,
} from './entity-link-types';
import { useEntityLinks } from './use-entity-links';

// ============================================================================
// Types
// ============================================================================

export interface RelatedEntitiesSectionProps {
  /** Source entity type (the card being viewed) */
  entityType: LinkEntityType;
  /** Source entity ID */
  entityId: string;
  /** Called when user clicks "+ Add" — parent opens AddEntityLinkModal */
  onAddLink?: (defaultLinkType?: EntityLinkType) => void;
  /** Called when user clicks navigate arrow on a link card */
  onNavigate?: (entityType: LinkEntityType, entityId: string) => void;
  /** Called after a link is removed (parent should refresh) */
  onLinkRemoved?: (linkId: string) => void;
  className?: string;
}

// ============================================================================
// Helper: remove a link via API
// ============================================================================

async function removeLink(linkId: string): Promise<void> {
  const res = await fetch(`/api/v1/library/entity-links/${linkId}`, { method: 'DELETE' });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
}

// ============================================================================
// Component
// ============================================================================

export function RelatedEntitiesSection({
  entityType,
  entityId,
  onAddLink,
  onNavigate,
  onLinkRemoved,
  className,
}: RelatedEntitiesSectionProps) {
  const { links, loading, error, refresh } = useEntityLinks(entityType, entityId);
  const [removing, setRemoving] = useState<string | null>(null);

  const handleRemove = async (linkId: string) => {
    setRemoving(linkId);
    try {
      await removeLink(linkId);
      onLinkRemoved?.(linkId);
      refresh();
    } catch {
      // silent — link stays visible
    } finally {
      setRemoving(null);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center py-8" aria-busy="true">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" aria-hidden="true" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex flex-col items-center gap-2 py-8 text-center" role="alert">
        <LinkIcon className="h-8 w-8 text-muted-foreground/40" aria-hidden="true" />
        <p className="font-nunito text-sm text-muted-foreground">{error}</p>
      </div>
    );
  }

  // Group links by link type
  const grouped = new Map<EntityLinkType, EntityLinkDto[]>();
  for (const link of links) {
    const existing = grouped.get(link.linkType) ?? [];
    existing.push(link);
    grouped.set(link.linkType, existing);
  }

  const hasAnyLinks = links.length > 0;

  return (
    <div className={cn('flex flex-col gap-0', className)} data-testid="related-entities-section">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-border/40 px-4 pb-3 pt-4">
        <div className="flex items-center gap-2">
          <LinkIcon className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
          <h3 className="font-quicksand text-sm font-bold text-foreground">Connections</h3>
          {hasAnyLinks && (
            <span className="rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-semibold text-muted-foreground">
              {links.length}
            </span>
          )}
        </div>

        {onAddLink && (
          <button
            type="button"
            onClick={() => onAddLink()}
            className={cn(
              'flex items-center gap-1',
              'rounded-lg border border-dashed border-border/60',
              'px-2.5 py-1 text-xs font-medium',
              'text-muted-foreground transition-all duration-150',
              'hover:border-border hover:bg-muted hover:text-foreground',
              'focus:outline-none focus:ring-2 focus:ring-slate-300'
            )}
            aria-label="Add new connection"
          >
            <Plus className="h-3 w-3" aria-hidden="true" />
            Add
          </button>
        )}
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto px-4 py-3">
        {!hasAnyLinks ? (
          <EmptyLinksState onAdd={onAddLink ? () => onAddLink() : undefined} />
        ) : (
          <div className="flex flex-col gap-5">
            {LINK_TYPE_GROUPS.map(group => {
              const groupLinks = group.types.flatMap(t => grouped.get(t) ?? []);
              if (groupLinks.length === 0) return null;

              return (
                <LinkTypeGroupSection
                  key={group.label}
                  label={group.label}
                  links={groupLinks}
                  removing={removing}
                  onNavigate={onNavigate}
                  onRemove={handleRemove}
                  onAdd={onAddLink ? () => onAddLink(group.types[0]) : undefined}
                />
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

// ============================================================================
// Sub-components
// ============================================================================

function EmptyLinksState({ onAdd }: { onAdd?: () => void }) {
  return (
    <div className="flex flex-col items-center gap-3 py-8 text-center">
      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-muted/60">
        <LinkIcon className="h-5 w-5 text-muted-foreground/50" aria-hidden="true" />
      </div>
      <div className="space-y-1">
        <p className="font-quicksand text-sm font-semibold text-muted-foreground">
          No connections yet
        </p>
        <p className="font-nunito text-xs text-muted-foreground/70">
          Link this entity to related games, agents, or events.
        </p>
      </div>
      {onAdd && (
        <button
          type="button"
          onClick={onAdd}
          className={cn(
            'flex items-center gap-1.5 rounded-lg',
            'bg-primary/10 px-3 py-1.5',
            'font-nunito text-xs font-semibold text-primary',
            'transition-colors duration-150 hover:bg-primary/20',
            'focus:outline-none focus:ring-2 focus:ring-primary/30'
          )}
        >
          <Plus className="h-3.5 w-3.5" aria-hidden="true" />
          Add first connection
        </button>
      )}
    </div>
  );
}

interface LinkTypeGroupSectionProps {
  label: string;
  links: EntityLinkDto[];
  removing: string | null;
  onNavigate?: (entityType: LinkEntityType, entityId: string) => void;
  onRemove: (linkId: string) => void;
  onAdd?: () => void;
}

function LinkTypeGroupSection({
  label,
  links,
  removing,
  onNavigate,
  onRemove,
  onAdd,
}: LinkTypeGroupSectionProps) {
  // Collapse if more than 3 links
  const [expanded, setExpanded] = useState(true);
  const COLLAPSE_THRESHOLD = 3;
  const visible = expanded ? links : links.slice(0, COLLAPSE_THRESHOLD);

  return (
    <section aria-label={`${label} section`}>
      {/* Section header */}
      <div className="mb-2 flex items-center justify-between">
        <div className="flex items-center gap-1.5">
          <span className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground/60">
            {label}
          </span>
          <span className="rounded-full bg-muted px-1 py-0.5 text-[9px] font-bold text-muted-foreground/70">
            {links.length}
          </span>
        </div>

        <div className="flex items-center gap-1">
          {links.length > COLLAPSE_THRESHOLD && (
            <button
              type="button"
              onClick={() => setExpanded(v => !v)}
              className="text-[10px] font-medium text-muted-foreground/60 hover:text-muted-foreground"
            >
              {expanded ? 'Show less' : `Show all ${links.length}`}
            </button>
          )}
          {onAdd && (
            <button
              type="button"
              onClick={onAdd}
              className={cn(
                'flex items-center gap-0.5 rounded-md px-1.5 py-0.5',
                'text-[10px] font-medium text-muted-foreground/60',
                'hover:bg-muted hover:text-muted-foreground',
                'transition-colors duration-150'
              )}
              aria-label={`Add ${label} link`}
            >
              <Plus className="h-2.5 w-2.5" aria-hidden="true" />
              Add
            </button>
          )}
        </div>
      </div>

      {/* Link cards */}
      <div className="flex flex-col gap-2">
        {visible.map(link => (
          <div
            key={link.id}
            className={cn('transition-opacity duration-200', removing === link.id && 'opacity-40')}
          >
            <EntityLinkCard
              link={link}
              targetName={resolveEntityName(link)}
              onNavigate={onNavigate}
              onRemove={link.isOwner ? onRemove : undefined}
              showBggBadge={link.isBggImported}
            />
          </div>
        ))}
      </div>
    </section>
  );
}

/**
 * Resolve a display name from the link DTO.
 * Since the DTO only has IDs, we use the target entity type + truncated ID as fallback.
 * In production, the parent should resolve names via a lookup.
 */
function resolveEntityName(link: EntityLinkDto): string {
  // Use metadata if available (can contain a cached name)
  if (link.metadata) {
    try {
      const meta = JSON.parse(link.metadata) as { name?: string };
      if (meta.name) return meta.name;
    } catch {
      // metadata is not JSON
      return link.metadata;
    }
  }
  return `${link.targetEntityType} ${link.targetEntityId.slice(0, 8)}…`;
}

export default RelatedEntitiesSection;
