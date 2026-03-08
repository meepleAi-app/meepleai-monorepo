'use client';

/**
 * AnalysisResultsPanel — Tabbed view of AI rulebook analysis results.
 * Issue #5454: Analysis results visible on game page.
 *
 * Tabs: Mechanics, Phases, FAQ, Glossary, State
 * Per-element confidence scores displayed with ConfidenceBadge.
 */

import { useState } from 'react';

import {
  BookOpen,
  Brain,
  HelpCircle,
  Layers,
  ListChecks,
  Trophy,
  AlertTriangle,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { ConfidenceBadge } from '@/components/ui/data-display/confidence-badge';
import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';

// ============================================================================
// Types
// ============================================================================

interface AnalysisResultsPanelProps {
  analyses: RulebookAnalysisDto[];
}

type TabId = 'mechanics' | 'phases' | 'faq' | 'glossary' | 'state';

interface TabDef {
  id: TabId;
  label: string;
  icon: React.ReactNode;
}

// ============================================================================
// Tab Configuration
// ============================================================================

const TABS: TabDef[] = [
  { id: 'mechanics', label: 'Mechanics', icon: <Brain className="h-4 w-4" /> },
  { id: 'phases', label: 'Phases', icon: <Layers className="h-4 w-4" /> },
  { id: 'faq', label: 'FAQ', icon: <HelpCircle className="h-4 w-4" /> },
  { id: 'glossary', label: 'Glossary', icon: <BookOpen className="h-4 w-4" /> },
  { id: 'state', label: 'State', icon: <ListChecks className="h-4 w-4" /> },
];

// ============================================================================
// Sub-Components
// ============================================================================

function MechanicsTab({ analysis }: { analysis: RulebookAnalysisDto }) {
  return (
    <div className="space-y-4">
      {/* Key Mechanics */}
      <div>
        <h4 className="text-sm font-semibold text-foreground mb-2">Key Mechanics</h4>
        <div className="flex flex-wrap gap-2">
          {analysis.keyMechanics.map(m => (
            <Badge key={m} variant="secondary" className="text-xs">
              {m}
            </Badge>
          ))}
          {analysis.keyMechanics.length === 0 && (
            <p className="text-sm text-muted-foreground">No mechanics extracted.</p>
          )}
        </div>
      </div>

      {/* Victory Conditions */}
      {analysis.victoryConditions && (
        <div>
          <h4 className="text-sm font-semibold text-foreground mb-2 flex items-center gap-2">
            <Trophy className="h-4 w-4 text-amber-500" />
            Victory Conditions
          </h4>
          <div className="rounded-lg border bg-card p-3 space-y-2">
            <p className="text-sm">
              <span className="font-medium">Primary:</span> {analysis.victoryConditions.primary}
            </p>
            {analysis.victoryConditions.alternatives.length > 0 && (
              <div>
                <span className="text-xs text-muted-foreground">Alternatives:</span>
                <ul className="list-disc list-inside text-sm ml-2">
                  {analysis.victoryConditions.alternatives.map(alt => (
                    <li key={alt}>{alt}</li>
                  ))}
                </ul>
              </div>
            )}
            {analysis.victoryConditions.isPointBased && (
              <p className="text-xs text-muted-foreground">
                Point-based
                {analysis.victoryConditions.targetPoints != null &&
                  ` (target: ${analysis.victoryConditions.targetPoints})`}
              </p>
            )}
          </div>
        </div>
      )}

      {/* Resources */}
      {analysis.resources.length > 0 && (
        <div>
          <h4 className="text-sm font-semibold text-foreground mb-2">Resources</h4>
          <div className="grid gap-2 sm:grid-cols-2">
            {analysis.resources.map(r => (
              <div key={r.name} className="rounded-lg border bg-card p-2 text-sm">
                <div className="flex items-center justify-between">
                  <span className="font-medium">{r.name}</span>
                  {r.isLimited && (
                    <Badge variant="outline" className="text-[10px]">
                      Limited
                    </Badge>
                  )}
                </div>
                <p className="text-xs text-muted-foreground mt-1">{r.usage}</p>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function PhasesTab({ analysis }: { analysis: RulebookAnalysisDto }) {
  const sorted = [...analysis.gamePhases].sort((a, b) => a.order - b.order);
  return (
    <div className="space-y-2">
      {sorted.map((phase, i) => (
        <div key={phase.name} className="flex gap-3 rounded-lg border bg-card p-3">
          <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
            {i + 1}
          </div>
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium">{phase.name}</span>
              {phase.isOptional && (
                <Badge variant="outline" className="text-[10px]">
                  Optional
                </Badge>
              )}
            </div>
            <p className="text-xs text-muted-foreground mt-0.5">{phase.description}</p>
          </div>
        </div>
      ))}
      {sorted.length === 0 && (
        <p className="text-sm text-muted-foreground">No game phases extracted.</p>
      )}
    </div>
  );
}

function FaqTab({ analysis }: { analysis: RulebookAnalysisDto }) {
  return (
    <div className="space-y-3">
      {analysis.generatedFaqs.map(faq => (
        <div key={faq.question} className="rounded-lg border bg-card p-3">
          <div className="flex items-start justify-between gap-2">
            <p className="text-sm font-medium">{faq.question}</p>
            <ConfidenceBadge confidence={Math.round(faq.confidence * 100)} />
          </div>
          <p className="text-sm text-muted-foreground mt-1">{faq.answer}</p>
          <div className="flex items-center gap-2 mt-2">
            <span className="text-[10px] text-muted-foreground">Source: {faq.sourceSection}</span>
            {faq.tags.map(tag => (
              <Badge key={tag} variant="outline" className="text-[10px]">
                {tag}
              </Badge>
            ))}
          </div>
        </div>
      ))}
      {/* Fallback: common questions if no generated FAQs */}
      {analysis.generatedFaqs.length === 0 && analysis.commonQuestions.length > 0 && (
        <div>
          <h4 className="text-sm font-semibold text-foreground mb-2">Common Questions</h4>
          <ul className="list-disc list-inside space-y-1 text-sm text-muted-foreground">
            {analysis.commonQuestions.map(q => (
              <li key={q}>{q}</li>
            ))}
          </ul>
        </div>
      )}
      {analysis.generatedFaqs.length === 0 && analysis.commonQuestions.length === 0 && (
        <p className="text-sm text-muted-foreground">No FAQ data available.</p>
      )}
    </div>
  );
}

function GlossaryTab({ analysis }: { analysis: RulebookAnalysisDto }) {
  const grouped = analysis.keyConcepts.reduce<Record<string, typeof analysis.keyConcepts>>(
    (acc, kc) => {
      const cat = kc.category || 'General';
      if (!acc[cat]) acc[cat] = [];
      acc[cat].push(kc);
      return acc;
    },
    {}
  );

  return (
    <div className="space-y-4">
      {Object.entries(grouped).map(([category, concepts]) => (
        <div key={category}>
          <h4 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-2">
            {category}
          </h4>
          <div className="space-y-1">
            {concepts.map(kc => (
              <div key={kc.term} className="rounded-lg border bg-card p-2">
                <span className="text-sm font-medium">{kc.term}</span>
                <p className="text-xs text-muted-foreground mt-0.5">{kc.definition}</p>
              </div>
            ))}
          </div>
        </div>
      ))}
      {analysis.keyConcepts.length === 0 && (
        <p className="text-sm text-muted-foreground">No glossary terms extracted.</p>
      )}
    </div>
  );
}

function StateTab({ analysis }: { analysis: RulebookAnalysisDto }) {
  return (
    <div className="space-y-4">
      {/* Completion Status */}
      <div className="flex items-center gap-2">
        <span className="text-sm font-medium">Completion:</span>
        <Badge
          variant={analysis.completionStatus === 'Complete' ? 'default' : 'secondary'}
          className="text-xs"
        >
          {analysis.completionStatus}
        </Badge>
      </div>

      {/* Missing Sections */}
      {analysis.missingSections && analysis.missingSections.length > 0 && (
        <div className="rounded-lg border border-amber-200 bg-amber-50/50 dark:border-amber-900/50 dark:bg-amber-950/20 p-3">
          <div className="flex items-center gap-2 mb-2">
            <AlertTriangle className="h-4 w-4 text-amber-500" />
            <span className="text-sm font-medium text-amber-700 dark:text-amber-400">
              Missing Sections
            </span>
          </div>
          <ul className="list-disc list-inside text-sm text-amber-600 dark:text-amber-300">
            {analysis.missingSections.map(s => (
              <li key={s}>{s}</li>
            ))}
          </ul>
        </div>
      )}

      {/* Game State Schema */}
      {analysis.gameStateSchemaJson && (
        <div>
          <h4 className="text-sm font-semibold text-foreground mb-2">Game State Schema</h4>
          <pre className="rounded-lg border bg-muted/50 p-3 text-xs overflow-x-auto max-h-64">
            {JSON.stringify(JSON.parse(analysis.gameStateSchemaJson), null, 2)}
          </pre>
        </div>
      )}

      {!analysis.gameStateSchemaJson &&
        (!analysis.missingSections || analysis.missingSections.length === 0) &&
        analysis.completionStatus === 'Complete' && (
          <p className="text-sm text-muted-foreground">
            Analysis is complete with no missing sections.
          </p>
        )}
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function AnalysisResultsPanel({ analyses }: AnalysisResultsPanelProps) {
  const [activeTab, setActiveTab] = useState<TabId>('mechanics');

  if (analyses.length === 0) return null;

  // Use the first (most recent) active analysis
  const analysis = analyses[0];

  return (
    <section data-testid="analysis-results-panel">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-lg font-semibold text-foreground flex items-center gap-2">
          <Brain className="h-5 w-5 text-primary" />
          AI Analysis
        </h3>
        <ConfidenceBadge confidence={Math.round(analysis.confidenceScore * 100)} />
      </div>

      {/* Summary */}
      <p className="text-sm text-muted-foreground mb-4">{analysis.summary}</p>

      {/* Tab Bar */}
      <div className="flex gap-1 border-b mb-4 overflow-x-auto" role="tablist">
        {TABS.map(tab => (
          <button
            key={tab.id}
            role="tab"
            aria-selected={activeTab === tab.id}
            className={`flex items-center gap-1.5 px-3 py-2 text-sm font-medium transition-colors whitespace-nowrap ${
              activeTab === tab.id
                ? 'border-b-2 border-primary text-primary'
                : 'text-muted-foreground hover:text-foreground'
            }`}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab Content */}
      <div className="min-h-[120px]">
        {activeTab === 'mechanics' && <MechanicsTab analysis={analysis} />}
        {activeTab === 'phases' && <PhasesTab analysis={analysis} />}
        {activeTab === 'faq' && <FaqTab analysis={analysis} />}
        {activeTab === 'glossary' && <GlossaryTab analysis={analysis} />}
        {activeTab === 'state' && <StateTab analysis={analysis} />}
      </div>
    </section>
  );
}
