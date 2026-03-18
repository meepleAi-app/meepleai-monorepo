/**
 * ChatMessage Story
 * Demonstrates user and AI chat messages with confidence, citations, and typing states.
 */

'use client';

import { ChatMessage } from '@/components/ui/meeple/chat-message';

import type { ShowcaseStory } from '../types';

type ChatMessageShowcaseProps = {
  role: string;
  content: string;
  confidence: number;
  isTyping: boolean;
  showFeedback: boolean;
};

export const chatMessageStory: ShowcaseStory<ChatMessageShowcaseProps> = {
  id: 'chat-message',
  title: 'ChatMessage',
  category: 'Meeple',
  description:
    'Chat message bubble with role-based layout, AI confidence badge, and feedback buttons.',

  component: function ChatMessageStory({
    role,
    content,
    confidence,
    isTyping,
    showFeedback,
  }: ChatMessageShowcaseProps) {
    return (
      <div className="w-full max-w-xl p-4">
        <ChatMessage
          role={role as 'user' | 'assistant'}
          content={isTyping ? '' : content}
          confidence={role === 'assistant' && !isTyping ? confidence : undefined}
          isTyping={isTyping}
          showFeedback={showFeedback && role === 'assistant'}
          avatar={role === 'user' ? { fallback: 'U' } : undefined}
          timestamp={new Date()}
        />
      </div>
    );
  },

  defaultProps: {
    role: 'assistant',
    content:
      'The rule states that each player takes one action per turn. You can move, build, or trade on your turn.',
    confidence: 85,
    isTyping: false,
    showFeedback: true,
  },

  controls: {
    role: {
      type: 'select',
      label: 'role',
      options: ['user', 'assistant'],
      default: 'assistant',
    },
    content: {
      type: 'text',
      label: 'content',
      default: 'The rule states that each player takes one action per turn.',
    },
    confidence: { type: 'range', label: 'confidence', min: 0, max: 100, step: 5, default: 85 },
    isTyping: { type: 'boolean', label: 'isTyping', default: false },
    showFeedback: { type: 'boolean', label: 'showFeedback', default: true },
  },

  presets: {
    aiConfident: {
      label: 'AI Confident',
      props: { role: 'assistant', confidence: 92, isTyping: false },
    },
    aiUncertain: {
      label: 'AI Uncertain',
      props: { role: 'assistant', confidence: 45, isTyping: false },
    },
    aiTyping: { label: 'AI Typing', props: { role: 'assistant', isTyping: true } },
    user: {
      label: 'User',
      props: { role: 'user', content: 'How many players can play Catan?', showFeedback: false },
    },
  },
};
