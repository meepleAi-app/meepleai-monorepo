'use client';

/**
 * RagDashboard Component - Compact Tabbed Layout
 *
 * Consolidated dashboard with tabbed navigation to reduce page length.
 * All 12+ sections grouped into 5 main tabs.
 *
 * Aesthetic: "Neural Command Center" - compact, efficient, sci-fi
 */

import React from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { Bot, ChevronRight, Cpu, LineChart, Workflow, Zap } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { cn } from '@/lib/utils';

import { AgentRagIntegration } from './AgentRagIntegration';
import { AgentRoleConfigurator } from './AgentRoleConfigurator';
import { ArchitectureExplorer } from './ArchitectureExplorer';
import { BusinessKpiCards } from './BusinessKpiCards';
import { BusinessRoiCalculator } from './BusinessRoiCalculator';
import { BusinessStrategyComparison } from './BusinessStrategyComparison';
import { BusinessUseCases } from './BusinessUseCases';
import { CostCalculator } from './CostCalculator';
import { DecisionWalkthrough } from './DecisionWalkthrough';
import { LayerDeepDocs } from './LayerDeepDocs';
import { ModelSelectionOptimizer } from './ModelSelectionOptimizer';
import { ParameterGuide } from './ParameterGuide';
import { PerformanceMetricsTable } from './PerformanceMetricsTable';
import { PocStatus } from './PocStatus';
import { PromptTemplateBuilder } from './PromptTemplateBuilder';
import { QuerySimulator } from './QuerySimulator';
import { METRICS } from './rag-data';
import { RagHero } from './RagHero';
import { StatsGrid } from './StatsGrid';
import { TechnicalReference } from './TechnicalReference';
import { TokenFlowVisualizer } from './TokenFlowVisualizer';
import { DEFAULT_STATS } from './types';
import { VariantComparisonTool } from './VariantComparisonTool';

import type { NavGroup } from './DashboardSidebar';
import type { ViewMode } from './types';

import './rag-dashboard.css';

// =============================================================================
// Types
// =============================================================================

type TechnicalTab = 'overview' | 'architecture' | 'agents' | 'performance' | 'walkthrough';

interface TabConfig {
  id: TechnicalTab;
  label: string;
  icon: React.ReactNode;
  description: string;
}

// =============================================================================
// Tab Configuration
// =============================================================================

const TECHNICAL_TABS: TabConfig[] = [
  {
    id: 'overview',
    label: 'Overview',
    icon: <Zap className="h-4 w-4" />,
    description: 'POC Status, Stats, Query Simulator',
  },
  {
    id: 'architecture',
    label: 'Architecture',
    icon: <Cpu className="h-4 w-4" />,
    description: 'System Design, Layers, Technical Reference',
  },
  {
    id: 'agents',
    label: 'Agents & Prompts',
    icon: <Bot className="h-4 w-4" />,
    description: 'Agent Integration, Roles, Prompt Builder',
  },
  {
    id: 'performance',
    label: 'Cost & Metrics',
    icon: <LineChart className="h-4 w-4" />,
    description: 'Cost Calculator, Model Selection, Variants',
  },
  {
    id: 'walkthrough',
    label: 'Walkthrough',
    icon: <Workflow className="h-4 w-4" />,
    description: 'Decision Flow Visualization',
  },
];

// =============================================================================
// Navigation Groups (for Breadcrumbs and legacy sidebar compatibility)
// =============================================================================

/**
 * Navigation groups derived from TECHNICAL_TABS for components that need
 * the NavGroup[] structure (e.g., Breadcrumbs, DashboardNav).
 */
export const NAVIGATION_GROUPS: NavGroup[] = TECHNICAL_TABS.map(tab => ({
  id: tab.id,
  label: tab.label,
  icon: tab.id, // Icon identifier string
  description: tab.description,
  sections: [{ id: tab.id, label: tab.label }], // Single section per tab in new design
}));

// =============================================================================
// Tab Navigation Sidebar
// =============================================================================

interface TabNavProps {
  activeTab: TechnicalTab;
  onTabChange: (tab: TechnicalTab) => void;
}

function TabNavSidebar({ activeTab, onTabChange }: TabNavProps) {
  return (
    <nav className="hidden lg:block w-56 shrink-0 border-r border-border/50 pr-4">
      <div className="sticky top-4 space-y-1">
        {TECHNICAL_TABS.map(tab => (
          <button
            key={tab.id}
            onClick={() => onTabChange(tab.id)}
            className={cn(
              'w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-left transition-all',
              'hover:bg-muted/50',
              activeTab === tab.id
                ? 'bg-primary/10 text-primary border-l-2 border-primary'
                : 'text-muted-foreground'
            )}
          >
            <span
              className={cn(
                'p-1.5 rounded-md',
                activeTab === tab.id ? 'bg-primary/20' : 'bg-muted/50'
              )}
            >
              {tab.icon}
            </span>
            <div className="flex-1 min-w-0">
              <div className="font-medium text-sm">{tab.label}</div>
              <div className="text-xs text-muted-foreground truncate">{tab.description}</div>
            </div>
            {activeTab === tab.id && <ChevronRight className="h-4 w-4 text-primary" />}
          </button>
        ))}
      </div>
    </nav>
  );
}

// =============================================================================
// Mobile Tab Bar
// =============================================================================

interface MobileTabBarProps {
  activeTab: TechnicalTab;
  onTabChange: (tab: TechnicalTab) => void;
}

function MobileTabBar({ activeTab, onTabChange }: MobileTabBarProps) {
  return (
    <div className="lg:hidden overflow-x-auto pb-2 mb-4 -mx-4 px-4">
      <div className="flex gap-2 min-w-max">
        {TECHNICAL_TABS.map(tab => (
          <button
            key={tab.id}
            onClick={() => onTabChange(tab.id)}
            className={cn(
              'flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium whitespace-nowrap transition-all',
              activeTab === tab.id
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted/50 text-muted-foreground hover:bg-muted'
            )}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>
    </div>
  );
}

// =============================================================================
// Section Wrapper (Simplified)
// =============================================================================

interface SectionProps {
  title: string;
  description?: string;
  children: React.ReactNode;
  className?: string;
}

function Section({ title, description, children, className }: SectionProps) {
  return (
    <div className={cn('space-y-4', className)}>
      <div>
        <h2 className="text-lg font-semibold font-quicksand">{title}</h2>
        {description && <p className="text-sm text-muted-foreground">{description}</p>}
      </div>
      {children}
    </div>
  );
}

// =============================================================================
// Technical View - Tabbed Content
// =============================================================================

interface TechnicalViewProps {
  activeTab: TechnicalTab;
  onTabChange: (tab: TechnicalTab) => void;
}

function TechnicalView({ activeTab, onTabChange }: TechnicalViewProps) {
  return (
    <div className="flex gap-6">
      {/* Sidebar Navigation */}
      <TabNavSidebar activeTab={activeTab} onTabChange={onTabChange} />

      {/* Main Content */}
      <div className="flex-1 min-w-0">
        {/* Mobile Tab Bar */}
        <MobileTabBar activeTab={activeTab} onTabChange={onTabChange} />

        {/* Tab Content */}
        <AnimatePresence mode="wait">
          <motion.div
            key={activeTab}
            initial={{ opacity: 0, x: 20 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: -20 }}
            transition={{ duration: 0.2 }}
            className="space-y-8"
          >
            {activeTab === 'overview' && (
              <>
                <Section title="POC Status" description="Current implementation vs TOMAC-RAG plan">
                  <PocStatus />
                </Section>

                <Section title="System Overview" description="Key performance metrics">
                  <StatsGrid stats={DEFAULT_STATS} viewMode="technical" />
                </Section>

                <div className="grid lg:grid-cols-2 gap-6">
                  <Section title="Query Simulator" description="Test routing system">
                    <QuerySimulator />
                  </Section>

                  <Section title="Token Flow" description="Token consumption by layer">
                    <TokenFlowVisualizer />
                  </Section>
                </div>
              </>
            )}

            {activeTab === 'architecture' && (
              <>
                <Section title="Architecture Explorer" description="Interactive system diagram">
                  <ArchitectureExplorer />
                </Section>

                <Section
                  title="Technical Reference"
                  description="Code examples, infrastructure, algorithms"
                >
                  <TechnicalReference />
                </Section>

                <Section title="Layer Documentation" description="Detailed technical docs">
                  <LayerDeepDocs />
                </Section>
              </>
            )}

            {activeTab === 'agents' && (
              <>
                <Section
                  title="Agent-RAG Integration"
                  description="Prompt assembly with RAG context"
                >
                  <AgentRagIntegration />
                </Section>

                <div className="grid lg:grid-cols-2 gap-6">
                  <Section title="Agent Roles" description="Pre-built configurations">
                    <AgentRoleConfigurator />
                  </Section>

                  <Section title="Prompt Builder" description="Assemble and preview prompts">
                    <PromptTemplateBuilder />
                  </Section>
                </div>
              </>
            )}

            {activeTab === 'performance' && (
              <>
                <Section
                  title="Parametri & Strategie"
                  description="Configurazione completa del sistema RAG"
                >
                  <ParameterGuide />
                </Section>

                <Section title="Cost Projection" description="Monthly cost estimates">
                  <CostCalculator />
                </Section>

                <div className="grid lg:grid-cols-2 gap-6">
                  <Section title="Model Selection" description="Optimize model choices">
                    <ModelSelectionOptimizer />
                  </Section>

                  <Section title="Performance Metrics" description="Real-time data">
                    <PerformanceMetricsTable />
                  </Section>
                </div>

                <Section
                  title={`${METRICS.totalVariants} RAG Variants`}
                  description="Strategy × Template × Tier combinations"
                >
                  <VariantComparisonTool />
                </Section>
              </>
            )}

            {activeTab === 'walkthrough' && (
              <Section
                title="Decision Walkthrough"
                description="Step-by-step visualization of RAG decision process"
              >
                <DecisionWalkthrough />
              </Section>
            )}
          </motion.div>
        </AnimatePresence>
      </div>
    </div>
  );
}

// =============================================================================
// Business View — enriched with new business components
// =============================================================================

function BusinessView() {
  return (
    <div className="space-y-8">
      <Section title="Business Metrics" description="Key cost efficiency and accuracy indicators">
        <BusinessKpiCards />
      </Section>

      <Section
        title="Strategy Comparison"
        description="Performance and cost breakdown by RAG strategy"
      >
        <BusinessStrategyComparison />
      </Section>

      <Section
        title="ROI Calculator"
        description="Project savings vs. a naive single-model approach"
      >
        <BusinessRoiCalculator />
      </Section>

      <Section
        title="Use Cases"
        description="Real-world board game scenarios mapped to TOMAC-RAG strategies"
      >
        <BusinessUseCases />
      </Section>
    </div>
  );
}

// =============================================================================
// Main Dashboard Component
// =============================================================================

export function RagDashboard() {
  const searchParams = useSearchParams();
  const router = useRouter();

  const viewMode = (searchParams.get('view') as ViewMode | null) ?? 'technical';
  const activeTab = (searchParams.get('tab') as TechnicalTab | null) ?? 'overview';

  function handleViewModeChange(mode: ViewMode) {
    const params = new URLSearchParams(searchParams.toString());
    params.set('view', mode);
    router.replace(`/rag?${params.toString()}`, { scroll: false });
  }

  function handleTabChange(tab: TechnicalTab) {
    const params = new URLSearchParams(searchParams.toString());
    params.set('tab', tab);
    router.replace(`/rag?${params.toString()}`, { scroll: false });
  }

  return (
    <div className="rag-dashboard min-h-screen">
      <div className="relative z-10">
        {/* Animated 6-layer pipeline hero with view toggle */}
        <RagHero viewMode={viewMode} onViewModeChange={handleViewModeChange} />

        {/* Main Content */}
        <main className="max-w-7xl mx-auto px-4 md:px-6 py-6">
          <AnimatePresence mode="wait">
            {viewMode === 'technical' ? (
              <motion.div
                key="technical"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.2 }}
              >
                <TechnicalView activeTab={activeTab} onTabChange={handleTabChange} />
              </motion.div>
            ) : (
              <motion.div
                key="business"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.2 }}
              >
                <BusinessView />
              </motion.div>
            )}
          </AnimatePresence>
        </main>

        {/* Footer */}
        <footer className="mt-8 py-4 px-6 border-t border-border/50 text-center text-xs text-muted-foreground">
          MeepleAI RAG Dashboard • Next.js 15 + shadcn/ui •{' '}
          <a
            href="https://github.com/meepleai"
            className="text-primary hover:underline"
            target="_blank"
            rel="noopener noreferrer"
          >
            GitHub
          </a>
        </footer>
      </div>
    </div>
  );
}
