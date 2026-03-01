/**
 * KB Processing Pipeline Status Page - /admin/knowledge-base/processing
 *
 * Dedicated pipeline status view distinct from /queue (which shows individual jobs).
 * Shows aggregated pipeline health, stage statuses, processing metrics, and
 * document distribution.
 *
 * @see Issue #4892
 */

import { type Metadata } from 'next';

import { ProcessingPipelineClient } from './components/processing-pipeline-client';

export const metadata: Metadata = {
  title: 'Pipeline Status',
  description: 'Monitor RAG pipeline health, processing metrics, and document distribution',
};

export default function ProcessingPipelinePage() {
  return <ProcessingPipelineClient />;
}
