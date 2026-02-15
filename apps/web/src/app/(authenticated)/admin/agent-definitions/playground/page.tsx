'use client';

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { ChevronDown, Download, RotateCcw, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { ChatInterface } from '@/components/playground/ChatInterface';
import { ComparisonView } from '@/components/playground/ComparisonView';
import { DebugPanel } from '@/components/playground/DebugPanel';
import { RagContextViewer } from '@/components/playground/RagContextViewer';
import { ScenarioManager } from '@/components/playground/ScenarioManager';
import {
  Button, Input, Label, RadioGroup, RadioGroupItem, Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
  Tabs, TabsContent, TabsList, TabsTrigger, Textarea,
} from '@/components/ui';
import { parsePlaygroundSSEChunk } from '@/lib/agent/playground-sse-parser';
import type { PlaygroundSSEHandlers } from '@/lib/agent/playground-sse-parser';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';
import { useApiClient } from '@/lib/api/context';
import type { PlaygroundTestScenarioDto } from '@/lib/api/schemas/playground-scenarios.schemas';
import { usePlaygroundStore } from '@/stores/playground-store';
import type { PlaygroundStrategy } from '@/stores/playground-store';

const STRATEGY_OPTIONS: { value: PlaygroundStrategy; label: string; description: string }[] = [
  {
    value: 'SingleModel',
    label: 'SingleModel (POC)',
    description: 'RAG + single LLM call via configured provider. Default for all agents.',
  },
  {
    value: 'RetrievalOnly',
    label: 'RetrievalOnly',
    description: 'Return RAG chunks only, no LLM call. Zero cost.',
  },
  {
    value: 'MultiModelConsensus',
    label: 'MultiModelConsensus',
    description: 'RAG + dual-model (GPT-4 + Claude) consensus response.',
  },
];

export default function AgentPlaygroundPage() {
  const [selectedAgentId, setSelectedAgentId] = useState<string>('');
  const [showAdvanced, setShowAdvanced] = useState(false);
  const {
    clearMessages, setCurrentAgent, addMessage, appendToLastMessage,
    updateMessageMetadata, setStreaming, addCitations, addStateUpdate,
    setFollowUpQuestions, setCompletionMetadata, setLatencyMs, clearResponseState,
    systemMessage, setSystemMessage,
    currentGameId, setCurrentGameId,
    strategy, setStrategy,
    modelOverride, setModelOverride,
    providerOverride, setProviderOverride,
    resetOverrides,
  } = usePlaygroundStore();

  const apiClient = useApiClient();

  const { data: agents = [] } = useQuery({
    queryKey: ['admin', 'agent-definitions', { activeOnly: true }],
    queryFn: () => agentDefinitionsApi.getAll({ activeOnly: true }),
  });

  const { data: gamesResult } = useQuery({
    queryKey: ['shared-games', { pageSize: 100 }],
    queryFn: () => apiClient.sharedGames.search({ pageSize: 100 }),
  });

  const games = gamesResult?.items ?? [];

  const handleSendMessage = async (message: string) => {
    if (!selectedAgentId) {
      toast.error('Please select an agent first');
      return;
    }

    // Reset per-response state before new request
    clearResponseState();
    setStreaming(true);

    const requestStartTime = Date.now();

    try {
      const response = await fetch(`/api/v1/admin/agent-definitions/${selectedAgentId}/playground/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          message,
          ...(systemMessage ? { systemMessage } : {}),
          ...(currentGameId ? { gameId: currentGameId } : {}),
          strategy,
          ...(modelOverride ? { modelOverride } : {}),
          ...(providerOverride ? { providerOverride } : {}),
        }),
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();

      // Add placeholder assistant message for streaming tokens
      addMessage({ role: 'assistant', content: '' });

      const handlers: PlaygroundSSEHandlers = {
        onToken: (token) => appendToLastMessage(token),
        onStateUpdate: (msg) => addStateUpdate(msg),
        onCitations: (citations) => addCitations(citations),
        onFollowUpQuestions: (questions) => setFollowUpQuestions(questions),
        onComplete: (metadata) => {
          setCompletionMetadata(metadata);
          setLatencyMs(Date.now() - requestStartTime);
          const lastMsg = usePlaygroundStore.getState().messages.slice(-1)[0];
          if (lastMsg) {
            updateMessageMetadata(lastMsg.id, {
              tokens: metadata.totalTokens,
              latency: Date.now() - requestStartTime,
            });
          }
        },
        onError: (error) => {
          toast.error(error.errorMessage || 'Stream error');
        },
        onHeartbeat: () => {
          // Keep-alive, no action needed
        },
      };

      while (reader) {
        const { done, value } = await reader.read();
        if (done) break;

        const chunk = decoder.decode(value, { stream: true });
        parsePlaygroundSSEChunk(chunk, handlers);
      }
    } catch (error) {
      toast.error(`Failed to send message: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setStreaming(false);
    }
  };

  const handleRunScenario = async (scenario: PlaygroundTestScenarioDto) => {
    // Apply scenario system message override if provided
    if (scenario.systemMessage) {
      setSystemMessage(scenario.systemMessage);
    }

    // Add user message and send
    addMessage({ role: 'user', content: scenario.userMessage });
    await handleSendMessage(scenario.userMessage);
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

      {/* Agent & Game Selectors */}
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

        <div className="flex-1">
          <label className="text-sm font-medium mb-2 block">Game Context (RAG)</label>
          <Select
            value={currentGameId ?? '__none__'}
            onValueChange={(value) => setCurrentGameId(value === '__none__' ? null : value)}
          >
            <SelectTrigger>
              <SelectValue placeholder="No game (pure LLM)" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__none__">No game (pure LLM)</SelectItem>
              {games.map((game) => (
                <SelectItem key={game.id} value={game.id}>
                  {game.title}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Strategy Selector */}
      <div>
        <label className="text-sm font-medium mb-3 block">Execution Strategy</label>
        <RadioGroup
          value={strategy}
          onValueChange={(value) => setStrategy(value as PlaygroundStrategy)}
          className="flex gap-4"
        >
          {STRATEGY_OPTIONS.map((option) => (
            <div key={option.value} className="flex items-start gap-2">
              <RadioGroupItem value={option.value} id={`strategy-${option.value}`} className="mt-0.5" />
              <Label htmlFor={`strategy-${option.value}`} className="cursor-pointer">
                <span className="text-sm font-medium">{option.label}</span>
                <p className="text-xs text-muted-foreground mt-0.5">{option.description}</p>
              </Label>
            </div>
          ))}
        </RadioGroup>
      </div>

      {/* Advanced Options - Model/Provider Override */}
      <div>
        <button
          type="button"
          onClick={() => setShowAdvanced(!showAdvanced)}
          className="flex items-center gap-1.5 text-sm font-medium text-muted-foreground hover:text-foreground transition-colors"
        >
          <ChevronDown className={`h-4 w-4 transition-transform ${showAdvanced ? 'rotate-0' : '-rotate-90'}`} />
          Advanced Options
          {(modelOverride || providerOverride) && (
            <span className="ml-1.5 text-xs bg-amber-100 text-amber-800 px-1.5 py-0.5 rounded">Override active</span>
          )}
        </button>
        {showAdvanced && (
          <div className="mt-3 space-y-3 pl-6 border-l-2 border-muted">
            <div className="flex gap-4 items-end">
              <div className="flex-1">
                <Label htmlFor="model-override" className="text-sm">Model Override</Label>
                <Input
                  id="model-override"
                  placeholder="e.g. gpt-4o, claude-3-opus, mistral-large..."
                  value={modelOverride ?? ''}
                  onChange={(e) => setModelOverride(e.target.value || null)}
                  className="mt-1 text-sm font-mono"
                />
              </div>
              <div className="w-48">
                <Label htmlFor="provider-override" className="text-sm">Provider</Label>
                <Select
                  value={providerOverride ?? '__none__'}
                  onValueChange={(value) => setProviderOverride(value === '__none__' ? null : value)}
                >
                  <SelectTrigger id="provider-override" className="mt-1">
                    <SelectValue placeholder="Agent default" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">Agent default</SelectItem>
                    <SelectItem value="OpenRouter">OpenRouter</SelectItem>
                    <SelectItem value="Ollama">Ollama</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={resetOverrides}
                disabled={!modelOverride && !providerOverride}
                className="shrink-0"
              >
                <RotateCcw className="h-3.5 w-3.5 mr-1.5" />
                Reset
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">
              Override the agent&apos;s configured model/provider for this request. Leave empty to use agent defaults.
            </p>
          </div>
        )}
      </div>

      {/* System Message */}
      <div>
        <label className="text-sm font-medium mb-2 block">System Message (optional)</label>
        <Textarea
          placeholder="Override the agent's system prompt for this session..."
          value={systemMessage}
          onChange={(e) => setSystemMessage(e.target.value)}
          rows={2}
          className="resize-none text-sm"
        />
      </div>

      {/* Main Layout */}
      <div className="grid grid-cols-3 gap-6">
        <div className="col-span-2">
          <ChatInterface agentId={selectedAgentId} onSendMessage={handleSendMessage} />
        </div>
        <div>
          <Tabs defaultValue="debug" className="w-full">
            <TabsList className="grid w-full grid-cols-4">
              <TabsTrigger value="debug">Debug</TabsTrigger>
              <TabsTrigger value="context">RAG</TabsTrigger>
              <TabsTrigger value="compare">Compare</TabsTrigger>
              <TabsTrigger value="scenarios">Scenarios</TabsTrigger>
            </TabsList>
            <TabsContent value="debug" className="mt-4">
              <DebugPanel />
            </TabsContent>
            <TabsContent value="context" className="mt-4">
              <RagContextViewer />
            </TabsContent>
            <TabsContent value="compare" className="mt-4">
              <ComparisonView agentId={selectedAgentId} />
            </TabsContent>
            <TabsContent value="scenarios" className="mt-4">
              <ScenarioManager
                agentDefinitionId={selectedAgentId}
                onRunScenario={handleRunScenario}
              />
            </TabsContent>
          </Tabs>
        </div>
      </div>
    </div>
  );
}
