'use client';

/**
 * EntityLinkCard — C4
 *
 * Mini-card component for a single EntityLink, used in the drawer Links tab.
 * Shows entity icon, name, link type chip, and action buttons (navigate / remove).
 *
 * Issue #5160 — Epic C4
 */

import React from 'react';

import {
  Gamepad2,
  User,
  PlayCircle,
  Bot,
  FileText,
  MessageCircle,
  Calendar,
  Wrench,
  Globe,
  ChevronRight,
  X,
} from 'lucide-react';

import { cn } from '@/lib/utils';

import { EntityLinkChip } from './entity-link-chip';
import { LINK_ENTITY_CONFIG, type EntityLinkDto, type LinkEntityType } from './entity-link-types';

// ============================================================================
// Entity Icon Map
// ============================================================================

const ENTITY_ICONS: Record<LinkEntityType, React.ComponentType<{ className?: string }>> = {
  Game: Gamepad2,
  Player: User,
  Session: PlayCircle,
  Agent: Bot,
  Document: FileText,
  ChatSession: MessageCircle,
  Event: Calendar,
  Toolkit: Wrench,
};

// ============================================================================
// Props
// ============================================================================

export interface EntityLinkCardProps {
  /** The EntityLink DTO */
  link: EntityLinkDto;
  /**
   * The entity name to display. The API returns only IDs in the link;
   * the parent component must resolve the name.
   */
  targetName: string;
  /** Called to navigate to the target entity */
  onNavigate?: (entityType: LinkEntityType, entityId: string) => void;
  /**
   * Called to remove the link. If undefined, the remove button is hidden.
   * BGG-imported links should not pass this prop.
   */
  onRemove?: (linkId: string) => void;
  /** When true, shows a BGG badge instead of the remove button */
  showBggBadge?: boolean;
  className?: string;
  'data-testid'?: string;
}

// ============================================================================
// Component
// ============================================================================

export function EntityLinkCard({
  link,
  targetName,
  onNavigate,
  onRemove,
  showBggBadge,
  className,
  'data-testid': testId,
}: EntityLinkCardProps) {
  const entityConfig = LINK_ENTITY_CONFIG[link.targetEntityType];
  const Icon = ENTITY_ICONS[link.targetEntityType];
  const entityColor = entityConfig.color;

  const handleNavigate = () => {
    onNavigate?.(link.targetEntityType, link.targetEntityId);
  };

  const handleRemove = (e: React.MouseEvent) => {
    e.stopPropagation();
    onRemove?.(link.id);
  };

  return (
    <div
      className={cn(
        'group/card flex items-start gap-3',
        'rounded-xl border border-border/50 bg-card/60 backdrop-blur-sm',
        'px-3 py-2.5',
        'transition-all duration-200',
        'hover:border-border hover:bg-card/80 hover:shadow-sm',
        className
      )}
      data-testid={testId ?? 'entity-link-card'}
    >
      {/* Entity icon */}
      <div
        className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full"
        style={{ background: `hsl(${entityColor} / 0.12)`, color: `hsl(${entityColor})` }}
        aria-hidden="true"
      >
        <Icon className="h-3.5 w-3.5" />
      </div>

      {/* Main content */}
      <div className="min-w-0 flex-1">
        {/* Entity name */}
        <p className="truncate font-quicksand text-sm font-semibold text-foreground leading-snug">
          {targetName}
        </p>

        {/* Link type chip */}
        <div className="mt-1">
          <EntityLinkChip linkType={link.linkType} size="sm" />
        </div>
      </div>

      {/* Actions */}
      <div className="ml-auto flex shrink-0 items-center gap-1 self-center">
        {/* BGG badge */}
        {showBggBadge && (
          <span
            className="inline-flex items-center gap-0.5 rounded-full border border-slate-200 bg-slate-50 px-1.5 py-0.5 text-[10px] font-medium text-slate-500"
            title="Imported from BoardGameGeek"
          >
            <Globe className="h-2.5 w-2.5" aria-hidden="true" />
            BGG
          </span>
        )}

        {/* Navigate button */}
        {onNavigate && (
          <button
            type="button"
            onClick={e => {
              e.stopPropagation();
              handleNavigate();
            }}
            className={cn(
              'flex h-6 w-6 items-center justify-center rounded-full',
              'bg-transparent text-muted-foreground/50',
              'transition-all duration-150',
              'hover:bg-muted hover:text-foreground',
              'focus:outline-none focus:ring-2 focus:ring-slate-300'
            )}
            aria-label={`Navigate to ${targetName}`}
          >
            <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
          </button>
        )}

        {/* Remove button — only when user is owner and not BGG */}
        {onRemove && !showBggBadge && (
          <button
            type="button"
            onClick={handleRemove}
            className={cn(
              'flex h-6 w-6 items-center justify-center rounded-full',
              'bg-transparent text-muted-foreground/40',
              'transition-all duration-150',
              'hover:bg-red-50 hover:text-red-500',
              'focus:outline-none focus:ring-2 focus:ring-red-300'
            )}
            aria-label={`Remove link to ${targetName}`}
          >
            <X className="h-3.5 w-3.5" aria-hidden="true" />
          </button>
        )}
      </div>
    </div>
  );
}

export default EntityLinkCard;
