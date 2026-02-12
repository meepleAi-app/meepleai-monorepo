/**
 * ISSUE-3777: Multi-Agent Chat Interface
 * Demo integration showing orchestrator with agent switching
 */

'use client';

import { useState } from 'react';
import { Send } from 'lucide-react';

import { AgentSelectorBadge } from './AgentSelectorBadge';
import { ChatMessage } from '../ui/meeple/chat-message';
import { Button } from '../ui/primitives/button';
import { Input } from '../ui/primitives/input';
import { useExecuteWorkflow, useSwitchAgent } from '@/hooks/agent/use-orchestrator';
import type { AgentType } from '@/lib/api/orchestrator-client';

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  agentType?: AgentType;
  confidence?: number;
  timestamp: Date;
}

interface MultiAgentChatProps {
  gameId: string;
  sessionId: string;
}

export function MultiAgentChat({ gameId, sessionId }: MultiAgentChatProps) {
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [currentAgent, setCurrentAgent] = useState<AgentType>('tutor');

  const { mutate: executeWorkflow, isPending: isExecuting } = useExecuteWorkflow();
  const { mutate: switchAgent, isPending: isSwitching } = useSwitchAgent();

  const handleSend = () => {
    if (!input.trim() || isExecuting) return;

    const userMessage: Message = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: input,
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMessage]);
    setInput('');

    executeWorkflow(
      {
        game_id: gameId,
        session_id: sessionId,
        query: input,
      },
      {
        onSuccess: (data) => {
          const assistantMessage: Message = {
            id: `assistant-${Date.now()}`,
            role: 'assistant',
            content: data.response,
            agentType: data.agent_type,
            confidence: Math.round(data.confidence * 100),
            timestamp: new Date(),
          };

          setMessages((prev) => [...prev, assistantMessage]);
          setCurrentAgent(data.agent_type); // Update current agent from response
        },
      }
    );
  };

  const handleAgentSwitch = (targetAgent: AgentType) => {
    switchAgent(
      { sessionId, gameId, targetAgent },
      {
        onSuccess: (data) => {
          setCurrentAgent(data.agent_type);

          // Add system message about switch
          const systemMessage: Message = {
            id: `system-${Date.now()}`,
            role: 'assistant',
            content: `Switched to ${data.agent_type.charAt(0).toUpperCase() + data.agent_type.slice(1)} agent`,
            agentType: data.agent_type,
            timestamp: new Date(),
          };
          setMessages((prev) => [...prev, systemMessage]);
        },
      }
    );
  };

  return (
    <div className="flex flex-col h-full">
      {/* Header with Agent Selector */}
      <div className="flex items-center justify-between p-4 border-b">
        <h3 className="text-lg font-semibold">Multi-Agent Chat</h3>
        <AgentSelectorBadge
          currentAgent={currentAgent}
          onSwitch={handleAgentSwitch}
          disabled={isExecuting || isSwitching}
          showSwitcher={true}
        />
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {messages.map((message) => (
          <ChatMessage
            key={message.id}
            role={message.role}
            content={message.content}
            agentType={message.agentType}
            confidence={message.confidence}
            timestamp={message.timestamp}
          />
        ))}

        {isExecuting && (
          <ChatMessage
            role="assistant"
            content=""
            isTyping
          />
        )}
      </div>

      {/* Input */}
      <div className="p-4 border-t">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            handleSend();
          }}
          className="flex gap-2"
        >
          <Input
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder={`Ask ${currentAgent}...`}
            disabled={isExecuting}
          />
          <Button type="submit" disabled={!input.trim() || isExecuting}>
            <Send className="h-4 w-4" />
          </Button>
        </form>
      </div>
    </div>
  );
}
