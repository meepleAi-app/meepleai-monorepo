'use client';

/**
 * EntityRelationshipGraph — C7
 *
 * Visualises EntityLinks as a radial React Flow graph.
 * Toggles between list view (RelatedEntitiesSection) and graph view.
 * Persists the last-used view in localStorage.
 *
 * Issue #5163 — Epic C7
 */

import React, { useState, useCallback, useMemo } from 'react';

import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  Position,
  type Node,
  type Edge,
  BackgroundVariant,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';

import { LayoutGrid, GitFork } from 'lucide-react';

import { cn } from '@/lib/utils';

import {
  LINK_ENTITY_CONFIG,
  LINK_TYPE_CONFIG,
  type EntityLinkDto,
  type EntityLinkType,
  type LinkEntityType,
} from './entity-link-types';
import { RelatedEntitiesSection } from './related-entities-section';
import { useEntityLinks } from './use-entity-links';

// ============================================================================
// Constants
// ============================================================================

const STORAGE_KEY = 'entity-relationship-graph-view';
type ViewMode = 'list' | 'graph';

// ============================================================================
// Graph helpers
// ============================================================================

function resolveEntityName(link: EntityLinkDto): string {
  if (link.metadata) {
    try {
      const meta = JSON.parse(link.metadata) as { name?: string };
      if (meta.name) return meta.name;
    } catch {
      return link.metadata;
    }
  }
  return `${link.targetEntityType} ${link.targetEntityId.slice(0, 8)}…`;
}

/** Build React Flow nodes + edges from a link list. */
function buildGraph(
  sourceEntityType: LinkEntityType,
  sourceEntityId: string,
  links: EntityLinkDto[]
): { nodes: Node[]; edges: Edge[] } {
  const sourceCfg = LINK_ENTITY_CONFIG[sourceEntityType];

  // Central node
  const centerNode: Node = {
    id: 'source',
    type: 'default',
    data: { label: `${sourceCfg.label} (you)` },
    position: { x: 0, y: 0 },
    style: {
      background: `hsl(${sourceCfg.color} / 0.18)`,
      border: `2px solid hsl(${sourceCfg.color} / 0.6)`,
      color: `hsl(${sourceCfg.color})`,
      borderRadius: 12,
      fontFamily: 'var(--font-quicksand, sans-serif)',
      fontWeight: 700,
      fontSize: 12,
      padding: '8px 14px',
      minWidth: 100,
      textAlign: 'center',
    },
    sourcePosition: Position.Right,
    targetPosition: Position.Left,
  };

  const nodes: Node[] = [centerNode];
  const edges: Edge[] = [];

  // Radial layout: spread link nodes around the center
  const angleStep = links.length > 0 ? (2 * Math.PI) / links.length : 0;
  const radius = Math.max(180, links.length * 40);

  links.forEach((link, i) => {
    const angle = i * angleStep - Math.PI / 2;
    const x = Math.cos(angle) * radius;
    const y = Math.sin(angle) * radius;

    const targetCfg = LINK_ENTITY_CONFIG[link.targetEntityType];
    const linkCfg = LINK_TYPE_CONFIG[link.linkType];
    const name = resolveEntityName(link);

    nodes.push({
      id: link.id,
      type: 'default',
      data: { label: name },
      position: { x, y },
      style: {
        background: `hsl(${targetCfg.color} / 0.12)`,
        border: `1.5px solid hsl(${targetCfg.color} / 0.4)`,
        color: `hsl(${targetCfg.color})`,
        borderRadius: 10,
        fontFamily: 'var(--font-nunito, sans-serif)',
        fontSize: 11,
        padding: '6px 12px',
        minWidth: 80,
        textAlign: 'center',
      },
      sourcePosition: Position.Right,
      targetPosition: Position.Left,
    });

    edges.push({
      id: `e-source-${link.id}`,
      source: 'source',
      target: link.id,
      label: `${linkCfg.label} ${linkCfg.directionIcon}`,
      style: { stroke: `hsl(${linkCfg.color} / 0.6)`, strokeWidth: 1.5 },
      labelStyle: {
        fontSize: 9,
        fontFamily: 'var(--font-nunito, sans-serif)',
        fill: `hsl(${linkCfg.color})`,
      },
      type: 'smoothstep',
      animated: false,
    });
  });

  return { nodes, edges };
}

// ============================================================================
// Props
// ============================================================================

export interface EntityRelationshipGraphProps {
  entityType: LinkEntityType;
  entityId: string;
  onAddLink?: (defaultLinkType?: EntityLinkType) => void;
  onNavigate?: (entityType: LinkEntityType, entityId: string) => void;
  onLinkRemoved?: (linkId: string) => void;
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function EntityRelationshipGraph({
  entityType,
  entityId,
  onAddLink,
  onNavigate,
  onLinkRemoved,
  className,
}: EntityRelationshipGraphProps) {
  const [viewMode, setViewMode] = useState<ViewMode>(() => {
    try {
      return (localStorage.getItem(STORAGE_KEY) as ViewMode) ?? 'list';
    } catch {
      return 'list';
    }
  });

  const handleViewModeChange = useCallback((mode: ViewMode) => {
    setViewMode(mode);
    try {
      localStorage.setItem(STORAGE_KEY, mode);
    } catch {
      // ignore
    }
  }, []);

  const { links, loading } = useEntityLinks(entityType, entityId);

  const { nodes, edges } = useMemo(
    () => buildGraph(entityType, entityId, links),
    [entityType, entityId, links]
  );

  return (
    <div className={cn('flex flex-col', className)} data-testid="entity-relationship-graph">
      {/* Toggle header */}
      <div className="flex items-center justify-end gap-1 border-b border-border/40 px-4 pb-2 pt-3">
        <ViewToggleButton
          active={viewMode === 'list'}
          onClick={() => handleViewModeChange('list')}
          aria-label="List view"
          title="List view"
        >
          <LayoutGrid className="h-3.5 w-3.5" aria-hidden="true" />
        </ViewToggleButton>
        <ViewToggleButton
          active={viewMode === 'graph'}
          onClick={() => handleViewModeChange('graph')}
          aria-label="Graph view"
          title="Graph view"
        >
          <GitFork className="h-3.5 w-3.5" aria-hidden="true" />
        </ViewToggleButton>
      </div>

      {/* Content */}
      {viewMode === 'list' ? (
        <RelatedEntitiesSection
          entityType={entityType}
          entityId={entityId}
          onAddLink={onAddLink}
          onNavigate={onNavigate}
          onLinkRemoved={onLinkRemoved}
        />
      ) : (
        <GraphView nodes={nodes} edges={edges} loading={loading} />
      )}
    </div>
  );
}

// ============================================================================
// Sub-components
// ============================================================================

function ViewToggleButton({
  active,
  onClick,
  children,
  'aria-label': ariaLabel,
  title,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
  'aria-label': string;
  title: string;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={ariaLabel}
      title={title}
      className={cn(
        'flex h-7 w-7 items-center justify-center rounded-md',
        'transition-all duration-150',
        'focus:outline-none focus:ring-2 focus:ring-slate-300',
        active
          ? 'bg-muted text-foreground'
          : 'text-muted-foreground/60 hover:bg-muted/60 hover:text-muted-foreground'
      )}
      aria-pressed={active}
    >
      {children}
    </button>
  );
}

function GraphView({ nodes, edges, loading }: { nodes: Node[]; edges: Edge[]; loading: boolean }) {
  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <span className="text-sm text-muted-foreground">Loading graph…</span>
      </div>
    );
  }

  return (
    <div className="h-[360px] w-full" data-testid="entity-graph-canvas">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        fitView
        fitViewOptions={{ padding: 0.3 }}
        minZoom={0.4}
        maxZoom={2}
        nodesDraggable={false}
        nodesConnectable={false}
        elementsSelectable={false}
        proOptions={{ hideAttribution: true }}
      >
        <Background variant={BackgroundVariant.Dots} gap={16} size={1} color="hsl(var(--border))" />
        <Controls showInteractive={false} />
        <MiniMap
          nodeColor={n => (n.style?.background as string | undefined) ?? 'hsl(var(--muted))'}
          maskColor="hsl(var(--background) / 0.7)"
          style={{ height: 80 }}
        />
      </ReactFlow>
    </div>
  );
}

export default EntityRelationshipGraph;
