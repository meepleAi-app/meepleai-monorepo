'use client';

/**
 * AgentRagIntegration Component
 *
 * Explains how MeepleAI's agent system integrates with RAG.
 * Shows the 5-component prompt assembly and agent roles.
 */

import React, { useState } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Bot,
  FileText,
  MessageSquare,
  ListChecks,
  Code2,
  ChevronRight,
  Zap,
  Scale,
  Target,
  Eye,
  EyeOff,
  Brain,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { RagStrategy } from './types';

// =============================================================================
// Types
// =============================================================================

interface PromptComponent {
  id: string;
  name: string;
  icon: React.ReactNode;
  color: string;
  description: string;
  example: string;
}

interface AgentRole {
  id: string;
  name: string;
  template: string;
  description: string;
  systemPromptSnippet: string;
  color: string;
}

// =============================================================================
// Data
// =============================================================================

const PROMPT_COMPONENTS: PromptComponent[] = [
  {
    id: 'system',
    name: 'System Prompt',
    icon: <Bot className="h-5 w-5" />,
    color: 'hsl(221 83% 53%)',
    description: 'Agent role definition and base behavioral instructions',
    example: `You are MeepleAI's RulesExpert, specialized in board game rules interpretation.
Your expertise includes: rule clarification, edge case resolution, official errata.
Always cite specific rulebook sections. Maintain a helpful, patient tone.`,
  },
  {
    id: 'context',
    name: 'RAG Context',
    icon: <FileText className="h-5 w-5" />,
    color: 'hsl(142 76% 36%)',
    description: 'Retrieved documents from vector search with relevance scores',
    example: `[CONTEXT - Relevance: 0.92]
Source: Catan Rulebook, Page 12
"When rolling a 7, the robber MUST be moved. The active player..."

[CONTEXT - Relevance: 0.87]
Source: Catan FAQ v2.3
"Q: Can I move the robber to a hex with no settlements? A: Yes..."`,
  },
  {
    id: 'query',
    name: 'User Query',
    icon: <MessageSquare className="h-5 w-5" />,
    color: 'hsl(262 83% 62%)',
    description: 'The original question with metadata (tier, history)',
    example: `User Query: "What happens when I roll a 7 in Catan?"
User Tier: User
Session Context: First question about Catan rules
Language: English`,
  },
  {
    id: 'template',
    name: 'Template Instructions',
    icon: <ListChecks className="h-5 w-5" />,
    color: 'hsl(25 95% 53%)',
    description: 'Query-type specific formatting and response guidelines',
    example: `Template: rule_lookup
Instructions:
1. Provide the EXACT rule text first (quoted)
2. Explain in simple terms
3. Note any common misinterpretations
4. Include page/section reference
5. Mention related rules if relevant`,
  },
  {
    id: 'format',
    name: 'Output Format',
    icon: <Code2 className="h-5 w-5" />,
    color: 'hsl(0 72% 51%)',
    description: 'Structured response requirements and validation rules',
    example: `Response Format:
{
  "answer": "Clear explanation...",
  "citations": [{ "source": "...", "page": "..." }],
  "confidence": 0.95,
  "relatedTopics": ["robber placement", "7 roll frequency"]
}

Validation: Must include at least 1 citation.`,
  },
];

const AGENT_ROLES: AgentRole[] = [
  {
    id: 'rules_expert',
    name: 'RulesExpert',
    template: 'rule_lookup',
    description: 'Specializes in precise rules interpretation and edge cases',
    systemPromptSnippet: 'Expert in board game rules. Always cite exact rulebook text.',
    color: 'hsl(221 83% 53%)',
  },
  {
    id: 'strategy_coach',
    name: 'StrategyCoach',
    template: 'strategy_advice',
    description: 'Provides tactical insights and winning strategies',
    systemPromptSnippet: 'Strategy expert. Analyze positions, suggest optimal moves.',
    color: 'hsl(142 76% 36%)',
  },
  {
    id: 'setup_wizard',
    name: 'SetupWizard',
    template: 'setup_guide',
    description: 'Guides players through game setup step-by-step',
    systemPromptSnippet: 'Setup specialist. Clear, numbered instructions with images.',
    color: 'hsl(25 95% 53%)',
  },
  {
    id: 'game_assistant',
    name: 'GameAssistant',
    template: 'educational',
    description: 'General assistant for learning and exploration',
    systemPromptSnippet: 'Friendly guide. Explain concepts, suggest games, answer broadly.',
    color: 'hsl(262 83% 62%)',
  },
];

const STRATEGY_PROMPTS: Record<RagStrategy, { name: string; description: string; promptStyle: string }> = {
  FAST: {
    name: 'Direct Response',
    description: 'Single-pass generation with minimal context',
    promptStyle: `[FAST MODE]
System: {agent_role} (condensed)
Context: Top 3 chunks only
Query: {user_query}
→ Direct answer, no reflection loop`,
  },
  BALANCED: {
    name: 'CRAG-Enhanced',
    description: 'Includes quality evaluation and potential re-retrieval',
    promptStyle: `[BALANCED MODE]
System: {agent_role} (full)
Context: Top 5 chunks + CRAG evaluation
Query: {user_query}
Template: {template_instructions}
→ Answer with confidence score
→ If confidence < 0.7, trigger re-retrieval`,
  },
  PRECISE: {
    name: 'Multi-Agent Chain',
    description: 'Analyzer → Strategist → Validator pipeline',
    promptStyle: `[PRECISE MODE - 3 Agents]
1. ANALYZER: Extract key concepts from {user_query}
2. RETRIEVER: Multi-hop search based on analysis
3. SYNTHESIZER: Generate comprehensive answer
4. VALIDATOR: Self-RAG reflection + citation check
→ Full structured output with evidence chain`,
  },
  EXPERT: {
    name: 'Web-Augmented Expert',
    description: 'Web search + multi-hop reasoning for external info',
    promptStyle: `[EXPERT MODE - Web Search]
System: {agent_role} (expert level)
1. QUERY_EXPANDER: Reformulate for web search
2. WEB_SEARCHER: Find official FAQs, errata, forums
3. MULTI_HOP: Follow citation chains (2-3 hops)
4. SYNTHESIZER: Merge internal + external sources
→ Answer with external citations + confidence`,
  },
  CONSENSUS: {
    name: 'Multi-LLM Voting',
    description: '3 independent voters + aggregator for critical decisions',
    promptStyle: `[CONSENSUS MODE - 3 Voters]
1. VOTER_1 (Claude): Analyze from rule perspective
2. VOTER_2 (GPT-4o): Analyze from strategy view
3. VOTER_3 (DeepSeek): Analyze for edge cases
4. AGGREGATOR: Synthesize votes + resolve conflicts
→ Majority consensus with dissent notes`,
  },
  CUSTOM: {
    name: 'Admin-Configured',
    description: 'Custom phase combinations defined by admins',
    promptStyle: `[CUSTOM MODE - Admin Config]
System: {admin_custom_prompt}
Phases: {selected_phases}
Models: {selected_models}
Context: {custom_context_rules}
→ Flexible output per configuration`,
  },
};

// =============================================================================
// Sub-Components
// =============================================================================

interface ComponentCardProps {
  component: PromptComponent;
  isSelected: boolean;
  onClick: () => void;
}

function ComponentCard({ component, isSelected, onClick }: ComponentCardProps) {
  return (
    <motion.div
      className={cn(
        'relative p-4 rounded-xl border cursor-pointer transition-all',
        'hover:border-primary/50 hover:bg-muted/50',
        isSelected && 'border-primary bg-primary/5'
      )}
      onClick={onClick}
      whileHover={{ scale: 1.02 }}
      whileTap={{ scale: 0.98 }}
    >
      <div className="flex items-start gap-3">
        <div
          className="p-2 rounded-lg"
          style={{ backgroundColor: `${component.color}20`, color: component.color }}
        >
          {component.icon}
        </div>
        <div className="flex-1 min-w-0">
          <div className="font-semibold text-sm">{component.name}</div>
          <div className="text-xs text-muted-foreground mt-1">{component.description}</div>
        </div>
        <ChevronRight
          className={cn('h-4 w-4 text-muted-foreground transition-transform', isSelected && 'rotate-90')}
        />
      </div>
    </motion.div>
  );
}

interface AgentBadgeProps {
  agent: AgentRole;
  isSelected: boolean;
  onClick: () => void;
}

function AgentBadge({ agent, isSelected, onClick }: AgentBadgeProps) {
  return (
    <button
      className={cn(
        'px-3 py-2 rounded-lg text-sm font-medium transition-all',
        'border hover:border-primary/50',
        isSelected ? 'border-primary bg-primary/10 text-primary' : 'border-border bg-muted/30'
      )}
      onClick={onClick}
    >
      <span style={{ color: isSelected ? agent.color : undefined }}>{agent.name}</span>
      <span className="text-xs text-muted-foreground ml-2">({agent.template})</span>
    </button>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface AgentRagIntegrationProps {
  className?: string;
}

export function AgentRagIntegration({ className }: AgentRagIntegrationProps) {
  const [selectedComponent, setSelectedComponent] = useState<string>('system');
  const [selectedAgent, setSelectedAgent] = useState<string>('rules_expert');
  const [selectedStrategy, setSelectedStrategy] = useState<RagStrategy>('BALANCED');
  const [showFullPrompt, setShowFullPrompt] = useState(false);

  const activeComponent = PROMPT_COMPONENTS.find(c => c.id === selectedComponent);
  const activeAgent = AGENT_ROLES.find(a => a.id === selectedAgent);
  const activeStrategy = STRATEGY_PROMPTS[selectedStrategy];

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Bot className="h-5 w-5 text-primary" />
          Agent-RAG Integration
        </CardTitle>
        <p className="text-sm text-muted-foreground mt-1">
          How MeepleAI agents assemble prompts using RAG-retrieved context
        </p>
      </CardHeader>

      <CardContent className="space-y-8">
        {/* 5-Component Assembly */}
        <section>
          <h3 className="text-sm font-semibold mb-4 flex items-center gap-2">
            <span className="w-6 h-6 rounded-full bg-primary/20 text-primary text-xs flex items-center justify-center">
              1
            </span>
            5-Component Prompt Assembly
          </h3>

          <div className="grid lg:grid-cols-2 gap-6">
            {/* Component List */}
            <div className="space-y-3">
              {PROMPT_COMPONENTS.map(component => (
                <ComponentCard
                  key={component.id}
                  component={component}
                  isSelected={selectedComponent === component.id}
                  onClick={() => setSelectedComponent(component.id)}
                />
              ))}
            </div>

            {/* Example Preview */}
            <AnimatePresence mode="wait">
              {activeComponent && (
                <motion.div
                  key={activeComponent.id}
                  initial={{ opacity: 0, x: 20 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: -20 }}
                  transition={{ duration: 0.2 }}
                  className="relative"
                >
                  <div
                    className="p-4 rounded-xl border-2"
                    style={{ borderColor: activeComponent.color }}
                  >
                    <div
                      className="text-sm font-semibold mb-3"
                      style={{ color: activeComponent.color }}
                    >
                      Example: {activeComponent.name}
                    </div>
                    <pre className="text-xs bg-muted/50 p-3 rounded-lg overflow-x-auto whitespace-pre-wrap font-mono">
                      {activeComponent.example}
                    </pre>
                  </div>

                  {/* Flow indicator */}
                  <div className="absolute -bottom-6 left-1/2 -translate-x-1/2">
                    <div className="w-0.5 h-6 bg-gradient-to-b from-border to-transparent" />
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        </section>

        {/* Agent Roles */}
        <section>
          <h3 className="text-sm font-semibold mb-4 flex items-center gap-2">
            <span className="w-6 h-6 rounded-full bg-primary/20 text-primary text-xs flex items-center justify-center">
              2
            </span>
            Specialized Agent Roles
          </h3>

          <div className="flex flex-wrap gap-2 mb-4">
            {AGENT_ROLES.map(agent => (
              <AgentBadge
                key={agent.id}
                agent={agent}
                isSelected={selectedAgent === agent.id}
                onClick={() => setSelectedAgent(agent.id)}
              />
            ))}
          </div>

          {activeAgent && (
            <motion.div
              key={activeAgent.id}
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              className="p-4 rounded-xl bg-muted/30 border"
            >
              <div className="flex items-start gap-4">
                <div
                  className="p-3 rounded-xl"
                  style={{ backgroundColor: `${activeAgent.color}20` }}
                >
                  <Bot className="h-6 w-6" style={{ color: activeAgent.color }} />
                </div>
                <div className="flex-1">
                  <div className="font-semibold" style={{ color: activeAgent.color }}>
                    {activeAgent.name}
                  </div>
                  <div className="text-sm text-muted-foreground mt-1">{activeAgent.description}</div>
                  <div className="mt-3 p-2 bg-muted/50 rounded-lg">
                    <div className="text-xs text-muted-foreground mb-1">System Prompt Snippet:</div>
                    <code className="text-xs font-mono">&quot;{activeAgent.systemPromptSnippet}&quot;</code>
                  </div>
                </div>
              </div>
            </motion.div>
          )}
        </section>

        {/* Strategy-Specific Prompts */}
        <section>
          <h3 className="text-sm font-semibold mb-4 flex items-center gap-2">
            <span className="w-6 h-6 rounded-full bg-primary/20 text-primary text-xs flex items-center justify-center">
              3
            </span>
            Strategy-Specific Prompt Patterns
          </h3>

          <div className="flex flex-wrap gap-2 mb-4">
            {(['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'] as RagStrategy[]).map(strategy => {
              const icons: Record<RagStrategy, React.ReactNode> = {
                FAST: <Zap className="h-4 w-4" />,
                BALANCED: <Scale className="h-4 w-4" />,
                PRECISE: <Target className="h-4 w-4" />,
                EXPERT: <Brain className="h-4 w-4" />,
                CONSENSUS: <MessageSquare className="h-4 w-4" />,
                CUSTOM: <Code2 className="h-4 w-4" />,
              };
              return (
                <button
                  key={strategy}
                  className={cn(
                    'flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all',
                    'border hover:border-primary/50',
                    selectedStrategy === strategy
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'border-border bg-muted/30'
                  )}
                  onClick={() => setSelectedStrategy(strategy)}
                >
                  {icons[strategy]}
                  {strategy}
                </button>
              );
            })}
          </div>

          <div className="p-4 rounded-xl bg-muted/30 border">
            <div className="flex items-center justify-between mb-3">
              <div>
                <div className="font-semibold">{activeStrategy.name}</div>
                <div className="text-sm text-muted-foreground">{activeStrategy.description}</div>
              </div>
            </div>
            <pre className="text-xs bg-background p-3 rounded-lg overflow-x-auto whitespace-pre-wrap font-mono border">
              {activeStrategy.promptStyle}
            </pre>
          </div>
        </section>

        {/* Full Prompt Preview */}
        <section>
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-primary/20 text-primary text-xs flex items-center justify-center">
                4
              </span>
              Full Prompt Preview
            </h3>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowFullPrompt(!showFullPrompt)}
            >
              {showFullPrompt ? (
                <>
                  <EyeOff className="h-4 w-4 mr-2" />
                  Hide
                </>
              ) : (
                <>
                  <Eye className="h-4 w-4 mr-2" />
                  Show
                </>
              )}
            </Button>
          </div>

          <AnimatePresence>
            {showFullPrompt && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                transition={{ duration: 0.3 }}
                className="overflow-hidden"
              >
                <div className="p-4 rounded-xl bg-gradient-to-br from-muted/50 to-muted/30 border max-h-96 overflow-y-auto">
                  <pre className="text-xs font-mono whitespace-pre-wrap">
                    {`╔════════════════════════════════════════════════════════════════╗
║                    ASSEMBLED PROMPT                            ║
║              ${activeAgent?.name || 'RulesExpert'} + ${selectedStrategy} Strategy                   ║
╚════════════════════════════════════════════════════════════════╝

┌─────────────────────────────────────────────────────────────────┐
│ [1] SYSTEM PROMPT                                               │
└─────────────────────────────────────────────────────────────────┘
You are MeepleAI's ${activeAgent?.name || 'RulesExpert'}.
${activeAgent?.systemPromptSnippet || 'Expert in board game rules.'}

Response Guidelines:
- Be accurate and cite sources
- Use clear, accessible language
- Maintain helpful tone

┌─────────────────────────────────────────────────────────────────┐
│ [2] RAG CONTEXT (Retrieved)                                     │
└─────────────────────────────────────────────────────────────────┘
[Document 1 - Score: 0.94]
Source: Catan Rulebook v5, Page 12
"When a 7 is rolled, every player counts their resource cards.
Anyone with more than 7 cards must discard half (rounded down)..."

[Document 2 - Score: 0.89]
Source: Catan Official FAQ
"The robber must be moved when a 7 is rolled. You cannot choose
to leave it in place..."

┌─────────────────────────────────────────────────────────────────┐
│ [3] USER QUERY                                                  │
└─────────────────────────────────────────────────────────────────┘
Query: "What happens when I roll a 7 in Catan?"
User Tier: User
Template: rule_lookup

┌─────────────────────────────────────────────────────────────────┐
│ [4] TEMPLATE INSTRUCTIONS (rule_lookup)                         │
└─────────────────────────────────────────────────────────────────┘
Format your response as:
1. Quote the exact rule text
2. Explain in simple terms
3. Note common mistakes
4. Include page reference

┌─────────────────────────────────────────────────────────────────┐
│ [5] OUTPUT FORMAT                                               │
└─────────────────────────────────────────────────────────────────┘
Respond with JSON:
{
  "answer": "<your explanation>",
  "ruleQuote": "<exact text from rulebook>",
  "citations": [{"source": "...", "page": "..."}],
  "confidence": <0.0-1.0>,
  "commonMistakes": ["..."],
  "relatedRules": ["..."]
}

══════════════════════════════════════════════════════════════════
Total Estimated Tokens: ~${selectedStrategy === 'FAST' ? '2,100' : selectedStrategy === 'BALANCED' ? '3,350' : '12,900'}
Strategy: ${selectedStrategy}
══════════════════════════════════════════════════════════════════`}
                  </pre>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </section>

        {/* Visual Flow Diagram */}
        <section className="pt-4 border-t border-border">
          <h3 className="text-sm font-semibold mb-4">Prompt Assembly Flow</h3>
          <div className="flex flex-wrap items-center justify-center gap-2 text-xs">
            {PROMPT_COMPONENTS.map((component, index) => (
              <React.Fragment key={component.id}>
                <div
                  className="px-3 py-2 rounded-lg font-medium"
                  style={{
                    backgroundColor: `${component.color}20`,
                    color: component.color,
                    border: `1px solid ${component.color}40`,
                  }}
                >
                  {component.name}
                </div>
                {index < PROMPT_COMPONENTS.length - 1 && (
                  <span className="text-muted-foreground">→</span>
                )}
              </React.Fragment>
            ))}
            <span className="text-muted-foreground ml-2">→</span>
            <div className="px-3 py-2 rounded-lg font-medium bg-primary/20 text-primary border border-primary/40">
              🤖 LLM Response
            </div>
          </div>
        </section>
      </CardContent>
    </Card>
  );
}
