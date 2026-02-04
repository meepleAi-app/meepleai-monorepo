'use client';

/**
 * RagDashboard Component
 *
 * Main dashboard component assembling all RAG visualization sections.
 * Features dual-audience view switching (Technical/Business) and
 * User Journey navigation pattern.
 *
 * Aesthetic: "Neural Gaming Interface" - sci-fi command center meets data visualization
 */

import React, { useState, useMemo, useCallback } from 'react';

import { motion } from 'framer-motion';
import { Dices, Code2, Briefcase, BookOpen, GitBranch, ExternalLink, RotateCcw } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import { AgentRagIntegration } from './AgentRagIntegration';
import { AgentRoleConfigurator } from './AgentRoleConfigurator';
import { ArchitectureExplorer } from './ArchitectureExplorer';
import { Breadcrumbs } from './Breadcrumbs';
import { CostCalculator } from './CostCalculator';
import { DashboardNav } from './DashboardNav';
import { DashboardSidebar } from './DashboardSidebar';
import { DecisionWalkthrough } from './DecisionWalkthrough';
import { GlobalSearch } from './GlobalSearch';
import { useAccordionState } from './hooks/useAccordionState';
import { useScrollSpy } from './hooks/useScrollSpy';
import { ScrollProgressBar } from './ScrollProgressBar';
import { LayerDeepDocs } from './LayerDeepDocs';
import { ModelSelectionOptimizer } from './ModelSelectionOptimizer';
import { PerformanceMetricsTable } from './PerformanceMetricsTable';
import { PromptTemplateBuilder } from './PromptTemplateBuilder';
import { QuerySimulator } from './QuerySimulator';
import { METRICS } from './rag-data';
import { AccordionSection, SectionGroup } from './SectionGroup';
import { StatsGrid } from './StatsGrid';
import { TokenFlowVisualizer } from './TokenFlowVisualizer';
import { DEFAULT_STATS } from './types';
import { VariantComparisonTool } from './VariantComparisonTool';

import type { NavGroup } from './DashboardSidebar';
import type { ViewMode } from './types';

import './rag-dashboard.css';

// =============================================================================
// Navigation Configuration
// =============================================================================

export const NAVIGATION_GROUPS: NavGroup[] = [
  {
    id: 'understand',
    label: 'Understand',
    icon: '🎓',
    description: 'Learn how TOMAC-RAG works',
    sections: [
      { id: 'overview', label: 'System Overview' },
      { id: 'architecture', label: 'Architecture' },
      { id: 'layers', label: 'Layer Documentation' },
    ],
  },
  {
    id: 'explore',
    label: 'Explore',
    icon: '🔍',
    description: 'Test and visualize the system',
    sections: [
      { id: 'query-sim', label: 'Query Simulator' },
      { id: 'token-flow', label: 'Token Flow' },
      { id: 'walkthrough', label: 'Decision Walkthrough' },
    ],
  },
  {
    id: 'compare',
    label: 'Compare',
    icon: '⚖️',
    description: 'Compare strategies and performance',
    sections: [
      { id: 'variants', label: '31 RAG Variants' },
      { id: 'performance', label: 'Performance Metrics' },
    ],
  },
  {
    id: 'build',
    label: 'Build',
    icon: '🔨',
    description: 'Tools for implementation',
    sections: [
      { id: 'prompts', label: 'Prompt Builder' },
      { id: 'roles', label: 'Agent Roles' },
      { id: 'agent-integration', label: 'Agent Integration' },
    ],
  },
  {
    id: 'optimize',
    label: 'Optimize',
    icon: '💰',
    description: 'Cost and model optimization',
    sections: [
      { id: 'cost', label: 'Cost Calculator' },
      { id: 'model-optimizer', label: 'Model Selection' },
    ],
  },
];

// Get all section IDs for scroll spy
const getAllSectionIds = (groups: NavGroup[]): string[] =>
  groups.flatMap((g) => g.sections.map((s) => s.id));

// =============================================================================
// View Mode Toggle
// =============================================================================

interface ViewToggleProps {
  mode: ViewMode;
  onChange: (mode: ViewMode) => void;
}

function ViewToggle({ mode, onChange }: ViewToggleProps) {
  return (
    <div className="rag-view-toggle">
      <button
        className="rag-view-toggle-btn flex items-center gap-2"
        data-active={mode === 'technical'}
        onClick={() => onChange('technical')}
      >
        <Code2 className="h-4 w-4" />
        <span>Technical</span>
      </button>
      <button
        className="rag-view-toggle-btn flex items-center gap-2"
        data-active={mode === 'business'}
        onClick={() => onChange('business')}
      >
        <Briefcase className="h-4 w-4" />
        <span>Business</span>
      </button>
    </div>
  );
}

// =============================================================================
// Header Section
// =============================================================================

interface HeaderProps {
  viewMode: ViewMode;
  onViewModeChange: (mode: ViewMode) => void;
  onResetAccordion?: () => void;
  onOpenSection?: (sectionId: string) => void;
}

function DashboardHeader({ viewMode, onViewModeChange, onResetAccordion, onOpenSection }: HeaderProps) {
  return (
    <motion.header
      className="relative overflow-hidden"
      initial={{ opacity: 0, y: -20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5 }}
    >
      {/* Background gradient */}
      <div className="absolute inset-0 bg-gradient-to-r from-primary/10 via-purple-500/10 to-orange-500/10" />
      <div className="absolute inset-0 bg-gradient-to-b from-transparent to-background/80" />

      <div className="relative z-10 px-6 py-8">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
          {/* Title */}
          <div className="flex items-center gap-4">
            <div className="relative">
              <div className="absolute inset-0 rounded-full bg-primary/20 blur-xl" />
              <div className="relative rounded-2xl bg-gradient-to-br from-primary to-purple-600 p-3">
                <Dices className="h-8 w-8 text-white" />
              </div>
            </div>
            <div>
              <h1 className="font-quicksand text-2xl font-bold md:text-3xl">
                <span className="text-foreground">MeepleAI</span>{' '}
                <span className="bg-gradient-to-r from-primary via-purple-500 to-orange-500 bg-clip-text text-transparent">
                  RAG Strategy Dashboard
                </span>
              </h1>
              <p className="mt-1 text-muted-foreground">
                Token-Optimized Modular Adaptive Corrective RAG (TOMAC-RAG)
              </p>
            </div>
          </div>

          {/* Actions */}
          <div className="flex items-center gap-4">
            {/* Global Search */}
            <GlobalSearch
              viewMode={viewMode}
              onOpenSection={onOpenSection}
            />

            <ViewToggle mode={viewMode} onChange={onViewModeChange} />

            <div className="hidden items-center gap-2 md:flex">
              {onResetAccordion && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={onResetAccordion}
                  title="Reset section collapse state"
                >
                  <RotateCcw className="mr-2 h-4 w-4" />
                  Reset
                </Button>
              )}
              <Button variant="outline" size="sm" asChild>
                <a href="/docs/03-api/rag/README.md" target="_blank">
                  <BookOpen className="mr-2 h-4 w-4" />
                  Docs
                </a>
              </Button>
              <Button variant="outline" size="sm" asChild>
                <a href="https://github.com/meepleai" target="_blank" rel="noopener noreferrer">
                  <GitBranch className="mr-2 h-4 w-4" />
                  Source
                </a>
              </Button>
            </div>
          </div>
        </div>
      </div>
    </motion.header>
  );
}

// =============================================================================
// Default Open Sections (First section of each group)
// =============================================================================

// Default open sections: first section of each group for good UX
// These are open by default when the page loads (or localStorage is empty)
const DEFAULT_OPEN_SECTIONS = [
  // UNDERSTAND group
  'overview',
  'architecture',
  'layers',
  // EXPLORE group
  'query-sim',
  'token-flow',
  'walkthrough',
  // COMPARE group
  'variants',
  'performance',
  // BUILD group
  'prompts',
  'roles',
  'agent-integration',
  // OPTIMIZE group
  'cost',
  'model-optimizer',
  // BUSINESS view
  'executive-summary',
];

// =============================================================================
// Section Wrapper with Accordion Support
// =============================================================================

interface SectionProps {
  id: string;
  title: string;
  description?: string;
  icon?: React.ReactNode;
  children: React.ReactNode;
  /** Whether this section uses accordion behavior */
  accordion?: boolean;
  /** Whether the section is open (for accordion mode) */
  isOpen?: boolean;
  /** Toggle callback (for accordion mode) */
  onToggle?: () => void;
}

function Section({
  id,
  title,
  description,
  icon,
  children,
  accordion = false,
  isOpen = true,
  onToggle,
}: SectionProps) {
  // Accordion mode - use AccordionSection
  if (accordion && onToggle) {
    return (
      <AccordionSection
        id={id}
        title={title}
        description={description}
        icon={icon}
        isOpen={isOpen}
        onToggle={onToggle}
      >
        {children}
      </AccordionSection>
    );
  }

  // Non-accordion mode - render with motion animation
  return (
    <motion.section
      id={id}
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4 }}
      className="scroll-mt-24 space-y-4"
    >
      <div className="flex items-center gap-3">
        {icon && <div className="text-primary">{icon}</div>}
        <div>
          <h3 className="font-quicksand text-lg font-semibold">{title}</h3>
          {description && <p className="text-sm text-muted-foreground">{description}</p>}
        </div>
      </div>
      {children}
    </motion.section>
  );
}

// =============================================================================
// Quick Links (Business View)
// =============================================================================

function QuickLinks() {
  const links = [
    { label: 'Executive Summary', href: '#executive-summary' },
    { label: 'Cost Projections', href: '#cost' },
    { label: 'ROI Analysis', href: '#overview' },
    { label: 'Implementation Timeline', href: '#architecture' },
  ];

  return (
    <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="mb-6 flex flex-wrap gap-2">
      {links.map((link) => (
        <a
          key={link.label}
          href={link.href}
          className="flex items-center gap-2 rounded-lg bg-muted/50 px-4 py-2 text-sm transition-colors hover:bg-muted"
        >
          {link.label}
          <ExternalLink className="h-3 w-3 opacity-50" />
        </a>
      ))}
    </motion.div>
  );
}

// =============================================================================
// Executive Summary (Business View Only)
// =============================================================================

function ExecutiveSummary() {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="grid gap-6 md:grid-cols-2 lg:grid-cols-3"
    >
      {/* Value Proposition */}
      <div className="rounded-xl border border-green-500/20 bg-gradient-to-br from-green-500/10 to-green-500/5 p-6">
        <h3 className="mb-3 font-semibold text-green-600 dark:text-green-400">Cost Efficiency</h3>
        <ul className="space-y-2 text-sm text-muted-foreground">
          <li>• {Math.abs(METRICS.tokenReduction)}% token reduction vs naive RAG</li>
          <li>• {METRICS.cacheHitRateTarget}% cache hit rate = instant responses</li>
          <li>• Free tier models for 60-70% of queries</li>
        </ul>
      </div>

      {/* Quality */}
      <div className="rounded-xl border border-blue-500/20 bg-gradient-to-br from-blue-500/10 to-blue-500/5 p-6">
        <h3 className="mb-3 font-semibold text-blue-600 dark:text-blue-400">Quality Assurance</h3>
        <ul className="space-y-2 text-sm text-muted-foreground">
          <li>• {METRICS.accuracy.ruleLookup.target}% accuracy target for rules</li>
          <li>• CRAG evaluation prevents hallucinations</li>
          <li>• Self-RAG catches 15% errors automatically</li>
        </ul>
      </div>

      {/* Scalability */}
      <div className="rounded-xl border border-purple-500/20 bg-gradient-to-br from-purple-500/10 to-purple-500/5 p-6">
        <h3 className="mb-3 font-semibold text-purple-600 dark:text-purple-400">Scalability</h3>
        <ul className="space-y-2 text-sm text-muted-foreground">
          <li>• {METRICS.totalVariants} configurable RAG variants</li>
          <li>• User tier-based resource allocation</li>
          <li>• Adaptive strategy selection</li>
        </ul>
      </div>
    </motion.div>
  );
}

// =============================================================================
// Main Dashboard Component
// =============================================================================

export function RagDashboard() {
  const [viewMode, setViewMode] = useState<ViewMode>('technical');

  // Memoize section IDs to prevent unnecessary re-renders
  const allSectionIds = useMemo(() => getAllSectionIds(NAVIGATION_GROUPS), []);

  // Track active section with scroll spy
  const activeSection = useScrollSpy(allSectionIds);

  // Accordion state with localStorage persistence
  const {
    isOpen,
    toggleSection,
    resetToDefaults,
    openSection,
  } = useAccordionState({
    defaultOpen: DEFAULT_OPEN_SECTIONS,
  });

  // Memoized toggle callback factory
  const createToggle = useCallback(
    (sectionId: string) => () => toggleSection(sectionId),
    [toggleSection]
  );

  return (
    <div className="rag-dashboard min-h-screen">
      {/* Scroll Progress Bar */}
      <ScrollProgressBar />

      <div className="relative z-10">
        {/* Header */}
        <DashboardHeader
          viewMode={viewMode}
          onViewModeChange={setViewMode}
          onResetAccordion={resetToDefaults}
          onOpenSection={openSection}
        />

        {/* Mobile Navigation */}
        <DashboardNav groups={NAVIGATION_GROUPS} activeSection={activeSection} />

        {/* Main Content with Sidebar */}
        <div className="flex">
          {/* Desktop Sidebar */}
          <DashboardSidebar
            groups={NAVIGATION_GROUPS}
            activeSection={activeSection}
            showProgress={true}
          />

          {/* Content */}
          <main className="mx-auto max-w-5xl flex-1 space-y-16 px-4 py-8 md:px-6">
            {/* Breadcrumb Navigation */}
            <Breadcrumbs
              activeSection={activeSection}
              groups={NAVIGATION_GROUPS}
              className="mb-4"
            />

            {/* Quick Links for Business View */}
            {viewMode === 'business' && <QuickLinks />}

            {/* ============================================================ */}
            {/* UNDERSTAND Group                                             */}
            {/* ============================================================ */}
            <SectionGroup
              id="understand"
              label="Understand"
              icon="🎓"
              description="Learn how TOMAC-RAG works"
            >
              {/* System Overview */}
              <Section
                id="overview"
                title="System Overview"
                description={
                  viewMode === 'technical'
                    ? 'Key performance metrics for the TOMAC-RAG system'
                    : 'Key business metrics and cost efficiency indicators'
                }
                accordion
                isOpen={isOpen('overview')}
                onToggle={createToggle('overview')}
              >
                <StatsGrid stats={DEFAULT_STATS} viewMode={viewMode} />
              </Section>

              {/* Architecture (Technical View Only) */}
              {viewMode === 'technical' && (
                <Section
                  id="architecture"
                  title="Architecture Explorer"
                  description="Interactive system architecture diagram"
                  accordion
                  isOpen={isOpen('architecture')}
                  onToggle={createToggle('architecture')}
                >
                  <ArchitectureExplorer />
                </Section>
              )}

              {/* Layer Deep Documentation (Technical View Only) */}
              {viewMode === 'technical' && (
                <Section
                  id="layers"
                  title="Layer Documentation"
                  description="Detailed technical docs with code examples and decision trees"
                  accordion
                  isOpen={isOpen('layers')}
                  onToggle={createToggle('layers')}
                >
                  <LayerDeepDocs />
                </Section>
              )}
            </SectionGroup>

            {/* ============================================================ */}
            {/* EXPLORE Group                                                */}
            {/* ============================================================ */}
            <SectionGroup
              id="explore"
              label="Explore"
              icon="🔍"
              description="Test and visualize the system"
            >
              {/* Query Simulator */}
              <Section
                id="query-sim"
                title="Query Simulator"
                description="Test the routing system with sample queries"
                accordion
                isOpen={isOpen('query-sim')}
                onToggle={createToggle('query-sim')}
              >
                <QuerySimulator />
              </Section>

              {/* Token Flow */}
              <Section
                id="token-flow"
                title="Token Flow"
                description="Visualize token consumption through each layer"
                accordion
                isOpen={isOpen('token-flow')}
                onToggle={createToggle('token-flow')}
              >
                <TokenFlowVisualizer />
              </Section>

              {/* Decision Walkthrough */}
              <Section
                id="walkthrough"
                title="Decision Walkthrough"
                description={
                  viewMode === 'technical'
                    ? 'Step-by-step visualization of RAG decision process'
                    : 'See how queries are processed through the system'
                }
                accordion
                isOpen={isOpen('walkthrough')}
                onToggle={createToggle('walkthrough')}
              >
                <DecisionWalkthrough />
              </Section>
            </SectionGroup>

            {/* ============================================================ */}
            {/* COMPARE Group (Technical View Only)                          */}
            {/* ============================================================ */}
            {viewMode === 'technical' && (
              <SectionGroup
                id="compare"
                label="Compare"
                icon="⚖️"
                description="Compare strategies and performance"
              >
                {/* Variant Comparison Tool */}
                <Section
                  id="variants"
                  title={`${METRICS.totalVariants} RAG Variants`}
                  description="Interactive comparison of all strategy × template × tier combinations"
                  accordion
                  isOpen={isOpen('variants')}
                  onToggle={createToggle('variants')}
                >
                  <VariantComparisonTool />
                </Section>

                {/* Performance Metrics */}
                <Section
                  id="performance"
                  title="Performance Metrics"
                  description="Real-time performance data across strategies and query types"
                  accordion
                  isOpen={isOpen('performance')}
                  onToggle={createToggle('performance')}
                >
                  <PerformanceMetricsTable />
                </Section>
              </SectionGroup>
            )}

            {/* ============================================================ */}
            {/* BUILD Group (Technical View Only)                            */}
            {/* ============================================================ */}
            {viewMode === 'technical' && (
              <SectionGroup
                id="build"
                label="Build"
                icon="🔨"
                description="Tools for implementation"
              >
                {/* Prompt Builder */}
                <Section
                  id="prompts"
                  title="Prompt Builder"
                  description="Interactive tool to assemble and preview RAG prompts"
                  accordion
                  isOpen={isOpen('prompts')}
                  onToggle={createToggle('prompts')}
                >
                  <PromptTemplateBuilder />
                </Section>

                {/* Agent Roles */}
                <Section
                  id="roles"
                  title="Agent Roles"
                  description="Pre-built agent configurations with system prompts"
                  accordion
                  isOpen={isOpen('roles')}
                  onToggle={createToggle('roles')}
                >
                  <AgentRoleConfigurator />
                </Section>

                {/* Agent Integration */}
                <Section
                  id="agent-integration"
                  title="Agent-RAG Integration"
                  description="How MeepleAI agents assemble prompts with RAG context"
                  accordion
                  isOpen={isOpen('agent-integration')}
                  onToggle={createToggle('agent-integration')}
                >
                  <AgentRagIntegration />
                </Section>
              </SectionGroup>
            )}

            {/* ============================================================ */}
            {/* OPTIMIZE Group                                               */}
            {/* ============================================================ */}
            <SectionGroup
              id="optimize"
              label="Optimize"
              icon="💰"
              description="Cost and model optimization"
            >
              {/* Cost Calculator */}
              <Section
                id="cost"
                title="Cost Projection"
                description={
                  viewMode === 'technical'
                    ? 'Estimate monthly costs based on usage patterns'
                    : 'Project ROI and cost savings with TOMAC-RAG optimization'
                }
                accordion
                isOpen={isOpen('cost')}
                onToggle={createToggle('cost')}
              >
                <CostCalculator />
              </Section>

              {/* Model Selection (Technical View Only) */}
              {viewMode === 'technical' && (
                <Section
                  id="model-optimizer"
                  title="Model Selection"
                  description="Optimize model choices based on cost, speed, and quality requirements"
                  accordion
                  isOpen={isOpen('model-optimizer')}
                  onToggle={createToggle('model-optimizer')}
                >
                  <ModelSelectionOptimizer />
                </Section>
              )}
            </SectionGroup>

            {/* ============================================================ */}
            {/* Business Summary (Business View Only)                        */}
            {/* ============================================================ */}
            {viewMode === 'business' && (
              <Section
                id="executive-summary"
                title="Executive Summary"
                description="Key takeaways for stakeholders"
                accordion
                isOpen={isOpen('executive-summary')}
                onToggle={createToggle('executive-summary')}
              >
                <ExecutiveSummary />
              </Section>
            )}
          </main>
        </div>

        {/* Footer */}
        <footer className="mt-16 border-t border-border px-6 py-8 text-center text-sm text-muted-foreground">
          <p>
            MeepleAI RAG Dashboard • Built with Next.js 14 + shadcn/ui •{' '}
            <a
              href="https://github.com/meepleai"
              className="text-primary hover:underline"
              target="_blank"
              rel="noopener noreferrer"
            >
              View on GitHub
            </a>
          </p>
        </footer>
      </div>
    </div>
  );
}
