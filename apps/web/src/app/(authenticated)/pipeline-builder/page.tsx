/**
 * Pipeline Builder Page
 *
 * Visual RAG pipeline builder for creating custom retrieval pipelines.
 *
 * @see Issue #3425 - Visual Pipeline Builder
 */

import { Suspense } from 'react';

import { PipelineBuilder } from '@/components/pipeline-builder';

import type { Metadata } from 'next';


export const metadata: Metadata = {
  title: 'Pipeline Builder | MeepleAI',
  description: 'Visual drag-drop builder for creating custom RAG pipelines',
};

function PipelineBuilderSkeleton() {
  return (
    <div className="flex h-[calc(100vh-4rem)] animate-pulse">
      {/* Left panel skeleton */}
      <div className="w-64 border-r bg-muted/30" />
      {/* Center canvas skeleton */}
      <div className="flex-1 bg-muted/20" />
      {/* Right panel skeleton */}
      <div className="w-80 border-l bg-muted/30" />
    </div>
  );
}

export default function PipelineBuilderPage() {
  return (
    <Suspense fallback={<PipelineBuilderSkeleton />}>
      <div className="h-[calc(100vh-4rem)]">
        <PipelineBuilder />
      </div>
    </Suspense>
  );
}
