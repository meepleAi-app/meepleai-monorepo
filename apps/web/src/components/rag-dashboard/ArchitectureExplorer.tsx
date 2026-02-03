'use client';

/**
 * ArchitectureExplorer Component
 *
 * Interactive architecture diagram replacing ASCII art.
 * Features: clickable nodes, zoom, layer filtering, documentation links.
 */

import React, { useState, useMemo } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Brain,
  Database,
  Search,
  CheckCircle2,
  Sparkles,
  Shield,
  User,
  Users,
  Server,
  HardDrive,
  Globe,
  Cpu,
  ExternalLink,
  Filter,
  ZoomIn,
  ZoomOut,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';


// =============================================================================
// Architecture Node Types
// =============================================================================

interface ArchNode {
  id: string;
  label: string;
  shortLabel?: string;
  icon: React.FC<{ className?: string }>;
  category: 'input' | 'layer' | 'service' | 'storage';
  description: string;
  connections: string[];
  color: string;
  position: { x: number; y: number };
}

const ARCH_NODES: ArchNode[] = [
  // Input
  {
    id: 'user',
    label: 'User Query',
    icon: User,
    category: 'input',
    description: 'User sends natural language question about game rules',
    connections: ['routing'],
    color: 'hsl(0, 0%, 50%)',
    position: { x: 50, y: 50 },
  },
  // Layers
  {
    id: 'routing',
    label: 'L1: Routing',
    shortLabel: 'Routing',
    icon: Brain,
    category: 'layer',
    description: 'Classifies query template, scores complexity, selects strategy',
    connections: ['cache', 'tier-resolver'],
    color: 'hsl(221, 83%, 53%)',
    position: { x: 200, y: 50 },
  },
  {
    id: 'cache',
    label: 'L2: Cache',
    shortLabel: 'Cache',
    icon: Database,
    category: 'layer',
    description: 'Memory + semantic cache for instant responses',
    connections: ['retrieval', 'redis'],
    color: 'hsl(262, 83%, 62%)',
    position: { x: 350, y: 50 },
  },
  {
    id: 'retrieval',
    label: 'L3: Retrieval',
    shortLabel: 'Retrieval',
    icon: Search,
    category: 'layer',
    description: 'Hybrid search: vector + keyword + metadata filtering',
    connections: ['crag', 'qdrant', 'postgres'],
    color: 'hsl(142, 76%, 36%)',
    position: { x: 500, y: 50 },
  },
  {
    id: 'crag',
    label: 'L4: CRAG',
    shortLabel: 'CRAG',
    icon: CheckCircle2,
    category: 'layer',
    description: 'Quality gate: evaluates retrieval, triggers web search if needed',
    connections: ['generation', 't5-evaluator', 'web-search'],
    color: 'hsl(45, 93%, 47%)',
    position: { x: 650, y: 50 },
  },
  {
    id: 'generation',
    label: 'L5: Generation',
    shortLabel: 'Generation',
    icon: Sparkles,
    category: 'layer',
    description: 'Adaptive LLM generation with template-specific prompts',
    connections: ['validation', 'llm-router'],
    color: 'hsl(25, 95%, 53%)',
    position: { x: 800, y: 50 },
  },
  {
    id: 'validation',
    label: 'L6: Validation',
    shortLabel: 'Validation',
    icon: Shield,
    category: 'layer',
    description: 'Citation check, hallucination detection, Self-RAG reflection',
    connections: ['response'],
    color: 'hsl(0, 72%, 51%)',
    position: { x: 950, y: 50 },
  },
  // Services
  {
    id: 'tier-resolver',
    label: 'Tier Resolver',
    icon: Users,
    category: 'service',
    description: 'Determines user tier and token budget',
    connections: [],
    color: 'hsl(200, 60%, 50%)',
    position: { x: 200, y: 180 },
  },
  {
    id: 'llm-router',
    label: 'LLM Router',
    icon: Cpu,
    category: 'service',
    description: 'Routes to optimal model based on strategy and tier',
    connections: [],
    color: 'hsl(200, 60%, 50%)',
    position: { x: 800, y: 180 },
  },
  {
    id: 't5-evaluator',
    label: 'T5 Evaluator',
    icon: Server,
    category: 'service',
    description: 'T5-Large model for relevance scoring (0 LLM tokens)',
    connections: [],
    color: 'hsl(200, 60%, 50%)',
    position: { x: 650, y: 180 },
  },
  {
    id: 'web-search',
    label: 'Web Search',
    icon: Globe,
    category: 'service',
    description: 'External search for ambiguous queries',
    connections: [],
    color: 'hsl(200, 60%, 50%)',
    position: { x: 730, y: 250 },
  },
  // Storage
  {
    id: 'redis',
    label: 'Redis Cache',
    icon: HardDrive,
    category: 'storage',
    description: 'In-memory cache for semantic similarity results',
    connections: [],
    color: 'hsl(10, 70%, 50%)',
    position: { x: 350, y: 180 },
  },
  {
    id: 'qdrant',
    label: 'Qdrant',
    icon: Database,
    category: 'storage',
    description: 'Vector database for document embeddings',
    connections: [],
    color: 'hsl(280, 60%, 50%)',
    position: { x: 450, y: 180 },
  },
  {
    id: 'postgres',
    label: 'PostgreSQL',
    icon: Database,
    category: 'storage',
    description: 'Metadata, user data, game catalog',
    connections: [],
    color: 'hsl(220, 60%, 50%)',
    position: { x: 550, y: 180 },
  },
  // Output
  {
    id: 'response',
    label: 'AI Response',
    icon: Sparkles,
    category: 'input',
    description: 'Validated answer with citations and confidence score',
    connections: [],
    color: 'hsl(142, 76%, 50%)',
    position: { x: 1100, y: 50 },
  },
];

// =============================================================================
// Node Component
// =============================================================================

interface NodeProps {
  node: ArchNode;
  isSelected: boolean;
  isFiltered: boolean;
  zoom: number;
  onClick: () => void;
}

function ArchitectureNode({ node, isSelected, isFiltered, zoom, onClick }: NodeProps) {
  const Icon = node.icon;
  const size = node.category === 'layer' ? 80 : 60;
  const scaledSize = size * zoom;

  return (
    <motion.div
      className={cn(
        'rag-node absolute cursor-pointer select-none',
        node.category === 'layer' && 'z-10',
        !isFiltered && 'opacity-30 pointer-events-none'
      )}
      style={{
        left: node.position.x * zoom,
        top: node.position.y * zoom,
        width: scaledSize,
        '--node-color': node.color,
      } as React.CSSProperties}
      data-selected={isSelected}
      onClick={onClick}
      whileHover={{ scale: 1.05 }}
      whileTap={{ scale: 0.98 }}
      initial={{ opacity: 0, scale: 0.8 }}
      animate={{ opacity: isFiltered ? 1 : 0.3, scale: 1 }}
      transition={{ duration: 0.2 }}
    >
      <div
        className={cn(
          'flex flex-col items-center justify-center gap-1 p-2 rounded-lg',
          'transition-all duration-200',
          isSelected && 'ring-2 ring-offset-2 ring-offset-background'
        )}
        style={{
          backgroundColor: isSelected ? `${node.color}20` : undefined,
          borderColor: isSelected ? node.color : undefined,
          ['--tw-ring-color' as string]: node.color,
        }}
      >
        <div
          className="rounded-full p-2"
          style={{ backgroundColor: `${node.color}20` }}
        >
          <span style={{ color: node.color }}>
            <Icon className="h-5 w-5" />
          </span>
        </div>
        <span className="text-xs font-medium text-center leading-tight">
          {node.shortLabel || node.label}
        </span>
      </div>
    </motion.div>
  );
}

// =============================================================================
// Node Detail Panel
// =============================================================================

interface DetailPanelProps {
  node: ArchNode | null;
  onClose: () => void;
}

function DetailPanel({ node, onClose }: DetailPanelProps) {
  if (!node) return null;

  const Icon = node.icon;
  const categoryColors = {
    input: 'bg-gray-500/10 text-gray-600',
    layer: 'bg-blue-500/10 text-blue-600',
    service: 'bg-purple-500/10 text-purple-600',
    storage: 'bg-orange-500/10 text-orange-600',
  };

  return (
    <motion.div
      initial={{ opacity: 0, x: 20 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: 20 }}
      className="absolute right-4 top-4 w-72 p-4 rounded-xl bg-card border border-border shadow-lg z-20"
    >
      <div className="flex items-start gap-3 mb-3">
        <div
          className="rounded-lg p-2"
          style={{ backgroundColor: `${node.color}20` }}
        >
          <span style={{ color: node.color }}>
            <Icon className="h-6 w-6" />
          </span>
        </div>
        <div className="flex-1">
          <h4 className="font-semibold">{node.label}</h4>
          <Badge className={cn('text-xs mt-1', categoryColors[node.category])}>
            {node.category}
          </Badge>
        </div>
        <button
          onClick={onClose}
          className="text-muted-foreground hover:text-foreground"
        >
          ×
        </button>
      </div>

      <p className="text-sm text-muted-foreground mb-4">{node.description}</p>

      {node.connections.length > 0 && (
        <div className="pt-3 border-t border-border">
          <div className="text-xs text-muted-foreground mb-2">Connects to:</div>
          <div className="flex flex-wrap gap-1">
            {node.connections.map(connId => {
              const connNode = ARCH_NODES.find(n => n.id === connId);
              return connNode ? (
                <Badge key={connId} variant="outline" className="text-xs">
                  {connNode.shortLabel || connNode.label}
                </Badge>
              ) : null;
            })}
          </div>
        </div>
      )}

      {node.category === 'layer' && (
        <Button
          variant="outline"
          size="sm"
          className="w-full mt-4"
          onClick={() => window.open(`#layer-${node.id}`, '_blank')}
        >
          <ExternalLink className="h-3 w-3 mr-2" />
          View Documentation
        </Button>
      )}
    </motion.div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface ArchitectureExplorerProps {
  className?: string;
}

export function ArchitectureExplorer({ className }: ArchitectureExplorerProps) {
  const [selectedNode, setSelectedNode] = useState<string | null>(null);
  const [filter, setFilter] = useState<ArchNode['category'] | 'all'>('all');
  const [zoom, setZoom] = useState(0.85);

  const selectedNodeData = useMemo(() => {
    return ARCH_NODES.find(n => n.id === selectedNode) || null;
  }, [selectedNode]);

  const categories: Array<{ value: ArchNode['category'] | 'all'; label: string }> = [
    { value: 'all', label: 'All' },
    { value: 'layer', label: 'Layers' },
    { value: 'service', label: 'Services' },
    { value: 'storage', label: 'Storage' },
  ];

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
          <CardTitle className="flex items-center gap-2">
            <Server className="h-5 w-5 text-primary" />
            Architecture Explorer
          </CardTitle>

          <div className="flex items-center gap-4">
            {/* Filter */}
            <div className="flex items-center gap-2">
              <Filter className="h-4 w-4 text-muted-foreground" />
              <div className="flex gap-1">
                {categories.map(cat => (
                  <button
                    key={cat.value}
                    onClick={() => setFilter(cat.value)}
                    className={cn(
                      'px-3 py-1 text-xs rounded-full transition-all',
                      filter === cat.value
                        ? 'bg-primary text-primary-foreground'
                        : 'bg-muted text-muted-foreground hover:bg-muted/80'
                    )}
                  >
                    {cat.label}
                  </button>
                ))}
              </div>
            </div>

            {/* Zoom */}
            <div className="flex items-center gap-2">
              <button
                onClick={() => setZoom(z => Math.max(0.5, z - 0.1))}
                className="p-1 rounded hover:bg-muted"
              >
                <ZoomOut className="h-4 w-4" />
              </button>
              <span className="text-xs font-mono w-12 text-center">
                {Math.round(zoom * 100)}%
              </span>
              <button
                onClick={() => setZoom(z => Math.min(1.5, z + 0.1))}
                className="p-1 rounded hover:bg-muted"
              >
                <ZoomIn className="h-4 w-4" />
              </button>
            </div>
          </div>
        </div>
      </CardHeader>

      <CardContent>
        <div
          className="relative overflow-auto rounded-lg bg-muted/30 border border-border"
          style={{ minHeight: 350, height: 350 * zoom }}
        >
          {/* Nodes */}
          {ARCH_NODES.map(node => (
            <ArchitectureNode
              key={node.id}
              node={node}
              isSelected={selectedNode === node.id}
              isFiltered={filter === 'all' || node.category === filter}
              zoom={zoom}
              onClick={() => setSelectedNode(selectedNode === node.id ? null : node.id)}
            />
          ))}

          {/* Connection Lines (simplified) */}
          <svg
            className="absolute inset-0 pointer-events-none"
            style={{ width: 1200 * zoom, height: 300 * zoom }}
          >
            {ARCH_NODES.flatMap(node =>
              node.connections.map(connId => {
                const target = ARCH_NODES.find(n => n.id === connId);
                if (!target) return null;

                const isActive =
                  (filter === 'all' || node.category === filter) &&
                  (filter === 'all' || target.category === filter);

                return (
                  <line
                    key={`${node.id}-${connId}`}
                    x1={(node.position.x + 40) * zoom}
                    y1={(node.position.y + 30) * zoom}
                    x2={target.position.x * zoom}
                    y2={(target.position.y + 30) * zoom}
                    stroke={isActive ? node.color : 'hsl(var(--border))'}
                    strokeWidth={isActive ? 2 : 1}
                    strokeOpacity={isActive ? 0.5 : 0.2}
                    strokeDasharray={target.category !== 'layer' ? '4 4' : undefined}
                  />
                );
              })
            )}
          </svg>

          {/* Detail Panel */}
          <AnimatePresence>
            {selectedNodeData && (
              <DetailPanel
                node={selectedNodeData}
                onClose={() => setSelectedNode(null)}
              />
            )}
          </AnimatePresence>
        </div>

        {/* Legend */}
        <div className="flex flex-wrap gap-4 mt-4 pt-4 border-t border-border text-sm">
          <div className="flex items-center gap-2">
            <div className="w-3 h-3 rounded-full bg-blue-500" />
            <span className="text-muted-foreground">RAG Layers</span>
          </div>
          <div className="flex items-center gap-2">
            <div className="w-3 h-3 rounded-full bg-purple-500" />
            <span className="text-muted-foreground">Services</span>
          </div>
          <div className="flex items-center gap-2">
            <div className="w-3 h-3 rounded-full bg-orange-500" />
            <span className="text-muted-foreground">Storage</span>
          </div>
          <div className="flex items-center gap-2 ml-auto text-xs text-muted-foreground">
            Click on a node for details
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
