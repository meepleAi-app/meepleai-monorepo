'use client';

/**
 * BusinessUseCases — Board game use case cards for Business view
 *
 * Maps real-world board game scenarios to the TOMAC-RAG strategy that handles
 * them, with accuracy and cost context.
 */

import { motion } from 'framer-motion';
import { BookOpen, HelpCircle, Scale, Trophy } from 'lucide-react';

// =============================================================================
// Use Case Data
// =============================================================================

const USE_CASES = [
  {
    icon: HelpCircle,
    title: 'Quick Rule Lookups',
    description:
      'Players asking simple "how does X work?" questions during a game session. Speed is everything — latency must stay under 200 ms.',
    strategy: 'FAST',
    strategyColor: 'hsl(142,76%,36%)',
    strategyBg: 'hsla(142,76%,36%,0.1)',
    strategyBorder: 'hsla(142,76%,36%,0.3)',
    accuracy: '78-85%',
    cost: '$0.0001',
    coverage: '60-70% of all queries',
    examples: [
      'Can I build on that street?',
      'How many dice do I roll?',
      'What does this card do?',
    ],
  },
  {
    icon: BookOpen,
    title: 'Setup Guides',
    description:
      'Step-by-step game setup instructions for complex games with many components. Balanced accuracy with CRAG validation.',
    strategy: 'BALANCED',
    strategyColor: 'hsl(221,83%,53%)',
    strategyBg: 'hsla(221,83%,53%,0.1)',
    strategyBorder: 'hsla(221,83%,53%,0.3)',
    accuracy: '85-92%',
    cost: '$0.01',
    coverage: '25-30% of all queries',
    examples: [
      'How do I set up Terraforming Mars?',
      'First game setup for Pandemic?',
      'Island setup for Catan?',
    ],
  },
  {
    icon: Trophy,
    title: 'Tournament Rules',
    description:
      'Competitive play requires exact rule interpretations. Multi-agent pipeline ensures citation-backed, high-confidence answers.',
    strategy: 'PRECISE',
    strategyColor: 'hsl(25,95%,53%)',
    strategyBg: 'hsla(25,95%,53%,0.1)',
    strategyBorder: 'hsla(25,95%,53%,0.3)',
    accuracy: '95-98%',
    cost: '$0.132',
    coverage: '5-10% of all queries',
    examples: [
      'Exact timing for interrupt actions in Netrunner',
      'Official FAQ ruling on card X',
      'Championship tiebreaker rules',
    ],
  },
  {
    icon: Scale,
    title: 'Rule Disputes',
    description:
      'When players disagree on a rule interpretation, multi-LLM consensus voting provides an authoritative resolution with high confidence.',
    strategy: 'CONSENSUS',
    strategyColor: 'hsl(0,72%,51%)',
    strategyBg: 'hsla(0,72%,51%,0.1)',
    strategyBorder: 'hsla(0,72%,51%,0.3)',
    accuracy: '97-99%',
    cost: '$0.09',
    coverage: '1-3% of all queries',
    examples: [
      'Who wins this combat in Spirit Island?',
      'Is this Dominion combo legal?',
      'Three different rule interpretations — which is correct?',
    ],
  },
] as const;
// =============================================================================
// Component
// =============================================================================

export function BusinessUseCases() {
  return (
    <div className="grid md:grid-cols-2 gap-4">
      {USE_CASES.map((uc, i) => {
        const Icon = uc.icon;
        return (
          <motion.div
            key={uc.strategy}
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.07, duration: 0.3 }}
            className="rounded-xl border p-5 space-y-3"
            style={{
              borderColor: uc.strategyBorder,
              backgroundColor: uc.strategyBg,
            }}
          >
            {/* Header */}
            <div className="flex items-start gap-3">
              <div
                className="p-2 rounded-lg shrink-0"
                style={{ backgroundColor: uc.strategyBorder }}
              >
                <Icon className="h-4 w-4" style={{ color: uc.strategyColor }} />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <h3 className="font-semibold text-sm text-foreground">{uc.title}</h3>
                  <span
                    className="text-xs font-bold px-2 py-0.5 rounded-full"
                    style={{ backgroundColor: uc.strategyBorder, color: uc.strategyColor }}
                  >
                    {uc.strategy}
                  </span>
                </div>
                <p className="text-xs text-muted-foreground mt-1 leading-relaxed">
                  {uc.description}
                </p>
              </div>
            </div>

            {/* Stats row */}
            <div className="flex gap-4 text-xs">
              <div>
                <span className="text-muted-foreground">Accuracy </span>
                <span className="font-semibold" style={{ color: uc.strategyColor }}>
                  {uc.accuracy}
                </span>
              </div>
              <div>
                <span className="text-muted-foreground">Cost </span>
                <span className="font-semibold" style={{ color: uc.strategyColor }}>
                  {uc.cost}/query
                </span>
              </div>
              <div>
                <span className="text-muted-foreground">Volume </span>
                <span className="font-semibold text-foreground">{uc.coverage}</span>
              </div>
            </div>

            {/* Example queries */}
            <ul className="space-y-1">
              {uc.examples.map(ex => (
                <li key={ex} className="flex items-start gap-1.5 text-xs text-muted-foreground">
                  <span
                    className="mt-0.5 shrink-0 h-1.5 w-1.5 rounded-full"
                    style={{ backgroundColor: uc.strategyColor }}
                    aria-hidden="true"
                  />
                  <span className="italic">&ldquo;{ex}&rdquo;</span>
                </li>
              ))}
            </ul>
          </motion.div>
        );
      })}
    </div>
  );
}
