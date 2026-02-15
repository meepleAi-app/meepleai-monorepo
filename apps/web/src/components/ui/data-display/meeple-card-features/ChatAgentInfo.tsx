/**
 * ChatAgentInfo - Chat Agent & Model Display
 * Issue #4400 - ChatSession-Specific Metadata & Status Display
 *
 * Displays the agent name and AI model badge powering the chat session.
 * Reuses getModelVariant logic from AgentModelInfo for badge styling.
 */

'use client';

import React from 'react';

import { Bot } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ChatAgent {
  /** Agent display name */
  name: string;
  /** AI model name (e.g., "GPT-4o-mini", "Claude 3.5 Sonnet") */
  modelName: string;
  /** Optional agent avatar URL */
  avatarUrl?: string;
}

export interface ChatAgentInfoProps {
  /** Chat agent info */
  agent: ChatAgent;
  /** Custom className */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Get model badge variant based on model family
 * (Reuses logic from AgentModelInfo)
 */
function getModelVariant(modelName: string): 'default' | 'secondary' | 'outline' {
  const name = modelName.toLowerCase();
  if (name.includes('gpt-4') || name.includes('claude')) return 'default';
  if (name.includes('gemini')) return 'secondary';
  return 'outline';
}

// ============================================================================
// Component
// ============================================================================

export const ChatAgentInfo = React.memo(function ChatAgentInfo({
  agent,
  className,
}: ChatAgentInfoProps) {
  const variant = getModelVariant(agent.modelName);

  return (
    <span
      className={cn('inline-flex items-center gap-1.5', className)}
      data-testid="chat-agent-info"
    >
      <Bot className="w-3.5 h-3.5 flex-shrink-0 text-muted-foreground" aria-hidden="true" />
      <span className="text-xs font-medium text-foreground truncate max-w-[100px]">
        {agent.name}
      </span>
      <Badge variant={variant} className="text-[10px] px-1.5 py-0">
        {agent.modelName}
      </Badge>
    </span>
  );
});

ChatAgentInfo.displayName = 'ChatAgentInfo';
