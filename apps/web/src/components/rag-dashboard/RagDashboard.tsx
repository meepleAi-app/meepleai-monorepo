'use client';

/**
 * RagDashboard Component
 *
 * Main dashboard component assembling all RAG visualization sections.
 * Features dual-audience view switching (Technical/Business).
 *
 * Aesthetic: "Neural Gaming Interface" - sci-fi command center meets data visualization
 */

import React, { useState } from 'react';

import { motion } from 'framer-motion';
import { Dices, Code2, Briefcase, BookOpen, GitBranch, ExternalLink } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import { AgentRagIntegration } from './AgentRagIntegration';
import { AgentRoleConfigurator } from './AgentRoleConfigurator';
import { ArchitectureExplorer } from './ArchitectureExplorer';
import { LayerDeepDocs } from './LayerDeepDocs';
import { PromptTemplateBuilder } from './PromptTemplateBuilder';
import { VariantComparisonTool } from './VariantComparisonTool';
import { CostCalculator } from './CostCalculator';
import { DecisionWalkthrough } from './DecisionWalkthrough';
import { ModelSelectionOptimizer } from './ModelSelectionOptimizer';
import { PerformanceMetricsTable } from './PerformanceMetricsTable';
import { QuerySimulator } from './QuerySimulator';
import { StatsGrid } from './StatsGrid';
import { TokenFlowVisualizer } from './TokenFlowVisualizer';
import { DEFAULT_STATS } from './types';
import { METRICS } from './rag-data';

import type { ViewMode } from './types';

import './rag-dashboard.css';

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
}

function DashboardHeader({ viewMode, onViewModeChange }: HeaderProps) {
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

      <div className="relative z-10 py-8 px-6">
        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
          {/* Title */}
          <div className="flex items-center gap-4">
            <div className="relative">
              <div className="absolute inset-0 bg-primary/20 blur-xl rounded-full" />
              <div className="relative bg-gradient-to-br from-primary to-purple-600 p-3 rounded-2xl">
                <Dices className="h-8 w-8 text-white" />
              </div>
            </div>
            <div>
              <h1 className="text-2xl md:text-3xl font-quicksand font-bold">
                <span className="text-foreground">MeepleAI</span>{' '}
                <span className="bg-gradient-to-r from-primary via-purple-500 to-orange-500 bg-clip-text text-transparent">
                  RAG Strategy Dashboard
                </span>
              </h1>
              <p className="text-muted-foreground mt-1">
                Token-Optimized Modular Adaptive Corrective RAG (TOMAC-RAG)
              </p>
            </div>
          </div>

          {/* Actions */}
          <div className="flex items-center gap-4">
            <ViewToggle mode={viewMode} onChange={onViewModeChange} />

            <div className="hidden md:flex items-center gap-2">
              <Button variant="outline" size="sm" asChild>
                <a href="/docs/03-api/rag/README.md" target="_blank">
                  <BookOpen className="h-4 w-4 mr-2" />
                  Docs
                </a>
              </Button>
              <Button variant="outline" size="sm" asChild>
                <a href="https://github.com/meepleai" target="_blank" rel="noopener noreferrer">
                  <GitBranch className="h-4 w-4 mr-2" />
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
// Section Wrapper
// =============================================================================

interface SectionProps {
  title: string;
  description?: string;
  icon?: React.ReactNode;
  children: React.ReactNode;
  delay?: number;
}

function Section({ title, description, icon, children, delay = 0 }: SectionProps) {
  return (
    <motion.section
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay, duration: 0.4 }}
      className="space-y-4"
    >
      <div className="flex items-center gap-3">
        {icon && <div className="text-primary">{icon}</div>}
        <div>
          <h2 className="text-lg font-semibold font-quicksand">{title}</h2>
          {description && (
            <p className="text-sm text-muted-foreground">{description}</p>
          )}
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
    { label: 'Executive Summary', href: '#overview' },
    { label: 'Cost Projections', href: '#calculator' },
    { label: 'ROI Analysis', href: '#stats' },
    { label: 'Implementation Timeline', href: '#architecture' },
  ];

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      className="flex flex-wrap gap-2 mb-6"
    >
      {links.map(link => (
        <a
          key={link.label}
          href={link.href}
          className="px-4 py-2 rounded-lg bg-muted/50 text-sm hover:bg-muted transition-colors flex items-center gap-2"
        >
          {link.label}
          <ExternalLink className="h-3 w-3 opacity-50" />
        </a>
      ))}
    </motion.div>
  );
}

// =============================================================================
// Main Dashboard Component
// =============================================================================

export function RagDashboard() {
  const [viewMode, setViewMode] = useState<ViewMode>('technical');

  return (
    <div className="rag-dashboard min-h-screen">
      <div className="relative z-10">
        {/* Header */}
        <DashboardHeader viewMode={viewMode} onViewModeChange={setViewMode} />

        {/* Main Content */}
        <main className="max-w-7xl mx-auto px-4 md:px-6 py-8 space-y-12">
          {/* Quick Links for Business View */}
          {viewMode === 'business' && <QuickLinks />}

          {/* Stats Overview */}
          <Section
            title="System Overview"
            description={
              viewMode === 'technical'
                ? 'Key performance metrics for the TOMAC-RAG system'
                : 'Key business metrics and cost efficiency indicators'
            }
            delay={0.1}
          >
            <StatsGrid stats={DEFAULT_STATS} viewMode={viewMode} />
          </Section>

          {/* Main Grid */}
          <div className="grid lg:grid-cols-2 gap-8">
            {/* Query Simulator */}
            <Section
              title="Query Simulator"
              description="Test the routing system with sample queries"
              delay={0.2}
            >
              <QuerySimulator />
            </Section>

            {/* Token Flow */}
            <Section
              title="Token Flow"
              description="Visualize token consumption through each layer"
              delay={0.3}
            >
              <TokenFlowVisualizer />
            </Section>
          </div>

          {/* Agent-RAG Integration (Technical View) */}
          {viewMode === 'technical' && (
            <Section
              title="Agent-RAG Integration"
              description="How MeepleAI agents assemble prompts with RAG context"
              delay={0.35}
            >
              <AgentRagIntegration />
            </Section>
          )}

          {/* Variant Comparison Tool (Technical View) */}
          {viewMode === 'technical' && (
            <Section
              title={`${METRICS.totalVariants} RAG Variants`}
              description="Interactive comparison of all strategy × template × tier combinations"
              delay={0.38}
            >
              <VariantComparisonTool />
            </Section>
          )}

          {/* Cost Calculator */}
          <Section
            title="Cost Projection"
            description={
              viewMode === 'technical'
                ? 'Estimate monthly costs based on usage patterns'
                : 'Project ROI and cost savings with TOMAC-RAG optimization'
            }
            delay={0.4}
          >
            <CostCalculator />
          </Section>

          {/* Architecture (Technical View Only) */}
          {viewMode === 'technical' && (
            <Section
              title="Architecture Explorer"
              description="Interactive system architecture diagram"
              delay={0.5}
            >
              <ArchitectureExplorer />
            </Section>
          )}

          {/* Layer Deep Documentation (Technical View Only) */}
          {viewMode === 'technical' && (
            <Section
              title="Layer Documentation"
              description="Detailed technical docs with code examples and decision trees"
              delay={0.55}
            >
              <LayerDeepDocs />
            </Section>
          )}

          {/* Prompt Template Builder (Technical View Only) */}
          {viewMode === 'technical' && (
            <Section
              title="Prompt Builder"
              description="Interactive tool to assemble and preview RAG prompts"
              delay={0.6}
            >
              <PromptTemplateBuilder />
            </Section>
          )}

          {/* Agent Role Configurator (Technical View Only) */}
          {viewMode === 'technical' && (
            <Section
              title="Agent Roles"
              description="Pre-built agent configurations with system prompts"
              delay={0.65}
            >
              <AgentRoleConfigurator />
            </Section>
          )}

          {/* Model Selection Optimizer (Technical View Only) */}
          {viewMode === 'technical' && (
            <Section
              title="Model Selection"
              description="Optimize model choices based on cost, speed, and quality requirements"
              delay={0.68}
            >
              <ModelSelectionOptimizer />
            </Section>
          )}

          {/* Performance Metrics Table (Technical View Only) */}
          {viewMode === 'technical' && (
            <Section
              title="Performance Metrics"
              description="Real-time performance data across strategies and query types"
              delay={0.72}
            >
              <PerformanceMetricsTable />
            </Section>
          )}

          {/* Decision Walkthrough (Both Views) */}
          <Section
            title="Decision Walkthrough"
            description={
              viewMode === 'technical'
                ? 'Step-by-step visualization of RAG decision process'
                : 'See how queries are processed through the system'
            }
            delay={0.7}
          >
            <DecisionWalkthrough />
          </Section>

          {/* Business Summary (Business View Only) */}
          {viewMode === 'business' && (
            <Section
              title="Executive Summary"
              description="Key takeaways for stakeholders"
              delay={0.5}
            >
              <motion.div
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                className="grid md:grid-cols-2 lg:grid-cols-3 gap-6"
              >
                {/* Value Proposition */}
                <div className="p-6 rounded-xl bg-gradient-to-br from-green-500/10 to-green-500/5 border border-green-500/20">
                  <h3 className="font-semibold text-green-600 dark:text-green-400 mb-3">
                    Cost Efficiency
                  </h3>
                  <ul className="space-y-2 text-sm text-muted-foreground">
                    <li>• {Math.abs(METRICS.tokenReduction)}% token reduction vs naive RAG</li>
                    <li>• {METRICS.cacheHitRateTarget}% cache hit rate = instant responses</li>
                    <li>• Free tier models for 60-70% of queries</li>
                  </ul>
                </div>

                {/* Quality */}
                <div className="p-6 rounded-xl bg-gradient-to-br from-blue-500/10 to-blue-500/5 border border-blue-500/20">
                  <h3 className="font-semibold text-blue-600 dark:text-blue-400 mb-3">
                    Quality Assurance
                  </h3>
                  <ul className="space-y-2 text-sm text-muted-foreground">
                    <li>• {METRICS.accuracy.ruleLookup.target}% accuracy target for rules</li>
                    <li>• CRAG evaluation prevents hallucinations</li>
                    <li>• Self-RAG catches 15% errors automatically</li>
                  </ul>
                </div>

                {/* Scalability */}
                <div className="p-6 rounded-xl bg-gradient-to-br from-purple-500/10 to-purple-500/5 border border-purple-500/20">
                  <h3 className="font-semibold text-purple-600 dark:text-purple-400 mb-3">
                    Scalability
                  </h3>
                  <ul className="space-y-2 text-sm text-muted-foreground">
                    <li>• {METRICS.totalVariants} configurable RAG variants</li>
                    <li>• User tier-based resource allocation</li>
                    <li>• Adaptive strategy selection</li>
                  </ul>
                </div>
              </motion.div>
            </Section>
          )}
        </main>

        {/* Footer */}
        <footer className="mt-16 py-8 px-6 border-t border-border text-center text-sm text-muted-foreground">
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
