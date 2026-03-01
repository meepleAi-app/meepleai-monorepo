/**
 * TagStrip Story
 */

import { Sword, Clock, Brain, Users, Dice5, BookOpen, Cpu } from 'lucide-react';

import { TagStrip } from '@/components/ui/tags/TagStrip';
import type { Tag, TagVariant } from '@/types/tags';

import type { ShowcaseStory } from '../types';

const GAME_TAGS: Tag[] = [
  { id: '1', label: 'Strategy', icon: Brain, color: 'hsl(262 83% 58%)', bgColor: 'hsl(262 83% 95%)' },
  { id: '2', label: '2-4 Players', icon: Users, color: 'hsl(221 83% 53%)', bgColor: 'hsl(221 83% 95%)' },
  { id: '3', label: '60-90 min', icon: Clock, color: 'hsl(38 92% 50%)', bgColor: 'hsl(38 92% 95%)' },
  { id: '4', label: 'Dice Rolling', icon: Dice5, color: 'hsl(16 90% 60%)', bgColor: 'hsl(16 90% 95%)' },
  { id: '5', label: 'Area Control', icon: Sword, color: 'hsl(350 89% 60%)', bgColor: 'hsl(350 89% 95%)' },
];

const AGENT_TAGS: Tag[] = [
  { id: '1', label: 'RAG', icon: Cpu, color: 'hsl(262 83% 58%)', bgColor: 'hsl(262 83% 95%)' },
  { id: '2', label: 'Game Rules', icon: BookOpen, color: 'hsl(221 83% 53%)', bgColor: 'hsl(221 83% 95%)' },
  { id: '3', label: 'Strategy', icon: Brain, color: 'hsl(38 92% 50%)', bgColor: 'hsl(38 92% 95%)' },
];

type TagStripShowcaseProps = {
  variant: string;
  maxVisible: number;
  tagSet: string;
};

export const tagStripStory: ShowcaseStory<TagStripShowcaseProps> = {
  id: 'tag-strip',
  title: 'TagStrip',
  category: 'Tags',
  description: 'Horizontal strip of colored tag badges with overflow indicator.',

  component: function TagStripStory({ variant, maxVisible, tagSet }: TagStripShowcaseProps) {
    const tags = tagSet === 'agent' ? AGENT_TAGS : GAME_TAGS;
    return (
      <div className="w-80 bg-white p-4 rounded-xl border border-border/40">
        <TagStrip
          tags={tags}
          variant={variant as TagVariant}
          maxVisible={maxVisible}
        />
      </div>
    );
  },

  defaultProps: {
    variant: 'desktop',
    maxVisible: 3,
    tagSet: 'game',
  },

  controls: {
    variant: {
      type: 'select',
      label: 'variant',
      options: ['desktop', 'tablet', 'mobile'],
      default: 'desktop',
    },
    maxVisible: { type: 'range', label: 'maxVisible', min: 1, max: 6, step: 1, default: 3 },
    tagSet: {
      type: 'select',
      label: 'tagSet',
      options: ['game', 'agent'],
      default: 'game',
    },
  },

  presets: {
    gameTags: { label: 'Game Tags', props: { tagSet: 'game', maxVisible: 3 } },
    agentTags: { label: 'Agent Tags', props: { tagSet: 'agent', maxVisible: 3 } },
    allVisible: { label: 'All Visible', props: { maxVisible: 6 } },
    mobile: { label: 'Mobile', props: { variant: 'mobile', maxVisible: 2 } },
  },
};
