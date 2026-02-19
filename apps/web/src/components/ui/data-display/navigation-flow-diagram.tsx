/**
 * NavigationFlowDiagram - Interactive entity relationship graph
 *
 * SVG-based visualization of the MeepleCard navigation graph showing
 * how entity types connect to each other. Features animated connections,
 * hover highlighting, and click-to-navigate.
 *
 * @module components/ui/data-display/navigation-flow-diagram
 * @see Issue #4699
 * @see config/entity-navigation.ts for the relationship graph
 */

'use client';

import React, { useCallback, useMemo, useState } from 'react';

import {
  Gamepad2,
  Bot,
  FileText,
  Users,
  MessageSquare,
  Dices,
} from 'lucide-react';

import { cn } from '@/lib/utils';

import type { MeepleEntityType } from './meeple-card';

// ============================================================================
// Entity Config
// ============================================================================

const ENTITY_CONFIG: Record<
  string,
  { label: string; color: string; icon: typeof Gamepad2 }
> = {
  game: { label: 'Game', color: 'hsl(25, 95%, 45%)', icon: Gamepad2 },
  agent: { label: 'Agent', color: 'hsl(38, 92%, 50%)', icon: Bot },
  document: { label: 'KB', color: 'hsl(210, 40%, 55%)', icon: FileText },
  session: { label: 'Session', color: 'hsl(240, 60%, 55%)', icon: Dices },
  player: { label: 'Player', color: 'hsl(262, 83%, 58%)', icon: Users },
  chatSession: { label: 'Chat', color: 'hsl(220, 80%, 55%)', icon: MessageSquare },
};

// ============================================================================
// Graph Edges (from entity-navigation.ts)
// ============================================================================

interface GraphEdge {
  from: string;
  to: string;
  bidirectional: boolean;
}

const GRAPH_EDGES: GraphEdge[] = [
  { from: 'game', to: 'document', bidirectional: true },
  { from: 'game', to: 'agent', bidirectional: true },
  { from: 'game', to: 'chatSession', bidirectional: true },
  { from: 'game', to: 'session', bidirectional: true },
  { from: 'agent', to: 'document', bidirectional: true },
  { from: 'agent', to: 'chatSession', bidirectional: true },
  { from: 'agent', to: 'session', bidirectional: true },
  { from: 'session', to: 'player', bidirectional: true },
  { from: 'session', to: 'chatSession', bidirectional: true },
  { from: 'player', to: 'game', bidirectional: true },
];

// ============================================================================
// Node positions (2-row layout)
// ============================================================================

interface NodePosition {
  entity: string;
  x: number;
  y: number;
}

const NODE_POSITIONS: NodePosition[] = [
  { entity: 'game', x: 120, y: 80 },
  { entity: 'document', x: 300, y: 80 },
  { entity: 'agent', x: 480, y: 80 },
  { entity: 'session', x: 180, y: 200 },
  { entity: 'player', x: 360, y: 200 },
  { entity: 'chatSession', x: 540, y: 200 },
];

// ============================================================================
// Types
// ============================================================================

export interface NavigationFlowDiagramProps {
  /** Callback when a node is clicked */
  onNodeClick?: (entity: MeepleEntityType) => void;
  /** Currently active/highlighted entity */
  activeEntity?: MeepleEntityType;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

// ============================================================================
// FlowNode Component
// ============================================================================

function FlowNode({
  entity,
  x,
  y,
  isHovered,
  isConnected,
  isActive,
  onMouseEnter,
  onMouseLeave,
  onClick,
}: {
  entity: string;
  x: number;
  y: number;
  isHovered: boolean;
  isConnected: boolean;
  isActive: boolean;
  onMouseEnter: () => void;
  onMouseLeave: () => void;
  onClick: () => void;
}) {
  const config = ENTITY_CONFIG[entity];
  if (!config) return null;
  const Icon = config.icon;

  const scale = isHovered ? 1.15 : isActive ? 1.1 : 1;
  const opacity = isHovered || isConnected || isActive ? 1 : 0.7;

  return (
    <g
      transform={`translate(${x}, ${y})`}
      onMouseEnter={onMouseEnter}
      onMouseLeave={onMouseLeave}
      onClick={onClick}
      className="cursor-pointer"
      role="button"
      tabIndex={0}
      aria-label={`Navigate to ${config.label}`}
    >
      {/* Glow effect */}
      {(isHovered || isActive) && (
        <circle
          r={34}
          fill="none"
          stroke={config.color}
          strokeWidth={2}
          opacity={0.4}
          className="animate-pulse"
        />
      )}

      {/* Node circle */}
      <circle
        r={28}
        fill="var(--card, white)"
        stroke={config.color}
        strokeWidth={isHovered || isActive ? 3 : 2}
        opacity={opacity}
        className="transition-all duration-200"
      />

      {/* Icon placeholder */}
      <foreignObject x={-12} y={-16} width={24} height={24}>
        <Icon
          className="w-6 h-6"
          style={{ color: config.color }}
        />
      </foreignObject>

      {/* Label */}
      <text
        y={24}
        textAnchor="middle"
        className="text-[11px] font-quicksand font-semibold fill-current"
        style={{
          fill: isHovered || isActive ? config.color : 'var(--muted-foreground, #666)',
          transition: 'fill 0.2s',
        }}
      >
        {config.label}
      </text>

      {/* Scale transform animation */}
      <animateTransform
        attributeName="transform"
        type="scale"
        from={`${scale}`}
        to={`${scale}`}
        dur="0.2s"
        fill="freeze"
      />
    </g>
  );
}

// ============================================================================
// FlowConnection Component
// ============================================================================

function FlowConnection({
  x1,
  y1,
  x2,
  y2,
  color1,
  color2,
  isHighlighted,
  bidirectional,
  edgeId,
}: {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  color1: string;
  color2: string;
  isHighlighted: boolean;
  bidirectional: boolean;
  edgeId: string;
}) {
  const gradientId = `grad-${edgeId}`;

  return (
    <g opacity={isHighlighted ? 1 : 0.25} className="transition-opacity duration-200">
      {/* Gradient definition */}
      <defs>
        <linearGradient id={gradientId} x1="0%" y1="0%" x2="100%" y2="0%">
          <stop offset="0%" stopColor={color1} />
          <stop offset="100%" stopColor={color2} />
        </linearGradient>
      </defs>

      {/* Connection line */}
      <line
        x1={x1}
        y1={y1}
        x2={x2}
        y2={y2}
        stroke={`url(#${gradientId})`}
        strokeWidth={isHighlighted ? 2.5 : 1.5}
        strokeDasharray={bidirectional ? 'none' : '6 4'}
        className="transition-all duration-200"
      />

      {/* Animated dash for flow direction */}
      {isHighlighted && (
        <line
          x1={x1}
          y1={y1}
          x2={x2}
          y2={y2}
          stroke={`url(#${gradientId})`}
          strokeWidth={2}
          strokeDasharray="4 8"
          className="animate-[dashOffset_1.5s_linear_infinite]"
        >
          <animate
            attributeName="stroke-dashoffset"
            from="0"
            to="-24"
            dur="1.5s"
            repeatCount="indefinite"
          />
        </line>
      )}
    </g>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function NavigationFlowDiagram({
  onNodeClick,
  activeEntity,
  className,
  'data-testid': testId,
}: NavigationFlowDiagramProps) {
  const [hoveredEntity, setHoveredEntity] = useState<string | null>(null);

  // Get position for entity
  const getPosition = useCallback((entity: string) => {
    return NODE_POSITIONS.find((n) => n.entity === entity);
  }, []);

  // Get connected entities for a given entity
  const getConnectedEntities = useCallback((entity: string): Set<string> => {
    const connected = new Set<string>();
    for (const edge of GRAPH_EDGES) {
      if (edge.from === entity) connected.add(edge.to);
      if (edge.to === entity) connected.add(edge.from);
    }
    return connected;
  }, []);

  // Connected entities for current hover
  const connectedEntities = useMemo(() => {
    if (!hoveredEntity) return new Set<string>();
    return getConnectedEntities(hoveredEntity);
  }, [hoveredEntity, getConnectedEntities]);

  // Is edge highlighted
  const isEdgeHighlighted = useCallback(
    (from: string, to: string) => {
      if (!hoveredEntity && !activeEntity) return true; // all visible when nothing hovered
      const target = hoveredEntity || activeEntity;
      return from === target || to === target;
    },
    [hoveredEntity, activeEntity]
  );

  return (
    <div
      className={cn('w-full', className)}
      data-testid={testId || 'navigation-flow-diagram'}
    >
      {/* Desktop: SVG diagram */}
      <div className="hidden md:block overflow-x-auto">
        <svg
          viewBox="0 0 660 260"
          className="w-full max-w-[660px] mx-auto"
          role="img"
          aria-label="Entity navigation flow diagram"
        >
          {/* Connections */}
          {GRAPH_EDGES.map((edge) => {
            const fromPos = getPosition(edge.from);
            const toPos = getPosition(edge.to);
            if (!fromPos || !toPos) return null;

            const fromConfig = ENTITY_CONFIG[edge.from];
            const toConfig = ENTITY_CONFIG[edge.to];

            return (
              <FlowConnection
                key={`${edge.from}-${edge.to}`}
                x1={fromPos.x}
                y1={fromPos.y}
                x2={toPos.x}
                y2={toPos.y}
                color1={fromConfig?.color || '#999'}
                color2={toConfig?.color || '#999'}
                isHighlighted={isEdgeHighlighted(edge.from, edge.to)}
                bidirectional={edge.bidirectional}
                edgeId={`${edge.from}-${edge.to}`}
              />
            );
          })}

          {/* Nodes */}
          {NODE_POSITIONS.map(({ entity, x, y }) => (
            <FlowNode
              key={entity}
              entity={entity}
              x={x}
              y={y}
              isHovered={hoveredEntity === entity}
              isConnected={connectedEntities.has(entity)}
              isActive={activeEntity === entity}
              onMouseEnter={() => setHoveredEntity(entity)}
              onMouseLeave={() => setHoveredEntity(null)}
              onClick={() => onNodeClick?.(entity as MeepleEntityType)}
            />
          ))}
        </svg>
      </div>

      {/* Mobile: Compact list with entity chips */}
      <div className="md:hidden">
        <div className="flex flex-wrap gap-2 justify-center">
          {Object.entries(ENTITY_CONFIG).map(([entity, config]) => {
            const Icon = config.icon;
            const isActive = activeEntity === entity;

            return (
              <button
                key={entity}
                type="button"
                onClick={() => onNodeClick?.(entity as MeepleEntityType)}
                className={cn(
                  'flex items-center gap-2 px-3 py-2 rounded-full',
                  'text-sm font-nunito font-medium',
                  'transition-all duration-200',
                  'border',
                  isActive
                    ? 'border-current shadow-md scale-105'
                    : 'border-border/50 hover:border-current hover:shadow-sm'
                )}
                style={{
                  color: config.color,
                  backgroundColor: isActive ? `${config.color}15` : 'transparent',
                }}
              >
                <Icon className="w-4 h-4" />
                <span>{config.label}</span>
              </button>
            );
          })}
        </div>

        {/* Show connections for active entity */}
        {activeEntity && (
          <div className="mt-4 text-center text-sm text-muted-foreground font-nunito">
            <span className="font-medium" style={{ color: ENTITY_CONFIG[activeEntity]?.color }}>
              {ENTITY_CONFIG[activeEntity]?.label}
            </span>
            {' connects to: '}
            {Array.from(getConnectedEntities(activeEntity)).map((e, i) => (
              <React.Fragment key={e}>
                {i > 0 && ', '}
                <span style={{ color: ENTITY_CONFIG[e]?.color }} className="font-medium">
                  {ENTITY_CONFIG[e]?.label}
                </span>
              </React.Fragment>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
