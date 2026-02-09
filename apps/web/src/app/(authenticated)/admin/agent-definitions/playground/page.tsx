'use client';

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { Download, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { ChatInterface } from '@/components/playground/ChatInterface';
import { DebugPanel } from '@/components/playground/DebugPanel';
import { Button, Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';
import { usePlaygroundStore } from '@/stores/playground-store';

export default function AgentPlaygroundPage() {
  const [selectedAgentId, setSelectedAgentId] = useState<string>('');
  const { clearMessages, setCurrentAgent, addMessage, appendToLastMessage, updateMessageMetadata, setStreaming } = usePlaygroundStore();

  const { data: agents = [] } = useQuery({
    queryKey: ['admin', 'agent-definitions', { activeOnly: true }],
    queryFn: () => agentDefinitionsApi.getAll({ activeOnly: true }),
  });

  const handleSendMessage = async (message: string) => {
    if (!selectedAgentId) {
      toast.error('Please select an agent first');
      return;
    }

    setStreaming(true);

    try {
      // Create EventSource for SSE
      const response = await fetch(`/api/v1/admin/agent-definitions/${selectedAgentId}/playground/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message }),
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();
      // Add placeholder assistant message
      addMessage({ role: 'assistant', content: '' });

      while (reader) {
        const { done, value } = await reader.read();
        if (done) break;

        const chunk = decoder.decode(value);
        const lines = chunk.split('\n');

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = JSON.parse(line.slice(6));

            if (line.startsWith('event: chunk')) {
              appendToLastMessage(data.content);
            } else if (line.startsWith('event: metadata')) {
              // Update last message metadata
              const lastMsg = usePlaygroundStore.getState().messages.slice(-1)[0];
              if (lastMsg) {
                updateMessageMetadata(lastMsg.id, {
                  tokens: data.tokens,
                  latency: data.latency,
                  model: data.model,
                });
              }
            }
          }
        }
      }
    } catch (error) {
      toast.error(`Failed to send message: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setStreaming(false);
    }
  };

  const handleClearChat = () => {
    clearMessages();
    toast.success('Chat cleared');
  };

  const handleExportJson = () => {
    const { messages } = usePlaygroundStore.getState();
    const json = JSON.stringify(messages, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `playground-${Date.now()}.json`;
    a.click();
    URL.revokeObjectURL(url);
    toast.success('Conversation exported as JSON');
  };

  const handleExportMarkdown = () => {
    const { messages } = usePlaygroundStore.getState();
    const md = messages
      .map((msg) => `**${msg.role}**: ${msg.content}`)
      .join('\n\n');
    const blob = new Blob([md], { type: 'text/markdown' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `playground-${Date.now()}.md`;
    a.click();
    URL.revokeObjectURL(url);
    toast.success('Conversation exported as Markdown');
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Agent Playground</h1>
          <p className="text-muted-foreground">Test and debug AI agents in real-time</p>
        </div>

        <div className="flex gap-2">
          <Button variant="outline" onClick={handleExportJson}>
            <Download className="h-4 w-4 mr-2" />
            Export JSON
          </Button>
          <Button variant="outline" onClick={handleExportMarkdown}>
            <Download className="h-4 w-4 mr-2" />
            Export MD
          </Button>
          <Button variant="destructive" onClick={handleClearChat}>
            <Trash2 className="h-4 w-4 mr-2" />
            Clear
          </Button>
        </div>
      </div>

      {/* Agent Selector */}
      <div className="flex gap-4 items-end">
        <div className="flex-1">
          <label className="text-sm font-medium mb-2 block">Select Agent</label>
          <Select value={selectedAgentId} onValueChange={(value) => {
            setSelectedAgentId(value);
            setCurrentAgent(value);
          }}>
            <SelectTrigger>
              <SelectValue placeholder="Choose an agent to test..." />
            </SelectTrigger>
            <SelectContent>
              {agents.map((agent) => (
                <SelectItem key={agent.id} value={agent.id}>
                  {agent.name} ({agent.config.model})
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Main Layout */}
      <div className="grid grid-cols-3 gap-6">
        <div className="col-span-2">
          <ChatInterface agentId={selectedAgentId} onSendMessage={handleSendMessage} />
        </div>
        <div>
          <DebugPanel />
        </div>
      </div>
    </div>
  );
}
