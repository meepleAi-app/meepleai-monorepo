import { RagPipelineClient } from './components/rag-pipeline-client';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'RAG Pipeline | Admin',
  description: 'Unified RAG pipeline management \u2014 upload, queue, metrics',
};

export default function RagPipelinePage() {
  return <RagPipelineClient />;
}
