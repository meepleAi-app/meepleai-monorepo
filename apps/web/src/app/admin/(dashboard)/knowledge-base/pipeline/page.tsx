import { type Metadata } from 'next';

import { ProcessingMetrics } from '@/components/admin/knowledge-base/processing-metrics';
import { RAGPipelineFlow } from '@/components/admin/knowledge-base/rag-pipeline-flow';

export const metadata: Metadata = {
  title: 'RAG Pipeline',
  description: 'Monitor the RAG pipeline stages and health',
};

export default function PipelineOverviewPage() {
  return (
    <div className="space-y-6">
      {/* Pipeline Flow + Distribution + Recent Activity */}
      <RAGPipelineFlow />

      {/* Processing Step Metrics */}
      <ProcessingMetrics />
    </div>
  );
}
