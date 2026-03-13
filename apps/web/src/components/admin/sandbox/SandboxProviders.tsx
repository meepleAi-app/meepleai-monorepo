'use client';

import type { ReactNode } from 'react';

import { PipelineProvider } from './contexts/PipelineContext';
import { SandboxSessionProvider } from './contexts/SandboxSessionContext';
import { SourceProvider } from './contexts/SourceContext';

export function SandboxProviders({ children }: { children: ReactNode }) {
  return (
    <SourceProvider>
      <PipelineProvider>
        <SandboxSessionProvider>{children}</SandboxSessionProvider>
      </PipelineProvider>
    </SourceProvider>
  );
}
