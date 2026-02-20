import { type Metadata } from 'next';

import { RAGPipelineFlow } from '@/components/admin/knowledge-base/rag-pipeline-flow';

export const metadata: Metadata = {
  title: 'RAG Pipeline',
  description: 'Monitor the RAG pipeline stages and health',
};

export default function PipelineOverviewPage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          RAG Pipeline Overview
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Monitor the RAG pipeline stages and health
        </p>
      </div>

      {/* Pipeline Flow + Distribution + Recent Activity (all inside component with live polling) */}
      <RAGPipelineFlow />
    </div>
  );
}
