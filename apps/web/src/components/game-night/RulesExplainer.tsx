'use client';

/**
 * RulesExplainer — Progressive rules presentation for table explanations.
 * Issue #5584: Rules Explainer progressive presentation.
 *
 * Presents rulebook analysis data in navigable sections optimized for
 * reading aloud to table companions during game night.
 *
 * Sections:
 * 1. Summary — 30-second overview
 * 2. Mechanics — each mechanic with brief explanation
 * 3. Turn Phases — sequence of phases with expandable details
 * 4. Victory Conditions — primary and alternatives
 * 5. Resources — what they are and how to use them
 * 6. FAQ — frequently asked questions
 *
 * Data source: RulebookAnalysisDto from GET /api/v1/shared-games/{id}/analysis
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import {
  BookOpen,
  ChevronRight,
  Clock,
  Cog,
  HelpCircle,
  Layers,
  Package,
  Pause,
  Play,
  RotateCcw,
  ScrollText,
  Trophy,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';

// ============================================================================
// Types
// ============================================================================

export interface RulesExplainerProps {
  /** The rulebook analysis data to display */
  analysis: RulebookAnalysisDto;
  /** Optional: game title override (defaults to analysis.gameTitle) */
  gameTitle?: string;
  /** Optional: show the explanation timer (default: true) */
  showTimer?: boolean;
}

type SectionId = 'summary' | 'mechanics' | 'phases' | 'victory' | 'resources' | 'faq';

interface SectionDef {
  id: SectionId;
  label: string;
  icon: React.ReactNode;
  /** Estimated reading time in seconds */
  estimatedSeconds: number;
}

// ============================================================================
// Constants
// ============================================================================

const SECTIONS: SectionDef[] = [
  {
    id: 'summary',
    label: 'Summary',
    icon: <ScrollText className="h-5 w-5" />,
    estimatedSeconds: 30,
  },
  { id: 'mechanics', label: 'Mechanics', icon: <Cog className="h-5 w-5" />, estimatedSeconds: 60 },
  {
    id: 'phases',
    label: 'Turn Phases',
    icon: <Layers className="h-5 w-5" />,
    estimatedSeconds: 90,
  },
  { id: 'victory', label: 'Victory', icon: <Trophy className="h-5 w-5" />, estimatedSeconds: 30 },
  {
    id: 'resources',
    label: 'Resources',
    icon: <Package className="h-5 w-5" />,
    estimatedSeconds: 45,
  },
  { id: 'faq', label: 'FAQ', icon: <HelpCircle className="h-5 w-5" />, estimatedSeconds: 60 },
];

// ============================================================================
// Helper: format seconds as mm:ss
// ============================================================================

function formatTime(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

// ============================================================================
// Section Sub-Components
// ============================================================================

function SummarySection({ analysis }: { analysis: RulebookAnalysisDto }) {
  return (
    <div className="space-y-4" data-testid="section-summary">
      <p className="text-xl leading-relaxed text-foreground">{analysis.summary}</p>
      {analysis.keyMechanics.length > 0 && (
        <div>
          <p className="text-lg text-muted-foreground mb-2">Key concepts at a glance:</p>
          <div className="flex flex-wrap gap-2">
            {analysis.keyMechanics.map(mechanic => (
              <Badge key={mechanic} variant="secondary" className="text-base px-3 py-1">
                {mechanic}
              </Badge>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function MechanicsSection({ analysis }: { analysis: RulebookAnalysisDto }) {
  if (analysis.keyMechanics.length === 0) {
    return (
      <p className="text-lg text-muted-foreground" data-testid="section-mechanics">
        No mechanics data available for this game.
      </p>
    );
  }

  // Pair mechanics with key concepts where possible
  const conceptMap = new Map(analysis.keyConcepts.map(kc => [kc.term.toLowerCase(), kc]));

  return (
    <div className="space-y-4" data-testid="section-mechanics">
      {analysis.keyMechanics.map((mechanic, index) => {
        const concept = conceptMap.get(mechanic.toLowerCase());
        return (
          <Card key={mechanic} className="border-l-4 border-l-primary">
            <CardContent className="p-5">
              <div className="flex items-start gap-3">
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-bold text-primary">
                  {index + 1}
                </span>
                <div>
                  <h4 className="text-xl font-semibold text-foreground">{mechanic}</h4>
                  {concept && (
                    <p className="text-lg text-muted-foreground mt-1">{concept.definition}</p>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>
        );
      })}
    </div>
  );
}

function PhasesSection({ analysis }: { analysis: RulebookAnalysisDto }) {
  const sorted = useMemo(
    () => [...analysis.gamePhases].sort((a, b) => a.order - b.order),
    [analysis.gamePhases]
  );

  if (sorted.length === 0) {
    return (
      <p className="text-lg text-muted-foreground" data-testid="section-phases">
        No turn phase data available for this game.
      </p>
    );
  }

  return (
    <div className="space-y-3" data-testid="section-phases">
      {sorted.map((phase, index) => (
        <Card key={phase.name} className="overflow-hidden">
          <CardContent className="p-5">
            <div className="flex gap-4">
              <div className="flex flex-col items-center">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary text-primary-foreground text-lg font-bold">
                  {index + 1}
                </div>
                {index < sorted.length - 1 && <div className="w-0.5 flex-1 bg-border mt-2" />}
              </div>
              <div className="flex-1 pb-2">
                <div className="flex items-center gap-2 mb-1">
                  <h4 className="text-xl font-semibold text-foreground">{phase.name}</h4>
                  {phase.isOptional && (
                    <Badge variant="outline" className="text-sm">
                      Optional
                    </Badge>
                  )}
                </div>
                <p className="text-lg text-muted-foreground leading-relaxed">{phase.description}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function VictorySection({ analysis }: { analysis: RulebookAnalysisDto }) {
  if (!analysis.victoryConditions) {
    return (
      <p className="text-lg text-muted-foreground" data-testid="section-victory">
        No victory condition data available for this game.
      </p>
    );
  }

  const vc = analysis.victoryConditions;

  return (
    <div className="space-y-4" data-testid="section-victory">
      {/* Primary */}
      <Card className="border-l-4 border-l-amber-500">
        <CardContent className="p-5">
          <div className="flex items-start gap-3">
            <Trophy className="h-6 w-6 text-amber-500 shrink-0 mt-1" />
            <div>
              <p className="text-sm font-medium text-amber-600 dark:text-amber-400 uppercase tracking-wide mb-1">
                Primary Win Condition
              </p>
              <p className="text-xl text-foreground leading-relaxed">{vc.primary}</p>
              {vc.isPointBased && (
                <p className="text-lg text-muted-foreground mt-2">
                  Point-based game
                  {vc.targetPoints != null && ` — target: ${vc.targetPoints} points`}
                </p>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Alternatives */}
      {vc.alternatives.length > 0 && (
        <div>
          <p className="text-lg font-medium text-muted-foreground mb-3">Alternative ways to win:</p>
          <div className="space-y-2">
            {vc.alternatives.map(alt => (
              <Card key={alt}>
                <CardContent className="p-4">
                  <p className="text-lg text-foreground">{alt}</p>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function ResourcesSection({ analysis }: { analysis: RulebookAnalysisDto }) {
  if (analysis.resources.length === 0) {
    return (
      <p className="text-lg text-muted-foreground" data-testid="section-resources">
        No resource data available for this game.
      </p>
    );
  }

  return (
    <div className="space-y-3" data-testid="section-resources">
      {analysis.resources.map(resource => (
        <Card key={resource.name}>
          <CardContent className="p-5">
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <h4 className="text-xl font-semibold text-foreground">{resource.name}</h4>
                  <Badge variant="outline" className="text-sm">
                    {resource.type}
                  </Badge>
                  {resource.isLimited && (
                    <Badge variant="destructive" className="text-sm">
                      Limited
                    </Badge>
                  )}
                </div>
                <p className="text-lg text-muted-foreground leading-relaxed">{resource.usage}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function FaqSection({ analysis }: { analysis: RulebookAnalysisDto }) {
  const hasFaqs = analysis.generatedFaqs.length > 0;
  const hasCommonQuestions = analysis.commonQuestions.length > 0;

  if (!hasFaqs && !hasCommonQuestions) {
    return (
      <p className="text-lg text-muted-foreground" data-testid="section-faq">
        No FAQ data available for this game.
      </p>
    );
  }

  return (
    <div className="space-y-4" data-testid="section-faq">
      {hasFaqs &&
        analysis.generatedFaqs.map(faq => (
          <Card key={faq.question}>
            <CardContent className="p-5">
              <p className="text-xl font-semibold text-foreground mb-2">{faq.question}</p>
              <p className="text-lg text-muted-foreground leading-relaxed">{faq.answer}</p>
              {faq.tags.length > 0 && (
                <div className="flex flex-wrap gap-1.5 mt-3">
                  {faq.tags.map(tag => (
                    <Badge key={tag} variant="outline" className="text-xs">
                      {tag}
                    </Badge>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        ))}
      {!hasFaqs && hasCommonQuestions && (
        <div>
          <p className="text-lg font-medium text-muted-foreground mb-3">Common Questions:</p>
          <ul className="space-y-2">
            {analysis.commonQuestions.map(q => (
              <li key={q} className="text-lg text-foreground flex items-start gap-2">
                <HelpCircle className="h-5 w-5 text-muted-foreground shrink-0 mt-1" />
                {q}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Timer Hook
// ============================================================================

function useExplanationTimer() {
  const [elapsed, setElapsed] = useState(0);
  const [isRunning, setIsRunning] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const start = useCallback(() => {
    setIsRunning(true);
  }, []);

  const pause = useCallback(() => {
    setIsRunning(false);
  }, []);

  const reset = useCallback(() => {
    setIsRunning(false);
    setElapsed(0);
  }, []);

  const toggle = useCallback(() => {
    setIsRunning(prev => !prev);
  }, []);

  useEffect(() => {
    if (isRunning) {
      intervalRef.current = setInterval(() => {
        setElapsed(prev => prev + 1);
      }, 1000);
    } else if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, [isRunning]);

  return { elapsed, isRunning, start, pause, reset, toggle };
}

// ============================================================================
// Section Content Renderer
// ============================================================================

function SectionContent({
  sectionId,
  analysis,
}: {
  sectionId: SectionId;
  analysis: RulebookAnalysisDto;
}) {
  switch (sectionId) {
    case 'summary':
      return <SummarySection analysis={analysis} />;
    case 'mechanics':
      return <MechanicsSection analysis={analysis} />;
    case 'phases':
      return <PhasesSection analysis={analysis} />;
    case 'victory':
      return <VictorySection analysis={analysis} />;
    case 'resources':
      return <ResourcesSection analysis={analysis} />;
    case 'faq':
      return <FaqSection analysis={analysis} />;
  }
}

// ============================================================================
// Main Component
// ============================================================================

export function RulesExplainer({ analysis, gameTitle, showTimer = true }: RulesExplainerProps) {
  const [activeSectionIndex, setActiveSectionIndex] = useState(0);
  const timer = useExplanationTimer();
  const contentRef = useRef<HTMLDivElement>(null);

  // Filter sections that have data
  const availableSections = useMemo(() => {
    return SECTIONS.filter(section => {
      switch (section.id) {
        case 'summary':
          return !!analysis.summary;
        case 'mechanics':
          return analysis.keyMechanics.length > 0;
        case 'phases':
          return analysis.gamePhases.length > 0;
        case 'victory':
          return !!analysis.victoryConditions;
        case 'resources':
          return analysis.resources.length > 0;
        case 'faq':
          return analysis.generatedFaqs.length > 0 || analysis.commonQuestions.length > 0;
        default:
          return false;
      }
    });
  }, [analysis]);

  const totalEstimatedSeconds = useMemo(
    () => availableSections.reduce((sum, s) => sum + s.estimatedSeconds, 0),
    [availableSections]
  );

  const activeSection = availableSections[activeSectionIndex];
  const isFirstSection = activeSectionIndex === 0;
  const isLastSection = activeSectionIndex === availableSections.length - 1;
  const progressPercent =
    availableSections.length > 1
      ? Math.round((activeSectionIndex / (availableSections.length - 1)) * 100)
      : 100;

  const goToSection = useCallback((index: number) => {
    setActiveSectionIndex(index);
    // Scroll to top of content area
    contentRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, []);

  const goNext = useCallback(() => {
    if (!isLastSection) {
      goToSection(activeSectionIndex + 1);
    }
  }, [activeSectionIndex, isLastSection, goToSection]);

  const goPrev = useCallback(() => {
    if (!isFirstSection) {
      goToSection(activeSectionIndex - 1);
    }
  }, [activeSectionIndex, isFirstSection, goToSection]);

  // Handle no available sections
  if (availableSections.length === 0) {
    return (
      <div className="text-center py-12" data-testid="rules-explainer-empty">
        <BookOpen className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
        <p className="text-lg text-muted-foreground">
          No analysis data available yet. Upload a rulebook PDF to generate the rules explanation.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6" data-testid="rules-explainer">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h2 className="text-2xl font-bold text-foreground flex items-center gap-2">
            <BookOpen className="h-6 w-6 text-primary" />
            {gameTitle ?? analysis.gameTitle} — Rules
          </h2>
          <p className="text-base text-muted-foreground mt-1">
            {availableSections.length} sections
            {totalEstimatedSeconds > 0 && (
              <> &middot; ~{formatTime(totalEstimatedSeconds)} estimated</>
            )}
          </p>
        </div>

        {/* Timer */}
        {showTimer && (
          <div
            className="flex items-center gap-2 rounded-lg border bg-card px-4 py-2"
            data-testid="explanation-timer"
          >
            <Clock className="h-4 w-4 text-muted-foreground" />
            <span className="text-lg font-mono font-semibold tabular-nums">
              {formatTime(timer.elapsed)}
            </span>
            <Button
              variant="ghost"
              size="icon"
              onClick={timer.toggle}
              aria-label={timer.isRunning ? 'Pause timer' : 'Start timer'}
            >
              {timer.isRunning ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
            </Button>
            <Button variant="ghost" size="icon" onClick={timer.reset} aria-label="Reset timer">
              <RotateCcw className="h-4 w-4" />
            </Button>
          </div>
        )}
      </div>

      {/* Progress Bar */}
      <div className="space-y-2">
        <Progress value={progressPercent} className="h-2" />
        <div className="flex justify-between text-sm text-muted-foreground">
          <span>
            Section {activeSectionIndex + 1} of {availableSections.length}
          </span>
          <span>{progressPercent}%</span>
        </div>
      </div>

      {/* Section Navigation Tabs */}
      <div className="flex gap-1.5 overflow-x-auto pb-2" role="tablist" aria-label="Rules sections">
        {availableSections.map((section, index) => (
          <button
            key={section.id}
            role="tab"
            aria-selected={index === activeSectionIndex}
            aria-controls={`section-panel-${section.id}`}
            className={`flex items-center gap-2 rounded-lg px-4 py-2.5 text-base font-medium transition-colors whitespace-nowrap ${
              index === activeSectionIndex
                ? 'bg-primary text-primary-foreground shadow-sm'
                : index < activeSectionIndex
                  ? 'bg-primary/10 text-primary hover:bg-primary/20'
                  : 'bg-muted text-muted-foreground hover:bg-muted/80'
            }`}
            onClick={() => goToSection(index)}
          >
            {section.icon}
            <span className="hidden sm:inline">{section.label}</span>
          </button>
        ))}
      </div>

      {/* Section Content */}
      <div ref={contentRef}>
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-2xl">
              {activeSection.icon}
              {activeSection.label}
            </CardTitle>
          </CardHeader>
          <CardContent
            id={`section-panel-${activeSection.id}`}
            role="tabpanel"
            aria-labelledby={`tab-${activeSection.id}`}
          >
            <SectionContent sectionId={activeSection.id} analysis={analysis} />
          </CardContent>
        </Card>
      </div>

      {/* Navigation Buttons */}
      <div className="flex items-center justify-between">
        <Button
          variant="outline"
          size="lg"
          onClick={goPrev}
          disabled={isFirstSection}
          className="text-base"
        >
          Previous
        </Button>
        <Button size="lg" onClick={goNext} disabled={isLastSection} className="text-base gap-2">
          Next Section
          <ChevronRight className="h-5 w-5" />
        </Button>
      </div>
    </div>
  );
}
