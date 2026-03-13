'use client';

import { SandboxProviders } from '@/components/admin/sandbox/SandboxProviders';

export function SandboxClient() {
  return (
    <SandboxProviders>
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 p-4 h-[calc(100vh-4rem)]">
        {/* Source Panel */}
        <div className="rounded-xl border bg-white/70 backdrop-blur-md p-4">
          <h2 className="font-quicksand font-semibold text-lg mb-2">Source</h2>
          <p className="text-muted-foreground text-sm">Game selection and PDF management</p>
        </div>

        {/* Pipeline Panel */}
        <div className="rounded-xl border bg-white/70 backdrop-blur-md p-4">
          <h2 className="font-quicksand font-semibold text-lg mb-2">Pipeline</h2>
          <p className="text-muted-foreground text-sm">Processing status</p>
        </div>

        {/* Agent Config Panel */}
        <div className="rounded-xl border bg-white/70 backdrop-blur-md p-4">
          <h2 className="font-quicksand font-semibold text-lg mb-2">Agent Config</h2>
          <p className="text-muted-foreground text-sm">RAG strategy and parameters</p>
        </div>

        {/* Test Chat Panel */}
        <div className="rounded-xl border bg-white/70 backdrop-blur-md p-4">
          <h2 className="font-quicksand font-semibold text-lg mb-2">Test Chat</h2>
          <p className="text-muted-foreground text-sm">Chat with debug panel</p>
        </div>
      </div>
    </SandboxProviders>
  );
}
