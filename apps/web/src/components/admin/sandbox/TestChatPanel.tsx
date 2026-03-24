'use client';

import { useState, useCallback } from 'react';

import { MessageSquare, Trash2 } from 'lucide-react';

import { usePipeline } from '@/components/admin/sandbox/contexts/PipelineContext';
import { Separator } from '@/components/ui/navigation/separator';
import { Button } from '@/components/ui/primitives/button';

import { AutoTestRunner } from './AutoTestRunner';
import { AutoTestSummary } from './AutoTestSummary';
import { DebugSidePanel } from './DebugSidePanel';
import { SandboxChat } from './SandboxChat';

import type { AutoTestResult, ChatMessage } from './types';

export function TestChatPanel() {
  const { isAllReady } = usePipeline();
  const [selectedMessageId, setSelectedMessageId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [autoTestResults, setAutoTestResults] = useState<AutoTestResult[] | null>(null);

  // Find selected message data for debug panel
  const selectedMessage = messages.find(m => m.id === selectedMessageId);
  const debugData = selectedMessage?.metadata
    ? {
        chunks: selectedMessage.metadata.chunks,
        trace: selectedMessage.metadata.trace,
        latencyMs: selectedMessage.metadata.latencyMs,
        avgConfidence: selectedMessage.metadata.avgConfidence,
      }
    : undefined;

  const handleSelectMessage = useCallback((id: string) => {
    setSelectedMessageId(prev => (prev === id ? null : id));
  }, []);

  const handleAutoTestComplete = useCallback((results: AutoTestResult[]) => {
    setAutoTestResults(results);
  }, []);

  const handleClear = useCallback(() => {
    setMessages([]);
    setSelectedMessageId(null);
    setAutoTestResults(null);
  }, []);

  return (
    <div
      className="flex h-full flex-col rounded-xl border bg-white/70 backdrop-blur-md overflow-hidden"
      data-testid="test-chat-panel"
    >
      {/* Panel header */}
      <div className="flex items-center justify-between border-b px-4 py-3">
        <div className="flex items-center gap-2">
          <MessageSquare className="h-5 w-5 text-amber-600" />
          <h2 className="font-quicksand font-semibold text-lg">Test Chat</h2>
        </div>
        <div className="flex items-center gap-2">
          <AutoTestRunner onComplete={handleAutoTestComplete} disabled={!isAllReady} />
          <Separator orientation="vertical" className="h-6" />
          <Button
            variant="ghost"
            size="sm"
            onClick={handleClear}
            className="text-muted-foreground"
            data-testid="clear-chat-button"
          >
            <Trash2 className="mr-1.5 h-4 w-4" />
            Pulisci
          </Button>
        </div>
      </div>

      {/* Split view */}
      <div className="flex flex-1 min-h-0">
        {/* Left: Chat */}
        <div className="flex flex-col w-[55%] border-r" data-testid="chat-area">
          <div className="flex-1 min-h-0">
            <SandboxChat
              selectedMessageId={selectedMessageId}
              onSelectMessage={handleSelectMessage}
            />
          </div>

          {/* Auto-test summary below chat */}
          {autoTestResults && (
            <div className="border-t p-3">
              <AutoTestSummary results={autoTestResults} />
            </div>
          )}
        </div>

        {/* Right: Debug panel */}
        <div className="w-[45%]" data-testid="debug-area">
          <DebugSidePanel messageData={debugData} />
        </div>
      </div>
    </div>
  );
}
