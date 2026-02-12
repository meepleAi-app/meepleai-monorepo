/**
 * ISSUE-3709: Agent Builder - Page Header
 * Title and breadcrumb navigation for Agent Builder
 */

'use client';

import Link from 'next/link';
import { ChevronRight, Sparkles } from 'lucide-react';

export function AgentBuilderHeader() {
  return (
    <div className="mb-8">
      {/* Breadcrumb */}
      <nav className="flex items-center space-x-2 text-sm text-muted-foreground mb-4">
        <Link href="/admin" className="hover:text-foreground transition-colors">
          Admin
        </Link>
        <ChevronRight className="h-4 w-4" />
        <Link href="/admin/ai-lab" className="hover:text-foreground transition-colors">
          AI Lab
        </Link>
        <ChevronRight className="h-4 w-4" />
        <Link href="/admin/ai-lab/agents" className="hover:text-foreground transition-colors">
          Agents
        </Link>
        <ChevronRight className="h-4 w-4" />
        <span className="text-foreground font-medium">Builder</span>
      </nav>

      {/* Title */}
      <div className="flex items-start gap-4">
        <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10">
          <Sparkles className="h-6 w-6 text-primary" />
        </div>
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Agent Builder</h1>
          <p className="text-muted-foreground mt-1">
            Create custom AI agents for your knowledge base
          </p>
        </div>
      </div>
    </div>
  );
}
