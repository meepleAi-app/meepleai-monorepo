/**
 * Agent Chat Popout Window (Task #3 - Issue #239)
 *
 * Dedicated window for agent chat without other UI elements.
 * Opened via window.open() from AdminAgentChat component.
 */

'use client';

import { useParams } from 'next/navigation';

import { AgentChat } from '@/components/agent/AgentChat';

export default function AgentChatPopoutPage() {
  const params = useParams();
  const agentId = params?.id as string;

  if (!agentId) {
    return (
      <div className="flex items-center justify-center h-screen">
        <p className="text-muted-foreground">Invalid agent ID</p>
      </div>
    );
  }

  return (
    <div className="h-screen bg-background">
      <AgentChat
        agentId={agentId}
        layout="full-page"
        agentName="Admin Agent Chat"
        strategy="SingleModel"
        className="h-full rounded-none border-0"
      />
    </div>
  );
}
