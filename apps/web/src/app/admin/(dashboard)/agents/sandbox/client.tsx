'use client';

import { AgentConfigPanel } from '@/components/admin/sandbox/AgentConfigPanel';
import { PipelinePanel } from '@/components/admin/sandbox/PipelinePanel';
import { SandboxProviders } from '@/components/admin/sandbox/SandboxProviders';
import { SourcePanel } from '@/components/admin/sandbox/SourcePanel';
import { TestChatPanel } from '@/components/admin/sandbox/TestChatPanel';

export function SandboxClient() {
  return (
    <SandboxProviders>
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 p-4 h-[calc(100vh-4rem)]">
        <div className="rounded-xl border bg-white/70 backdrop-blur-md p-4 overflow-y-auto">
          <SourcePanel />
        </div>

        <div className="rounded-xl border bg-white/70 backdrop-blur-md p-4 overflow-y-auto">
          <PipelinePanel />
        </div>

        <div className="rounded-xl border bg-white/70 backdrop-blur-md p-4 overflow-y-auto">
          <AgentConfigPanel />
        </div>

        <div className="rounded-xl border bg-white/70 backdrop-blur-md p-0 overflow-hidden">
          <TestChatPanel />
        </div>
      </div>
    </SandboxProviders>
  );
}
