'use client';

/**
 * DecisionWalkthrough Component
 *
 * Interactive scenario visualization showing step-by-step
 * RAG decision process for real-world queries.
 */

import React, { useState, useEffect } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  PlayCircle,
  PauseCircle,
  SkipForward,
  RotateCcw,
  Brain,
  Database,
  Search,
  CheckCircle2,
  Sparkles,
  Shield,
  ChevronRight,
  Clock,
  Coins,
  User,
  MessageSquare,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { RagStrategy, QueryTemplate, UserTier } from './types';

// =============================================================================
// Types
// =============================================================================

interface WalkthroughStep {
  id: string;
  layer: string;
  layerIcon: React.ReactNode;
  color: string;
  title: string;
  description: string;
  decision: string;
  tokensUsed: number;
  latencyMs: number;
  details: string[];
}

interface WalkthroughScenario {
  id: string;
  query: string;
  userTier: UserTier;
  strategy: RagStrategy;
  template: QueryTemplate;
  totalTokens: number;
  totalLatencyMs: number;
  steps: WalkthroughStep[];
  finalResponse: string;
}

// =============================================================================
// Data - Scenarios
// =============================================================================

const SCENARIOS: WalkthroughScenario[] = [
  {
    id: 'catan-7-rule',
    query: 'What happens when I roll a 7 in Catan?',
    userTier: 'User',
    strategy: 'BALANCED',
    template: 'rule_lookup',
    totalTokens: 3350,
    totalLatencyMs: 1850,
    steps: [
      {
        id: 'routing',
        layer: 'L1: Intelligent Routing',
        layerIcon: <Brain className="h-5 w-5" />,
        color: 'hsl(221 83% 53%)',
        title: 'Query Classification',
        description: 'Analyzing query complexity and selecting optimal strategy',
        decision: 'rule_lookup template, BALANCED strategy selected',
        tokensUsed: 320,
        latencyMs: 150,
        details: [
          'Keyword match: "rules", "what happens", "roll"',
          'Complexity score: 2/5 (straightforward rule question)',
          'User tier: User (BALANCED allowed)',
          'Template confidence: 0.94',
        ],
      },
      {
        id: 'cache',
        layer: 'L2: Semantic Cache',
        layerIcon: <Database className="h-5 w-5" />,
        color: 'hsl(262 83% 62%)',
        title: 'Cache Lookup',
        description: 'Checking memory and semantic cache for similar queries',
        decision: 'Semantic match found (0.87 similarity) - using as hint',
        tokensUsed: 310,
        latencyMs: 50,
        details: [
          'Exact match: Not found',
          'Semantic search: Found similar query "Catan 7 roll"',
          'Similarity score: 0.87 (below 0.90 threshold)',
          'Action: Use as retrieval hint, not as cached response',
        ],
      },
      {
        id: 'retrieval',
        layer: 'L3: Modular Retrieval',
        layerIcon: <Search className="h-5 w-5" />,
        color: 'hsl(142 76% 36%)',
        title: 'Document Retrieval',
        description: 'Hybrid search combining vector and BM25',
        decision: 'Retrieved 5 relevant documents with RRF fusion',
        tokensUsed: 1200,
        latencyMs: 400,
        details: [
          'Vector search: 5 candidates (pgvector)',
          'BM25 search: 5 candidates (PostgreSQL FTS)',
          'RRF fusion: Combined and ranked',
          'Top results: Rulebook p.12, FAQ v2.3, Strategy Guide',
          'Relevance scores: 0.94, 0.89, 0.82',
        ],
      },
      {
        id: 'crag',
        layer: 'L4: CRAG Evaluation',
        layerIcon: <CheckCircle2 className="h-5 w-5" />,
        color: 'hsl(45 93% 47%)',
        title: 'Quality Evaluation',
        description: 'T5-Large evaluating retrieval quality',
        decision: 'Score: CORRECT (0.91) - proceeding to generation',
        tokensUsed: 500,
        latencyMs: 200,
        details: [
          'T5-Large relevance score: 0.91',
          'Classification: CORRECT (>0.8 threshold)',
          'Action: Proceed to generation',
          'No decompose/recompose needed',
          'Web augmentation: Not triggered',
        ],
      },
      {
        id: 'generation',
        layer: 'L5: Adaptive Generation',
        layerIcon: <Sparkles className="h-5 w-5" />,
        color: 'hsl(25 95% 53%)',
        title: 'Response Generation',
        description: 'Claude 3.5 Sonnet generating structured response',
        decision: 'Generated response with 3 citations',
        tokensUsed: 850,
        latencyMs: 900,
        details: [
          'Model: Claude 3.5 Sonnet',
          'Template: rule_lookup format',
          'Input tokens: 580',
          'Output tokens: 270',
          'Citations extracted: 3',
          'Confidence: 0.95',
        ],
      },
      {
        id: 'validation',
        layer: 'L6: Self-Validation',
        layerIcon: <Shield className="h-5 w-5" />,
        color: 'hsl(0 72% 51%)',
        title: 'Response Validation',
        description: 'Cross-encoder alignment check',
        decision: 'Validation passed - all citations verified',
        tokensUsed: 170,
        latencyMs: 150,
        details: [
          'Cross-encoder: ms-marco-MiniLM',
          'Citation verification: 3/3 verified',
          'Alignment score: 0.93',
          'Hallucination check: Passed',
          'Final confidence: 0.94',
        ],
      },
    ],
    finalResponse: `**Rule Quote:**
"When a '7' is rolled, no one receives any resources. Instead: (1) Each player with more than 7 Resource Cards must discard half (rounded down). (2) The active player must move the robber. (3) The player may steal 1 card from any player with a settlement or city adjacent to the robber's new hex."

**Explanation:**
Rolling a 7 in Catan triggers three actions in sequence. First, anyone with 8+ cards loses half. Then the rolling player moves the robber to any hex and can steal from adjacent players.

**Common Mistakes:**
- Forgetting to discard before moving robber
- Thinking you can leave robber in place
- Not stealing when able to

**Citation:** Catan Rulebook v5, Page 12`,
  },
  {
    id: 'strategy-advice',
    query: 'Should I build a settlement or buy a development card in Catan?',
    userTier: 'Editor',
    strategy: 'PRECISE',
    template: 'strategy_advice',
    totalTokens: 12900,
    totalLatencyMs: 4200,
    steps: [
      {
        id: 'routing',
        layer: 'L1: Intelligent Routing',
        layerIcon: <Brain className="h-5 w-5" />,
        color: 'hsl(221 83% 53%)',
        title: 'Query Classification',
        description: 'Analyzing strategic decision request',
        decision: 'strategy_advice template, PRECISE strategy selected',
        tokensUsed: 360,
        latencyMs: 180,
        details: [
          'Keyword match: "should I", "strategy", "or"',
          'Complexity score: 4/5 (requires analysis)',
          'User tier: Editor (PRECISE allowed)',
          'Decision type: Comparative strategy',
        ],
      },
      {
        id: 'cache',
        layer: 'L2: Semantic Cache',
        layerIcon: <Database className="h-5 w-5" />,
        color: 'hsl(262 83% 62%)',
        title: 'Cache Lookup',
        description: 'No similar strategy queries cached',
        decision: 'Cache miss - proceeding to retrieval',
        tokensUsed: 310,
        latencyMs: 60,
        details: [
          'Strategy queries rarely cached (context-dependent)',
          'Semantic search: No relevant matches',
          'Action: Full retrieval required',
        ],
      },
      {
        id: 'retrieval',
        layer: 'L3: Modular Retrieval',
        layerIcon: <Search className="h-5 w-5" />,
        color: 'hsl(142 76% 36%)',
        title: 'Multi-Hop Retrieval',
        description: 'Extended retrieval with entity expansion',
        decision: '3 hops: settlements, dev cards, timing strategies',
        tokensUsed: 3500,
        latencyMs: 800,
        details: [
          'Hop 1: Settlement building strategies',
          'Hop 2: Development card strategies',
          'Hop 3: Timing and resource efficiency',
          'Entity expansion: VP paths, longest road, largest army',
          'Total documents: 12 (deduplicated)',
        ],
      },
      {
        id: 'crag',
        layer: 'L4: CRAG Evaluation',
        layerIcon: <CheckCircle2 className="h-5 w-5" />,
        color: 'hsl(45 93% 47%)',
        title: 'Quality Evaluation',
        description: 'Comprehensive context evaluation',
        decision: 'Score: CORRECT (0.88) - sufficient for analysis',
        tokensUsed: 500,
        latencyMs: 250,
        details: [
          'T5-Large score: 0.88',
          'Coverage: Both options well-documented',
          'Action: Proceed without web augmentation',
        ],
      },
      {
        id: 'generation',
        layer: 'L5: Adaptive Generation',
        layerIcon: <Sparkles className="h-5 w-5" />,
        color: 'hsl(25 95% 53%)',
        title: 'Multi-Agent Generation',
        description: 'Claude 3.5 Opus with Analyzer → Synthesizer → Validator',
        decision: '3-agent pipeline completed with comparative analysis',
        tokensUsed: 6000,
        latencyMs: 2200,
        details: [
          'Agent 1 (Analyzer): Position assessment',
          'Agent 2 (Synthesizer): Strategy comparison',
          'Agent 3 (Validator): Recommendation validation',
          'Model: Claude 3.5 Opus',
          'Total passes: 3',
        ],
      },
      {
        id: 'validation',
        layer: 'L6: Self-Validation',
        layerIcon: <Shield className="h-5 w-5" />,
        color: 'hsl(0 72% 51%)',
        title: 'Self-RAG Reflection',
        description: 'Full reflection loop with confidence scoring',
        decision: 'Confidence: 0.91 - response validated',
        tokensUsed: 2230,
        latencyMs: 710,
        details: [
          'Self-RAG reflection: 2 iterations',
          'Initial confidence: 0.85',
          'After refinement: 0.91',
          'Reasoning chain: Verified',
          'Counterarguments: Addressed',
        ],
      },
    ],
    finalResponse: `**Analysis:**
This decision depends heavily on your current position and the game state.

**Option 1: Build Settlement**
- Pros: Guaranteed VP, resource production, expansion potential
- Cons: Requires specific resources (B-G-L-W), needs open spot
- Best when: You have good road position, specific resources available

**Option 2: Buy Development Card**
- Pros: Flexible VP path, can get knights, cheaper (O-W-G)
- Cons: Random outcome, delayed VP
- Best when: Going for largest army, need ore outlet

**Recommendation:**
Build the settlement IF you have a valuable spot secured. The guaranteed VP and production usually outweigh dev card variance. Exception: Buy dev card if you're 2-3 knights from largest army lead.

**Key Factor:** Your current knight count and road position.`,
  },
];

// =============================================================================
// Sub-Components
// =============================================================================

interface StepCardProps {
  step: WalkthroughStep;
  isActive: boolean;
  isComplete: boolean;
  stepNumber: number;
}

function StepCard({ step, isActive, isComplete, stepNumber }: StepCardProps) {
  return (
    <motion.div
      className={cn(
        'p-4 rounded-xl border transition-all',
        isActive && 'ring-2 ring-primary border-primary bg-primary/5',
        isComplete && !isActive && 'border-green-500/50 bg-green-500/5',
        !isActive && !isComplete && 'border-border opacity-50'
      )}
      initial={{ opacity: 0, x: -20 }}
      animate={{ opacity: isComplete || isActive ? 1 : 0.5, x: 0 }}
      transition={{ delay: stepNumber * 0.1 }}
    >
      <div className="flex items-start gap-3">
        <div
          className={cn('p-2 rounded-lg transition-colors', isComplete && 'bg-green-500/20')}
          style={{
            backgroundColor: isActive ? `${step.color}20` : undefined,
            color: isActive ? step.color : isComplete ? 'hsl(142 76% 36%)' : undefined,
          }}
        >
          {isComplete ? <CheckCircle2 className="h-5 w-5 text-green-500" /> : step.layerIcon}
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center justify-between">
            <span className="text-xs font-mono text-muted-foreground">{step.layer}</span>
            <div className="flex items-center gap-2 text-xs">
              <span className="flex items-center gap-1">
                <Coins className="h-3 w-3" />
                {step.tokensUsed}
              </span>
              <span className="flex items-center gap-1">
                <Clock className="h-3 w-3" />
                {step.latencyMs}ms
              </span>
            </div>
          </div>
          <div className="font-semibold mt-1">{step.title}</div>
          <p className="text-sm text-muted-foreground mt-1">{step.description}</p>

          <AnimatePresence>
            {isActive && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                className="overflow-hidden"
              >
                <div className="mt-3 p-3 rounded-lg bg-muted/50 border">
                  <div className="text-sm font-medium mb-2" style={{ color: step.color }}>
                    Decision: {step.decision}
                  </div>
                  <ul className="space-y-1 text-xs text-muted-foreground">
                    {step.details.map((detail, i) => (
                      <li key={i} className="flex items-start gap-1">
                        <ChevronRight className="h-3 w-3 mt-0.5 shrink-0" />
                        {detail}
                      </li>
                    ))}
                  </ul>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </div>
    </motion.div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface DecisionWalkthroughProps {
  className?: string;
}

export function DecisionWalkthrough({ className }: DecisionWalkthroughProps) {
  const [selectedScenarioId, setSelectedScenarioId] = useState<string>('catan-7-rule');
  const [currentStepIndex, setCurrentStepIndex] = useState<number>(-1);
  const [isPlaying, setIsPlaying] = useState<boolean>(false);
  const [showResponse, setShowResponse] = useState<boolean>(false);

  const scenario = SCENARIOS.find(s => s.id === selectedScenarioId);

  // Auto-advance when playing
  useEffect(() => {
    if (!isPlaying || !scenario) return;

    const timer = setTimeout(() => {
      if (currentStepIndex < scenario.steps.length - 1) {
        setCurrentStepIndex(prev => prev + 1);
      } else {
        setIsPlaying(false);
        setShowResponse(true);
      }
    }, 2000);

    return () => clearTimeout(timer);
  }, [isPlaying, currentStepIndex, scenario]);

  const handlePlay = () => {
    if (currentStepIndex === -1) {
      setCurrentStepIndex(0);
    }
    setIsPlaying(true);
    setShowResponse(false);
  };

  const handlePause = () => {
    setIsPlaying(false);
  };

  const handleSkip = () => {
    if (!scenario) return;
    setCurrentStepIndex(scenario.steps.length - 1);
    setIsPlaying(false);
    setShowResponse(true);
  };

  const handleReset = () => {
    setCurrentStepIndex(-1);
    setIsPlaying(false);
    setShowResponse(false);
  };

  const handleScenarioChange = (id: string) => {
    setSelectedScenarioId(id);
    setCurrentStepIndex(-1);
    setIsPlaying(false);
    setShowResponse(false);
  };

  if (!scenario) return null;

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <PlayCircle className="h-5 w-5 text-primary" />
          Decision Walkthrough
        </CardTitle>
        <p className="text-sm text-muted-foreground mt-1">
          Step-by-step visualization of RAG decision process
        </p>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Scenario Selection */}
        <div className="flex flex-wrap gap-2">
          {SCENARIOS.map(s => (
            <button
              key={s.id}
              className={cn(
                'px-4 py-2 rounded-lg text-sm font-medium transition-all border',
                selectedScenarioId === s.id
                  ? 'border-primary bg-primary/10 text-primary'
                  : 'border-border hover:border-primary/50'
              )}
              onClick={() => handleScenarioChange(s.id)}
            >
              {s.template === 'rule_lookup' ? 'Rule Query' : 'Strategy Query'}
            </button>
          ))}
        </div>

        {/* Query Info */}
        <div className="p-4 rounded-xl bg-muted/30 border">
          <div className="flex items-start gap-3">
            <MessageSquare className="h-5 w-5 text-primary mt-1" />
            <div className="flex-1">
              <div className="text-lg font-medium">&ldquo;{scenario.query}&rdquo;</div>
              <div className="flex flex-wrap gap-4 mt-2 text-sm">
                <span className="flex items-center gap-1">
                  <User className="h-4 w-4" />
                  {scenario.userTier}
                </span>
                <span className="px-2 py-0.5 rounded-full bg-primary/20 text-primary text-xs font-medium">
                  {scenario.strategy}
                </span>
                <span className="text-muted-foreground">{scenario.template}</span>
              </div>
            </div>
            <div className="text-right text-sm">
              <div className="flex items-center gap-1">
                <Coins className="h-4 w-4" />
                <span className="font-mono">{scenario.totalTokens}</span>
              </div>
              <div className="flex items-center gap-1 text-muted-foreground">
                <Clock className="h-4 w-4" />
                <span className="font-mono">{scenario.totalLatencyMs}ms</span>
              </div>
            </div>
          </div>
        </div>

        {/* Controls */}
        <div className="flex items-center justify-center gap-2">
          {isPlaying ? (
            <Button variant="outline" size="sm" onClick={handlePause}>
              <PauseCircle className="h-4 w-4 mr-2" />
              Pause
            </Button>
          ) : (
            <Button variant="default" size="sm" onClick={handlePlay}>
              <PlayCircle className="h-4 w-4 mr-2" />
              {currentStepIndex === -1 ? 'Start' : 'Resume'}
            </Button>
          )}
          <Button variant="outline" size="sm" onClick={handleSkip}>
            <SkipForward className="h-4 w-4 mr-2" />
            Skip to End
          </Button>
          <Button variant="outline" size="sm" onClick={handleReset}>
            <RotateCcw className="h-4 w-4 mr-2" />
            Reset
          </Button>
        </div>

        {/* Progress Bar */}
        <div className="relative h-2 bg-muted rounded-full overflow-hidden">
          <motion.div
            className="h-full bg-primary"
            initial={{ width: 0 }}
            animate={{
              width: `${((currentStepIndex + 1) / scenario.steps.length) * 100}%`,
            }}
            transition={{ duration: 0.3 }}
          />
        </div>

        {/* Steps */}
        <div className="space-y-3">
          {scenario.steps.map((step, index) => (
            <StepCard
              key={step.id}
              step={step}
              stepNumber={index}
              isActive={index === currentStepIndex}
              isComplete={index < currentStepIndex || showResponse}
            />
          ))}
        </div>

        {/* Final Response */}
        <AnimatePresence>
          {showResponse && (
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
              className="p-4 rounded-xl bg-gradient-to-br from-green-500/10 to-green-500/5 border border-green-500/20"
            >
              <div className="flex items-center gap-2 text-green-600 dark:text-green-400 font-semibold mb-3">
                <CheckCircle2 className="h-5 w-5" />
                Final Response
              </div>
              <pre className="text-sm whitespace-pre-wrap font-sans">{scenario.finalResponse}</pre>
            </motion.div>
          )}
        </AnimatePresence>
      </CardContent>
    </Card>
  );
}
