'use client';

/**
 * VariantComparisonTool Component
 *
 * Interactive comparison matrix for RAG variants.
 * Shows strategy × template combinations with model options.
 * NOTE: Anonymous users have NO ACCESS (31 variants, was 36).
 */

import React, { useState, useMemo } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Grid3X3,
  Filter,
  Zap,
  Scale,
  Target,
  ChevronDown,
  Search,
  Info,
  Check,
  X,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { RagStrategy, QueryTemplate, UserTier } from './types';

// =============================================================================
// Types
// =============================================================================

interface RagVariant {
  id: string;
  strategy: RagStrategy;
  template: QueryTemplate;
  tier: UserTier;
  model: string;
  avgTokens: number;
  costPer1K: number; // Cost per 1000 queries in dollars
  accuracyTarget: number;
  latencyMs: number;
  features: string[];
}

// =============================================================================
// Data - RAG Variants (31 total - Anonymous removed, NO ACCESS)
// =============================================================================

/**
 * NOTE: Anonymous users have NO ACCESS to the RAG system.
 * Authentication is required for all RAG operations.
 * All variants are for authenticated users only (User, Editor, Admin, Premium).
 */
const RAG_VARIANTS: RagVariant[] = [
  // FAST Strategy Variants (7) - Authenticated users only
  { id: 'fast-rule-user', strategy: 'FAST', template: 'rule_lookup', tier: 'User', model: 'Gemini 2.0 Flash', avgTokens: 2100, costPer1K: 0, accuracyTarget: 88, latencyMs: 750, features: ['Zero-cost', 'Context window'] },
  { id: 'fast-resource-user', strategy: 'FAST', template: 'resource_planning', tier: 'User', model: 'Gemini 2.0 Flash', avgTokens: 1900, costPer1K: 0, accuracyTarget: 85, latencyMs: 850, features: ['Zero-cost', 'Tables'] },
  { id: 'fast-setup-user', strategy: 'FAST', template: 'setup_guide', tier: 'User', model: 'Gemini 2.0 Flash', avgTokens: 2300, costPer1K: 0, accuracyTarget: 92, latencyMs: 800, features: ['Zero-cost', 'Images'] },
  { id: 'fast-strategy-user', strategy: 'FAST', template: 'strategy_advice', tier: 'User', model: 'Gemini 2.0 Flash', avgTokens: 2000, costPer1K: 0, accuracyTarget: 78, latencyMs: 900, features: ['Zero-cost', 'Scenarios'] },
  { id: 'fast-edu-user', strategy: 'FAST', template: 'educational', tier: 'User', model: 'Gemini 2.0 Flash', avgTokens: 1800, costPer1K: 0, accuracyTarget: 90, latencyMs: 750, features: ['Zero-cost', 'Examples'] },
  { id: 'fast-rule-editor', strategy: 'FAST', template: 'rule_lookup', tier: 'Editor', model: 'Gemini 2.0 Flash', avgTokens: 2400, costPer1K: 0, accuracyTarget: 90, latencyMs: 700, features: ['Zero-cost', 'Extended context'] },
  { id: 'fast-edu-editor', strategy: 'FAST', template: 'educational', tier: 'Editor', model: 'Gemini 2.0 Flash', avgTokens: 2200, costPer1K: 0, accuracyTarget: 92, latencyMs: 700, features: ['Zero-cost', 'Deep dive'] },

  // BALANCED Strategy Variants (12)
  { id: 'bal-rule-user', strategy: 'BALANCED', template: 'rule_lookup', tier: 'User', model: 'Claude 3.5 Sonnet', avgTokens: 3200, costPer1K: 0.15, accuracyTarget: 94, latencyMs: 1200, features: ['CRAG', 'Citations'] },
  { id: 'bal-rule-editor', strategy: 'BALANCED', template: 'rule_lookup', tier: 'Editor', model: 'Claude 3.5 Sonnet', avgTokens: 3500, costPer1K: 0.18, accuracyTarget: 95, latencyMs: 1100, features: ['CRAG', 'FAQ links'] },
  { id: 'bal-resource-user', strategy: 'BALANCED', template: 'resource_planning', tier: 'User', model: 'DeepSeek Chat', avgTokens: 2800, costPer1K: 0.02, accuracyTarget: 90, latencyMs: 1300, features: ['CRAG', 'Calculations'] },
  { id: 'bal-resource-editor', strategy: 'BALANCED', template: 'resource_planning', tier: 'Editor', model: 'Claude 3.5 Sonnet', avgTokens: 3100, costPer1K: 0.15, accuracyTarget: 92, latencyMs: 1200, features: ['CRAG', 'Scaling'] },
  { id: 'bal-setup-user', strategy: 'BALANCED', template: 'setup_guide', tier: 'User', model: 'Claude 3.5 Sonnet', avgTokens: 3400, costPer1K: 0.17, accuracyTarget: 96, latencyMs: 1150, features: ['CRAG', 'Variants'] },
  { id: 'bal-setup-editor', strategy: 'BALANCED', template: 'setup_guide', tier: 'Editor', model: 'Claude 3.5 Sonnet', avgTokens: 3700, costPer1K: 0.19, accuracyTarget: 97, latencyMs: 1100, features: ['CRAG', 'Video links'] },
  { id: 'bal-strategy-user', strategy: 'BALANCED', template: 'strategy_advice', tier: 'User', model: 'DeepSeek Chat', avgTokens: 3000, costPer1K: 0.02, accuracyTarget: 85, latencyMs: 1400, features: ['CRAG', 'Analysis'] },
  { id: 'bal-strategy-editor', strategy: 'BALANCED', template: 'strategy_advice', tier: 'Editor', model: 'Claude 3.5 Sonnet', avgTokens: 3300, costPer1K: 0.16, accuracyTarget: 88, latencyMs: 1250, features: ['CRAG', 'Positions'] },
  { id: 'bal-edu-user', strategy: 'BALANCED', template: 'educational', tier: 'User', model: 'DeepSeek Chat', avgTokens: 2600, costPer1K: 0.02, accuracyTarget: 92, latencyMs: 1200, features: ['CRAG', 'Guides'] },
  { id: 'bal-edu-editor', strategy: 'BALANCED', template: 'educational', tier: 'Editor', model: 'Claude 3.5 Sonnet', avgTokens: 2900, costPer1K: 0.14, accuracyTarget: 94, latencyMs: 1100, features: ['CRAG', 'Tutorials'] },
  { id: 'bal-rule-admin', strategy: 'BALANCED', template: 'rule_lookup', tier: 'Admin', model: 'Claude 3.5 Sonnet', avgTokens: 3800, costPer1K: 0.20, accuracyTarget: 96, latencyMs: 1000, features: ['CRAG', 'Full audit'] },
  { id: 'bal-setup-admin', strategy: 'BALANCED', template: 'setup_guide', tier: 'Admin', model: 'Claude 3.5 Sonnet', avgTokens: 4000, costPer1K: 0.21, accuracyTarget: 98, latencyMs: 1000, features: ['CRAG', 'All variants'] },

  // PRECISE Strategy Variants (12)
  { id: 'prec-rule-editor', strategy: 'PRECISE', template: 'rule_lookup', tier: 'Editor', model: 'Claude 3.5 Opus', avgTokens: 8500, costPer1K: 1.50, accuracyTarget: 98, latencyMs: 2500, features: ['Multi-agent', 'Self-RAG', 'Web search'] },
  { id: 'prec-rule-admin', strategy: 'PRECISE', template: 'rule_lookup', tier: 'Admin', model: 'Claude 3.5 Opus', avgTokens: 9200, costPer1K: 1.65, accuracyTarget: 99, latencyMs: 2400, features: ['Multi-agent', 'Self-RAG', 'Errata check'] },
  { id: 'prec-resource-editor', strategy: 'PRECISE', template: 'resource_planning', tier: 'Editor', model: 'GPT-4o', avgTokens: 7800, costPer1K: 0.85, accuracyTarget: 95, latencyMs: 2800, features: ['Multi-agent', 'Optimization'] },
  { id: 'prec-resource-admin', strategy: 'PRECISE', template: 'resource_planning', tier: 'Admin', model: 'Claude 3.5 Opus', avgTokens: 8500, costPer1K: 1.50, accuracyTarget: 97, latencyMs: 2600, features: ['Multi-agent', 'Full planning'] },
  { id: 'prec-setup-editor', strategy: 'PRECISE', template: 'setup_guide', tier: 'Editor', model: 'GPT-4o', avgTokens: 9000, costPer1K: 0.95, accuracyTarget: 98, latencyMs: 2700, features: ['Multi-agent', 'Visual aids'] },
  { id: 'prec-setup-admin', strategy: 'PRECISE', template: 'setup_guide', tier: 'Admin', model: 'Claude 3.5 Opus', avgTokens: 9800, costPer1K: 1.75, accuracyTarget: 99, latencyMs: 2500, features: ['Multi-agent', 'All expansions'] },
  { id: 'prec-strategy-editor', strategy: 'PRECISE', template: 'strategy_advice', tier: 'Editor', model: 'Claude 3.5 Opus', avgTokens: 12000, costPer1K: 2.10, accuracyTarget: 92, latencyMs: 3500, features: ['Multi-agent', 'Deep analysis'] },
  { id: 'prec-strategy-admin', strategy: 'PRECISE', template: 'strategy_advice', tier: 'Admin', model: 'Claude 3.5 Opus', avgTokens: 13500, costPer1K: 2.40, accuracyTarget: 94, latencyMs: 3200, features: ['Multi-agent', 'Simulations'] },
  { id: 'prec-strategy-premium', strategy: 'PRECISE', template: 'strategy_advice', tier: 'Premium', model: 'Claude 3.5 Opus', avgTokens: 15000, costPer1K: 2.70, accuracyTarget: 96, latencyMs: 3000, features: ['Multi-agent', 'Expert mode'] },
  { id: 'prec-edu-editor', strategy: 'PRECISE', template: 'educational', tier: 'Editor', model: 'GPT-4o', avgTokens: 7500, costPer1K: 0.80, accuracyTarget: 96, latencyMs: 2400, features: ['Multi-agent', 'Curriculum'] },
  { id: 'prec-edu-admin', strategy: 'PRECISE', template: 'educational', tier: 'Admin', model: 'Claude 3.5 Opus', avgTokens: 8200, costPer1K: 1.45, accuracyTarget: 98, latencyMs: 2300, features: ['Multi-agent', 'Full course'] },
  { id: 'prec-rule-premium', strategy: 'PRECISE', template: 'rule_lookup', tier: 'Premium', model: 'Claude 3.5 Opus', avgTokens: 10000, costPer1K: 1.80, accuracyTarget: 99.5, latencyMs: 2200, features: ['Multi-agent', 'Self-RAG', 'Legal precision'] },
];

const STRATEGY_CONFIG: Record<RagStrategy, { color: string; icon: React.ReactNode; description: string }> = {
  FAST: { color: 'hsl(142 76% 36%)', icon: <Zap className="h-4 w-4" />, description: 'Zero-cost models, minimal processing' },
  BALANCED: { color: 'hsl(221 83% 53%)', icon: <Scale className="h-4 w-4" />, description: 'CRAG evaluation, hybrid search' },
  PRECISE: { color: 'hsl(262 83% 62%)', icon: <Target className="h-4 w-4" />, description: 'Multi-agent, Self-RAG, web augmentation' },
  EXPERT: { color: 'hsl(25 95% 53%)', icon: <Zap className="h-4 w-4" />, description: 'Web search + multi-hop reasoning' },
  CONSENSUS: { color: 'hsl(330 80% 60%)', icon: <Scale className="h-4 w-4" />, description: 'Multi-LLM voting (3 voters)' },
  CUSTOM: { color: 'hsl(0 0% 45%)', icon: <Target className="h-4 w-4" />, description: 'Admin-configured phases' },
};

const TEMPLATE_LABELS: Record<QueryTemplate, string> = {
  rule_lookup: 'Rules',
  resource_planning: 'Resources',
  setup_guide: 'Setup',
  strategy_advice: 'Strategy',
  educational: 'Learning',
};

// NOTE: Anonymous is excluded - NO ACCESS to RAG system (authentication required)
const TIER_ORDER: UserTier[] = ['User', 'Editor', 'Admin', 'Premium'];

// =============================================================================
// Sub-Components
// =============================================================================

interface FilterPillProps {
  label: string;
  active: boolean;
  onClick: () => void;
  color?: string;
  icon?: React.ReactNode;
}

function FilterPill({ label, active, onClick, color, icon }: FilterPillProps) {
  return (
    <button
      className={cn(
        'px-3 py-1.5 rounded-full text-xs font-medium transition-all flex items-center gap-1.5',
        'border hover:border-primary/50',
        active
          ? 'border-primary bg-primary/10 text-primary'
          : 'border-border bg-muted/30 text-muted-foreground'
      )}
      onClick={onClick}
      style={active && color ? { borderColor: color, backgroundColor: `${color}20`, color } : undefined}
    >
      {icon}
      {label}
    </button>
  );
}

interface VariantCardProps {
  variant: RagVariant;
  isSelected: boolean;
  onClick: () => void;
}

function VariantCard({ variant, isSelected, onClick }: VariantCardProps) {
  const strategyConfig = STRATEGY_CONFIG[variant.strategy];

  return (
    <motion.div
      className={cn(
        'p-3 rounded-xl border cursor-pointer transition-all',
        'hover:border-primary/50 hover:shadow-lg',
        isSelected && 'ring-2 ring-primary border-primary'
      )}
      onClick={onClick}
      whileHover={{ scale: 1.02 }}
      whileTap={{ scale: 0.98 }}
      layout
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-2">
          <div
            className="p-1 rounded"
            style={{ backgroundColor: `${strategyConfig.color}20`, color: strategyConfig.color }}
          >
            {strategyConfig.icon}
          </div>
          <span className="text-xs font-mono text-muted-foreground">{variant.tier}</span>
        </div>
        <span
          className="text-xs font-semibold px-2 py-0.5 rounded-full"
          style={{ backgroundColor: `${strategyConfig.color}20`, color: strategyConfig.color }}
        >
          {variant.strategy}
        </span>
      </div>

      {/* Template */}
      <div className="font-semibold text-sm mb-2">
        {TEMPLATE_LABELS[variant.template]}
      </div>

      {/* Model */}
      <div className="text-xs text-muted-foreground mb-3">
        {variant.model}
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 gap-2 text-xs">
        <div className="p-2 rounded-lg bg-muted/50">
          <div className="text-muted-foreground">Tokens</div>
          <div className="font-mono font-semibold">{variant.avgTokens.toLocaleString()}</div>
        </div>
        <div className="p-2 rounded-lg bg-muted/50">
          <div className="text-muted-foreground">$/1K</div>
          <div className="font-mono font-semibold">
            {variant.costPer1K === 0 ? 'Free' : `$${variant.costPer1K.toFixed(2)}`}
          </div>
        </div>
        <div className="p-2 rounded-lg bg-muted/50">
          <div className="text-muted-foreground">Accuracy</div>
          <div className="font-mono font-semibold">{variant.accuracyTarget}%</div>
        </div>
        <div className="p-2 rounded-lg bg-muted/50">
          <div className="text-muted-foreground">Latency</div>
          <div className="font-mono font-semibold">{variant.latencyMs}ms</div>
        </div>
      </div>

      {/* Features */}
      <div className="flex flex-wrap gap-1 mt-3">
        {variant.features.map(feature => (
          <span
            key={feature}
            className="text-[10px] px-1.5 py-0.5 rounded bg-muted text-muted-foreground"
          >
            {feature}
          </span>
        ))}
      </div>
    </motion.div>
  );
}

interface ComparisonPanelProps {
  selected: RagVariant[];
  onRemove: (id: string) => void;
}

function ComparisonPanel({ selected, onRemove }: ComparisonPanelProps) {
  if (selected.length === 0) return null;

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="mt-6 p-4 rounded-xl bg-gradient-to-br from-primary/10 to-primary/5 border border-primary/20"
    >
      <div className="flex items-center justify-between mb-4">
        <h4 className="font-semibold flex items-center gap-2">
          <Info className="h-4 w-4" />
          Comparing {selected.length} Variants
        </h4>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border">
              <th className="text-left py-2 px-2 font-medium text-muted-foreground">Variant</th>
              <th className="text-center py-2 px-2 font-medium text-muted-foreground">Strategy</th>
              <th className="text-center py-2 px-2 font-medium text-muted-foreground">Template</th>
              <th className="text-center py-2 px-2 font-medium text-muted-foreground">Tier</th>
              <th className="text-right py-2 px-2 font-medium text-muted-foreground">Tokens</th>
              <th className="text-right py-2 px-2 font-medium text-muted-foreground">Cost/1K</th>
              <th className="text-right py-2 px-2 font-medium text-muted-foreground">Accuracy</th>
              <th className="text-right py-2 px-2 font-medium text-muted-foreground">Latency</th>
              <th className="text-center py-2 px-2"></th>
            </tr>
          </thead>
          <tbody>
            {selected.map(variant => (
              <tr key={variant.id} className="border-b border-border/50 hover:bg-muted/30">
                <td className="py-2 px-2 font-mono text-xs">{variant.model}</td>
                <td className="py-2 px-2 text-center">
                  <span
                    className="text-xs font-semibold px-2 py-0.5 rounded-full"
                    style={{
                      backgroundColor: `${STRATEGY_CONFIG[variant.strategy].color}20`,
                      color: STRATEGY_CONFIG[variant.strategy].color,
                    }}
                  >
                    {variant.strategy}
                  </span>
                </td>
                <td className="py-2 px-2 text-center text-xs">{TEMPLATE_LABELS[variant.template]}</td>
                <td className="py-2 px-2 text-center text-xs">{variant.tier}</td>
                <td className="py-2 px-2 text-right font-mono">{variant.avgTokens.toLocaleString()}</td>
                <td className="py-2 px-2 text-right font-mono">
                  {variant.costPer1K === 0 ? (
                    <span className="text-green-500">Free</span>
                  ) : (
                    `$${variant.costPer1K.toFixed(2)}`
                  )}
                </td>
                <td className="py-2 px-2 text-right font-mono">{variant.accuracyTarget}%</td>
                <td className="py-2 px-2 text-right font-mono">{variant.latencyMs}ms</td>
                <td className="py-2 px-2 text-center">
                  <button
                    onClick={() => onRemove(variant.id)}
                    className="p-1 rounded hover:bg-red-500/20 text-red-500"
                  >
                    <X className="h-3 w-3" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Summary */}
      {selected.length >= 2 && (
        <div className="mt-4 grid grid-cols-4 gap-4 text-center">
          <div className="p-2 rounded-lg bg-muted/50">
            <div className="text-xs text-muted-foreground">Avg Tokens</div>
            <div className="font-mono font-semibold">
              {Math.round(selected.reduce((sum, v) => sum + v.avgTokens, 0) / selected.length).toLocaleString()}
            </div>
          </div>
          <div className="p-2 rounded-lg bg-muted/50">
            <div className="text-xs text-muted-foreground">Avg Cost</div>
            <div className="font-mono font-semibold">
              ${(selected.reduce((sum, v) => sum + v.costPer1K, 0) / selected.length).toFixed(2)}
            </div>
          </div>
          <div className="p-2 rounded-lg bg-muted/50">
            <div className="text-xs text-muted-foreground">Avg Accuracy</div>
            <div className="font-mono font-semibold">
              {Math.round(selected.reduce((sum, v) => sum + v.accuracyTarget, 0) / selected.length)}%
            </div>
          </div>
          <div className="p-2 rounded-lg bg-muted/50">
            <div className="text-xs text-muted-foreground">Avg Latency</div>
            <div className="font-mono font-semibold">
              {Math.round(selected.reduce((sum, v) => sum + v.latencyMs, 0) / selected.length)}ms
            </div>
          </div>
        </div>
      )}
    </motion.div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface VariantComparisonToolProps {
  className?: string;
}

export function VariantComparisonTool({ className }: VariantComparisonToolProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedStrategies, setSelectedStrategies] = useState<RagStrategy[]>(['FAST', 'BALANCED', 'PRECISE']);
  const [selectedTemplates, setSelectedTemplates] = useState<QueryTemplate[]>([
    'rule_lookup',
    'resource_planning',
    'setup_guide',
    'strategy_advice',
    'educational',
  ]);
  const [selectedTiers, setSelectedTiers] = useState<UserTier[]>(TIER_ORDER);
  const [showFilters, setShowFilters] = useState(false);
  const [comparedVariants, setComparedVariants] = useState<string[]>([]);

  // Filter variants
  const filteredVariants = useMemo(() => {
    return RAG_VARIANTS.filter(variant => {
      // Strategy filter
      if (!selectedStrategies.includes(variant.strategy)) return false;

      // Template filter
      if (!selectedTemplates.includes(variant.template)) return false;

      // Tier filter
      if (!selectedTiers.includes(variant.tier)) return false;

      // Search filter
      if (searchQuery) {
        const query = searchQuery.toLowerCase();
        const searchable = [
          variant.model,
          variant.strategy,
          variant.template,
          variant.tier,
          ...variant.features,
        ]
          .join(' ')
          .toLowerCase();
        if (!searchable.includes(query)) return false;
      }

      return true;
    });
  }, [selectedStrategies, selectedTemplates, selectedTiers, searchQuery]);

  // Get selected variant objects
  const selectedVariantObjects = useMemo(() => {
    return RAG_VARIANTS.filter(v => comparedVariants.includes(v.id));
  }, [comparedVariants]);

  const toggleStrategy = (strategy: RagStrategy) => {
    setSelectedStrategies(prev =>
      prev.includes(strategy) ? prev.filter(s => s !== strategy) : [...prev, strategy]
    );
  };

  const toggleTemplate = (template: QueryTemplate) => {
    setSelectedTemplates(prev =>
      prev.includes(template) ? prev.filter(t => t !== template) : [...prev, template]
    );
  };

  const toggleTier = (tier: UserTier) => {
    setSelectedTiers(prev => (prev.includes(tier) ? prev.filter(t => t !== tier) : [...prev, tier]));
  };

  const toggleVariantComparison = (id: string) => {
    setComparedVariants(prev =>
      prev.includes(id) ? prev.filter(v => v !== id) : prev.length < 5 ? [...prev, id] : prev
    );
  };

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2">
            <Grid3X3 className="h-5 w-5 text-primary" />
            {RAG_VARIANTS.length} RAG Variant Comparison
          </CardTitle>
          <Button variant="outline" size="sm" onClick={() => setShowFilters(!showFilters)}>
            <Filter className="h-4 w-4 mr-2" />
            Filters
            <ChevronDown className={cn('h-4 w-4 ml-2 transition-transform', showFilters && 'rotate-180')} />
          </Button>
        </div>
        <p className="text-sm text-muted-foreground mt-1">
          Click variants to compare. Showing {filteredVariants.length} of {RAG_VARIANTS.length} variants.
        </p>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Search */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <input
            type="text"
            placeholder="Search by model, strategy, template, or feature..."
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            className="w-full pl-10 pr-4 py-2 rounded-lg bg-muted/50 border border-border focus:border-primary focus:ring-1 focus:ring-primary text-sm"
          />
        </div>

        {/* Filters */}
        <AnimatePresence>
          {showFilters && (
            <motion.div
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: 'auto', opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
              className="space-y-4 overflow-hidden"
            >
              {/* Strategy Filters */}
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-2">Strategy</div>
                <div className="flex flex-wrap gap-2">
                  {(['FAST', 'BALANCED', 'PRECISE'] as RagStrategy[]).map(strategy => (
                    <FilterPill
                      key={strategy}
                      label={strategy}
                      active={selectedStrategies.includes(strategy)}
                      onClick={() => toggleStrategy(strategy)}
                      color={STRATEGY_CONFIG[strategy].color}
                      icon={STRATEGY_CONFIG[strategy].icon}
                    />
                  ))}
                </div>
              </div>

              {/* Template Filters */}
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-2">Template</div>
                <div className="flex flex-wrap gap-2">
                  {(Object.keys(TEMPLATE_LABELS) as QueryTemplate[]).map(template => (
                    <FilterPill
                      key={template}
                      label={TEMPLATE_LABELS[template]}
                      active={selectedTemplates.includes(template)}
                      onClick={() => toggleTemplate(template)}
                    />
                  ))}
                </div>
              </div>

              {/* Tier Filters */}
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-2">User Tier</div>
                <div className="flex flex-wrap gap-2">
                  {TIER_ORDER.map(tier => (
                    <FilterPill
                      key={tier}
                      label={tier}
                      active={selectedTiers.includes(tier)}
                      onClick={() => toggleTier(tier)}
                    />
                  ))}
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        {/* Quick Stats */}
        <div className="grid grid-cols-3 gap-4">
          {(['FAST', 'BALANCED', 'PRECISE'] as RagStrategy[]).map(strategy => {
            const variants = filteredVariants.filter(v => v.strategy === strategy);
            return (
              <div
                key={strategy}
                className="p-3 rounded-xl border"
                style={{ borderColor: `${STRATEGY_CONFIG[strategy].color}40` }}
              >
                <div className="flex items-center gap-2 mb-2">
                  <span style={{ color: STRATEGY_CONFIG[strategy].color }}>
                    {STRATEGY_CONFIG[strategy].icon}
                  </span>
                  <span className="font-semibold text-sm">{strategy}</span>
                </div>
                <div className="text-xs text-muted-foreground">
                  {variants.length} variants
                  {variants.length > 0 && (
                    <span className="ml-2">
                      ({variants.filter(v => v.costPer1K === 0).length} free)
                    </span>
                  )}
                </div>
              </div>
            );
          })}
        </div>

        {/* Variant Grid */}
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          <AnimatePresence mode="popLayout">
            {filteredVariants.map(variant => (
              <VariantCard
                key={variant.id}
                variant={variant}
                isSelected={comparedVariants.includes(variant.id)}
                onClick={() => toggleVariantComparison(variant.id)}
              />
            ))}
          </AnimatePresence>
        </div>

        {filteredVariants.length === 0 && (
          <div className="text-center py-12 text-muted-foreground">
            No variants match your filters. Try adjusting the filters above.
          </div>
        )}

        {/* Comparison Panel */}
        <ComparisonPanel
          selected={selectedVariantObjects}
          onRemove={id => setComparedVariants(prev => prev.filter(v => v !== id))}
        />

        {/* Selection hint */}
        {comparedVariants.length > 0 && comparedVariants.length < 5 && (
          <div className="text-center text-sm text-muted-foreground">
            <Check className="h-4 w-4 inline mr-1" />
            {comparedVariants.length}/5 variants selected for comparison
          </div>
        )}
      </CardContent>
    </Card>
  );
}
