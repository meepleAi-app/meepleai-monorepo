/**
 * TimelineStep Stories
 *
 * Examples of RAG pipeline step timeline component usage
 */

import type { Meta, StoryObj } from '@storybook/react';
import { TimelineStep } from './TimelineStep';
import { useState } from 'react';

const meta = {
  title: 'Admin/RAG/TimelineStep',
  component: TimelineStep,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof TimelineStep>;

export default meta;
type Story = StoryObj<typeof meta>;

// Example data matching the HTML mockup structure
const exampleSteps = [
  {
    icon: '💬',
    name: 'Query',
    durationMs: 12,
    percentOfTotal: 1.2,
    latencyClass: 'green' as const,
    barColor: 'rgb(34, 197, 94)', // green
    details: [
      {
        label: 'Original Query',
        value: '"best cooperative board games for 2 players"',
        mono: true,
      },
      { label: 'Language', value: 'English (en)' },
      { label: 'Intent', value: 'recommendation', badge: 'primary' as const },
      {
        label: 'Entities',
        value: 'cooperative, 2-player, board games',
        mono: true,
      },
    ],
  },
  {
    icon: '🔢',
    name: 'Embedding',
    durationMs: 45,
    percentOfTotal: 4.6,
    latencyClass: 'green' as const,
    barColor: 'rgb(34, 197, 94)',
    details: [
      { label: 'Model', value: 'all-MiniLM-L6-v2', mono: true },
      { label: 'Dimensions', value: '384', mono: true },
      { label: 'Cache', value: 'MISS', badge: 'red' as const },
      { label: 'Tokens', value: '12 tokens', mono: true },
    ],
  },
  {
    icon: '🔍',
    name: 'Vector Search',
    durationMs: 185,
    percentOfTotal: 18.8,
    latencyClass: 'amber' as const,
    barColor: 'rgb(251, 191, 36)', // amber
    details: [
      { label: 'Results Found', value: '23 documents', mono: true },
      { label: 'Search Type', value: 'Hybrid', badge: 'primary' as const },
      {
        label: 'Min Score',
        value: '0.42',
        mono: true,
        style: { color: 'rgb(239, 68, 68)' },
      },
      {
        label: 'Max Score',
        value: '0.94',
        mono: true,
        style: { color: 'rgb(34, 197, 94)' },
      },
      {
        label: 'Avg Score',
        value: '0.71',
        mono: true,
        style: { color: 'rgb(251, 191, 36)' },
      },
      { label: 'Collection', value: 'game_rules_v2', mono: true },
    ],
  },
  {
    icon: '🤖',
    name: 'LLM Generation',
    durationMs: 608,
    percentOfTotal: 61.9,
    latencyClass: 'red' as const,
    barColor: 'rgb(239, 68, 68)', // red
    details: [
      { label: 'Provider', value: 'OpenRouter' },
      { label: 'Model', value: 'anthropic/claude-3.5-sonnet', mono: true },
      { label: 'Prompt Tokens', value: '1,842', mono: true },
      { label: 'Completion Tokens', value: '326', mono: true },
      { label: 'Temperature', value: '0.3', mono: true },
      { label: 'Confidence', value: '0.87', badge: 'green' as const },
    ],
  },
];

/**
 * Default collapsed step
 */
export const Default: Story = {
  args: {
    ...exampleSteps[0],
    isActive: false,
    isOpen: false,
  },
};

/**
 * Expanded step showing details
 */
export const Expanded: Story = {
  args: {
    ...exampleSteps[0],
    isActive: false,
    isOpen: true,
  },
};

/**
 * Active and expanded step
 */
export const ActiveExpanded: Story = {
  args: {
    ...exampleSteps[2],
    isActive: true,
    isOpen: true,
  },
};

/**
 * High latency step (red)
 */
export const HighLatency: Story = {
  args: {
    ...exampleSteps[3],
    isActive: false,
    isOpen: true,
  },
};

/**
 * Interactive timeline with multiple steps
 */
export const InteractiveTimeline = () => {
  const [openIndex, setOpenIndex] = useState<number | null>(0);
  const [activeIndex, setActiveIndex] = useState<number>(0);

  return (
    <div className="space-y-2 max-w-4xl">
      <h3 className="text-lg font-quicksand font-bold mb-4">
        RAG Pipeline Timeline (982ms total)
      </h3>
      {exampleSteps.map((step, idx) => (
        <TimelineStep
          key={idx}
          {...step}
          isActive={activeIndex === idx}
          isOpen={openIndex === idx}
          onToggle={() => {
            setOpenIndex(openIndex === idx ? null : idx);
            setActiveIndex(idx);
          }}
        />
      ))}
    </div>
  );
};

/**
 * Step with wide detail item
 */
export const WideDetailItem: Story = {
  args: {
    icon: '📝',
    name: 'Response',
    durationMs: 12,
    percentOfTotal: 1.2,
    latencyClass: 'green',
    barColor: 'rgb(34, 197, 94)',
    isOpen: true,
    details: [
      {
        label: 'Answer Preview',
        value:
          'Based on my analysis of cooperative board games for 2 players, here are the top recommendations: Pandemic (score: 0.96), Spirit Island (0.94), Gloomhaven: Jaws of the Lion (0.91)...',
        wide: true,
      },
      { label: 'Citations', value: '5 documents', mono: true },
      { label: 'Quality', value: 'High', badge: 'green' },
      { label: 'Response Size', value: '1.2 KB', mono: true },
    ],
  },
};
