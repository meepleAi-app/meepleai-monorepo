import { type Metadata } from 'next';
import Link from 'next/link';

import {
  BotIcon,
  ArrowLeftIcon,
  PlusIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';

export const metadata: Metadata = {
  title: 'Agent Builder',
  description: 'Create and manage AI agent configurations',
};

export default function AgentBuilderPage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href="/admin/agents">
            <Button variant="ghost" size="icon" className="h-8 w-8">
              <ArrowLeftIcon className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
              Agent Builder
            </h1>
            <p className="text-sm text-muted-foreground mt-1">
              Create and manage AI agent definitions
            </p>
          </div>
        </div>
      </div>

      {/* Quick access cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <Link href="/admin/agent-definitions/create">
          <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60 hover:border-amber-300/60 dark:hover:border-amber-600/40 transition-all cursor-pointer group">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-amber-500 to-orange-600 flex items-center justify-center">
                  <PlusIcon className="h-4 w-4 text-white" />
                </div>
                Create New Agent
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground">
                Design a new agent with custom prompts, tools, and strategies
              </p>
            </CardContent>
          </Card>
        </Link>

        <Link href="/admin/agent-definitions">
          <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60 hover:border-amber-300/60 dark:hover:border-amber-600/40 transition-all cursor-pointer group">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center">
                  <BotIcon className="h-4 w-4 text-white" />
                </div>
                Agent Definitions
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground">
                Browse and manage existing agent configurations
              </p>
            </CardContent>
          </Card>
        </Link>

        <Link href="/admin/agent-definitions/playground">
          <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60 hover:border-amber-300/60 dark:hover:border-amber-600/40 transition-all cursor-pointer group">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-emerald-500 to-green-600 flex items-center justify-center">
                  <span className="text-white text-sm">▶</span>
                </div>
                Agent Playground
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground">
                Test agent interactions in a sandbox environment
              </p>
            </CardContent>
          </Card>
        </Link>
      </div>
    </div>
  );
}
