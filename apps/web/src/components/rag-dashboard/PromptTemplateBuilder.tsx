'use client';

/**
 * PromptTemplateBuilder Component
 *
 * Interactive prompt assembly UI for building RAG prompts.
 * Allows users to select components and preview the final prompt.
 */

import React, { useState, useMemo } from 'react';

import { motion } from 'framer-motion';
import {
  Wand2,
  Bot,
  FileText,
  MessageSquare,
  ListChecks,
  Code2,
  Copy,
  Check,
  RefreshCw,
  Zap,
  Scale,
  Target,
  Download,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import { STRATEGIES } from './rag-data';

import type { RagStrategy, QueryTemplate } from './types';

// =============================================================================
// Types
// =============================================================================

interface AgentConfig {
  id: string;
  name: string;
  systemPrompt: string;
  specialization: string;
  color: string;
}

interface TemplateConfig {
  id: QueryTemplate;
  name: string;
  instructions: string;
  outputFormat: string;
}

interface ContextDocument {
  id: string;
  title: string;
  content: string;
  relevance: number;
}

// =============================================================================
// Data
// =============================================================================

const AGENTS: AgentConfig[] = [
  {
    id: 'rules_expert',
    name: 'RulesExpert',
    systemPrompt: `You are MeepleAI's RulesExpert, a specialist in board game rules interpretation.

Your expertise includes:
- Precise rule clarification with citations
- Edge case resolution
- Official errata and FAQ integration
- Tournament-level rule accuracy

Guidelines:
- Always cite specific rulebook sections
- Distinguish between official rules and house rules
- Be patient and thorough in explanations
- Acknowledge ambiguity when present`,
    specialization: 'Rules interpretation and edge cases',
    color: 'hsl(221 83% 53%)',
  },
  {
    id: 'strategy_coach',
    name: 'StrategyCoach',
    systemPrompt: `You are MeepleAI's StrategyCoach, an expert in board game strategy and tactics.

Your expertise includes:
- Opening strategies and early game positioning
- Mid-game tactics and resource management
- End-game optimization
- Player count adaptations

Guidelines:
- Analyze the current position before suggesting moves
- Consider multiple strategic approaches
- Explain the reasoning behind recommendations
- Account for opponent counterplay`,
    specialization: 'Strategy analysis and tactical advice',
    color: 'hsl(142 76% 36%)',
  },
  {
    id: 'setup_wizard',
    name: 'SetupWizard',
    systemPrompt: `You are MeepleAI's SetupWizard, a guide for board game setup procedures.

Your expertise includes:
- Step-by-step setup instructions
- Player count variations
- Expansion integration
- Component organization

Guidelines:
- Use numbered lists for clarity
- Include component counts and placements
- Note common setup mistakes
- Provide visual references when helpful`,
    specialization: 'Game setup and component organization',
    color: 'hsl(25 95% 53%)',
  },
  {
    id: 'game_assistant',
    name: 'GameAssistant',
    systemPrompt: `You are MeepleAI's GameAssistant, a friendly guide for all board game questions.

Your expertise includes:
- Game recommendations based on preferences
- Learning new games
- Group game selection
- General board game knowledge

Guidelines:
- Be friendly and encouraging
- Adapt explanations to skill level
- Suggest related games when appropriate
- Make learning fun and accessible`,
    specialization: 'General assistance and game recommendations',
    color: 'hsl(262 83% 62%)',
  },
];

const TEMPLATES: TemplateConfig[] = [
  {
    id: 'rule_lookup',
    name: 'Rule Lookup',
    instructions: `Answer format for rule questions:
1. Quote the EXACT rule text from the source
2. Explain the rule in simple terms
3. Provide common examples
4. Note frequent misinterpretations
5. Include page/section reference`,
    outputFormat: `{
  "ruleQuote": "Exact text from rulebook",
  "explanation": "Clear explanation",
  "examples": ["Example 1", "Example 2"],
  "commonMistakes": ["Mistake 1"],
  "citation": { "source": "Rulebook", "page": "12" }
}`,
  },
  {
    id: 'resource_planning',
    name: 'Resource Planning',
    instructions: `Answer format for resource questions:
1. Calculate exact resource requirements
2. Show the math/formula used
3. Provide scaling for different player counts
4. Include efficiency tips
5. Note resource scarcity scenarios`,
    outputFormat: `{
  "calculation": "Detailed calculation",
  "formula": "Formula used",
  "perPlayerCount": { "2": 10, "3": 15, "4": 20 },
  "tips": ["Efficiency tip 1"],
  "notes": "Additional context"
}`,
  },
  {
    id: 'setup_guide',
    name: 'Setup Guide',
    instructions: `Answer format for setup questions:
1. List all components needed
2. Provide numbered setup steps
3. Include placement diagrams descriptions
4. Note player count variations
5. Highlight common setup errors`,
    outputFormat: `{
  "components": ["Component list"],
  "steps": [
    { "step": 1, "action": "...", "notes": "..." }
  ],
  "variations": { "2p": "...", "4p": "..." },
  "warnings": ["Common error 1"]
}`,
  },
  {
    id: 'strategy_advice',
    name: 'Strategy Advice',
    instructions: `Answer format for strategy questions:
1. Analyze the current game state
2. Identify key decision points
3. Provide 2-3 strategic options
4. Explain pros/cons of each
5. Give a recommended action with reasoning`,
    outputFormat: `{
  "analysis": "Current state assessment",
  "options": [
    { "option": "...", "pros": [...], "cons": [...] }
  ],
  "recommendation": "Best choice",
  "reasoning": "Why this is optimal"
}`,
  },
  {
    id: 'educational',
    name: 'Educational',
    instructions: `Answer format for learning questions:
1. Start with a simple overview
2. Break down complex concepts
3. Use analogies when helpful
4. Build from basics to advanced
5. Include practice scenarios`,
    outputFormat: `{
  "overview": "Simple introduction",
  "concepts": [
    { "name": "...", "explanation": "...", "example": "..." }
  ],
  "analogies": ["Real-world comparison"],
  "practiceScenarios": ["Try this..."]
}`,
  },
];

const SAMPLE_CONTEXTS: ContextDocument[] = [
  {
    id: 'ctx1',
    title: 'Catan Rulebook - Rolling a 7',
    content: `When a "7" is rolled, no one receives any resources. Instead:
1. Each player counts their Resource Cards. Any player with more than 7 Resource Cards must select half (rounded down) of them and return them to their respective supply stacks.
2. The player who rolled the "7" must move the robber to a different hex.
3. That player may steal 1 Resource Card from any one player who has a settlement or city adjacent to the robber's new hex.`,
    relevance: 0.94,
  },
  {
    id: 'ctx2',
    title: 'Catan FAQ - Robber Questions',
    content: `Q: Can I move the robber to the desert?
A: Yes, the robber can be moved to the desert hex. No one will be robbed when the robber is on the desert.

Q: Can I choose not to steal a card?
A: No, if there is at least one adjacent settlement/city with a player who has cards, you must steal from one of them.`,
    relevance: 0.89,
  },
  {
    id: 'ctx3',
    title: 'Catan Strategy Guide - 7 Roll',
    content: `The "7" roll occurs approximately 16.67% of the time (most common roll). Strategic considerations:
- Keep your hand at 7 or fewer cards when possible
- Target players with the most cards or key resources
- Use robber placement to block opponents' best hexes`,
    relevance: 0.82,
  },
];

/**
 * Strategy configuration for UI display.
 * Uses STRATEGIES from rag-data.ts as Single Source of Truth for descriptions.
 */
const STRATEGY_UI_CONFIG: Record<RagStrategy, { label: string; icon: React.ReactNode; description: string }> = {
  FAST: { label: 'FAST', icon: <Zap className="h-4 w-4" />, description: STRATEGIES.FAST.descriptionEn },
  BALANCED: { label: 'BALANCED', icon: <Scale className="h-4 w-4" />, description: STRATEGIES.BALANCED.descriptionEn },
  PRECISE: { label: 'PRECISE', icon: <Target className="h-4 w-4" />, description: STRATEGIES.PRECISE.descriptionEn },
  EXPERT: { label: 'EXPERT', icon: <Zap className="h-4 w-4" />, description: STRATEGIES.EXPERT.descriptionEn },
  CONSENSUS: { label: 'CONSENSUS', icon: <Scale className="h-4 w-4" />, description: STRATEGIES.CONSENSUS.descriptionEn },
  CUSTOM: { label: 'CUSTOM', icon: <Target className="h-4 w-4" />, description: STRATEGIES.CUSTOM.descriptionEn },
};

// =============================================================================
// Sub-Components
// =============================================================================

interface SelectionCardProps {
  title: string;
  items: { id: string; name: string; color?: string }[];
  selected: string;
  onSelect: (id: string) => void;
  icon: React.ReactNode;
}

function SelectionCard({ title, items, selected, onSelect, icon }: SelectionCardProps) {
  return (
    <div className="space-y-2">
      <div className="text-xs font-medium text-muted-foreground flex items-center gap-2">
        {icon}
        {title}
      </div>
      <div className="flex flex-wrap gap-2">
        {items.map(item => (
          <button
            key={item.id}
            className={cn(
              'px-3 py-1.5 rounded-lg text-sm font-medium transition-all',
              'border hover:border-primary/50',
              selected === item.id
                ? 'border-primary bg-primary/10 text-primary'
                : 'border-border bg-muted/30'
            )}
            style={
              selected === item.id && item.color
                ? { borderColor: item.color, backgroundColor: `${item.color}20`, color: item.color }
                : undefined
            }
            onClick={() => onSelect(item.id)}
          >
            {item.name}
          </button>
        ))}
      </div>
    </div>
  );
}

interface ContextSelectorProps {
  contexts: ContextDocument[];
  selected: string[];
  onToggle: (id: string) => void;
}

function ContextSelector({ contexts, selected, onToggle }: ContextSelectorProps) {
  return (
    <div className="space-y-2">
      <div className="text-xs font-medium text-muted-foreground flex items-center gap-2">
        <FileText className="h-3 w-3" />
        RAG Context (Select documents to include)
      </div>
      <div className="space-y-2 max-h-48 overflow-y-auto">
        {contexts.map(ctx => (
          <button
            key={ctx.id}
            className={cn(
              'w-full p-3 rounded-lg text-left transition-all border',
              selected.includes(ctx.id)
                ? 'border-green-500 bg-green-500/10'
                : 'border-border bg-muted/30 hover:border-primary/50'
            )}
            onClick={() => onToggle(ctx.id)}
          >
            <div className="flex items-center justify-between mb-1">
              <span className="text-sm font-medium">{ctx.title}</span>
              <span className="text-xs px-2 py-0.5 rounded-full bg-muted">
                {Math.round(ctx.relevance * 100)}% relevant
              </span>
            </div>
            <p className="text-xs text-muted-foreground line-clamp-2">{ctx.content}</p>
          </button>
        ))}
      </div>
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface PromptTemplateBuilderProps {
  className?: string;
}

export function PromptTemplateBuilder({ className }: PromptTemplateBuilderProps) {
  const [selectedAgent, setSelectedAgent] = useState<string>('rules_expert');
  const [selectedTemplate, setSelectedTemplate] = useState<QueryTemplate>('rule_lookup');
  const [selectedStrategy, setSelectedStrategy] = useState<RagStrategy>('BALANCED');
  const [selectedContexts, setSelectedContexts] = useState<string[]>(['ctx1', 'ctx2']);
  const [userQuery, setUserQuery] = useState('What happens when I roll a 7 in Catan?');
  const [copied, setCopied] = useState(false);

  const agent = AGENTS.find(a => a.id === selectedAgent);
  const template = TEMPLATES.find(t => t.id === selectedTemplate);
  const contexts = SAMPLE_CONTEXTS.filter(c => selectedContexts.includes(c.id));

  // Build the full prompt
  const assembledPrompt = useMemo(() => {
    if (!agent || !template) return '';

    const contextSection = contexts
      .map(
        (ctx, i) => `[CONTEXT ${i + 1} - Relevance: ${ctx.relevance}]
Source: ${ctx.title}
${ctx.content}`
      )
      .join('\n\n');

    return `═══════════════════════════════════════════════════════════════════
                    ASSEMBLED RAG PROMPT
           Agent: ${agent.name} | Strategy: ${selectedStrategy}
═══════════════════════════════════════════════════════════════════

┌─────────────────────────────────────────────────────────────────┐
│ [1] SYSTEM PROMPT                                               │
└─────────────────────────────────────────────────────────────────┘
${agent.systemPrompt}

┌─────────────────────────────────────────────────────────────────┐
│ [2] RAG CONTEXT                                                 │
└─────────────────────────────────────────────────────────────────┘
${contextSection || '[No context selected]'}

┌─────────────────────────────────────────────────────────────────┐
│ [3] USER QUERY                                                  │
└─────────────────────────────────────────────────────────────────┘
${userQuery || '[No query entered]'}

┌─────────────────────────────────────────────────────────────────┐
│ [4] TEMPLATE INSTRUCTIONS (${template.name})                    │
└─────────────────────────────────────────────────────────────────┘
${template.instructions}

┌─────────────────────────────────────────────────────────────────┐
│ [5] OUTPUT FORMAT                                               │
└─────────────────────────────────────────────────────────────────┘
${template.outputFormat}

═══════════════════════════════════════════════════════════════════`;
  }, [agent, template, contexts, selectedStrategy, userQuery]);

  // Estimate token count (rough approximation)
  const estimatedTokens = useMemo(() => {
    return Math.round(assembledPrompt.length / 4);
  }, [assembledPrompt]);

  const handleCopy = () => {
    navigator.clipboard.writeText(assembledPrompt);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleExport = () => {
    const blob = new Blob([assembledPrompt], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `rag-prompt-${selectedAgent}-${selectedTemplate}.txt`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleReset = () => {
    setSelectedAgent('rules_expert');
    setSelectedTemplate('rule_lookup');
    setSelectedStrategy('BALANCED');
    setSelectedContexts(['ctx1', 'ctx2']);
    setUserQuery('What happens when I roll a 7 in Catan?');
  };

  const toggleContext = (id: string) => {
    setSelectedContexts(prev => (prev.includes(id) ? prev.filter(c => c !== id) : [...prev, id]));
  };

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2">
            <Wand2 className="h-5 w-5 text-primary" />
            Prompt Template Builder
          </CardTitle>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={handleReset}>
              <RefreshCw className="h-4 w-4 mr-2" />
              Reset
            </Button>
          </div>
        </div>
        <p className="text-sm text-muted-foreground mt-1">
          Build and preview RAG prompts by selecting components
        </p>
      </CardHeader>

      <CardContent className="space-y-6">
        <div className="grid lg:grid-cols-2 gap-6">
          {/* Left Column - Selectors */}
          <div className="space-y-6">
            {/* Agent Selection */}
            <SelectionCard
              title="Agent Role"
              items={AGENTS.map(a => ({ id: a.id, name: a.name, color: a.color }))}
              selected={selectedAgent}
              onSelect={setSelectedAgent}
              icon={<Bot className="h-3 w-3" />}
            />

            {/* Template Selection */}
            <SelectionCard
              title="Query Template"
              items={TEMPLATES.map(t => ({ id: t.id, name: t.name }))}
              selected={selectedTemplate}
              onSelect={id => setSelectedTemplate(id as QueryTemplate)}
              icon={<ListChecks className="h-3 w-3" />}
            />

            {/* Strategy Selection */}
            <div className="space-y-2">
              <div className="text-xs font-medium text-muted-foreground flex items-center gap-2">
                <Zap className="h-3 w-3" />
                RAG Strategy
              </div>
              <div className="flex gap-2">
                {(['FAST', 'BALANCED', 'PRECISE'] as RagStrategy[]).map(strategy => {
                  const config = STRATEGY_UI_CONFIG[strategy];
                  return (
                    <button
                      key={strategy}
                      className={cn(
                        'flex-1 flex flex-col items-center gap-1 p-3 rounded-lg transition-all',
                        'border hover:border-primary/50',
                        selectedStrategy === strategy
                          ? 'border-primary bg-primary/10 text-primary'
                          : 'border-border bg-muted/30'
                      )}
                      onClick={() => setSelectedStrategy(strategy)}
                    >
                      {config.icon}
                      <span className="text-sm font-medium">{config.label}</span>
                      <span className="text-[10px] text-muted-foreground text-center">
                        {config.description}
                      </span>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Context Selection */}
            <ContextSelector
              contexts={SAMPLE_CONTEXTS}
              selected={selectedContexts}
              onToggle={toggleContext}
            />

            {/* User Query */}
            <div className="space-y-2">
              <div className="text-xs font-medium text-muted-foreground flex items-center gap-2">
                <MessageSquare className="h-3 w-3" />
                User Query
              </div>
              <textarea
                value={userQuery}
                onChange={e => setUserQuery(e.target.value)}
                placeholder="Enter your question..."
                className="w-full p-3 rounded-lg bg-muted/50 border border-border focus:border-primary focus:ring-1 focus:ring-primary text-sm resize-none"
                rows={3}
              />
            </div>
          </div>

          {/* Right Column - Preview */}
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <div className="text-xs font-medium text-muted-foreground flex items-center gap-2">
                <Code2 className="h-3 w-3" />
                Assembled Prompt Preview
              </div>
              <div className="flex items-center gap-2">
                <span className="text-xs text-muted-foreground">
                  ~{estimatedTokens.toLocaleString()} tokens
                </span>
                <Button variant="ghost" size="sm" onClick={handleCopy} className="h-7">
                  {copied ? <Check className="h-3 w-3 text-green-500" /> : <Copy className="h-3 w-3" />}
                </Button>
                <Button variant="ghost" size="sm" onClick={handleExport} className="h-7">
                  <Download className="h-3 w-3" />
                </Button>
              </div>
            </div>

            <motion.div
              className="relative"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              transition={{ duration: 0.3 }}
            >
              <pre className="text-xs bg-muted/50 p-4 rounded-xl overflow-auto max-h-[500px] font-mono border whitespace-pre-wrap">
                {assembledPrompt}
              </pre>
            </motion.div>

            {/* Token Breakdown */}
            <div className="grid grid-cols-5 gap-2 text-center text-xs">
              <div className="p-2 rounded-lg bg-blue-500/10 border border-blue-500/20">
                <div className="text-muted-foreground">System</div>
                <div className="font-mono font-semibold text-blue-500">
                  ~{Math.round((agent?.systemPrompt.length || 0) / 4)}
                </div>
              </div>
              <div className="p-2 rounded-lg bg-green-500/10 border border-green-500/20">
                <div className="text-muted-foreground">Context</div>
                <div className="font-mono font-semibold text-green-500">
                  ~{Math.round(contexts.reduce((sum, c) => sum + c.content.length, 0) / 4)}
                </div>
              </div>
              <div className="p-2 rounded-lg bg-purple-500/10 border border-purple-500/20">
                <div className="text-muted-foreground">Query</div>
                <div className="font-mono font-semibold text-purple-500">
                  ~{Math.round(userQuery.length / 4)}
                </div>
              </div>
              <div className="p-2 rounded-lg bg-orange-500/10 border border-orange-500/20">
                <div className="text-muted-foreground">Template</div>
                <div className="font-mono font-semibold text-orange-500">
                  ~{Math.round((template?.instructions.length || 0) / 4)}
                </div>
              </div>
              <div className="p-2 rounded-lg bg-red-500/10 border border-red-500/20">
                <div className="text-muted-foreground">Format</div>
                <div className="font-mono font-semibold text-red-500">
                  ~{Math.round((template?.outputFormat.length || 0) / 4)}
                </div>
              </div>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
