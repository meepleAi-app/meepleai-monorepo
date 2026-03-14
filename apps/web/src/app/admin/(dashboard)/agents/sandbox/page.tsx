import { Suspense } from 'react';

import { SandboxClient } from './client';

export const metadata = {
  title: 'RAG Sandbox | MeepleAI Admin',
};

export default function SandboxPage() {
  return (
    <Suspense fallback={<SandboxSkeleton />}>
      <SandboxClient />
    </Suspense>
  );
}

function SandboxSkeleton() {
  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 p-4">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="h-80 rounded-xl bg-white/50 animate-pulse" />
      ))}
    </div>
  );
}
