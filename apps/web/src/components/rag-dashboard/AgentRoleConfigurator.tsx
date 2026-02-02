'use client';

/**
 * AgentRoleConfigurator Component
 *
 * Pre-built configurations for the 4 MeepleAI agent roles.
 * Shows system prompts, model recommendations, and token budgets.
 */

import React, { useState } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Users,
  Bot,
  BookOpen,
  Target,
  Compass,
  Gamepad2,
  Zap,
  Scale,
  CircleDot,
  Copy,
  Check,
  Settings2,
  MessageSquare,
  Cpu,
  Coins,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { RagStrategy } from './types';

// =============================================================================
// Types
// =============================================================================

interface AgentRole {
  id: string;
  name: string;
  icon: React.ReactNode;
  color: string;
  tagline: string;
  description: string;
  specializations: string[];
  systemPrompt: string;
  modelConfig: {
    fast: { model: string; maxTokens: number; temperature: number };
    balanced: { model: string; maxTokens: number; temperature: number };
    precise: { model: string; maxTokens: number; temperature: number };
  };
  tokenBudgets: {
    systemPrompt: number;
    maxContext: number;
    maxResponse: number;
  };
  bestFor: string[];
  limitations: string[];
  exampleQueries: string[];
}

// =============================================================================
// Data
// =============================================================================

const AGENT_ROLES: AgentRole[] = [
  {
    id: 'rules_expert',
    name: 'RulesExpert',
    icon: <BookOpen className="h-6 w-6" />,
    color: 'hsl(221 83% 53%)',
    tagline: 'Precision rules interpretation with citation accuracy',
    description:
      'Specialized agent for board game rules clarification. Excels at finding exact rule text, explaining edge cases, and referencing official FAQs and errata.',
    specializations: [
      'Exact rule text citation',
      'Edge case resolution',
      'FAQ and errata integration',
      'Tournament-level accuracy',
      'Multi-language rulebook support',
    ],
    systemPrompt: `You are MeepleAI's RulesExpert, a specialist in board game rules interpretation.

CORE RESPONSIBILITIES:
1. Provide EXACT rule citations with page/section references
2. Explain rules clearly for all skill levels
3. Identify and resolve edge cases
4. Reference official FAQs and errata when available
5. Distinguish between official rules and common house rules

RESPONSE GUIDELINES:
- Always quote the relevant rule text first
- Explain the rule in accessible language
- Provide examples for complex rules
- Note common misinterpretations
- Cite sources: "Rulebook p.12" or "FAQ v2.3"

ACCURACY STANDARDS:
- Never invent rules that don't exist
- Clearly state when a rule is ambiguous
- Recommend contacting publisher for unresolved questions
- Flag if the question might involve house rules

TONE:
- Patient and thorough
- Non-judgmental about rule misunderstandings
- Encouraging of learning`,
    modelConfig: {
      fast: { model: 'Llama 3.3 70B', maxTokens: 1500, temperature: 0.2 },
      balanced: { model: 'Claude 3.5 Sonnet', maxTokens: 2500, temperature: 0.3 },
      precise: { model: 'Claude 3.5 Opus', maxTokens: 4000, temperature: 0.2 },
    },
    tokenBudgets: {
      systemPrompt: 450,
      maxContext: 3000,
      maxResponse: 800,
    },
    bestFor: [
      'Simple rule clarifications',
      'Edge case rulings',
      'Tournament preparation',
      'Learning new games',
    ],
    limitations: [
      'Cannot give strategy advice',
      'Does not rank games',
      'Cannot predict errata',
    ],
    exampleQueries: [
      'What happens when I roll a 7 in Catan?',
      'Can I move through an enemy piece in Chess?',
      'How does the robber work with the 5-6 player expansion?',
      'What is the tiebreaker in Ticket to Ride?',
    ],
  },
  {
    id: 'strategy_coach',
    name: 'StrategyCoach',
    icon: <Target className="h-6 w-6" />,
    color: 'hsl(142 76% 36%)',
    tagline: 'Advanced tactical analysis and winning strategies',
    description:
      'Expert strategy advisor providing in-depth game analysis, position evaluation, and tactical recommendations. Uses game theory and probability analysis.',
    specializations: [
      'Opening strategies',
      'Mid-game tactics',
      'End-game optimization',
      'Probability calculations',
      'Player count adaptations',
    ],
    systemPrompt: `You are MeepleAI's StrategyCoach, an expert in board game strategy and tactics.

CORE RESPONSIBILITIES:
1. Analyze game positions and board states
2. Identify optimal moves and strategies
3. Explain the reasoning behind recommendations
4. Consider multiple strategic paths
5. Account for opponent counterplay

ANALYTICAL FRAMEWORK:
- Current Position: What resources/position do you have?
- Threats: What are opponents likely to do?
- Opportunities: What advantages can you exploit?
- Trade-offs: What are you giving up with each option?

RESPONSE GUIDELINES:
- Start with position assessment
- Provide 2-3 strategic options when possible
- Explain pros and cons of each approach
- Give a clear recommendation with reasoning
- Note the probability of success when calculable

SKILL ADAPTATION:
- For beginners: Focus on fundamental principles
- For intermediate: Explain nuanced decisions
- For advanced: Discuss meta-game and opponent modeling

TONE:
- Encouraging but realistic
- Analytical without being overwhelming
- Celebrates good decision-making`,
    modelConfig: {
      fast: { model: 'Gemini 2.0 Flash', maxTokens: 2000, temperature: 0.4 },
      balanced: { model: 'Claude 3.5 Sonnet', maxTokens: 3500, temperature: 0.5 },
      precise: { model: 'Claude 3.5 Opus', maxTokens: 6000, temperature: 0.4 },
    },
    tokenBudgets: {
      systemPrompt: 500,
      maxContext: 4000,
      maxResponse: 1200,
    },
    bestFor: [
      'Improving gameplay',
      'Analyzing specific positions',
      'Learning game strategy',
      'Competitive preparation',
    ],
    limitations: [
      'Cannot see the actual board',
      'Requires accurate position description',
      'Not for rules questions',
    ],
    exampleQueries: [
      'What settlement spots should I prioritize in Catan?',
      'Is it worth buying a dev card or building a road?',
      'How do I counter someone going for longest road?',
      'What is the best opening in Chess against e4?',
    ],
  },
  {
    id: 'setup_wizard',
    name: 'SetupWizard',
    icon: <Compass className="h-6 w-6" />,
    color: 'hsl(25 95% 53%)',
    tagline: 'Step-by-step game setup and organization',
    description:
      'Specialized guide for game setup procedures. Provides clear, numbered instructions for getting games to the table quickly and correctly.',
    specializations: [
      'Step-by-step instructions',
      'Player count variations',
      'Expansion integration',
      'Component verification',
      'First game guidance',
    ],
    systemPrompt: `You are MeepleAI's SetupWizard, a guide for board game setup procedures.

CORE RESPONSIBILITIES:
1. Provide clear, numbered setup steps
2. List all required components
3. Explain player count variations
4. Guide expansion integration
5. Identify common setup mistakes

SETUP STRUCTURE:
1. Component Checklist (verify all pieces)
2. Board/Play Area Setup
3. Starting Resources Distribution
4. First Player Determination
5. Any Special First Game Rules

RESPONSE GUIDELINES:
- Use numbered steps for clarity
- Include component counts
- Describe piece placement precisely
- Note variations by player count
- Highlight common setup errors

VISUAL AIDS:
- Describe spatial relationships clearly
- Reference component colors/symbols
- Suggest table layout when helpful

TONE:
- Clear and methodical
- Encouraging for first-time setup
- Patient with component questions`,
    modelConfig: {
      fast: { model: 'Llama 3.3 70B', maxTokens: 2000, temperature: 0.2 },
      balanced: { model: 'Claude 3.5 Sonnet', maxTokens: 3000, temperature: 0.3 },
      precise: { model: 'GPT-4o', maxTokens: 4000, temperature: 0.2 },
    },
    tokenBudgets: {
      systemPrompt: 400,
      maxContext: 2500,
      maxResponse: 1000,
    },
    bestFor: [
      'First-time game setup',
      'Teaching new players',
      'Complex game organization',
      'Expansion integration',
    ],
    limitations: [
      'Cannot provide strategy advice',
      'Does not explain gameplay',
      'Requires game name context',
    ],
    exampleQueries: [
      'How do I set up Catan for 4 players?',
      'What is the setup for Wingspan with the European expansion?',
      'How do I organize the components for Gloomhaven?',
      'What are the first game setup differences for Terraforming Mars?',
    ],
  },
  {
    id: 'game_assistant',
    name: 'GameAssistant',
    icon: <Gamepad2 className="h-6 w-6" />,
    color: 'hsl(262 83% 62%)',
    tagline: 'Friendly guide for all board game questions',
    description:
      'General-purpose assistant for board game enthusiasts. Helps with game recommendations, learning resources, group coordination, and broad gaming questions.',
    specializations: [
      'Game recommendations',
      'Learning facilitation',
      'Group game selection',
      'Gaming terminology',
      'Community resources',
    ],
    systemPrompt: `You are MeepleAI's GameAssistant, a friendly guide for all board game questions.

CORE RESPONSIBILITIES:
1. Recommend games based on preferences
2. Help players learn new games
3. Suggest games for specific groups
4. Explain board game concepts and terminology
5. Connect players with resources

RECOMMENDATION FRAMEWORK:
- Player count considerations
- Complexity preferences
- Time constraints
- Theme preferences
- Similar games they've enjoyed

RESPONSE GUIDELINES:
- Be welcoming to all skill levels
- Ask clarifying questions when needed
- Provide multiple options with explanations
- Include where to find more information
- Celebrate the joy of board gaming

SKILL ADAPTATION:
- New to board games: Start with gateway games
- Experienced: Suggest deeper cuts
- Looking for variety: Mix mechanics and themes

TONE:
- Warm and enthusiastic
- Non-judgmental about preferences
- Encouraging exploration
- Celebrates the hobby`,
    modelConfig: {
      fast: { model: 'Gemini 2.0 Flash', maxTokens: 1500, temperature: 0.6 },
      balanced: { model: 'DeepSeek Chat', maxTokens: 2500, temperature: 0.7 },
      precise: { model: 'Claude 3.5 Sonnet', maxTokens: 3500, temperature: 0.6 },
    },
    tokenBudgets: {
      systemPrompt: 400,
      maxContext: 2000,
      maxResponse: 800,
    },
    bestFor: [
      'Game recommendations',
      'New player orientation',
      'Group game selection',
      'General questions',
    ],
    limitations: [
      'For specific rules, use RulesExpert',
      'For strategy, use StrategyCoach',
      'For setup, use SetupWizard',
    ],
    exampleQueries: [
      'What game should I play with 5 people who are new to board games?',
      'What is a good next step after Catan?',
      'How long does Wingspan typically take?',
      'What does "worker placement" mean?',
    ],
  },
];

// =============================================================================
// Sub-Components
// =============================================================================

interface AgentCardProps {
  agent: AgentRole;
  isSelected: boolean;
  onClick: () => void;
}

function AgentCard({ agent, isSelected, onClick }: AgentCardProps) {
  return (
    <motion.button
      className={cn(
        'w-full p-4 rounded-xl text-left transition-all border',
        'hover:border-primary/50',
        isSelected ? 'ring-2 ring-primary border-primary' : 'border-border'
      )}
      onClick={onClick}
      whileHover={{ scale: 1.02 }}
      whileTap={{ scale: 0.98 }}
    >
      <div className="flex items-start gap-3">
        <div
          className="p-2 rounded-lg"
          style={{ backgroundColor: `${agent.color}20`, color: agent.color }}
        >
          {agent.icon}
        </div>
        <div className="flex-1 min-w-0">
          <div className="font-semibold" style={{ color: isSelected ? agent.color : undefined }}>
            {agent.name}
          </div>
          <p className="text-xs text-muted-foreground mt-1 line-clamp-2">{agent.tagline}</p>
        </div>
      </div>
    </motion.button>
  );
}

interface ModelConfigCardProps {
  strategy: RagStrategy;
  config: { model: string; maxTokens: number; temperature: number };
  color: string;
}

function ModelConfigCard({ strategy, config, color }: ModelConfigCardProps) {
  const icons = {
    fast: <Zap className="h-4 w-4" />,
    balanced: <Scale className="h-4 w-4" />,
    precise: <CircleDot className="h-4 w-4" />,
  };

  return (
    <div className="p-3 rounded-lg bg-muted/30 border">
      <div className="flex items-center gap-2 mb-2">
        <span style={{ color }}>{icons[strategy.toLowerCase() as keyof typeof icons]}</span>
        <span className="font-semibold text-sm">{strategy}</span>
      </div>
      <div className="space-y-1 text-xs">
        <div className="flex justify-between">
          <span className="text-muted-foreground">Model:</span>
          <span className="font-mono">{config.model}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Max Tokens:</span>
          <span className="font-mono">{config.maxTokens}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Temperature:</span>
          <span className="font-mono">{config.temperature}</span>
        </div>
      </div>
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface AgentRoleConfiguratorProps {
  className?: string;
}

export function AgentRoleConfigurator({ className }: AgentRoleConfiguratorProps) {
  const [selectedAgentId, setSelectedAgentId] = useState<string>('rules_expert');
  const [activeTab, setActiveTab] = useState<'overview' | 'prompt' | 'config'>('overview');
  const [copied, setCopied] = useState(false);

  const selectedAgent = AGENT_ROLES.find(a => a.id === selectedAgentId);

  const handleCopyPrompt = () => {
    if (selectedAgent) {
      navigator.clipboard.writeText(selectedAgent.systemPrompt);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  if (!selectedAgent) return null;

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Users className="h-5 w-5 text-primary" />
          Agent Role Configurator
        </CardTitle>
        <p className="text-sm text-muted-foreground mt-1">
          Pre-built agent configurations with system prompts and model settings
        </p>
      </CardHeader>

      <CardContent className="space-y-6">
        <div className="grid lg:grid-cols-3 gap-6">
          {/* Agent Selection */}
          <div className="space-y-3">
            <div className="text-xs font-medium text-muted-foreground flex items-center gap-2">
              <Bot className="h-3 w-3" />
              Select Agent Role
            </div>
            <div className="space-y-2">
              {AGENT_ROLES.map(agent => (
                <AgentCard
                  key={agent.id}
                  agent={agent}
                  isSelected={selectedAgentId === agent.id}
                  onClick={() => setSelectedAgentId(agent.id)}
                />
              ))}
            </div>
          </div>

          {/* Agent Details */}
          <div className="lg:col-span-2 space-y-4">
            <AnimatePresence mode="wait">
              <motion.div
                key={selectedAgent.id}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -10 }}
                transition={{ duration: 0.2 }}
                className="space-y-4"
              >
                {/* Header */}
                <div className="flex items-start gap-4 p-4 rounded-xl bg-muted/30 border">
                  <div
                    className="p-3 rounded-xl"
                    style={{ backgroundColor: `${selectedAgent.color}20` }}
                  >
                    <span style={{ color: selectedAgent.color }}>{selectedAgent.icon}</span>
                  </div>
                  <div className="flex-1">
                    <h3 className="text-lg font-semibold" style={{ color: selectedAgent.color }}>
                      {selectedAgent.name}
                    </h3>
                    <p className="text-sm text-muted-foreground mt-1">{selectedAgent.description}</p>
                  </div>
                </div>

                {/* Tab Navigation */}
                <div className="flex gap-2 border-b border-border pb-2">
                  {[
                    { id: 'overview', label: 'Overview', icon: <Settings2 className="h-3 w-3" /> },
                    { id: 'prompt', label: 'System Prompt', icon: <MessageSquare className="h-3 w-3" /> },
                    { id: 'config', label: 'Model Config', icon: <Cpu className="h-3 w-3" /> },
                  ].map(tab => (
                    <button
                      key={tab.id}
                      className={cn(
                        'flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all',
                        activeTab === tab.id
                          ? 'bg-primary/10 text-primary'
                          : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'
                      )}
                      onClick={() => setActiveTab(tab.id as typeof activeTab)}
                    >
                      {tab.icon}
                      {tab.label}
                    </button>
                  ))}
                </div>

                {/* Tab Content */}
                <div className="min-h-[300px]">
                  {activeTab === 'overview' && (
                    <div className="space-y-4">
                      {/* Specializations */}
                      <div>
                        <div className="text-xs font-medium text-muted-foreground mb-2">
                          Specializations
                        </div>
                        <div className="flex flex-wrap gap-2">
                          {selectedAgent.specializations.map(spec => (
                            <span
                              key={spec}
                              className="px-2 py-1 rounded-full text-xs"
                              style={{
                                backgroundColor: `${selectedAgent.color}20`,
                                color: selectedAgent.color,
                              }}
                            >
                              {spec}
                            </span>
                          ))}
                        </div>
                      </div>

                      {/* Best For / Limitations */}
                      <div className="grid sm:grid-cols-2 gap-4">
                        <div className="p-3 rounded-lg bg-green-500/10 border border-green-500/20">
                          <div className="text-xs font-medium text-green-600 dark:text-green-400 mb-2">
                            Best For
                          </div>
                          <ul className="space-y-1 text-xs">
                            {selectedAgent.bestFor.map(item => (
                              <li key={item} className="flex items-start gap-1">
                                <Check className="h-3 w-3 text-green-500 mt-0.5 shrink-0" />
                                {item}
                              </li>
                            ))}
                          </ul>
                        </div>
                        <div className="p-3 rounded-lg bg-orange-500/10 border border-orange-500/20">
                          <div className="text-xs font-medium text-orange-600 dark:text-orange-400 mb-2">
                            Limitations
                          </div>
                          <ul className="space-y-1 text-xs">
                            {selectedAgent.limitations.map(item => (
                              <li key={item} className="text-muted-foreground">
                                • {item}
                              </li>
                            ))}
                          </ul>
                        </div>
                      </div>

                      {/* Example Queries */}
                      <div>
                        <div className="text-xs font-medium text-muted-foreground mb-2">
                          Example Queries
                        </div>
                        <div className="space-y-2">
                          {selectedAgent.exampleQueries.map(query => (
                            <div
                              key={query}
                              className="p-2 rounded-lg bg-muted/30 text-sm italic border"
                            >
                              &ldquo;{query}&rdquo;
                            </div>
                          ))}
                        </div>
                      </div>

                      {/* Token Budgets */}
                      <div>
                        <div className="text-xs font-medium text-muted-foreground mb-2 flex items-center gap-2">
                          <Coins className="h-3 w-3" />
                          Token Budgets
                        </div>
                        <div className="grid grid-cols-3 gap-3">
                          <div className="p-2 rounded-lg bg-muted/30 text-center">
                            <div className="text-xs text-muted-foreground">System</div>
                            <div className="font-mono font-semibold">
                              {selectedAgent.tokenBudgets.systemPrompt}
                            </div>
                          </div>
                          <div className="p-2 rounded-lg bg-muted/30 text-center">
                            <div className="text-xs text-muted-foreground">Max Context</div>
                            <div className="font-mono font-semibold">
                              {selectedAgent.tokenBudgets.maxContext}
                            </div>
                          </div>
                          <div className="p-2 rounded-lg bg-muted/30 text-center">
                            <div className="text-xs text-muted-foreground">Max Response</div>
                            <div className="font-mono font-semibold">
                              {selectedAgent.tokenBudgets.maxResponse}
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  )}

                  {activeTab === 'prompt' && (
                    <div className="space-y-3">
                      <div className="flex items-center justify-between">
                        <div className="text-xs font-medium text-muted-foreground">
                          Full System Prompt ({selectedAgent.tokenBudgets.systemPrompt} tokens)
                        </div>
                        <Button variant="ghost" size="sm" onClick={handleCopyPrompt} className="h-7">
                          {copied ? (
                            <Check className="h-3 w-3 text-green-500" />
                          ) : (
                            <Copy className="h-3 w-3" />
                          )}
                        </Button>
                      </div>
                      <pre className="text-xs bg-muted/50 p-4 rounded-xl overflow-auto max-h-80 font-mono border whitespace-pre-wrap">
                        {selectedAgent.systemPrompt}
                      </pre>
                    </div>
                  )}

                  {activeTab === 'config' && (
                    <div className="space-y-4">
                      <div className="text-xs font-medium text-muted-foreground">
                        Model Configuration by Strategy
                      </div>
                      <div className="grid sm:grid-cols-3 gap-4">
                        <ModelConfigCard
                          strategy="FAST"
                          config={selectedAgent.modelConfig.fast}
                          color="hsl(142 76% 36%)"
                        />
                        <ModelConfigCard
                          strategy="BALANCED"
                          config={selectedAgent.modelConfig.balanced}
                          color="hsl(221 83% 53%)"
                        />
                        <ModelConfigCard
                          strategy="PRECISE"
                          config={selectedAgent.modelConfig.precise}
                          color="hsl(262 83% 62%)"
                        />
                      </div>

                      {/* Config Explanation */}
                      <div className="p-4 rounded-lg bg-muted/30 border text-sm">
                        <div className="font-medium mb-2">Configuration Notes</div>
                        <ul className="space-y-1 text-xs text-muted-foreground">
                          <li>
                            • <strong>FAST:</strong> Uses free models for cost efficiency, lower
                            token limits
                          </li>
                          <li>
                            • <strong>BALANCED:</strong> Mid-tier models with CRAG evaluation
                          </li>
                          <li>
                            • <strong>PRECISE:</strong> Premium models with multi-agent pipeline
                          </li>
                          <li>
                            • Temperature affects creativity vs consistency (lower = more
                            deterministic)
                          </li>
                        </ul>
                      </div>
                    </div>
                  )}
                </div>
              </motion.div>
            </AnimatePresence>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
