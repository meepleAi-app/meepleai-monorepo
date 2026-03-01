'use client';

/**
 * Agent Testing Page
 * Two tabs: Auto Test Suite (run standardized questions) + Interactive Chat (free-form Q&A).
 */

import { useState } from 'react';
import Link from 'next/link';

import {
  ArrowLeftIcon,
  FlaskConicalIcon,
  MessageSquareIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import { AutoTestSuite } from './AutoTestSuite';
import { InteractiveChat } from './InteractiveChat';

// ─── Types ───────────────────────────────────────────────────────────────────

interface AgentTestingPageProps {
  gameId: string;
  gameTitle?: string;
}

type Tab = 'auto-test' | 'chat';

// ─── Component ───────────────────────────────────────────────────────────────

export function AgentTestingPage({ gameId, gameTitle }: AgentTestingPageProps) {
  const [activeTab, setActiveTab] = useState<Tab>('auto-test');

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link href="/admin/shared-games/all">
          <Button variant="ghost" size="icon" className="h-8 w-8">
            <ArrowLeftIcon className="h-4 w-4" />
          </Button>
        </Link>
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Test Agent: {gameTitle ?? 'Game'}
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Verify the RAG agent&apos;s ability to answer questions from the processed rulebook
          </p>
        </div>
      </div>

      {/* Tab Bar */}
      <div className="flex gap-1 rounded-lg bg-slate-100 dark:bg-zinc-800 p-1">
        <button
          onClick={() => setActiveTab('auto-test')}
          className={`flex items-center gap-2 rounded-md px-4 py-2 text-sm font-medium transition-colors ${
            activeTab === 'auto-test'
              ? 'bg-white dark:bg-zinc-700 text-foreground shadow-sm'
              : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          <FlaskConicalIcon className="h-4 w-4" />
          Auto Test
        </button>
        <button
          onClick={() => setActiveTab('chat')}
          className={`flex items-center gap-2 rounded-md px-4 py-2 text-sm font-medium transition-colors ${
            activeTab === 'chat'
              ? 'bg-white dark:bg-zinc-700 text-foreground shadow-sm'
              : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          <MessageSquareIcon className="h-4 w-4" />
          Interactive Chat
        </button>
      </div>

      {/* Tab Content */}
      <div className="min-h-[500px]">
        {activeTab === 'auto-test' && <AutoTestSuite gameId={gameId} />}
        {activeTab === 'chat' && <InteractiveChat gameId={gameId} gameTitle={gameTitle} />}
      </div>
    </div>
  );
}
